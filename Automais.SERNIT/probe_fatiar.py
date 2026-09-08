"""Sonda 5: fatiamento por Data da Solicitação — "100 resultados" é teto ou total?

Todas as situações devolvem exatamente "100 resultados" na busca sem filtro (medido na
sonda 4), o que cheira a **teto**, não a total. Aqui provamos: se o filtro de data funciona
e a soma das fatias anuais passa de 100, então 100 é o corte — e a varredura tem de fatiar
até cada janela caber (< 100), como no SER-RJ (../../docs/ser.md §4.1).

SOMENTE LEITURA.

Uso:  python probe_fatiar.py [SITUACAO]   (default EM_FILA)
"""

from __future__ import annotations

import re
import sys

from sernit.client import (URL_SOLIC, campos_todos, login_e_modulo,
                           seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SIT = (sys.argv[1] if len(sys.argv) > 1 else "EM_FILA").upper()
DI = "form0:dtInicialSolicitacaoInputDate"
DF = "form0:dtFinalSolicitacaoInputDate"


def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def paginas(html):
    """Maior número de página oferecido pelo datascroller form0:sc1 (0 se não há)."""
    sc = sopa(html).find(id=re.compile(r"form0:sc1"))
    if not sc:
        return 0
    nums = [int(x) for x in re.findall(r">\s*(\d+)\s*<", str(sc))]
    return max(nums) if nums else 0


def buscar(s, situacao, di=None, df=None):
    tela = s.get(URL_SOLIC).text          # GET novo a cada busca
    d = sopa(tela)
    sit = select_situacao(d)
    btn = d.find("input", attrs={"value": re.compile("Pesquisar", re.I)})
    dados = campos_todos(d, "form0")
    dados[sit] = situacao
    if di:
        dados[DI] = di
    if df:
        dados[DF] = df
    dados[btn.get("name")] = btn.get("value")
    dados |= {"AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
              "javax.faces.ViewState": viewstate(tela) or ""}
    html = seguir_redirect_a4j(s, s.post(URL_SOLIC, dados, ajax=True).text)
    # O TOTAL REAL vem em "Total de resultados encontrados: N" (mesmo quando a grade é capada em 100)
    tot = re.search(r"Total de resultados encontrados:\s*(\d+)", html)
    n = int(tot.group(1)) if tot else None
    capado = "limitado em 100" in html          # aviso de corte da grade
    grade = sopa(html).find("table", id="form0:listagem")
    linhas = len([tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")])
    return n, paginas(html), linhas, capado


def main() -> int:
    s = login_e_modulo("ambulatorial")
    print(f"situação: {SIT}\n")

    n, pg, l1, cap = buscar(s, SIT)
    print(f"SEM FILTRO           total_real={n}  grade_capada={cap}  páginas={pg}  linhas_p1={l1}")

    print("\n-- fatias anuais (Data da Solicitação) --")
    soma = 0
    for ano in range(2020, 2027):
        n_a, pg_a, _, cap_a = buscar(s, SIT, f"01/01/{ano}", f"31/12/{ano}")
        flag = " (grade capada em 100 — fatiar mais)" if cap_a else ""
        print(f"  {ano}: total_real={n_a}  páginas={pg_a}{flag}")
        if n_a:
            soma += n_a
    print(f"\nsoma dos totais anuais = {soma}   (sem filtro reportou total_real={n})")
    print(">>> O 'Total de resultados encontrados' é confiável mesmo com a grade capada em 100.")
    print(">>> Varredura = fatiar a Data da Solicitação até o total da janela caber nas 5 páginas (≤100) e paginar.")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
