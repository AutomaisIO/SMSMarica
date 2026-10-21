"""Sonda a GUIA de agendamento do SER (solicitação AGENDADA -> guia em PDF).

Passos:
  menu   pesquisa AGENDADAS com agendamento de amanhã em diante, sorteia uma linha e
         lista os itens do menu Opções dela (não clica em nada). Guarda o sorteio em
         <saida>/sorteio.json para o passo seguinte.
  guia   abre o item de guia da linha sorteada e salva o que vier (PDF ou HTML).

Leitura. A guia pode carimbar uma "via" no SER (1ª, 2ª…) — é exatamente o que se quer medir,
por isso o passo `guia` só roda quando chamado.

A saída tem dado de paciente: vai para fora do repositório (--saida, padrão o scratchpad).
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import pathlib
import random
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
XHR = {"X-Requested-With": "XMLHttpRequest"}


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


def entrar() -> httpx.Client:
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    d = BeautifulSoup(c.get("/ser/login").text, "html.parser")
    r = c.post(d.find("form", id="login").get("action"), data=campos(d, "login") | {
        "login": "login",
        "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar",
        "javax.faces.ViewState": viewstate(d, "login") or "j_id1",
    })
    if 'id="login:username"' in r.text:
        sys.exit("LOGIN FALHOU")
    home = c.get("/ser/home.seam").text
    fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
    dh = BeautifulSoup(home, "html.parser")
    r = c.post("/ser/home", data=campos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(dh, fid) or "",
    }, headers=XHR)
    loc = r.headers.get("location")
    if not loc:
        sys.exit("módulo ambulatorial não ativou")
    c.get(loc)
    return c


def pesquisar(c, inicio: str, fim: str, id_solic: str = ""):
    """Pesquisa AGENDADA pela Data da Consulta/Exame (o dia em que o paciente vai ser atendido;
    "Data de Agendamento da Regulação" é o dia em que o regulador agendou). Devolve
    (doc_form, html_resposta)."""
    h = c.get(URL_SOLIC).text
    d = BeautifulSoup(h, "html.parser")
    f = d.find("form", id="form0")
    botao = d.find(attrs={"title": "Pesquisar"}).get("id")
    dados = campos(d, "form0") | {
        "form0": "form0", botao: botao, "AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
        "form0:j_id75": "AGENDADA",
        "form0:dtInicialConsultaExameInputDate": inicio,
        "form0:dtFinalConsultaExameInputDate": fim,
        "form0:idSolicitacao": id_solic,
        "javax.faces.ViewState": viewstate(d, "form0") or "",
    }
    r = c.post(f.get("action"), data=dados, headers=XHR)
    corpo = r.text
    m = re.search(r'<meta name="Location" content="([^"]+)"', corpo)
    if m:
        corpo = c.get(m.group(1).replace("&amp;", "&")).text
    return d, corpo


def linhas(html):
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id="form0:listagem")
    out = []
    if not t:
        return out
    for i, tr in enumerate(t.select("tbody > tr")):
        tds = tr.find_all("td", recursive=False)
        if len(tds) < 13:
            continue
        txt = [td.get_text(" ", strip=True) for td in tds]
        out.append({"i": i, "id": txt[0], "tipo": txt[1], "recurso": txt[2],
                    "data_solicitacao": txt[3], "paciente": txt[4], "agendado_para": txt[11],
                    "situacao": txt[12], "_tr": tr})
    return out


def itens_menu(tr):
    """Itens do menu Opções da linha: (texto, id, onclick)."""
    itens = []
    for el in tr.find_all(attrs={"onclick": True}):
        texto = el.get_text(" ", strip=True)
        itens.append({"texto": texto, "id": el.get("id"), "onclick": el.get("onclick")[:400]})
    for a in tr.find_all("a"):
        if not a.get("onclick"):
            itens.append({"texto": a.get_text(" ", strip=True), "id": a.get("id"),
                          "href": a.get("href")})
    return itens


def passo_menu(c, saida: pathlib.Path, dias: int):
    amanha = dt.date.today() + dt.timedelta(days=1)
    fim = amanha + dt.timedelta(days=dias)
    _, corpo = pesquisar(c, amanha.strftime("%d/%m/%Y"), fim.strftime("%d/%m/%Y"))
    (saida / "pesquisa.html").write_text(corpo, encoding="utf-8")
    ls = linhas(corpo)
    print(f"AGENDADAS {amanha:%d/%m} → {fim:%d/%m}: {len(ls)} linhas na 1ª página")
    if not ls:
        msg = BeautifulSoup(corpo, "html.parser").find(id="form0:messages")
        print("  mensagens:", msg.get_text(" ", strip=True) if msg else "(nenhuma)")
        return
    esc = random.choice(ls)
    print(f"sorteada: id={esc['id']} {esc['tipo']} | {esc['recurso']} | agendado para "
          f"{esc['agendado_para']} | {esc['situacao']}")
    itens = itens_menu(esc["_tr"])
    for it in itens:
        print("  item:", json.dumps(it, ensure_ascii=False))
    (saida / "linha_sorteada.html").write_text(str(esc["_tr"]), encoding="utf-8")
    (saida / "sorteio.json").write_text(json.dumps(
        {k: v for k, v in esc.items() if k != "_tr"} | {"itens": itens},
        ensure_ascii=False, indent=2), encoding="utf-8")


def item_guia(tr):
    """O item "Emitir CHAVE DE AUTORIZAÇÃO DO AGENDAMENTO - Nª via" (POST comum via jsfcljs)."""
    for el in tr.find_all(attrs={"onclick": re.compile("jsfcljs")}):
        texto = el.get_text(" ", strip=True)
        if "CHAVE DE AUTORIZA" in texto.upper():
            m = re.search(r"\{'([^']+)':'\1'\}", el["onclick"])
            return texto, m.group(1)
    return None, None


def linha_por_id(c, id_solic):
    d, corpo = pesquisar(c, "", "", id_solic)
    for ln in linhas(corpo):
        if ln["id"] == id_solic:
            return BeautifulSoup(corpo, "html.parser"), ln
    sys.exit(f"solicitação {id_solic} não voltou na pesquisa AGENDADA")


def texto_pdf(caminho: pathlib.Path) -> str:
    import pypdf
    return "\n".join(p.extract_text() or "" for p in pypdf.PdfReader(caminho).pages)


def passo_guia(c, saida: pathlib.Path, id_solic: str):
    doc, ln = linha_por_id(c, id_solic)
    texto, param = item_guia(ln["_tr"])
    if not param:
        sys.exit("a linha não oferece o item da chave de autorização")
    print(f"antes: menu diz {texto!r}  (param {param})")
    f = doc.find("form", id="form0")
    dados = campos(doc, "form0") | {"form0": "form0", param: param,
                                     "javax.faces.ViewState": viewstate(doc, "form0") or ""}
    r = c.post(f.get("action"), data=dados)  # POST comum: sem AJAXREQUEST
    ctype = r.headers.get("content-type", "")
    disp = r.headers.get("content-disposition", "")
    print(f"resposta: HTTP {r.status_code} | {ctype} | {disp or '(sem content-disposition)'} | "
          f"{len(r.content)} bytes")
    if r.content[:4] == b"%PDF":
        nome = re.search(r'filename="?([^";]+)', disp)
        arq = saida / f"guia_{id_solic}_{nome.group(1) if nome else 'chave.pdf'}"
        arq.write_bytes(r.content)
        print(f"PDF salvo: {arq}")
        txt = texto_pdf(arq)
        (saida / f"guia_{id_solic}.txt").write_text(txt, encoding="utf-8")
        for m in re.finditer(r"[^\n]*\bvia\b[^\n]*", txt, re.I):
            print("  no PDF:", m.group(0).strip())
    else:
        arq = saida / f"guia_{id_solic}_resposta.html"
        arq.write_bytes(r.content)
        print(f"não veio PDF; resposta salva em {arq}")

    _, ln2 = linha_por_id(c, id_solic)
    print(f"depois: menu diz {item_guia(ln2['_tr'])[0]!r}")


def passo_historico(c, id_solic: str):
    """A emissão da via aparece na trilha da solicitação?"""
    doc, ln = linha_por_id(c, id_solic)
    el = next(e for e in ln["_tr"].find_all(attrs={"onclick": re.compile("A4J.AJAX.Submit")})
              if "Historico" in e.get_text(" ", strip=True))
    f = doc.find("form", id="form0")
    r = c.post(f.get("action"), data=campos(doc, "form0") | {
        "form0": "form0", el["id"]: el["id"], "AJAXREQUEST": "_viewRoot", "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(doc, "form0") or ""}, headers=XHR)
    m = re.search(r'<meta name="Location" content="([^"]+)"', r.text)
    corpo = c.get(m.group(1).replace("&amp;", "&")).text if m else r.text
    t = BeautifulSoup(corpo, "html.parser").find("table", id="form0:historicoList")
    for tr in (t.find_all("tr") if t else []):
        # a 1ª célula da linha vem vazia; o cabeçalho se repete no corpo
        tds = [td.get_text(" ", strip=True) for td in tr.find_all("td")][1:]
        if tds and re.match(r"\d{2}/\d{2}/\d{4}", tds[0]):
            print("  |", " | ".join(tds[:7]))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("passo", choices=["menu", "guia", "historico"])
    ap.add_argument("--id", help="Id Solicitação (passo guia; padrão: o do sorteio.json)")
    ap.add_argument("--saida", required=True, type=pathlib.Path)
    ap.add_argument("--dias", type=int, default=30)
    a = ap.parse_args()
    a.saida.mkdir(parents=True, exist_ok=True)
    c = entrar()
    print("login + módulo ok")
    if a.passo == "menu":
        passo_menu(c, a.saida, a.dias)
    else:
        id_solic = a.id or json.loads((a.saida / "sorteio.json").read_text(encoding="utf-8"))["id"]
        (passo_guia(c, a.saida, id_solic) if a.passo == "guia" else passo_historico(c, id_solic))
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
