"""Sonda a tela Consulta -> Histórico de Consulta/Exame para medir o TETO de registros.

A tela de Solicitação (solicitar-consulta-pesquisar) corta em 100 (5 páginas de 20).
O Bernardo aponta que esta corta em 500. Se confirmar, a varredura passa a precisar de
~5x menos fatias — e o truncamento por dia praticamente some.

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

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")

BASE = "https://ser.saude.rj.gov.br"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

URL_SOLIC = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"
URL_HIST = "/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam"


def campos(doc, form_id):
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
        n = s.get("name")
        if n:
            o = s.find("option", selected=True) or s.find("option")
            d[n] = (o.get("value") if o else "") or ""
    return d


def viewstate(doc, form_id):
    f = doc.find("form", id=form_id)
    i = f.find("input", attrs={"name": "javax.faces.ViewState"}) if f else None
    return i.get("value") if i else None


def paginas(html, scroller="form0:sc1"):
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id=f"{scroller}_table")
    if not t:
        return 0
    n = [int(x.get_text(strip=True)) for x in t.find_all("td") if x.get_text(strip=True).isdigit()]
    return max(n) if n else 1


def main() -> int:
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})

    # login
    pag = c.get("/ser/login").text
    d = BeautifulSoup(pag, "html.parser")
    action = d.find("form", id="login").get("action")
    dados = campos(d, "login") | {
        "login": "login",
        "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar",
        "javax.faces.ViewState": viewstate(d, "login") or "j_id1",
    }
    r = c.post(action, data=dados)
    if 'id="login:username"' in r.text:
        print("LOGIN FALHOU"); return 1
    print("login ok")

    # módulo ambulatorial (AJAXREQUEST é obrigatório)
    home = c.get("/ser/home.seam").text
    fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
    dh = BeautifulSoup(home, "html.parser")
    r = c.post("/ser/home", data=campos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(dh, fid) or "",
    }, headers={"X-Requested-With": "XMLHttpRequest"})
    loc = r.headers.get("location")
    if not loc:
        print("módulo não ativou"); return 1
    c.get(loc)
    print("módulo ambulatorial ok")

    # ---- tela de SOLICITAÇÃO (teto conhecido: 100) ----
    h = c.get(URL_SOLIC).text
    d = BeautifulSoup(h, "html.parser")
    botao = d.find(attrs={"title": "Pesquisar"}).get("id")
    r = c.post(URL_SOLIC, data=campos(d, "form0") | {
        "form0": "form0", botao: botao, "AJAXREQUEST": "form0",
        "form0:j_id75": "CANCELADA", "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(d, "form0") or "",
    }, headers={"X-Requested-With": "XMLHttpRequest"})
    p_solic = paginas(r.text)
    print(f"\n[SOLICITAÇÃO] CANCELADA sem data -> {p_solic} páginas = ~{p_solic*20} registros")

    # ---- tela de HISTÓRICO (teto a medir) ----
    h = c.get(URL_HIST).text
    d = BeautifulSoup(h, "html.parser")
    print(f"\n[HISTÓRICO] tela carregada ({len(h)} chars)")
    situ = d.find("select", attrs={"name": "form0:j_id57"})
    valores = [o.get("value") for o in situ.find_all("option")] if situ else []
    print("  combo de situação:", valores)

    r = c.post(URL_HIST, data=campos(d, "form0") | {
        "form0": "form0", "form0:btnSearch": "form0:btnSearch", "AJAXREQUEST": "form0",
        "form0:j_id57": "CANCELADA", "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(d, "form0") or "",
    }, headers={"X-Requested-With": "XMLHttpRequest"})

    corpo = r.text
    m = re.search(r'<meta name="Location" content="([^"]+)"', corpo)
    if m:
        corpo = c.get(m.group(1).replace("&amp;", "&")).text
        print("  (seguiu redirect A4J)")

    for sc in ("form0:sc1", "form0:sc", "form0:scroller"):
        p = paginas(corpo, sc)
        if p:
            print(f"  scroller {sc}: {p} páginas = ~{p*20} registros")
            break
    else:
        ids = sorted({x for x in re.findall(r'id="(form0:\w*[sS]c\w*)_table"', corpo)})
        print("  scroller não encontrado; candidatos:", ids)

    dd = BeautifulSoup(corpo, "html.parser")
    tab = dd.find("table", id="form0:listagem")
    print("  linhas na 1a página:", len(tab.select("tbody tr")) if tab else "(sem grade)")

    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
