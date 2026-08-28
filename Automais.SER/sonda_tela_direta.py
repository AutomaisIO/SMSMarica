"""Depois do login, a tela de Solicitação abre SEM ativar o módulo?

O `goModulo` sumiu da home (a SES-RJ redesenhou a tela: agora são "module-card", e nem eles vêm
renderizados no HTML que o servidor entrega). O motor headless trava nessa etapa e toda consulta de
cadastro pelo SER morre junto.

A pergunta que decide a correção: a ativação do módulo ainda é PRÉ-REQUISITO, ou dá para ir direto
à tela? Se abrir direto, a correção é tornar a etapa de módulo opcional em vez de fatal.

Leitura pura: login + GETs. Nenhum POST de escrita.

Uso:  python sonda_tela_direta.py
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
BASE = os.environ.get("SER_BASE_URL", "https://ser.saude.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/126.0 Safari/537.36")

TELA_SOLICITACAO = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"


def campos_do_form(doc, form_id):
    form = doc.find("form", id=form_id)
    if form is None:
        return {}
    out = {}
    for el in form.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or el.has_attr("disabled"):
            continue
        if (el.get("type") or "").lower() in ("submit", "button", "image", "reset"):
            continue
        out[nome] = el.get("value") or ""
    return out


def viewstate(html):
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def main() -> int:
    usuario, senha = os.environ.get("SER_USUARIO", ""), os.environ.get("SER_SENHA", "")
    with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                      follow_redirects=True, verify=False) as c:
        d = BeautifulSoup(c.get("/ser/login").text, "html.parser")
        fl = d.find("form", id="login")
        r = c.post(fl.get("action"), data=campos_do_form(d, "login") | {
            "login": "login", "login:username": usuario, "login:password": senha,
            "login:entrar": "Entrar",
            "javax.faces.ViewState": viewstate(str(fl)) or "j_id1",
        })
        if 'id="login:username"' in r.text:
            print("!! LOGIN FALHOU"); return 1
        print(f"[1] login OK — SEM passar pela home nem ativar módulo")

        # Vai DIRETO à tela de Solicitação, que é onde mora o painel de paciente.
        r = c.get(TELA_SOLICITACAO)
        html = r.text
        print(f"[2] GET tela de Solicitação -> HTTP {r.status_code}, {len(html)} bytes")
        print(f"    URL final: {r.url}")

        cap = RAIZ / "capturas"; cap.mkdir(exist_ok=True)
        (cap / "sonda_tela_direta.html").write_text(html, encoding="utf-8")

        doc = BeautifulSoup(html, "html.parser")
        tem_form0 = doc.find("form", id="form0") is not None
        tem_pesquisar = 'title="Pesquisar"' in html or "btnSearch" in html
        tem_login = 'id="login:username"' in html
        tem_cadsus = "numeroCADSUS" in html

        print(f"    form0 presente      : {tem_form0}")
        print(f"    botão Pesquisar     : {tem_pesquisar}")
        print(f"    campo numeroCADSUS  : {tem_cadsus}")
        print(f"    caiu no login       : {tem_login}")

        if tem_login:
            print("\n>> A tela EXIGE o módulo ativo (jogou de volta ao login).")
            return 3

        if tem_form0 and tem_pesquisar:
            print("\n>> A TELA ABRE DIRETO. A ativação do módulo deixou de ser pré-requisito —")
            print("   a correção é não tratar a etapa de módulo como fatal.")
            return 0

        print("\n>> Abriu algo, mas sem os marcadores esperados. Ver capturas/sonda_tela_direta.html")
        print("   trecho:", " ".join(html[:400].split()))
        return 4


if __name__ == "__main__":
    raise SystemExit(main())
