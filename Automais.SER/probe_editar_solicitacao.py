"""EDIÇÃO de uma solicitação existente no SER — a primeira escrita do laboratório.

Até aqui todo o laboratório era somente-leitura (a trava de `probe_campos_dinamicos.py` recusa
qualquer parâmetro com verbo de escrita). Esta sonda é a exceção deliberada: ela GRAVA. Por isso
a trava aqui é **invertida** — em vez de proibir o verbo, ela compara o que vai no POST contra o
que a própria tela renderizou e recusa se QUALQUER campo além do alvo tiver mudado.

O caminho, todo lido da página (nada de `j_id` chumbado — é posicional e muda quando a SES-RJ
recompila):

  1. login + módulo ambulatorial
  2. tela de Solicitação -> Pesquisar por `form0:idSolicitacao`
  3. na linha do resultado, menu "Ação" -> `<a>Editar</a>`  (A4J, id lido do menu daquela linha)
  4. a aba Editar volta preenchida; `form0:procedimento` é a **Hipótese** (diagnóstica)
  5. altera SÓ esse campo e aciona `<a title="Gravar">`
  6. relê a solicitação do zero para confirmar que gravou

Por padrão é ENSAIO (não grava). Para gravar de verdade é preciso `--gravar`.

Uso:
  python probe_editar_solicitacao.py 3968654 --sufixo "."            # ensaio
  python probe_editar_solicitacao.py 3968654 --sufixo "." --gravar   # grava
"""

from __future__ import annotations

import argparse
import os
import pathlib
import re
import sys
from urllib.parse import urlencode

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

CAMPO_ID = "form0:idSolicitacao"
CAMPO_SITUACAO = "form0:j_id75"

# A Hipótese NÃO é texto livre: é uma `rich:suggestionbox` de CID (o input tem
# alt="Digite o nome ou o código" e um irmão `..._selection`). O valor que vale é o CID
# selecionado, guardado no lado do SER — o texto digitado é descartado em silêncio.
# Medido em 10/08/2026 na solicitação 3968654: o SER respondeu "salva com sucesso" e o
# campo voltou sem a alteração. Não use este campo como alvo de escrita.
CAMPO_HIPOTESE = "form0:procedimento"

# O WhatsApp NÃO TEM `id`, só um `name` posicional (`form0:j_id178` hoje). Chumbar isso daria,
# na próxima recompilação da SES-RJ, uma gravação no campo errado — sem erro nenhum. Por isso o
# campo é sempre resolvido pelo RÓTULO do <label> irmão, na hora.
ROTULO_ALVO = {
    "whatsapp": "telefone whatsapp",
    "residencial": "telefone residencial",
    "contato": "telefone contato",
}


def campo_por_rotulo(html: str, rotulo: str) -> tuple[str, str]:
    """(name, valor atual) do input cujo <label> irmão casa com o rótulo. Nunca chumbado."""
    d = sopa(html)
    f = d.find("form", id="form0")
    achados = []
    for el in (f.find_all("input") if f else []):
        nome = el.get("name")
        if not nome or el.has_attr("disabled"):
            continue
        lab = el.parent.find("label") if el.parent else None
        if not lab:
            continue
        texto = " ".join(lab.get_text(" ", strip=True).split())
        limpo = texto.replace("*", "").strip().lower().rstrip(":")
        if limpo == rotulo.lower():
            achados.append((nome, el.get("value") or "", texto))

    if len(achados) != 1:
        raise SystemExit(
            f"esperava exatamente 1 campo com rótulo {rotulo!r}, achei {len(achados)}: "
            f"{[(a[0], a[2]) for a in achados]}")
    return achados[0][0], achados[0][1]


# ---------------------------------------------------------------- utilidades de HTML

def sopa(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def viewstate(html: str) -> str:
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else ""


def charset_da_pagina(r: httpx.Response) -> str:
    """O que o SER declara. Decide como o acento da Hipótese viaja no POST."""
    ct = r.headers.get("content-type", "")
    if (m := re.search(r"charset=([\w-]+)", ct, re.I)):
        return m.group(1).lower()
    if (m := re.search(r'charset=["\']?([\w-]+)', r.text[:2000], re.I)):
        return m.group(1).lower()
    return "utf-8"


def campos_do_form(doc: BeautifulSoup, form_id: str = "form0") -> dict[str, str]:
    """Tudo que o NAVEGADOR mandaria deste form.

    Espelha o navegador de propósito: campo `disabled` não é enviado (o SER trava a identidade
    do paciente assim, e ele tem esses valores do lado dele), radio/checkbox só vai se marcado,
    e botão não entra — quem entra é só o que a gente acionar explicitamente.
    """
    f = doc.find("form", id=form_id)
    out: dict[str, str] = {}
    if not f:
        return out

    for i in f.find_all("input"):
        nome = i.get("name")
        tipo = (i.get("type") or "text").lower()
        if not nome or i.has_attr("disabled"):
            continue
        if tipo in {"submit", "button", "image", "reset", "file"}:
            continue
        if tipo in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        out[nome] = i.get("value") or ""

    for s in f.find_all("select"):
        nome = s.get("name")
        if not nome or s.has_attr("disabled"):
            continue
        o = s.find("option", selected=True) or s.find("option")
        out[nome] = (o.get("value") if o else "") or ""

    for t in f.find_all("textarea"):
        nome = t.get("name")
        if nome and not t.has_attr("disabled"):
            out[nome] = t.get_text() or ""

    return out


def mensagens(html: str) -> str:
    d = sopa(html)
    partes = []
    for ident in ("form0:divMensagens", "form0:messages", "divMensagens"):
        if (el := d.find(id=ident)):
            partes.append(" ".join(el.get_text(" ", strip=True).split()))
    for el in d.select(".rich-messages, .rich-message, .errorMessage, .mensagemErro"):
        partes.append(" ".join(el.get_text(" ", strip=True).split()))
    return " | ".join(p for p in partes if p)[:900]


# ---------------------------------------------------------------- sessão

class Ser:
    def __init__(self, c: httpx.Client):
        self.c = c
        self.charset = "utf-8"

    def postar(self, url: str, dados: dict[str, str], ajax: bool = True) -> str:
        """POST no charset que o SER declarou — senão o acento da Hipótese chega quebrado."""
        corpo = urlencode(dados, encoding=self.charset, errors="xmlcharrefreplace")
        headers = {"Content-Type": f"application/x-www-form-urlencoded; charset={self.charset}"}
        if ajax:
            headers["X-Requested-With"] = "XMLHttpRequest"
        r = self.c.post(url, content=corpo, headers=headers)
        html = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(m.group(1).replace("&amp;", "&")).text
        if loc := r.headers.get("location"):
            if "<form" not in html:
                html = self.c.get(loc).text
        return html

    def login(self) -> None:
        r = self.c.get("/ser/login")
        self.charset = charset_da_pagina(r)
        d = sopa(r.text)
        acao = d.find("form", id="login").get("action")
        html = self.postar(acao, campos_do_form(d, "login") | {
            "login": "login",
            "login:username": os.environ["SER_USUARIO"],
            "login:password": os.environ["SER_SENHA"],
            "login:entrar": "Entrar",
            "javax.faces.ViewState": viewstate(r.text) or "j_id1",
        }, ajax=False)
        if 'id="login:username"' in html:
            raise SystemExit("LOGIN FALHOU — confira SER_USUARIO/SER_SENHA no .env")

        home = self.c.get("/ser/home.seam").text
        fid = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home).group(1)
        dh = sopa(home)
        corpo = urlencode(campos_do_form(dh, fid) | {
            fid: fid, f"{fid}:goModulo": f"{fid}:goModulo", "param1": "ambulatorial",
            "AJAXREQUEST": fid, "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": viewstate(home),
        }, encoding=self.charset)
        r = self.c.post("/ser/home", content=corpo, headers={
            "Content-Type": f"application/x-www-form-urlencoded; charset={self.charset}",
            "X-Requested-With": "XMLHttpRequest"})
        if not r.headers.get("location"):
            raise SystemExit("módulo ambulatorial não ativou")
        self.c.get(r.headers["location"])
        print(f"login + módulo ambulatorial ok  (charset do SER: {self.charset})")


# ---------------------------------------------------------------- passos

def pesquisar(ser: Ser, id_solic: str, situacao: str | None) -> str:
    """Tela nova a cada busca — ViewState de tela anterior já contaminou busca antes."""
    tela = ser.c.get(URL_SOLIC).text
    d = sopa(tela)
    botao = d.find(attrs={"title": "Pesquisar"})
    if botao is None:
        raise SystemExit("tela de Solicitação sem botão Pesquisar — sessão derrubada?")
    acao = d.find("form", id="form0").get("action") or URL_SOLIC

    dados = campos_do_form(d) | {
        "form0": "form0",
        botao.get("id"): botao.get("id"),
        CAMPO_ID: id_solic,
        "AJAXREQUEST": "form0",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(tela),
    }
    if situacao:
        dados[CAMPO_SITUACAO] = situacao
    return ser.postar(acao, dados)


def linha_unica(html: str, id_solic: str) -> tuple[int, str, str]:
    """(índice da linha, nome do paciente, id do <a>Editar</a>) — exige 1 linha e o ID batendo."""
    d = sopa(html)
    tab = d.find("table", id="form0:listagem")
    if tab is None:
        raise SystemExit("a busca não devolveu grade. " + (mensagens(html) or "(sem mensagem)"))

    linhas = [tr for tr in tab.select("tbody tr")
              if any(re.fullmatch(r"\d{5,}", td.get_text(strip=True)) for td in tr.find_all("td"))]
    if len(linhas) != 1:
        raise SystemExit(f"esperava exatamente 1 linha para o ID {id_solic}, vieram {len(linhas)}")

    tr = linhas[0]
    celulas = [td.get_text(" ", strip=True) for td in tr.find_all("td")]
    if id_solic not in celulas:
        raise SystemExit(f"a linha devolvida não é o ID {id_solic}: {celulas[:6]}")

    idx_m = re.search(r"form0:listagem:(\d+):", str(tr))
    if not idx_m:
        raise SystemExit("não consegui identificar o índice da linha na grade")
    idx = int(idx_m.group(1))

    menu = d.find(id=re.compile(rf"^form0:listagem:{idx}:j_id\d+_menu$"))
    if menu is None:
        raise SystemExit("linha sem menu de Ação")

    nome = ""
    if (cab := menu.find("div", class_=re.compile("rich-menu-item-disabled"))):
        nome = " ".join(cab.get_text(" ", strip=True).split())

    editar = next((a for a in menu.find_all("a")
                   if a.get_text(strip=True).lower() == "editar"), None)
    if editar is None:
        rotulos = [a.get_text(strip=True) for a in menu.find_all("a")]
        raise SystemExit(f"esta solicitação não oferece 'Editar'. Ações disponíveis: {rotulos}")

    print("\n--- LINHA ENCONTRADA " + "-" * 56)
    for c in celulas:
        if c:
            print(f"    {c[:96]}")
    return idx, nome, editar.get("id")


def abrir_editar(ser: Ser, html_busca: str, id_editar: str) -> str:
    d = sopa(html_busca)
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    dados = campos_do_form(d) | {
        "form0": "form0",
        id_editar: id_editar,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(html_busca),
    }
    html = ser.postar(acao, dados)
    if CAMPO_HIPOTESE not in html:
        (CAP / "editar_falhou.html").write_text(html, encoding="utf-8")
        raise SystemExit("a aba Editar não abriu preenchida (sem form0:procedimento). "
                         "Resposta em capturas/editar_falhou.html. " + mensagens(html))
    return html


def hipotese(html: str) -> str:
    d = sopa(html)
    el = d.find(attrs={"name": CAMPO_HIPOTESE})
    return (el.get("value") or "") if el is not None else ""


def gravar(ser: Ser, html_edicao: str, campo: str, novo_valor: str, ensaio: bool) -> str:
    d = sopa(html_edicao)
    acao = d.find("form", id="form0").get("action") or URL_SOLIC

    botao = d.find("a", attrs={"title": "Gravar"})
    if botao is None:
        raise SystemExit("não achei o botão Gravar na tela de edição")
    id_gravar = botao.get("id")

    renderizado = campos_do_form(d)
    if campo not in renderizado:
        raise SystemExit(f"o campo {campo!r} não está entre os que a tela manda")
    payload = dict(renderizado)
    payload[campo] = novo_valor

    # ---- TRAVA INVERTIDA: só o campo alvo pode divergir do que a tela renderizou.
    divergentes = [k for k in payload
                   if k != campo and payload.get(k) != renderizado.get(k)]
    faltando = [k for k in renderizado if k not in payload]
    if divergentes or faltando:
        raise SystemExit(f"TRAVA: o POST alteraria outros campos {divergentes} "
                         f"ou omitiria {faltando}")

    payload |= {
        "form0": "form0",
        id_gravar: id_gravar,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(html_edicao),
    }

    print(f"\n--- POST DE GRAVAÇÃO " + "-" * 56)
    print(f"    botão Gravar     : {id_gravar}")
    print(f"    campos no envio  : {len(payload)}")
    print(f"    único alterado   : {campo}")
    print(f"      de : {renderizado.get(campo)!r}")
    print(f"      pra: {novo_valor!r}")

    if ensaio:
        print("\n    ENSAIO — nada foi enviado. Rode de novo com --gravar para valer.")
        return ""

    html = ser.postar(acao, payload)
    (CAP / "editar_resposta_gravar.html").write_text(html, encoding="utf-8")
    print(f"\n    resposta: {len(html)} bytes -> capturas/editar_resposta_gravar.html")
    if (msg := mensagens(html)):
        print(f"    mensagem do SER: {msg}")
    return html


# ---------------------------------------------------------------- principal

def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("id_solicitacao")
    p.add_argument("--campo", default="whatsapp", choices=sorted(ROTULO_ALVO),
                   help="qual telefone alterar (resolvido pelo rótulo, nunca pelo j_id)")
    p.add_argument("--valor", help="novo valor. Sem isso, é só leitura do estado atual.")
    p.add_argument("--restaurar", action="store_true",
                   help="ida e volta: grava --valor, confere, e devolve o valor original")
    p.add_argument("--situacao", default="EM_FILA")
    p.add_argument("--gravar", action="store_true", help="grava de verdade (sem isso é ensaio)")
    a = p.parse_args()
    rotulo = ROTULO_ALVO[a.campo]

    with httpx.Client(base_url=BASE, timeout=120, follow_redirects=True,
                      headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"}) as c:
        ser = Ser(c)
        ser.login()

        print(f"\nbuscando solicitação {a.id_solicitacao} (situação {a.situacao})...")
        busca = pesquisar(ser, a.id_solicitacao, a.situacao)
        try:
            idx, nome, id_editar = linha_unica(busca, a.id_solicitacao)
        except SystemExit as e:
            print(f"  ({e}) — tentando sem filtro de situação")
            busca = pesquisar(ser, a.id_solicitacao, None)
            idx, nome, id_editar = linha_unica(busca, a.id_solicitacao)

        print(f"\n    paciente : {nome}")
        print(f"    ação Editar da linha {idx}: {id_editar}")

        edicao = abrir_editar(ser, busca, id_editar)
        (CAP / f"editar_{a.id_solicitacao}.html").write_text(edicao, encoding="utf-8")
        print(f"    aba Editar aberta: {len(edicao)} bytes "
              f"-> capturas/editar_{a.id_solicitacao}.html")

        campo, atual = campo_por_rotulo(edicao, rotulo)
        print(f"\n--- {rotulo.upper()} " + "-" * max(4, 72 - len(rotulo)))
        print(f"    campo no SER : {campo}   (resolvido pelo rótulo)")
        print(f"    valor atual  : {atual!r}")

        # A Hipótese fica à vista de propósito: é a lição do teste de 10/08/2026.
        _, hip = CAMPO_HIPOTESE, hipotese(edicao)
        print(f"    (hipótese, só p/ referência: {hip!r})")

        if not a.valor:
            print("\n    LEITURA APENAS — passe --valor para alterar.")
            return 0
        if a.valor == atual:
            print(f"\n    já está em {a.valor!r} — nada a fazer.")
            return 0

        gravar(ser, edicao, campo, a.valor, ensaio=not a.gravar)
        if not a.gravar:
            return 0

        def reler() -> str:
            b = pesquisar(ser, a.id_solicitacao, a.situacao)
            try:
                _, _, ident = linha_unica(b, a.id_solicitacao)
            except SystemExit:
                b = pesquisar(ser, a.id_solicitacao, None)
                _, _, ident = linha_unica(b, a.id_solicitacao)
            return campo_por_rotulo(abrir_editar(ser, b, ident), rotulo)[1]

        # ---- releitura do zero: a única prova de que gravou. "Salvo com sucesso" não é prova —
        # a Hipótese devolveu essa mensagem e não mudou nada.
        print("\n--- CONFERÊNCIA (busca nova, do zero) " + "-" * 39)
        depois = reler()
        ok = depois == a.valor
        print(f"    valor relido: {depois!r}")
        print("    " + ("GRAVOU ✓" if ok else f"NÃO BATE — esperava {a.valor!r}"))

        if ok and a.restaurar:
            print(f"\n--- RESTAURANDO o valor original {atual!r} " + "-" * 24)
            b = pesquisar(ser, a.id_solicitacao, a.situacao)
            _, _, ident = linha_unica(b, a.id_solicitacao)
            gravar(ser, abrir_editar(ser, b, ident), campo, atual, ensaio=False)
            volta = reler()
            print(f"    valor relido: {volta!r}")
            print("    " + ("RESTAURADO ✓" if volta == atual
                            else f"!! FICOU {volta!r} — restaure à mão para {atual!r}"))
            return 0 if volta == atual else 1

        return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
