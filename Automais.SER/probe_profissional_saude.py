"""Levantamento do cadastro de Profissionais de Saúde no SER (menu Cadastro → Profissionais).

O que mede (para modelarmos a gestão de médicos no SMSMarica e o envio de médico novo ao SER):
  1. como se chega à tela (módulo exigido, permissão do perfil);
  2. a pesquisa: filtros, colunas, paginação, contagem com filtro vazio/genérico;
  3. a ficha de um profissional existente (só LER os campos — nada de salvar);
  4. o formulário vazio de inclusão (só abrir — o botão de gravar NÃO é clicado);
  5. as listas de domínio (especialidade, conselho, UF, CBO...) → capturas/profissional/listas.json;
  6. a relação com o combo `form0:medicoResp` da nova solicitação e o `form0:especialidadeMedico`.

LEITURA PURA. Permitido: login, GETs, POST de PESQUISA, A4J de troca de combo que só
re-renderiza, abrir tela de "novo"/"editar" para ler. PROIBIDO acionar qualquer gravar/salvar/
incluir/excluir/alterar/vincular/ativar — nem para ver mensagem de erro.

Uma sessão só: os cookies ficam em capturas/profissional/sessao.json (gitignored) e cada passo
reaproveita a sessão; só faz login de novo se ela tiver caído.

Uso (Git Bash: MSYS_NO_PATHCONV=1):  python probe_profissional_saude.py <passo> [args]
      inicio                      GET da tela de pesquisa (login + módulo ambulatorial se preciso)
      pesquisar <nome> <rotulo>   POST do botão Pesquisar (form0:j_id36)
      pagina <arquivo> <n|last>   datascroller A4J da listagem
      novo                        aba "Adicionar Novo" (form vazio)
      buscacpf existente|invalido onblur do CPF na aba novo (consulta; traz a ficha se existir)
      cbo <termo>...              sugestões do autocomplete de CBO
      listas                      combos de domínio → listas.json
      abrir/pesqabrir             link "Editar" da linha — responde HTTP 500 (também no navegador)
      exportar [max_paginas]      filtro vazio + TODAS as páginas → listagem_completa.json
      amostra [n] [semente]       onblur de CPF de N registros COM CPF → amostra_fichas.json

Achados (01/10/2026): ver o INDICE.md (seção Automais.SER) e o relatório da sessão.
HTML cru (com PII) fica em capturas/profissional/ — gitignored.
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
BASE = os.environ.get("SER_BASE_URL", "https://ser.saude.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/126.0 Safari/537.36")
CAP = RAIZ / "capturas" / "profissional"
CAP.mkdir(parents=True, exist_ok=True)
SESSAO = CAP / "sessao.json"

TELA_PESQUISAR = "/ser/pages/cadastro/profissionalSaude/profissional-pesquisar.seam"
TELA_SOLICITACAO = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"

# Palavras que denunciam um botão de escrita. Qualquer submit com isso no id/value/title é barrado.
PALAVRAS_ESCRITA = ("gravar", "salvar", "incluir", "confirmar", "excluir", "alterar",
                    "vincular", "ativar", "desativar", "remover", "adicionar", "inativar")


# ------------------------------------------------------------------ utilitários HTML

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
            o = s.find("option", selected=True)
            d[n] = (o.get("value") if o else "") or ""
    for t in f.find_all("textarea"):
        n = t.get("name")
        if n:
            d[n] = t.get_text() or ""
    return d


def viewstate(doc, form_id):
    f = doc.find("form", id=form_id)
    i = f.find("input", attrs={"name": "javax.faces.ViewState"}) if f else None
    return i.get("value") if i else None


def action(doc, form_id):
    f = doc.find("form", id=form_id)
    return f.get("action") if f else None


def salvar(nome, texto):
    (CAP / nome).write_text(texto, encoding="utf-8")


def pausa():
    time.sleep(1.0)


def checar_leitura(nome_param: str) -> None:
    """Trava de segurança: nenhum parâmetro de botão com cara de escrita sai daqui."""
    low = nome_param.lower()
    for p in PALAVRAS_ESCRITA:
        if p in low:
            raise SystemExit(f"BLOQUEADO: parâmetro '{nome_param}' parece ação de escrita")


# ------------------------------------------------------------------ sessão

def cliente() -> httpx.Client:
    c = httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                     follow_redirects=True, verify=False)
    if SESSAO.exists():
        for ck in json.loads(SESSAO.read_text(encoding="utf-8")):
            c.cookies.set(ck["name"], ck["value"], domain=ck["domain"], path=ck["path"])
    return c


def guardar_sessao(c: httpx.Client) -> None:
    SESSAO.write_text(json.dumps([
        {"name": k.name, "value": k.value, "domain": k.domain, "path": k.path}
        for k in c.cookies.jar]), encoding="utf-8")


def login(c: httpx.Client) -> None:
    c.cookies.clear()
    d = BeautifulSoup(c.get("/ser/login").text, "html.parser")
    acao = d.find("form", id="login").get("action")
    r = c.post(acao, data=campos(d, "login") | {
        "login": "login",
        "login:username": os.environ["SER_USUARIO"],
        "login:password": os.environ["SER_SENHA"],
        "login:entrar": "Entrar",
        "javax.faces.ViewState": viewstate(d, "login") or "j_id1",
    })
    if 'id="login:username"' in r.text:
        raise SystemExit("LOGIN FALHOU")
    print("login ok")
    pausa()
    home = c.get("/ser/home.seam").text
    salvar("home.html", home)
    mods = re.findall(r"goModulo\('([a-z_]+)'\)", home)
    print("módulos na home:", mods)
    m = re.search(r'<script[^>]*\bid="([^":]+):goModulo"', home)
    if not m:
        raise SystemExit("home sem goModulo (aviso pendente? 'Marcar Lida' presente: "
                         f"{'Marcar Lida' in home})")
    fid = m.group(1)
    dh = BeautifulSoup(home, "html.parser")
    pausa()
    r = c.post(action(dh, fid), data=campos(dh, fid) | {
        fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
        "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(dh, fid) or "",
    }, headers={"X-Requested-With": "XMLHttpRequest"}, follow_redirects=False)
    loc = r.headers.get("location")
    if not loc:
        m = re.search(r'<meta[^>]*name="Location"[^>]*content="([^"]+)"', r.text)
        loc = m.group(1) if m else None
    if not loc:
        raise SystemExit("módulo não ativou")
    pausa()
    c.get(loc)
    print("módulo ambulatorial ok")
    guardar_sessao(c)


def get_logado(c: httpx.Client, url: str) -> httpx.Response:
    """GET que refaz login se a sessão caiu (cai no login ou HTTP 500 de módulo)."""
    r = c.get(url)
    if 'id="login:username"' in r.text or r.status_code == 500:
        print(f"  (sessão caída: HTTP {r.status_code}; relogando)")
        login(c)
        pausa()
        r = c.get(url)
    guardar_sessao(c)
    return r


# ------------------------------------------------------------------ inspeção

def descrever_form(html: str, form_id: str | None = None) -> list[dict]:
    """Lista os controles de um form: nome, tipo, rótulo próximo, obrigatório, nº de opções."""
    doc = BeautifulSoup(html, "html.parser")
    forms = [doc.find("form", id=form_id)] if form_id else doc.find_all("form")
    out = []
    for f in forms:
        if f is None:
            continue
        for el in f.find_all(["input", "select", "textarea", "button", "a"]):
            nome = el.get("name") or el.get("id")
            if not nome:
                continue
            tipo = el.name if el.name != "input" else (el.get("type") or "text")
            if el.name == "a" and not (el.get("onclick") and ("jsfcljs" in el["onclick"] or
                                                              "A4J" in el["onclick"])):
                continue
            if tipo == "hidden" and "ViewState" in nome:
                continue
            # rótulo: <label for>, ou texto do td/div anterior
            rot = ""
            if el.get("id"):
                lab = doc.find("label", attrs={"for": el["id"]})
                if lab:
                    rot = lab.get_text(" ", strip=True)
            if not rot:
                td = el.find_parent(["td", "div"])
                prev = td.find_previous_sibling(["td", "div"]) if td else None
                if prev:
                    rot = prev.get_text(" ", strip=True)[:60]
                if not rot and td:
                    rot = td.get_text(" ", strip=True)[:60]
            item = {"form": f.get("id"), "nome": nome, "id": el.get("id"), "tipo": tipo,
                    "rotulo": rot, "valor": (el.get("value") or "")[:40],
                    "title": el.get("title") or "", "disabled": el.has_attr("disabled"),
                    "onchange": (el.get("onchange") or el.get("onclick") or "")[:160]}
            if el.name == "select":
                ops = el.find_all("option")
                item["n_opcoes"] = len(ops)
                item["amostra"] = [(o.get("value"), o.get_text(strip=True)) for o in ops[:4]]
            if el.name == "input" and el.get("maxlength"):
                item["maxlength"] = el["maxlength"]
            out.append(item)
    return out


def imprimir_form(itens):
    for i in itens:
        extra = f" opções={i['n_opcoes']} {i['amostra']}" if "n_opcoes" in i else ""
        ml = f" maxlen={i['maxlength']}" if i.get("maxlength") else ""
        dis = " DISABLED" if i["disabled"] else ""
        print(f"  [{i['form']}] {i['tipo']:9} {i['nome']:55} rot={i['rotulo'][:40]!r}"
              f" title={i['title'][:30]!r}{ml}{dis}{extra}")
        if i["onchange"]:
            print(f"        on: {i['onchange']}")


def texto_limpo(html, ini, fim=None):
    return " ".join(BeautifulSoup(html[ini:fim], "html.parser").get_text(" ").split())


# ------------------------------------------------------------------ passos

def passo_inicio(c):
    r = get_logado(c, TELA_PESQUISAR)
    print(f"GET pesquisar -> HTTP {r.status_code}, {len(r.text)} bytes, url={r.url}")
    salvar("pesquisar.html", r.text)
    doc = BeautifulSoup(r.text, "html.parser")
    msgs = doc.find_all(class_=re.compile("rich-messages|message|erro", re.I))
    for m in msgs[:5]:
        t = m.get_text(" ", strip=True)
        if t:
            print("  msg:", t[:200])
    print("forms:", [f.get("id") for f in doc.find_all("form")])
    imprimir_form(descrever_form(r.text, "form0") or descrever_form(r.text))


def html_de(r: httpx.Response) -> str:
    """O SER serve ISO-8859-1 e às vezes DECLARA UTF-8 (resposta A4J) — o header mente.
    Decodificar pelo conteúdo: UTF-8 estrito, senão ISO-8859-1."""
    try:
        return r.content.decode("utf-8")
    except UnicodeDecodeError:
        return r.content.decode("iso-8859-1")


def resumo_listagem(html: str) -> None:
    doc = BeautifulSoup(html, "html.parser")
    t = doc.find("table", id="form0:listagem")
    linhas = t.select("tbody#form0\\:listagem\\:tb > tr") if t else []
    print(f"  linhas na página: {len(linhas)}")
    for tr in linhas[:3]:
        tds = tr.find_all("td", recursive=False)
        # formato, sem dado pessoal: tamanho/forma de cada célula
        forma = []
        for td in tds:
            v = td.get_text(" ", strip=True)
            forma.append(re.sub(r"[A-Za-zÀ-ÿ]", "A", re.sub(r"\d", "9", v))[:30])
        acoes = [(a.get("id"), a.get("title"), (a.get("onclick") or "")[:200])
                 for a in tds[0].find_all(["a", "input", "img"])] if tds else []
        print("   forma:", forma)
        print("   ações:", acoes)
    sc = doc.find(id="form0:sc1_table")
    if sc:
        print("  paginador:", " ".join(sc.get_text(" ", strip=True).split()))
    for m in doc.find_all(class_=re.compile("rich-messages-label|message", re.I)):
        t = m.get_text(" ", strip=True)
        if t:
            print("  msg:", t[:200])


def passo_pesquisar(c, nome="", rotulo="vazio", fonetica=""):
    r = get_logado(c, TELA_PESQUISAR)
    html = html_de(r)
    doc = BeautifulSoup(html, "html.parser")
    dados = campos(doc, "form0")
    dados["form0:nome"] = nome
    if fonetica:
        dados["form0:fonetica"] = "on"
    dados["form0:j_id36"] = "Pesquisar"   # botão Pesquisar (submit comum) — leitura
    pausa()
    r = c.post(action(doc, "form0"), data=dados)
    html = html_de(r)
    salvar(f"pesquisa_{rotulo}.html", html)
    print(f"POST pesquisar nome={'<vazio>' if not nome else '<termo>'} -> HTTP {r.status_code},"
          f" {len(html)} bytes")
    resumo_listagem(html)
    guardar_sessao(c)


def passo_pagina(c, arquivo="pesquisa_vazio.html", pagina="last"):
    """Datascroller A4J (ajaxSingle) da listagem — só troca de página, leitura."""
    html = (CAP / arquivo).read_text(encoding="utf-8")
    doc = BeautifulSoup(html, "html.parser")
    dados = campos(doc, "form0") | {
        "AJAXREQUEST": "_viewRoot", "ajaxSingle": "form0:sc1", "form0:sc1": pagina,
        "AJAX:EVENTS_COUNT": "1",
    }
    pausa()
    r = c.post(action(doc, "form0"), data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
    h = html_de(r)
    salvar(f"pagina_{pagina}.html", h)
    print(f"A4J página={pagina} -> HTTP {r.status_code}, {len(h)} bytes")
    resumo_listagem(h)
    guardar_sessao(c)


def passo_abrir(c, arquivo="pesquisa_silva.html", linha="0", rotulo="ficha"):
    """Clica no link 'Editar' (h:commandLink → jsfcljs) de uma linha: só ABRE a ficha. Nada salva."""
    html = (CAP / arquivo).read_text(encoding="utf-8")
    doc = BeautifulSoup(html, "html.parser")
    link = doc.find("td", id=re.compile(rf"^form0:listagem:{linha}:")).find("a")
    m = re.search(r"\{'([^']+)':'([^']+)'\}", link.get("onclick") or "")
    if not m or link.get_text(strip=True) != "Editar":
        raise SystemExit("link Editar não encontrado")
    dados = campos(doc, "form0") | {m.group(1): m.group(2)}
    pausa()
    r = c.post(action(doc, "form0"), data=dados)
    h = html_de(r)
    salvar(f"{rotulo}.html", h)
    print(f"Editar linha {linha} -> HTTP {r.status_code}, {len(h)} bytes, url={r.url}")
    imprimir_form(descrever_form(h, "form0"))
    guardar_sessao(c)


def passo_novo(c, rotulo="novo_vazio"):
    """Troca para a aba 'Adicionar Novo' (tabPanel switchType=server): só ABRE o formulário vazio."""
    r = get_logado(c, TELA_PESQUISAR)
    html = html_de(r)
    doc = BeautifulSoup(html, "html.parser")
    dados = campos(doc, "form0") | {"form0:editar_server_submit": "form0:editar_server_submit"}
    pausa()
    r = c.post(action(doc, "form0"), data=dados)
    h = html_de(r)
    salvar(f"{rotulo}.html", h)
    print(f"aba Adicionar Novo -> HTTP {r.status_code}, {len(h)} bytes")
    imprimir_form(descrever_form(h, "form0"))
    guardar_sessao(c)


def resposta_a4j_resumo(h: str) -> None:
    doc = BeautifulSoup(h, "html.parser")
    ids = re.search(r'name="Ajax-Update-Ids"[^>]*content="([^"]*)"', h)
    print("  Ajax-Update-Ids:", ids.group(1) if ids else "(sem)")
    for m in doc.find_all(["span", "div", "li"], class_=re.compile("message|erro|rich-messages", re.I)):
        t = m.get_text(" ", strip=True)
        if t:
            print("  msg:", t[:200])
    for nome in ("form0:txtNome", "form0:telefone", "form0:btnGravar", "form0:aplicInicial2"):
        el = doc.find(attrs={"name": nome})
        if el is not None:
            v = el.get("value") or ""
            print(f"  {nome}: valor={'<preenchido %d chars>' % len(v) if v else '<vazio>'}"
                  f" disabled={el.has_attr('disabled')}")
    tb = doc.find("tbody", id="form0:listagem:tb")
    if tb is not None:
        linhas = tb.find_all("tr", recursive=False)
        print(f"  lotações na tabela: {len(linhas)}")
        for tr in linhas[:5]:
            tds = tr.find_all("td", recursive=False)
            print("    especialidade:", tds[1].get_text(strip=True) if len(tds) > 1 else "?",
                  "| ativo:", "marcado" if tr.find("input", checked=True) else "não")


def passo_buscacpf(c, origem="existente"):
    """Aba Adicionar Novo + onblur do CPF (a4j:support supBuscaCpfNovo) — consulta, não grava."""
    r = get_logado(c, TELA_PESQUISAR)
    doc = BeautifulSoup(html_de(r), "html.parser")
    pausa()
    r = c.post(action(doc, "form0"), data=campos(doc, "form0") |
               {"form0:editar_server_submit": "form0:editar_server_submit"})
    h = html_de(r)
    doc = BeautifulSoup(h, "html.parser")
    if origem == "existente":
        base = BeautifulSoup((CAP / "pesquisa_vazio.html").read_text(encoding="utf-8"),
                             "html.parser")
        cpf = next(td.get_text(strip=True) for td in base.select(
            'tbody[id="form0:listagem:tb"] > tr > td:nth-of-type(2)') if td.get_text(strip=True))
    else:
        cpf = "111.111.111-11"   # CPF inválido de propósito (DV/sequência)
    dados = campos(doc, "form0") | {
        "form0:aplicInicial2": cpf, "form0:supBuscaCpfNovo": "form0:supBuscaCpfNovo",
        "ajaxSingle": "form0:aplicInicial2", "AJAXREQUEST": "_viewRoot", "AJAX:EVENTS_COUNT": "1",
    }
    pausa()
    r = c.post(action(doc, "form0"), data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
    h = html_de(r)
    salvar(f"buscacpf_{origem}.html", h)
    print(f"onblur CPF ({origem}) -> HTTP {r.status_code}, {len(h)} bytes")
    resposta_a4j_resumo(h)
    guardar_sessao(c)


def passo_cbo(c, *termos):
    """Sugestões do autocomplete de CBO (form0:txtCBO) na aba Adicionar Novo — leitura."""
    r = get_logado(c, TELA_PESQUISAR)
    doc = BeautifulSoup(html_de(r), "html.parser")
    pausa()
    r = c.post(action(doc, "form0"), data=campos(doc, "form0") |
               {"form0:editar_server_submit": "form0:editar_server_submit"})
    h = html_de(r)
    salvar("novo_vazio.html", h)
    doc = BeautifulSoup(h, "html.parser")
    m = re.search(r"Richfaces\.onAvailable\('form0:txtCBO'.*?'form0','form0:txtCBO','([^']+)'", h)
    box = m.group(1)
    listas = json.loads((CAP / "listas.json").read_text(encoding="utf-8")) \
        if (CAP / "listas.json").exists() else {}
    listas.setdefault("cbo_sugestoes", {})
    for termo in termos:
        dados = campos(doc, "form0") | {
            "AJAXREQUEST": "_viewRoot", "inputvalue": termo, box: box, "ajaxSingle": box,
            "AJAX:EVENTS_COUNT": "1", "form0:txtCBO": termo,
        }
        pausa()
        r = c.post(action(doc, "form0"), data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
        hh = html_de(r)
        salvar(f"cbo_{re.sub(r'\\W', '_', termo)}.html", hh)
        d2 = BeautifulSoup(hh, "html.parser")
        t = d2.find("table", id=f"{box}:suggest")
        linhas = []
        if t:
            for tr in t.find_all("tr"):
                cel = [td.get_text(" ", strip=True) for td in tr.find_all("td")]
                if any(cel):
                    linhas.append(cel)
        listas["cbo_sugestoes"][termo] = linhas
        print(f"CBO '{termo}': {len(linhas)} sugestões; ex.: {linhas[:4]}")
    (CAP / "listas.json").write_text(json.dumps(listas, ensure_ascii=False, indent=1),
                                     encoding="utf-8")
    guardar_sessao(c)


def passo_listas(c):
    """Extrai os combos de domínio já capturados (pesquisa + aba novo) para listas.json."""
    listas = json.loads((CAP / "listas.json").read_text(encoding="utf-8")) \
        if (CAP / "listas.json").exists() else {}
    for arq, nome_json in (("pesquisar.html", None), ("novo_vazio.html", None)):
        doc = BeautifulSoup((CAP / arq).read_text(encoding="utf-8"), "html.parser")
        f = doc.find("form", id="form0")
        for s in f.find_all("select"):
            ops = [{"value": o.get("value"), "rotulo": o.get_text(strip=True)}
                   for o in s.find_all("option")]
            listas[s.get("name")] = ops
            print(f"{arq}: {s.get('name')} -> {len(ops)} opções")
    (CAP / "listas.json").write_text(json.dumps(listas, ensure_ascii=False, indent=1),
                                     encoding="utf-8")


def linhas_listagem(html: str) -> list[dict]:
    """Linhas da listagem de profissionais: índice absoluto, CPF, Documento, Tipo, Nome, Ativo."""
    doc = BeautifulSoup(html, "html.parser")
    tb = doc.find("tbody", id="form0:listagem:tb")
    out = []
    for tr in (tb.find_all("tr", recursive=False) if tb else []):
        tds = tr.find_all("td", recursive=False)
        if len(tds) < 6:
            continue
        m = re.match(r"form0:listagem:(\d+):", tds[0].get("id") or "")
        out.append({
            "idx": int(m.group(1)) if m else None,
            "cpf": tds[1].get_text(" ", strip=True),
            "documento": tds[2].get_text(" ", strip=True),
            "tipo_documento": tds[3].get_text(" ", strip=True),
            "nome": tds[4].get_text(" ", strip=True),
            "ativo": tds[5].find("input", checked=True) is not None,
        })
    return out


def passo_exportar(c, max_paginas="80"):
    """Pesquisa com filtro vazio e percorre TODAS as páginas do datascroller (A4J, leitura).
    Grava capturas/profissional/listagem_completa.json (PII — gitignored)."""
    r = get_logado(c, TELA_PESQUISAR)
    doc = BeautifulSoup(html_de(r), "html.parser")
    dados = campos(doc, "form0") | {"form0:nome": "", "form0:j_id36": "Pesquisar"}
    pausa()
    r = c.post(action(doc, "form0"), data=dados)
    html = html_de(r)
    salvar("pesquisa_vazio.html", html)
    doc = BeautifulSoup(html, "html.parser")
    acao, base = action(doc, "form0"), campos(doc, "form0")
    todas = {l["idx"]: l for l in linhas_listagem(html)}
    print(f"página 1: {len(todas)} linhas")
    for n in range(2, int(max_paginas) + 1):
        pausa()
        r = c.post(acao, data=base | {"AJAXREQUEST": "_viewRoot", "ajaxSingle": "form0:sc1",
                                      "form0:sc1": str(n), "AJAX:EVENTS_COUNT": "1"},
                   headers={"X-Requested-With": "XMLHttpRequest"})
        h = html_de(r)
        vs = re.search(r'name="javax.faces.ViewState"[^>]*value="([^"]+)"', h)
        if vs:
            base["javax.faces.ViewState"] = vs.group(1)
        novas = [l for l in linhas_listagem(h) if l["idx"] not in todas]
        print(f"página {n}: HTTP {r.status_code}, {len(novas)} linhas novas")
        if not novas:
            break
        todas.update({l["idx"]: l for l in novas})
    lista = [todas[k] for k in sorted(todas)]
    (CAP / "listagem_completa.json").write_text(json.dumps(lista, ensure_ascii=False, indent=1),
                                                encoding="utf-8")
    print(f"total exportado: {len(lista)} registros")
    guardar_sessao(c)


def passo_amostra(c, n="15", semente="20261001"):
    """Ficha (lotações/telefone) de até N profissionais COM CPF na listagem, via onblur do CPF na
    aba Adicionar Novo — consulta, não grava. Grava amostra_fichas.json (PII — gitignored)."""
    import random
    lista = json.loads((CAP / "listagem_completa.json").read_text(encoding="utf-8"))
    com_cpf = [l for l in lista if re.sub(r"\D", "", l["cpf"])]
    random.Random(int(semente)).shuffle(com_cpf)
    alvo = com_cpf[:int(n)]
    fichas = []
    for l in alvo:
        # A aba precisa ser reaberta a cada CPF: depois do 1º onblur a view não responde mais
        # a outro onblur (volta sem nome e sem lotações). Medido 01/10/2026.
        r = get_logado(c, TELA_PESQUISAR)
        doc = BeautifulSoup(html_de(r), "html.parser")
        pausa()
        r = c.post(action(doc, "form0"), data=campos(doc, "form0") |
                   {"form0:editar_server_submit": "form0:editar_server_submit"})
        doc = BeautifulSoup(html_de(r), "html.parser")
        acao, base = action(doc, "form0"), campos(doc, "form0")
        if "form0:aplicInicial2" not in base:
            raise SystemExit("aba Adicionar Novo sem o campo de CPF")
        pausa()
        r = c.post(acao, data=base | {
            "form0:aplicInicial2": l["cpf"], "form0:supBuscaCpfNovo": "form0:supBuscaCpfNovo",
            "ajaxSingle": "form0:aplicInicial2", "AJAXREQUEST": "_viewRoot",
            "AJAX:EVENTS_COUNT": "1"}, headers={"X-Requested-With": "XMLHttpRequest"})
        h = html_de(r)
        d = BeautifulSoup(h, "html.parser")
        nome = (d.find(attrs={"name": "form0:txtNome"}) or {}).get("value") or ""
        tel = (d.find(attrs={"name": "form0:telefone"}) or {}).get("value") or ""
        lot = []
        tb = d.find("tbody", id="form0:listagem:tb")
        for tr in (tb.find_all("tr", recursive=False) if tb else []):
            tds = tr.find_all("td", recursive=False)
            lot.append({"unidade": tds[0].get_text(" ", strip=True) if tds else "",
                        "especialidade": tds[1].get_text(" ", strip=True) if len(tds) > 1 else "",
                        "ativo": tr.find("input", checked=True) is not None})
        fichas.append({"cpf": l["cpf"], "nome_listagem": l["nome"], "nome_ficha": nome,
                       "telefone": tel, "lotacoes": lot, "http": r.status_code})
        mesmo = " ".join(nome.split()).upper() == " ".join(l["nome"].split()).upper()
        print(f"ficha: HTTP {r.status_code}, nome {'confere' if mesmo else 'DIFERE'},"
              f" {len(lot)} lotações")
    (CAP / "amostra_fichas.json").write_text(json.dumps(fichas, ensure_ascii=False, indent=1),
                                             encoding="utf-8")
    guardar_sessao(c)


def passo_pesqabrir(c, nome="SILVA", linha="1", rotulo="ficha_editar"):
    """Pesquisa e abre a ficha (Editar) no mesmo processo, sem paginar no meio."""
    passo_pesquisar(c, nome, "pa")
    passo_abrir(c, "pesquisa_pa.html", linha, rotulo)


def main():
    passo = sys.argv[1] if len(sys.argv) > 1 else "inicio"
    with cliente() as c:
        if not SESSAO.exists():
            login(c)
            pausa()
        globals()[f"passo_{passo}"](c, *sys.argv[2:])


if __name__ == "__main__":
    main()
