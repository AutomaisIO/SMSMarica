"""Lista as solicitações COM MANDADO JUDICIAL do SER (módulo Ambulatorial), por situação.

SOMENTE LEITURA: login + pesquisa na tela de Solicitação com o checkbox "Somente com mandado
judicial". A grade não tem ícone nem coluna de judicial — só esse filtro separa. Medido em
30/09/2026: 169 no total (AGENDADA 12, CHEGADA_NAO_CONFIRMADA 21, CANCELADA 10, CHEGADA_CONFIRMADA 103,
ALTA 23), e os 169 já estão em smsmarica.ser_solicitacao — só falta a marcação. Ver docs/ser.md §12.

A grade corta em 100 (5 páginas de 20) sem avisar: situação que bate 5 páginas é refeita fatiada
por ano da Data da Solicitação.

Uso:  python sonda_judicial.py            -> capturas/judicial_ids.json (IDs + situação, sem PII no terminal)
No Git Bash, rode com MSYS_NO_PATHCONV=1 se passar caminhos /ser/... como argumento.
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

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
CAP = RAIZ / "capturas"
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = "https://ser.saude.rj.gov.br"
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
URL_SOLIC = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"


def campos(doc, form_id):
    f = doc.find("form", id=form_id)
    d = {}
    if not f:
        return d
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset"} or i.has_attr("disabled"):
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        d[n] = i.get("value") or ""
    for s in f.find_all("select"):
        n = s.get("name")
        if n and not s.has_attr("disabled"):
            o = s.find("option", selected=True) or s.find("option")
            d[n] = (o.get("value") if o else "") or ""
    return d


def viewstate(doc, form_id):
    f = doc.find("form", id=form_id)
    i = f.find("input", attrs={"name": "javax.faces.ViewState"}) if f else None
    return i.get("value") if i else None


def paginas(doc):
    t = doc.find("table", id="form0:sc1_table")
    if not t:
        return 0
    n = [int(x.get_text(strip=True)) for x in t.find_all("td") if x.get_text(strip=True).isdigit()]
    return max(n) if n else 1


def linhas_da(doc):
    tab = doc.find("table", id="form0:listagem")
    out = []
    for tr in (tab.select("tbody tr") if tab else []):
        tds = [td.get_text(" ", strip=True) for td in tr.find_all("td")]
        if len(tds) >= 13 and re.fullmatch(r"\d{5,8}", tds[0] or ""):
            out.append({"id": tds[0], "tipo": tds[1], "data_solicitacao": tds[3], "situacao": tds[12]})
    return out


def entrar() -> httpx.Client:
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True, headers={"User-Agent": UA})
    d = BeautifulSoup(c.get("/ser/login").text, "html.parser")
    r = c.post(d.find("form", id="login").get("action"), data=campos(d, "login") | {
        "login": "login", "login:username": os.environ["SER_USUARIO"], "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar", "javax.faces.ViewState": viewstate(d, "login") or "j_id1"})
    if 'id="login:username"' in r.text:
        raise SystemExit("LOGIN FALHOU (a credencial do .env do lab costuma estar desatualizada)")
    home = c.get("/ser/home.seam").text
    fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
    dh = BeautifulSoup(home, "html.parser")
    r = c.post("/ser/home", data=campos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(dh, fid) or ""},
        headers={"X-Requested-With": "XMLHttpRequest"})
    c.get(r.headers["location"])
    return c


def main() -> int:
    c = entrar()
    d = BeautifulSoup(c.get(URL_SOLIC).text, "html.parser")
    form = d.find("form", id="form0")
    situ = next(s.get("name") for s in form.find_all("select")
                if any(o.get("value") == "EM_FILA" for o in s.find_all("option")))
    valores = [o.get("value") for o in form.find("select", attrs={"name": situ}).find_all("option") if o.get("value")]
    mand = next(cb.get("name") for cb in form.find_all("input", attrs={"type": "checkbox"})
                if cb.find_next("strong") and "mandado" in cb.find_next("strong").get_text().lower())
    botao = d.find(attrs={"title": "Pesquisar"}).get("id")

    def pesquisar(valor, extra):
        dd = BeautifulSoup(c.get(URL_SOLIC).text, "html.parser")
        vs = viewstate(dd, "form0") or ""
        r = c.post(URL_SOLIC, data=campos(dd, "form0") | {
            "form0": "form0", botao: botao, "AJAXREQUEST": "form0", situ: valor, mand: "on",
            "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": vs} | extra,
            headers={"X-Requested-With": "XMLHttpRequest"})
        res = BeautifulSoup(r.text, "html.parser")
        n = paginas(res)
        out = linhas_da(res)
        for p in range(2, n + 1):  # datascroller A4J: ajaxSingle + form0:sc1 = página
            rp = c.post(URL_SOLIC, data=campos(res, "form0") | {
                "form0": "form0", "AJAXREQUEST": "form0", "ajaxSingle": "form0:sc1", "form0:sc1": str(p),
                "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(res, "form0") or vs},
                headers={"X-Requested-With": "XMLHttpRequest"})
            out += linhas_da(BeautifulSoup(rp.text, "html.parser"))
        return n, out

    todos = {}
    for valor in valores:
        n, linhas = pesquisar(valor, {})
        if n >= 5:  # teto silencioso de 100
            linhas = []
            for ano in range(2010, 2031):
                n2, l2 = pesquisar(valor, {"form0:dtInicialSolicitacaoInputDate": f"01/01/{ano}",
                                           "form0:dtFinalSolicitacaoInputDate": f"31/12/{ano}"})
                if n2 >= 5:
                    print(f"  ATENÇÃO: {valor} {ano} ainda no teto de 100 — fatiar por mês")
                linhas += l2
        todos[valor] = list({x["id"]: x for x in linhas}.values())
        print(f"{valor:25} {len(todos[valor])}")
    CAP.mkdir(exist_ok=True)
    (CAP / "judicial_ids.json").write_text(json.dumps(todos, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"total: {sum(len(v) for v in todos.values())} -> capturas/judicial_ids.json")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
