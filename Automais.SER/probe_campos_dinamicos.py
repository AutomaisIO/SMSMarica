"""Varredura COMPLETA dos campos dinâmicos da aba Editar (criar solicitação no SER).

O SER monta o formulário de pedido em duas partes: um bloco fixo (paciente, médico, risco,
unidade de origem…) e um bloco DINÂMICO — `form0:camposDinamicos` — cujo conteúdo muda conforme
o **Recurso** escolhido. Cada campo dinâmico é um par
`form0:container_dinamico_id_<N>` (rótulo) + `form0:dinamico_id_<N>` (o campo).

Esta sonda percorre o catálogo inteiro de recursos (CONSULTA e EXAME), troca o combo e anota
quais campos dinâmicos aparecem. No fim agrupa os recursos por "assinatura de formulário", que é
o que responde a pergunta do Bernardo: quais pedidos têm campos diferentes, e quais.

SOMENTE LEITURA. Trocar combo apenas re-renderiza a view; o botão Gravar nunca é acionado e a
trava recusa qualquer parâmetro com verbo de escrita.

Uso:  python probe_campos_dinamicos.py [CONSULTA|EXAME|AMBOS] [limite]
"""

from __future__ import annotations

import json
import os
import pathlib
import re
import sys
import time

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
CAP = RAIZ / "capturas"
CAP.mkdir(exist_ok=True)
SAIDA = CAP / "campos_dinamicos.json"

BASE = "https://ser.saude.rj.gov.br"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")
URL_SOLIC = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"

ESCRITA = re.compile(
    r"(salvar|gravar|confirmar|inserir|incluir|excluir|remover|deletar|apagar|cancelar|"
    r"agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|executar|"
    r"efetivar|finalizar|aprovar|reprovar|transferir|solicitar|enviar|submeter|"
    r"registrar|followup|pendenciar)", re.I)
LIBERADOS = {"form0:editar_server_submit", "form0:pesquisar_server_submit"}

EVT_TIPO = "form0:j_id51"       # onchange de comboTipoRecurso
EVT_RECURSO = "form0:j_id57"    # onchange de comboRecurso


def guardar(dados: dict) -> dict:
    for k in dados:
        if k not in LIBERADOS and ESCRITA.search(k):
            raise SystemExit(f"TRAVA: POST recusado — parâmetro de escrita: {k!r}")
    return dados


def sopa(html):
    return BeautifulSoup(html, "html.parser")


def viewstate(html):
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def hidden_do_form(html):
    d = sopa(html)
    f = d.find("form", id="form0")
    out = {}
    if f:
        for i in f.find_all("input", attrs={"type": "hidden"}):
            if i.get("name"):
                out[i["name"]] = i.get("value") or ""
    return out


def campos_todos(doc, form_id="form0"):
    f = doc.find("form", id=form_id)
    d = {}
    if not f:
        return d
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset"}:
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        d[n] = i.get("value") or ""
    for s in f.find_all("select"):
        if s.get("name"):
            o = s.find("option", selected=True) or s.find("option")
            d[s["name"]] = (o.get("value") if o else "") or ""
    for t in f.find_all("textarea"):
        if t.get("name"):
            d[t["name"]] = t.get_text() or ""
    return d


def campos_dinamicos(html):
    """{id: {rotulo, tipo, obrigatorio, opcoes}} do bloco form0:camposDinamicos."""
    d = sopa(html)
    out = {}
    for cont in d.find_all(id=re.compile(r"^form0:container_dinamico_id_\d+$")):
        num = cont["id"].rsplit("_", 1)[-1]
        texto = " ".join(cont.get_text(" ", strip=True).split())
        el = cont.find(["input", "select", "textarea"])
        if el is None:
            continue
        tipo = el.name if el.name != "input" else (el.get("type") or "text").lower()
        info = {
            "rotulo": texto.replace(" *", "").strip(" :*"),
            "tipo": tipo,
            "obrigatorio": "*" in texto,
        }
        if el.name == "select":
            info["opcoes"] = [o.get_text(strip=True)[:60] for o in el.find_all("option")][:40]
        out[num] = info
    return out


def assinatura(dins):
    return tuple(sorted((n, i["rotulo"], i["tipo"]) for n, i in dins.items()))


def login_e_modulo(c):
    pag = c.get("/ser/login").text
    d = sopa(pag)
    r = c.post(d.find("form", id="login").get("action"), data=guardar(campos_todos(d, "login") | {
        "login": "login",
        "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar",
        "javax.faces.ViewState": viewstate(str(d.find("form", id="login"))) or "j_id1",
    }))
    if 'id="login:username"' in r.text:
        raise SystemExit("LOGIN FALHOU")
    home = c.get("/ser/home.seam").text
    fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
    dh = sopa(home)
    r = c.post("/ser/home", data=guardar(campos_todos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(str(dh.find("form", id=fid))) or "",
    }), headers={"X-Requested-With": "XMLHttpRequest"})
    if not r.headers.get("location"):
        raise SystemExit("módulo não ativou")
    c.get(r.headers["location"])
    print("login + módulo ok")


class Editar:
    def __init__(self, c):
        self.c = c
        self.full = ""     # última página COMPLETA (fonte dos hidden)
        self.vs = None
        self.act = URL_SOLIC

    def abrir(self):
        tela = self.c.get(URL_SOLIC).text
        d = sopa(tela)
        act = d.find("form", id="form0").get("action") or URL_SOLIC
        r = self.c.post(act, data=guardar(campos_todos(d) | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(tela) or "",
        }))
        html = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(m.group(1).replace("&amp;", "&")).text
        self.full = html
        self.vs = viewstate(html) or self.vs
        dd = sopa(html)
        if dd.find("form", id="form0"):
            self.act = dd.find("form", id="form0").get("action") or self.act
        return html

    def mudar(self, campo, valor, evento):
        dados = hidden_do_form(self.full)
        dados[campo] = valor
        dados |= {
            "AJAXREQUEST": "_viewRoot",
            evento: evento,
            "ajaxSingle": campo,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs or "",
        }
        r = self.c.post(self.act, data=guardar(dados),
                        headers={"X-Requested-With": "XMLHttpRequest"})
        html = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(m.group(1).replace("&amp;", "&")).text
        if '<form id="form0"' in html:
            self.full = html
        if (vs := viewstate(html)):
            self.vs = vs
        return html


def catalogo(html):
    d = sopa(html)
    sel = d.find("select", attrs={"name": "form0:comboRecurso"})
    if not sel:
        return []
    return [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
            for o in sel.find_all("option") if (o.get("value") or "").strip()]


def main() -> int:
    alvo = (sys.argv[1] if len(sys.argv) > 1 else "AMBOS").upper()
    limite = int(sys.argv[2]) if len(sys.argv) > 2 else 0

    c = httpx.Client(base_url=BASE, timeout=120, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)
    ed = Editar(c)

    resultado = {}
    if SAIDA.exists():
        resultado = json.loads(SAIDA.read_text(encoding="utf-8"))

    tipos = ["CONSULTA", "EXAME"] if alvo == "AMBOS" else [alvo]

    for tipo in tipos:
        ed.abrir()
        html = ed.mudar("form0:comboTipoRecurso", tipo, EVT_TIPO)
        cat = catalogo(html)
        base = campos_dinamicos(html)
        print(f"\n=== {tipo}: {len(cat)} recursos | formulário base: "
              f"{[i['rotulo'] for i in base.values()]}")

        feitos = resultado.setdefault(tipo, {})
        alvo_lista = cat[:limite] if limite else cat
        t0 = time.time()
        for n, (valor, nome) in enumerate(alvo_lista, 1):
            if valor in feitos:
                continue
            try:
                h = ed.mudar("form0:comboRecurso", valor, EVT_RECURSO)
                dins = campos_dinamicos(h)
            except Exception as e:  # noqa: BLE001
                print(f"  !! {valor} {nome[:40]}: {e}")
                continue
            feitos[valor] = {"nome": nome, "campos": dins}
            if n % 10 == 0 or n == len(alvo_lista):
                SAIDA.write_text(json.dumps(resultado, ensure_ascii=False, indent=1), encoding="utf-8")
                dec = time.time() - t0
                print(f"  {n}/{len(alvo_lista)}  ({dec:.0f}s, {dec/max(n,1):.1f}s/recurso)  "
                      f"último: {nome[:44]} -> {len(dins)} campos")

        SAIDA.write_text(json.dumps(resultado, ensure_ascii=False, indent=1), encoding="utf-8")

    # ------------------------------------------------------- agrupamento
    print("\n" + "=" * 78)
    print("FORMULÁRIOS DISTINTOS (recursos agrupados por conjunto de campos dinâmicos)")
    print("=" * 78)
    for tipo, itens in resultado.items():
        grupos = {}
        for valor, info in itens.items():
            chave = assinatura(info["campos"])
            grupos.setdefault(chave, []).append(info["nome"])
        print(f"\n### {tipo}: {len(itens)} recursos em {len(grupos)} formulários distintos")
        for chave, nomes in sorted(grupos.items(), key=lambda kv: -len(kv[1])):
            print(f"\n  [{len(nomes)} recursos] campos:")
            for num, rot, tp in chave:
                print(f"      id={num:>5}  {tp:9} {rot[:60]}")
            for nm in nomes[:4]:
                print(f"      ex.: {nm[:66]}")
            if len(nomes) > 4:
                print(f"      ... e mais {len(nomes)-4}")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
