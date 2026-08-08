"""Reconhecimento COMPLETO da aba EDITAR da tela de Solicitação — onde o SER cria pedido.

Mapeia a tela e, principalmente, **como os campos mudam conforme o que se pede**: o combo de
Recurso dispara re-render, e o próprio SER tem um `verificarPetCt()` no `oncomplete`, o que já
denuncia procedimento com regra própria.

SOMENTE LEITURA — e aqui isso é delicado, porque é a tela de criação. A trava do motor .NET não
vale nesta sonda, então ela tem a própria: lista branca de navegação, lista negra de verbos de
escrita, e toda requisição passa por `guardar()`. Só trocamos aba e combos, que apenas
re-renderizam a view. O botão `Gravar` nunca é acionado.
"""

from __future__ import annotations

import json
import os
import pathlib
import re
import sys

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
CAP = RAIZ / "capturas"
CAP.mkdir(exist_ok=True)

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


def guardar(dados: dict) -> dict:
    for chave in dados:
        if chave in LIBERADOS:
            continue
        if ESCRITA.search(chave):
            raise SystemExit(f"TRAVA: POST recusado — parâmetro de escrita: {chave!r}")
    return dados


# --------------------------------------------------------------------- leitura do form

def sopa(html):
    return BeautifulSoup(html, "html.parser")


def hidden_do_form(doc, form_id="form0"):
    """Só os campos HIDDEN — é o que o A4J manda quando `ajaxSingle` está presente
    (`appendFormControls(single=true)` só inclui hidden + o próprio controle)."""
    f = doc.find("form", id=form_id)
    d = {}
    if not f:
        return d
    for i in f.find_all("input", attrs={"type": "hidden"}):
        if i.get("name"):
            d[i["name"]] = i.get("value") or ""
    return d


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


def viewstate(html_ou_doc):
    txt = html_ou_doc if isinstance(html_ou_doc, str) else str(html_ou_doc)
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', txt)
    return m.group(1) if m else None


def action(doc, form_id="form0"):
    f = doc.find("form", id=form_id)
    return f.get("action") if f else None


def rotulo_de(el):
    """Rótulo visível: o <td> anterior que tenha texto e não contenha outro controle."""
    td = el.find_parent("td")
    while td is not None:
        ant = td.find_previous_sibling("td")
        while ant is not None:
            if not ant.find(["input", "select", "textarea"]):
                t = " ".join(ant.get_text(" ", strip=True).split())
                if t:
                    return t[:60]
            ant = ant.find_previous_sibling("td")
        td = td.find_parent("td")
    return ""


def inventario(html):
    doc = sopa(html)
    f = doc.find("form", id="form0")
    if not f:
        return {}
    itens = {}
    for el in f.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or nome in {"form0", "autoScroll", "javax.faces.ViewState"}:
            continue
        if nome.endswith("_link_hidden_") or nome.endswith("_selection"):
            continue
        tipo = el.name if el.name != "input" else (el.get("type") or "text").lower()
        if tipo == "hidden":
            continue
        # campo dentro de bloco escondido não conta como "presente"
        oculto = False
        for pai in el.parents:
            est = (pai.get("style") or "") if hasattr(pai, "get") else ""
            if "display:none" in est.replace(" ", ""):
                oculto = True
                break
        if oculto:
            continue
        info = {"tipo": tipo, "rotulo": rotulo_de(el)}
        if el.name == "select":
            ops = [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
                   for o in el.find_all("option")]
            info["n_opcoes"] = len(ops)
            info["opcoes"] = ops if len(ops) <= 30 else ops[:30]
        itens[nome] = info
    return itens


# --------------------------------------------------------------------- sessão

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
    print("login + módulo ambulatorial ok")


class Tela:
    """Mantém a última página COMPLETA (fonte dos campos) e o ViewState corrente."""

    def __init__(self, c):
        self.c = c
        self.html = ""
        self.vs = None
        self.act = URL_SOLIC

    def abrir_editar(self):
        tela = self.c.get(URL_SOLIC).text
        d = sopa(tela)
        r = self.c.post(action(d) or URL_SOLIC, data=guardar(campos_todos(d) | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(str(d.find("form", id="form0"))) or "",
        }))
        html = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(m.group(1).replace("&amp;", "&")).text
        self._absorver(html)
        (CAP / "criar_00_aba_editar.html").write_text(html, encoding="utf-8")
        return html

    def mudar(self, campo, valor, evento, rotulo):
        """Dispara o onchange A4J de um combo (só re-renderiza; não grava nada)."""
        d = sopa(self.html)
        dados = hidden_do_form(d)
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
        self._absorver(html)
        nome = re.sub(r"\W+", "_", rotulo)[:40]
        (CAP / f"criar_{nome}.html").write_text(html, encoding="utf-8")
        return html

    def _absorver(self, html):
        # Só promove a "página de formulário" quem tem o form completo; resposta A4J parcial
        # não serve de base para o submit seguinte (mesma regra do resto do motor).
        if '<form id="form0"' in html:
            self.html = html
            d = sopa(html)
            self.act = action(d) or self.act
        if (vs := viewstate(html)):
            self.vs = vs


def diff(antes, depois):
    entram = [k for k in depois if k not in antes]
    saem = [k for k in antes if k not in depois]
    mudam = [k for k in depois if k in antes
             and depois[k].get("n_opcoes") != antes[k].get("n_opcoes")]
    return entram, saem, mudam


def main() -> int:
    c = httpx.Client(base_url=BASE, timeout=120, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)
    t = Tela(c)

    html = t.abrir_editar()
    base = inventario(html)
    print(f"\n=== BASE: aba Editar recém-aberta — {len(base)} campos ===")
    for n, i in base.items():
        ops = f"  [{i['n_opcoes']} opções]" if "n_opcoes" in i else ""
        print(f"  {n:44} {i['tipo']:8} {i['rotulo'][:38]:40}{ops}")

    relatorio = {"base": base, "variacoes": {}}

    # 1) É AMBULATÓRIO ESTADUAL? -> muda o fluxo
    for valor in ("1", "2"):
        rot = f"comboSisReg={valor}"
        try:
            h = t.mudar("form0:comboSisReg", valor, "form0:j_id46", rot)
        except Exception as e:  # noqa: BLE001
            print(f"  !! {rot}: {e}")
            continue
        inv = inventario(h)
        e, s, m = diff(base, inv)
        print(f"\n--- {rot}: +{len(e)} -{len(s)} ~{len(m)}")
        for k in e: print(f"     ENTRA  {k}  ({inv[k]['rotulo'][:40]})")
        for k in s: print(f"     SAI    {k}")
        relatorio["variacoes"][rot] = {"entram": e, "saem": s}

    # volta para o estado que popula o catálogo e escolhe CONSULTA
    t.abrir_editar()
    base2 = inventario(t.html)
    h = t.mudar("form0:comboTipoRecurso", "CONSULTA", "form0:j_id51", "tipo=CONSULTA")
    inv = inventario(h)
    e, s, m = diff(base2, inv)
    print(f"\n--- Tipo=CONSULTA: +{len(e)} -{len(s)} ~{len(m)}")
    for k in e: print(f"     ENTRA  {k}  ({inv[k]['rotulo'][:40]})")
    for k in m: print(f"     MUDA   {k}: {base2[k].get('n_opcoes')} -> {inv[k].get('n_opcoes')} opções")

    recursos = inv.get("form0:comboRecurso", {}).get("opcoes", [])
    print(f"\n=== catálogo de RECURSO para CONSULTA: {inv.get('form0:comboRecurso', {}).get('n_opcoes', 0)} ===")
    for v, txt in recursos[:30]:
        print(f"     {v:>8}  {txt[:70]}")

    relatorio["recursos_consulta"] = recursos
    (CAP / "criar_relatorio.json").write_text(
        json.dumps(relatorio, ensure_ascii=False, indent=2), encoding="utf-8")
    print("\nrelatório parcial -> capturas/criar_relatorio.json")

    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
