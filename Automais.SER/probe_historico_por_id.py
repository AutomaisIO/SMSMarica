"""Por que a busca por ID da fase de histórico devolve 0 linhas?

Em 08/08/2026 a Carga Inicial falhou em ~100% da fase de histórico com
"A busca pelo ID X em <situacao> devolveu 0 linhas — esperava exatamente 1" (14.363 erros,
2 leituras boas). Esta sonda reproduz a busca do motor e isola a causa testando variações:

  A) ID + Situação, tela recém-aberta      (exatamente o que o motor faz na 1ª vez)
  B) ID + Situação, reusando o form        (o que o motor faz da 2ª em diante)
  C) só ID, sem situação
  D) ID + Situação + datas em branco explícitas
  E) o mesmo ID logo depois de outra busca (testa vazamento de estado entre buscas)

SOMENTE LEITURA.
"""

from __future__ import annotations

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

BASE = "https://ser.saude.rj.gov.br"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")
URL = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"

CAMPO_SITUACAO = "form0:j_id75"
CAMPO_ID = "form0:idSolicitacao"
CAMPO_DT_INI = "form0:dtInicialSolicitacaoInputDate"
CAMPO_DT_FIM = "form0:dtFinalSolicitacaoInputDate"


def campos(doc, form_id="form0"):
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
    return d


def vs(html):
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else ""


def linhas(html):
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id="form0:listagem")
    if not t:
        return None
    out = []
    for tr in t.select("tbody tr"):
        tds = tr.find_all("td")
        if not tds:
            continue
        v = tds[0].get_text(strip=True)
        if re.fullmatch(r"\d{5,}", v):
            out.append(v)
    return out


def mensagens(html):
    d = BeautifulSoup(html, "html.parser")
    c = d.find(id="form0:messages")
    return " ".join(c.get_text(" ", strip=True).split()) if c else ""


def login_e_modulo(c):
    pag = c.get("/ser/login").text
    d = BeautifulSoup(pag, "html.parser")
    r = c.post(d.find("form", id="login").get("action"), data=campos(d, "login") | {
        "login": "login", "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"], "login:entrar": "Entrar",
        "javax.faces.ViewState": vs(pag) or "j_id1",
    })
    if 'id="login:username"' in r.text:
        raise SystemExit("LOGIN FALHOU")
    home = c.get("/ser/home.seam").text
    fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
    dh = BeautifulSoup(home, "html.parser")
    r = c.post("/ser/home", data=campos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": vs(home),
    }, headers={"X-Requested-With": "XMLHttpRequest"})
    c.get(r.headers["location"])
    print("login + módulo ok\n")


class Motor:
    """Imita SerLeitorService: guarda o último HTML COMPLETO e o ViewState."""

    def __init__(self, c):
        self.c = c
        self.form = ""
        self.viewstate = ""

    def preparar(self):
        h = self.c.get(URL).text
        self.form = h
        self.viewstate = vs(h)
        return h

    def pesquisar(self, extras, reusar=True):
        if not reusar or not self.form:
            self.preparar()
        d = BeautifulSoup(self.form, "html.parser")
        botao = d.find(attrs={"title": "Pesquisar"}).get("id")
        act = d.find("form", id="form0").get("action")
        dados = campos(d) | {"form0": "form0", botao: botao,
                             "AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
                             "javax.faces.ViewState": self.viewstate}
        dados |= extras
        r = self.c.post(act, data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
        h = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', h)):
            h = self.c.get(m.group(1).replace("&amp;", "&")).text
        # "Absorver": só promove página que tenha o botão Pesquisar
        if BeautifulSoup(h, "html.parser").find(attrs={"title": "Pesquisar"}):
            self.form = h
        if (v := vs(h)):
            self.viewstate = v
        return h


def conta(rotulo, html, esperado=None):
    ls = linhas(html)
    msg = mensagens(html)
    n = "sem grade" if ls is None else len(ls)
    ok = "" if esperado is None else ("  <<< ACHOU" if ls and esperado in ls else "  <<< NAO ACHOU")
    print(f"  {rotulo:52} -> {str(n):>9} linhas{ok}")
    if msg:
        print(f"       mensagem do SER: {msg[:150]}")
    if ls:
        print(f"       ids: {ls[:6]}")
    return ls


def main() -> int:
    alvo = sys.argv[1] if len(sys.argv) > 1 else "2727024"
    situacao = sys.argv[2] if len(sys.argv) > 2 else "AGENDADA"
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)
    print(f"alvo: ID={alvo} situacao={situacao}\n")

    m = Motor(c)

    print("A) tela recém-aberta + ID + Situação  (o que o motor faz na 1ª vez)")
    m.preparar()
    conta("A", m.pesquisar({CAMPO_ID: alvo, CAMPO_SITUACAO: situacao}, reusar=True), alvo)

    print("\nB) segunda busca REUSANDO o form da resposta anterior (o que o motor faz depois)")
    conta("B", m.pesquisar({CAMPO_ID: alvo, CAMPO_SITUACAO: situacao}, reusar=True), alvo)

    print("\nC) terceira busca, também reusando")
    conta("C", m.pesquisar({CAMPO_ID: alvo, CAMPO_SITUACAO: situacao}, reusar=True), alvo)

    print("\nD) tela NOVA a cada busca (GET antes)")
    m.preparar()
    conta("D", m.pesquisar({CAMPO_ID: alvo, CAMPO_SITUACAO: situacao}, reusar=False), alvo)

    print("\nE) tela nova + ID + Situação + DATAS EXPLICITAMENTE VAZIAS")
    m.preparar()
    conta("E", m.pesquisar({CAMPO_ID: alvo, CAMPO_SITUACAO: situacao,
                            CAMPO_DT_INI: "", CAMPO_DT_FIM: ""}, reusar=False), alvo)

    print("\nF) o que a tela mandou nos campos de data ao reabrir (herança de estado?)")
    d = BeautifulSoup(m.form, "html.parser")
    cs = campos(d)
    for k in (CAMPO_DT_INI, CAMPO_DT_FIM, CAMPO_ID, CAMPO_SITUACAO, "form0:comboTipoRecurso"):
        print(f"     {k:46} = {cs.get(k)!r}")

    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
