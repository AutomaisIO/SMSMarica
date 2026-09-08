"""Sonda 4: menu de Ação (Opções) por situação.

Percorre as 7 situações, busca cada uma e imprime os itens do menu de Ação da 1ª linha.
Confirma o esperado pelo SER-RJ §4.2: Cancelada sem *Editar*, Alta sem *Histórico*.
SOMENTE LEITURA.

Uso:  python probe_menu_acao.py
"""

from __future__ import annotations

import re
import sys

from sernit.client import (URL_SOLIC, campos_todos, login_e_modulo,
                           seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SITUACOES = ["EM_FILA", "PENDENTE", "AGENDADA", "CHEGADA_NAO_CONFIRMADA",
             "CHEGADA_CONFIRMADA", "CANCELADA", "ALTA"]


def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def buscar(s, situacao):
    tela = s.get(URL_SOLIC).text            # GET novo a cada busca (armadilha de reuso, SER §4.3)
    d = sopa(tela)
    sit = select_situacao(d)
    btn = d.find("input", attrs={"value": re.compile("Pesquisar", re.I)})
    dados = campos_todos(d, "form0")
    dados[sit] = situacao
    dados[btn.get("name")] = btn.get("value")
    dados |= {"AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
              "javax.faces.ViewState": viewstate(tela) or ""}
    return seguir_redirect_a4j(s, s.post(URL_SOLIC, dados, ajax=True).text)


def main() -> int:
    s = login_e_modulo("ambulatorial")
    for situacao in SITUACOES:
        html = buscar(s, situacao)
        d = sopa(html)
        grade = d.find("table", id="form0:listagem")
        corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
        cont = re.search(r"(\d+)\s+resultado", html)
        n = cont.group(1) if cont else "?"
        if not corpo:
            print(f"{situacao:24} {n:>4} resultados — (grade vazia, sem linha para ler menu)")
            continue
        itens = []
        for a in corpo[0].find_all("a"):
            t = " ".join(a.get_text(" ", strip=True).split())
            if t and t.lower() not in {"opções", "opcoes"}:
                itens.append(t)
        print(f"{situacao:24} {n:>4} resultados — Ação: {itens}")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
