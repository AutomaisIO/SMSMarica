"""Testa a hipótese: `X-Requested-With: XMLHttpRequest` num submit NÃO-ajax (abrir a aba Editar)
faz o A4J devolver a tela re-renderizada (pesquisa) em vez de navegar ao editar → combo some.

Compara, no mesmo login: abrir a aba (A) SEM o header (como o lab) vs (B) COM o header (como o
backend). SOMENTE LEITURA.
"""
from __future__ import annotations
import re, sys
from sernit.client import (URL_SOLIC, campos_todos, login_e_modulo,
                           seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def abrir(s, headers, rotulo):
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    dados = campos_todos(d, "form0") | {
        "form0": "form0",
        "form0:editar_server_submit": "form0:editar_server_submit",
        "javax.faces.ViewState": viewstate(tela) or "",
    }
    r = s.post(act, dados, headers=headers)
    final = seguir_redirect_a4j(s, r.text)
    print(f"[{rotulo}] headers={headers} resp_bytes={len(r.text)} "
          f"final_bytes={len(final)} comboTipoRecurso={'SIM' if 'form0:comboTipoRecurso' in final else 'NÃO'}")


def main() -> int:
    s = login_e_modulo("ambulatorial")
    abrir(s, None, "SEM X-Requested-With (lab)")
    abrir(s, {"X-Requested-With": "XMLHttpRequest"}, "COM X-Requested-With (backend)")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
