"""A tela de SOLICITAÇÃO também exporta? (o `form0:btnExport` que o docs/ser.md marca como
"não avaliado")

Se o export dessa tela ignorar o teto de 100 da grade — como o da tela de Histórico ignora o de
20 por página —, a varredura de ALTA para de depender de paginação, que é hoje o único caminho
do motor sem sinal de truncamento declarado (perdeu 853 registros EM SILÊNCIO em 07/08/2026).

Mede três coisas:
  1. quantas páginas a grade mostra (o teto inferido: 5 = 100 registros);
  2. quantas linhas o .xls traz — se > 100, o teto é só da TELA;
  3. se existe aviso escrito de corte, como o "retorno limitado em 500" da tela de Histórico.

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

URL_SOLIC = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"
COMBO_SITUACAO = "form0:j_id75"


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


def action(doc, form_id):
    f = doc.find("form", id=form_id)
    return f.get("action") if f else None


def paginas(html, scroller="form0:sc1"):
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id=f"{scroller}_table")
    if not t:
        return 0
    n = [int(x.get_text(strip=True)) for x in t.find_all("td") if x.get_text(strip=True).isdigit()]
    return max(n) if n else 1


def mensagens(html):
    d = BeautifulSoup(html, "html.parser")
    cx = d.find(id="form0:messages")
    return cx.get_text(" ", strip=True) if cx else ""


def linhas_da_grade(html):
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id="form0:listagem")
    if not t:
        return []
    ids = []
    for tr in t.select("tbody tr"):
        tds = tr.find_all("td")
        if not tds:
            continue
        v = tds[0].get_text(strip=True)
        if re.fullmatch(r"\d{5,}", v):
            ids.append(v)
    return ids


def login_e_modulo(c):
    pag = c.get("/ser/login").text
    d = BeautifulSoup(pag, "html.parser")
    r = c.post(d.find("form", id="login").get("action"), data=campos(d, "login") | {
        "login": "login",
        "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar",
        "javax.faces.ViewState": viewstate(d, "login") or "j_id1",
    })
    if 'id="login:username"' in r.text:
        raise SystemExit("LOGIN FALHOU")
    print("login ok")

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
        raise SystemExit("módulo não ativou")
    c.get(loc)
    print("módulo ambulatorial ok")


def rodada(c, rotulo, situacao, tipo=None, inicio=None, fim=None):
    print(f"\n=== {rotulo}: situacao={situacao} tipo={tipo or '(todos)'} "
          f"janela={inicio or '-'}..{fim or '-'} ===")

    # tela SEMPRE nova (regra do GET novo por busca)
    tela = c.get(URL_SOLIC).text
    d = BeautifulSoup(tela, "html.parser")
    act = action(d, "form0")
    botao = d.find(attrs={"title": "Pesquisar"}).get("id")

    dados = campos(d, "form0")
    dados[COMBO_SITUACAO] = situacao
    if tipo:
        dados["form0:comboTipoRecurso"] = tipo
    if inicio:
        dados["form0:dtInicialSolicitacaoInputDate"] = inicio
    if fim:
        dados["form0:dtFinalSolicitacaoInputDate"] = fim

    r = c.post(act, data=dados | {
        "form0": "form0", botao: botao, "AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(d, "form0") or "",
    }, headers={"X-Requested-With": "XMLHttpRequest"})

    corpo = r.text
    if (m := re.search(r'<meta name="Location" content="([^"]+)"', corpo)):
        corpo = c.get(m.group(1).replace("&amp;", "&")).text
        print("  (seguiu redirect A4J)")
    (CAP / f"solic_result_{rotulo}.html").write_text(corpo, encoding="utf-8")

    p = paginas(corpo)
    ids_grade = linhas_da_grade(corpo)
    print(f"  GRADE : {p} página(s) → teto inferido ~{p*20} | 1ª página tem {len(ids_grade)} linhas")
    print(f"  aviso : {mensagens(corpo) or '(nenhum)'}")

    # ---- o experimento: exportar ----
    dr = BeautifulSoup(corpo, "html.parser")
    act_export = action(dr, "form0") or act
    base_campos = campos(dr, "form0") or dados
    vs = viewstate(dr, "form0") or viewstate(d, "form0") or ""

    # o filtro vai junto de novo: se o SER refizer a consulta em vez de reusar a conversa,
    # a planilha ainda sai do recorte certo
    base_campos[COMBO_SITUACAO] = situacao
    if tipo:
        base_campos["form0:comboTipoRecurso"] = tipo
    if inicio:
        base_campos["form0:dtInicialSolicitacaoInputDate"] = inicio
    if fim:
        base_campos["form0:dtFinalSolicitacaoInputDate"] = fim

    # Exportar é jsfcljs (Mojarra): POST comum, SEM AJAXREQUEST
    r = c.post(act_export, data=base_campos | {
        "form0": "form0",
        "form0:btnExport": "form0:btnExport",
        "javax.faces.ViewState": vs,
    })
    corpo_bin = r.content
    ole2 = corpo_bin[:8] == b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1"
    print(f"  EXPORT: {len(corpo_bin)} bytes | content-type={r.headers.get('content-type')} | OLE2={'SIM' if ole2 else 'NÃO'}")

    if not ole2:
        (CAP / f"solic_export_ruim_{rotulo}.bin").write_bytes(corpo_bin)
        print("    !! não veio planilha — salvo para inspeção")
        return None

    (CAP / f"solic_export_{rotulo}.xls").write_bytes(corpo_bin)
    import xlrd
    livro = xlrd.open_workbook(file_contents=corpo_bin)
    aba = livro.sheet_by_index(0)
    cab = [str(v.value).strip() for v in aba.row(0)]
    print(f"    abas={livro.sheet_names()} | colunas={cab}")
    col_id = next((i for i, n in enumerate(cab) if "ID" in n.upper()), 0)
    ids = []
    for i in range(1, aba.nrows):
        v = aba.cell_value(i, col_id)
        ids.append(str(int(v)) if isinstance(v, float) else str(v).strip())
    print(f"    LINHAS NO ARQUIVO: {aba.nrows - 1}  (grade mostrava no máximo ~{p*20})")
    print(f"    1º id={ids[0] if ids else '-'} | último id={ids[-1] if ids else '-'} | únicos={len(set(ids))}")
    if ids_grade:
        cobre = set(ids_grade).issubset(set(ids))
        print(f"    contém as linhas da 1ª página da grade: {'SIM' if cobre else 'NÃO'}")
    return ids


def main() -> int:
    c = httpx.Client(base_url=BASE, timeout=180, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)

    # 1) ALTA sem data: é o recorte que hoje perde 853 registros em silêncio
    alta = rodada(c, "alta_total", "ALTA")

    # 2) ALTA só CONSULTA: nosso espelho tem 737, o SER mostra 1.067
    rodada(c, "alta_consulta", "ALTA", tipo="CONSULTA")

    # 3) controle: EM_FILA, onde já sabemos o número certo (2.423)
    rodada(c, "emfila_total", "EM_FILA")

    print("\n=== CONCLUSÃO ===")
    if alta is not None and len(alta) > 100:
        print(f"O export da tela de Solicitação IGNORA o teto de 100: {len(alta)} linhas.")
        print("→ ALTA pode sair da paginação e passar a ser lida por arquivo.")
    elif alta is not None:
        print(f"O export trouxe {len(alta)} linhas — o teto de 100 vale para o arquivo também.")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
