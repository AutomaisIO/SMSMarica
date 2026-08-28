"""Dá para consultar o cadastro em N sessões simultâneas com a MESMA credencial?

A consulta de cadastro pelo SER custa ~3s por paciente e o motor serializa tudo num semáforo,
porque a sessão é stateful (ViewState, conversa Seam, aba aberta). A saída para ganhar escala seria
abrir N sessões independentes — cookie jars separados — com a mesma credencial.

O que esta sonda mede, nesta ordem de importância:

1. CORREÇÃO. Cada sessão pesquisa um CNS diferente AO MESMO TEMPO e confere se o painel devolveu
   o cadastro DAQUELE CNS. Se uma sessão receber o paciente de outra, o SER guarda estado por
   USUÁRIO (não por sessão) e a ideia inteira está morta — velocidade que troca identidade de
   paciente é o pior defeito possível neste sistema.
2. Se o SER aceita N logins simultâneos do mesmo usuário.
3. Quanto se ganha de verdade, comparado ao caminho serial.

Leitura pura: login, navegação e pesquisa. Nenhuma escrita.

Uso:  python sonda_paralelo.py [n_sessoes]      (default 4)
"""
from __future__ import annotations

import os
import pathlib
import re
import sys
import time
from concurrent.futures import ThreadPoolExecutor

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
BASE = os.environ.get("SER_BASE_URL", "https://ser.saude.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/126.0 Safari/537.36")
TELA = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"

# CNS de teste: vêm de um arquivo LOCAL fora do repositório (o export da agenda), nunca chumbados
# aqui — são dados de paciente real.
FONTE_CNS = pathlib.Path(
    os.environ.get("SONDA_CNS_ARQUIVO", "")) if os.environ.get("SONDA_CNS_ARQUIVO") else None


def campos_do_form(doc, form_id):
    form = doc.find("form", id=form_id)
    if form is None:
        return {}
    out = {}
    for el in form.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or el.has_attr("disabled"):
            continue
        if (el.get("type") or "").lower() in ("submit", "button", "image", "reset"):
            continue
        out[nome] = el.get("value") or ""
    return out


def viewstate(html):
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def mascara(v: str) -> str:
    return v[:3] + "…" + v[-2:] if len(v) > 6 else "***"


class Sessao:
    """Uma sessão isolada: cliente HTTP com cookie jar próprio."""

    def __init__(self, indice: int):
        self.i = indice
        self.c = httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=120,
                              follow_redirects=True, verify=False)
        self.html = ""    # a aba COMPLETA: base de todos os POSTs
        self.vs = None    # ViewState corrente, que avança a cada resposta

    def entrar(self) -> str:
        d = BeautifulSoup(self.c.get("/ser/login").text, "html.parser")
        fl = d.find("form", id="login")
        r = self.c.post(fl.get("action"), data=campos_do_form(d, "login") | {
            "login": "login",
            "login:username": os.environ["SER_USUARIO"],
            "login:password": os.environ["SER_SENHA"],
            "login:entrar": "Entrar",
            "javax.faces.ViewState": viewstate(str(fl)) or "j_id1",
        })
        if 'id="login:username"' in r.text:
            return "login falhou"

        home = self.c.get("/ser/home.seam").text
        m = re.search(r'<script[^>]*\bid="([^":]+):goModulo"', home)
        if not m:
            return "sem goModulo (aviso pendente?)"
        fid = m.group(1)
        dh = BeautifulSoup(home, "html.parser")
        r = self.c.post("/ser/home", data=campos_do_form(dh, fid) | {
            fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
            "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": viewstate(str(dh.find("form", id=fid))) or "",
        }, headers={"X-Requested-With": "XMLHttpRequest"}, follow_redirects=False)
        destino = r.headers.get("location") or (
            m2.group(1) if (m2 := re.search(r'<meta name="Location" content="([^"]+)"', r.text)) else None)
        if not destino:
            return "módulo não ativou"
        self.c.get(destino.replace("&amp;", "&"))
        return "ok"

    def abrir_aba(self) -> bool:
        tela = self.c.get(TELA).text
        d = BeautifulSoup(tela, "html.parser")
        f0 = d.find("form", id="form0")
        acao = (f0.get("action") if f0 else None) or TELA
        r = self.c.post(acao, data=campos_do_form(d, "form0") | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(tela) or "",
        })
        html = r.text
        if (mm := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(mm.group(1).replace("&amp;", "&")).text
        self.html = html
        self.vs = viewstate(html)
        return "numeroCADSUS" in html

    def pesquisar(self, cns: str) -> tuple[str, str]:
        """
        Devolve (cns_devolvido, nome_mascarado) — para conferir se veio o paciente CERTO.

        A BASE do POST é sempre o HTML COMPLETO da aba (self.html), nunca a resposta anterior: a
        resposta da pesquisa é um fragmento A4J parcial, que não traz o botão nem os hidden do
        form. Promover esse fragmento a base foi o que fez a segunda pesquisa falhar com "sem botão
        Pesquisar" — a mesma armadilha do §3.4 do docs/ser.md. Do fragmento aproveita-se só o
        ViewState novo.
        """
        botao = re.search(r'<a[^>]*title="Pesquisar"[^>]*id="([^"]+)"', self.html) \
            or re.search(r'<a[^>]*id="([^"]+)"[^>]*title="Pesquisar"', self.html)
        if not botao:
            return ("", "sem botão Pesquisar")
        bid = botao.group(1)
        d = BeautifulSoup(self.html, "html.parser")
        dados = campos_do_form(d, "form0") | {
            "form0:numeroCADSUS": cns,
            "AJAXREQUEST": "_viewRoot",
            bid: bid, "ajaxSingle": bid,
            "javax.faces.ViewState": self.vs or viewstate(self.html) or "",
        }
        f0 = d.find("form", id="form0")
        acao = (f0.get("action") if f0 else None) or TELA
        r = self.c.post(acao, data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
        html = r.text
        if (mm := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(mm.group(1).replace("&amp;", "&")).text

        # Só o ViewState avança; a base continua sendo a aba completa.
        self.vs = viewstate(html) or self.vs

        dd = BeautifulSoup(html, "html.parser")
        painel = dd.find(id="form0:painelDadosDoPaciente")
        if painel is None:
            return ("", "painel ausente")
        cns_dev = (dd.find(id="form0:cns") or {}).get("value") if dd.find(id="form0:cns") else ""
        nome = (dd.find(id="form0:nome") or {}).get("value") if dd.find(id="form0:nome") else ""
        return ("".join(ch for ch in (cns_dev or "") if ch.isdigit()), mascara(nome or ""))

    def fechar(self):
        self.c.close()


def ler_cns(qtd: int) -> list[str]:
    if FONTE_CNS and FONTE_CNS.exists():
        vistos, saida = set(), []
        for linha in FONTE_CNS.read_text(encoding="utf-8", errors="replace").splitlines():
            c = linha.split(";")
            if len(c) >= 38:
                cns = "".join(ch for ch in c[9] if ch.isdigit())
                if len(cns) == 15 and cns not in vistos:
                    vistos.add(cns)
                    saida.append(cns)
            if len(saida) >= qtd:
                break
        return saida
    return []


def main(argv: list[str]) -> int:
    n = int(argv[0]) if argv else 4
    cnss = ler_cns(n)
    if len(cnss) < n:
        print("!! preciso de CNS de teste. Defina SONDA_CNS_ARQUIVO apontando para um export da")
        print("   agenda do SISREG (arquivo local, fora do repositório).", file=sys.stderr)
        return 2

    print(f"abrindo {n} sessões simultâneas com a MESMA credencial…\n")
    sessoes = [Sessao(i) for i in range(n)]

    t0 = time.time()
    with ThreadPoolExecutor(max_workers=n) as ex:
        estados = list(ex.map(lambda s: s.entrar(), sessoes))
    print(f"[1] login+módulo em paralelo ({time.time()-t0:.1f}s): {estados}")
    if any(e != "ok" for e in estados):
        print("\n>> O SER recusou sessões simultâneas com a mesma credencial.")
        for s in sessoes: s.fechar()
        return 3

    t0 = time.time()
    with ThreadPoolExecutor(max_workers=n) as ex:
        abriu = list(ex.map(lambda s: s.abrir_aba(), sessoes))
    print(f"[2] aba Editar em paralelo ({time.time()-t0:.1f}s): {abriu}")

    # O TESTE QUE DECIDE: cada sessão pesquisa um CNS diferente ao mesmo tempo.
    t0 = time.time()
    with ThreadPoolExecutor(max_workers=n) as ex:
        res = list(ex.map(lambda par: par[0].pesquisar(par[1]), zip(sessoes, cnss)))
    paralelo = time.time() - t0

    print(f"\n[3] {n} pesquisas SIMULTÂNEAS em {paralelo:.1f}s")
    print(f"    {'sessão':8} {'pediu':18} {'voltou':18} {'confere?':9} paciente")
    ok = True
    for i, (pedido, (voltou, nome)) in enumerate(zip(cnss, res)):
        confere = voltou == pedido
        ok = ok and confere
        print(f"    {i:<8} {mascara(pedido):18} {mascara(voltou) if voltou else '(nada)':18} "
              f"{'SIM' if confere else 'NÃO!!':9} {nome}")

    # Comparação honesta: OUTROS N CNS, um de cada vez, numa sessão só, com a aba já aberta.
    # Repetir os mesmos números mediria o cache do SER, não o trabalho.
    outros = ler_cns(n * 2)[n:]
    t0 = time.time()
    conferidas = []
    for cns in outros:
        voltou, nome = sessoes[0].pesquisar(cns)
        conferidas.append((cns, voltou, nome))
    serial = time.time() - t0
    print(f"\n[4] {len(outros)} pesquisas NOVAS em SÉRIE, aba já aberta: {serial:.1f}s "
          f"({serial/max(len(outros),1):.2f}s cada)")
    # Tempo baixo demais é suspeito: pode significar que a pesquisa nem aconteceu. Conferir o
    # que voltou é o que separa "rápido" de "não fez nada".
    for pedido, voltou, nome in conferidas:
        print(f"       pediu {mascara(pedido)} -> voltou {mascara(voltou) if voltou else '(NADA)'}"
              f" {'OK' if voltou == pedido else '!! NÃO CONFERE'} {nome}")

    # E o custo de fazer como o motor faz hoje: reabrindo a aba a cada paciente.
    t0 = time.time()
    for cns in ler_cns(n * 3)[n * 2:]:
        sessoes[0].abrir_aba()
        sessoes[0].pesquisar(cns)
    comAba = time.time() - t0
    print(f"[5] mesmas pesquisas REABRINDO a aba (como o motor faz hoje): {comAba:.1f}s "
          f"({comAba/max(n,1):.2f}s cada)")

    print(f"\n    paralelo({n} de uma vez) {paralelo:.1f}s"
          f"  |  série com aba aberta {serial:.1f}s"
          f"  |  série reabrindo aba {comAba:.1f}s")

    for s in sessoes:
        s.fechar()

    if not ok:
        print("\n>> REPROVADO: alguma sessão devolveu o cadastro de OUTRO CNS. O SER guarda estado")
        print("   por usuário, não por sessão — paralelizar com a mesma credencial trocaria")
        print("   identidade de paciente. NÃO seguir por esse caminho.")
        return 4

    print("\n>> APROVADO: cada sessão devolveu o cadastro que pediu, em paralelo.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
