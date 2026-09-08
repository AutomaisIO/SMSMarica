"""Sonda 10: autocomplete de CID (Hipótese) da nova solicitação do SERNIT.

O campo `form0:procedimento` é um rich:suggestionbox (box lido do init da página, volátil). O fetch
de sugestões manda `inputvalue=<termo>` + `ajaxSingle=<box>` com `AJAXREQUEST=_viewRoot`; a resposta
traz a tabela `<box>:suggest` com as linhas de CID. SOMENTE LEITURA (só o fetch; nada é gravado).

Uso:  python probe_cid.py <termo>        (ex.: "diabetes", "A09", "hipertens")
"""

from __future__ import annotations

import re
import sys

from sernit.client import (CAP, URL_SOLIC, campos_todos, hidden_do_form,
                           login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

if len(sys.argv) < 2:
    raise SystemExit('Uso: python probe_cid.py <termo>   (ex.: "diabetes")')
TERMO = sys.argv[1]


def box_do_procedimento(html):
    """Id do rich:suggestionbox de form0:procedimento, lido do init (nunca chumbado)."""
    m = re.search(
        r"new RichFaces\.Suggestion\('form0','form0:procedimento','([^']+)'", html)
    return m.group(1) if m else None


def main() -> int:
    s = login_e_modulo("ambulatorial")

    # abre a aba Nova
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    act = d.find("form", id="form0").get("action") or URL_SOLIC
    dados = campos_todos(d, "form0") | {
        "form0": "form0",
        "form0:editar_server_submit": "form0:editar_server_submit",
        "javax.faces.ViewState": viewstate(tela) or "",
    }
    nova = seguir_redirect_a4j(s, s.post(act, dados).text)

    box = box_do_procedimento(nova)
    print(f"box do CID: {box!r}")
    if not box:
        print("!! suggestionbox de procedimento não localizado — ver capturas/nova.html")
        return 1

    dn = sopa(nova)
    act = dn.find("form", id="form0").get("action") or URL_SOLIC

    # fetch de sugestões: inputvalue=<termo>, ajaxSingle=<box>, AJAXREQUEST=_viewRoot
    fetch = hidden_do_form(nova, "form0")
    fetch |= {
        box: box,
        "inputvalue": TERMO,
        "ajaxSingle": box,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(nova) or "",
    }
    resp = seguir_redirect_a4j(s, s.post(act, fetch, ajax=True).text)
    (CAP / "cid_sugestoes.html").write_text(resp, encoding="utf-8")
    print(f"resposta do fetch: {len(resp)} bytes -> capturas/cid_sugestoes.html")

    dr = sopa(resp)
    tabela = dr.find(id=f"{box}:suggest")
    if tabela is None:
        # às vezes a suggest vem sem o sufixo :suggest — tenta achar tabela de sugestões
        tabela = dr.find("table", id=re.compile(re.escape(box)))
    if tabela is None:
        print("!! tabela de sugestões não veio. Trecho:", " ".join(resp[:300].split()))
        return 1

    linhas = tabela.find_all("tr")
    print(f"\n{len(linhas)} sugestão(ões) para {TERMO!r}:")
    for tr in linhas[:25]:
        cels = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all("td")]
        if any(cels):
            print("   ", cels)
    if len(linhas) > 25:
        print(f"   ... e mais {len(linhas)-25}")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
