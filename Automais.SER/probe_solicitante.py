"""Prova: o filtro de Solicitante da tela de Histórico liga com autocomplete + onselect.

Protocolo extraído do ui.pack.js/framework.pack.js servidos pelo próprio SER (07/08/2026):

  1. fetch de sugestões  -> A4J com  inputvalue=<texto>, form0:j_id37=form0:j_id37,
                            ajaxSingle=form0:j_id37, AJAXREQUEST=_viewRoot
  2. onselect            -> A4J com  form0:j_id37_selection=<índice da linha escolhida>,
                            form0:j_id37:j_id42=form0:j_id37:j_id42,
                            ajaxSingle=form0:j_id37, AJAXREQUEST=_viewRoot
     (o hidden _selection é preenchido SÓ durante este submit e limpo em seguida —
      por isso parecia sempre vazio no DOM)
  3. btnSearch           -> como o motor já faz; seguir o redirect A4J embutido
  4. btnExport           -> POST comum (jsfcljs) no action da página de RESULTADO

Rodadas: 2x filtrada (determinismo; 1º registro esperado 2727024) + 1x só-texto
(controle; espera-se o recorte do Estado, 1º registro 2875441).

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
CAP = RAIZ / "capturas"

BASE = "https://ser.saude.rj.gov.br"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

URL_HIST = "/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam"

SOLICITANTE = "GESTOR SMS MARICA"
SUGG_INPUT = "form0:suggUnidadeSol"
SUGG_BOX = "form0:j_id37"                      # container do rich:suggestionbox
SUGG_ONSELECT = "form0:j_id37:j_id42"          # a4j:support event=onselect
SUGG_SELECTION = "form0:j_id37_selection"      # hidden com o índice da linha
SITUACAO_COMBO = "form0:j_id57"


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


def action_do_form(doc, form_id):
    f = doc.find("form", id=form_id)
    return f.get("action") if f else None


def viewstate_da_resposta(html):
    """A4J re-renderiza o ViewState no corpo parcial; usar o mais novo se vier."""
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else None


def login_e_modulo(c: httpx.Client) -> None:
    pag = c.get("/ser/login").text
    d = BeautifulSoup(pag, "html.parser")
    action = d.find("form", id="login").get("action")
    r = c.post(action, data=campos(d, "login") | {
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


def sugestoes_da_resposta(html):
    """Linhas do form0:j_id37:suggest na ordem do DOM (o índice do onselect é esta ordem)."""
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id=f"{SUGG_BOX}:suggest")
    if not t:
        return None
    corpo = t.find("tbody") or t
    linhas = []
    for tr in corpo.find_all("tr", recursive=False):
        linhas.append(" ".join(tr.get_text(" ", strip=True).split()))
    return linhas


def primeira_linha_e_ids(html):
    """Grade do Histórico: Tipo | Recurso | ID | Data | CNS | Paciente | ... — ID é a 3ª coluna."""
    d = BeautifulSoup(html, "html.parser")
    t = d.find("table", id="form0:listagem")
    if not t:
        return None, []
    ids, primeira = [], None
    for tr in t.select("tbody tr"):
        tds = tr.find_all("td")
        if len(tds) < 4:
            continue
        cid = tds[2].get_text(strip=True)
        if re.fullmatch(r"\d{6,}", cid):
            ids.append(cid)
            if primeira is None:
                primeira = " | ".join(td.get_text(" ", strip=True) for td in tds[:8])
    return primeira, ids


def exportar(c: httpx.Client, resultado_html: str, rotulo: str):
    """POST do btnExport no action da página de RESULTADO (jsfcljs, sem AJAXREQUEST)."""
    d = BeautifulSoup(resultado_html, "html.parser")
    action = action_do_form(d, "form0")
    dados = campos(d, "form0") | {
        "form0:btnExport": "form0:btnExport",
        "javax.faces.ViewState": viewstate(d, "form0") or "",
    }
    r = c.post(action, data=dados)
    corpo = r.content
    ole2 = corpo[:8] == b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1"
    print(f"  export: {len(corpo)} bytes, OLE2={'SIM' if ole2 else 'NÃO'}")
    if not ole2:
        (CAP / f"export_ruim_{rotulo}.html").write_bytes(corpo)
        return None
    caminho = CAP / f"export_{rotulo}.xls"
    caminho.write_bytes(corpo)
    import xlrd
    livro = xlrd.open_workbook(file_contents=corpo)
    aba = livro.sheet_by_index(0)
    cab = [str(v.value).strip() for v in aba.row(0)]
    col_id = next(i for i, n in enumerate(cab) if "ID" in n.upper())
    ids = []
    for i in range(1, aba.nrows):
        v = aba.cell_value(i, col_id)
        ids.append(str(int(v)) if isinstance(v, float) else str(v).strip())
    print(f"  export: {aba.nrows - 1} linhas; 1º ID = {ids[0] if ids else '(vazio)'}")
    return ids


def aviso_de_corte(html):
    d = BeautifulSoup(html, "html.parser")
    m = d.find(id="form0:messages")
    return m.get_text(" ", strip=True) if m else ""


def rodada(c: httpx.Client, rotulo: str, com_autocomplete: bool, com_export: bool = False):
    print(f"\n=== RODADA {rotulo} (autocomplete={'SIM' if com_autocomplete else 'NÃO'}) ===")
    # tela SEMPRE nova (regra: nunca reusar página de resultado)
    tela = c.get(URL_HIST).text
    d = BeautifulSoup(tela, "html.parser")
    action = action_do_form(d, "form0")
    vs = viewstate(d, "form0") or ""
    base = campos(d, "form0")
    base[SITUACAO_COMBO] = "AGENDADA"
    base["form0:tipo"] = "CONSULTA"

    if com_autocomplete:
        # --- 1) fetch de sugestões ---
        dados = dict(base)
        dados[SUGG_INPUT] = SOLICITANTE
        dados[SUGG_SELECTION] = ""
        r = c.post(action, data=dados | {
            "AJAXREQUEST": "_viewRoot",
            "inputvalue": SOLICITANTE,
            SUGG_BOX: SUGG_BOX,
            "ajaxSingle": SUGG_BOX,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": vs,
        }, headers={"X-Requested-With": "XMLHttpRequest"})
        (CAP / f"sugg_fetch_{rotulo}.html").write_text(r.text, encoding="utf-8")
        linhas = sugestoes_da_resposta(r.text)
        if linhas is None:
            print("  !! resposta do fetch não trouxe a tabela de sugestões"); return None
        print(f"  sugestões ({len(linhas)}):")
        for i, s in enumerate(linhas):
            print(f"    [{i}] {s[:90]}")
        alvo = [i for i, s in enumerate(linhas) if s.strip().upper() == SOLICITANTE]
        if not alvo:
            alvo = [i for i, s in enumerate(linhas) if SOLICITANTE in s.upper()]
        if not alvo:
            print("  !! GESTOR SMS MARICA não veio nas sugestões"); return None
        idx = alvo[0]
        print(f"  -> selecionando índice {idx}")
        vs = viewstate_da_resposta(r.text) or vs

        # --- 2) onselect (com o hidden _selection preenchido, como o browser faz) ---
        dados = dict(base)
        dados[SUGG_INPUT] = SOLICITANTE
        dados[SUGG_SELECTION] = str(idx)
        r = c.post(action, data=dados | {
            "AJAXREQUEST": "_viewRoot",
            SUGG_ONSELECT: SUGG_ONSELECT,
            "ajaxSingle": SUGG_BOX,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": vs,
        }, headers={"X-Requested-With": "XMLHttpRequest"})
        (CAP / f"sugg_onselect_{rotulo}.html").write_text(r.text, encoding="utf-8")
        vs = viewstate_da_resposta(r.text) or vs

    # --- 3) busca ---
    dados = dict(base)
    dados[SUGG_INPUT] = SOLICITANTE
    dados[SUGG_SELECTION] = ""          # o browser limpa após o onselect
    r = c.post(action, data=dados | {
        "AJAXREQUEST": "_viewRoot",
        "form0:btnSearch": "form0:btnSearch",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": vs,
    }, headers={"X-Requested-With": "XMLHttpRequest"})
    m = re.search(r'<meta name="Location" content="([^"]+)"', r.text)
    if not m:
        (CAP / f"busca_sem_redirect_{rotulo}.html").write_text(r.text, encoding="utf-8")
        print("  !! busca não devolveu redirect A4J"); return None
    resultado = c.get(m.group(1).replace("&amp;", "&")).text
    (CAP / f"resultado_{rotulo}.html").write_text(resultado, encoding="utf-8")

    primeira, ids = primeira_linha_e_ids(resultado)
    aviso = aviso_de_corte(resultado)
    print(f"  1º registro : {primeira}")
    print(f"  ids página 1: {ids[:20]}")
    print(f"  mensagens   : {aviso or '(nenhuma)'}")
    ids_export = exportar(c, resultado, rotulo) if com_export else None
    return {"primeira": primeira, "ids": ids, "aviso": aviso, "export": ids_export}


def main() -> int:
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)

    r1 = rodada(c, "A_filtrada", com_autocomplete=True, com_export=True)
    r2 = rodada(c, "B_filtrada", com_autocomplete=True)
    r3 = rodada(c, "C_so_texto", com_autocomplete=False)

    print("\n=== VEREDITO ===")
    ok = True
    if not (r1 and r2):
        print("FALHOU: rodada filtrada não completou"); ok = False
    else:
        det = r1["ids"] == r2["ids"] and r1["primeira"] == r2["primeira"]
        print(f"determinismo A==B : {'SIM' if det else 'NÃO'}")
        alvo = r1["ids"][0] if r1["ids"] else "?"
        print(f"1º registro A     : {alvo} ({(r1['primeira'] or '')[:70]})")
        print(f"esperado          : 2727024 (DAIANA, 06/01/2020)")
        if alvo != "2727024":
            print("FALHOU: 1º registro não é 2727024"); ok = False
        if not det:
            ok = False
    if r1 and r1.get("export") is not None:
        exp = r1["export"]
        casa = exp[: len(r1["ids"])] == r1["ids"] if r1["ids"] else False
        print(f"export: {len(exp)} linhas; 1º = {exp[0] if exp else '?'}; "
              f"grade página 1 contida no export: {'SIM' if casa else 'NÃO'}")
        if not exp or exp[0] != (r1["ids"][0] if r1["ids"] else None):
            print("FALHOU: export não abre com o mesmo 1º registro da grade"); ok = False
    if r3 and r1:
        iguais = r3["ids"] == r1["ids"]
        print(f"controle só-texto difere da filtrada: {'SIM' if not iguais else 'NÃO (suspeito!)'}")
        print(f"1º do controle    : {(r3['primeira'] or '')[:80]}")

    c.close()
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
