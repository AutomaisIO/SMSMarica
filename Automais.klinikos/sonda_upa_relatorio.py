"""Sonda de relatório da UPA/Santa Rita (build 2025) pela TELA — version-proof. SOMENTE LEITURA.

Em vez de chumbar o `parN` (que difere entre builds), dirige a tela do relatório: preenche datas
e TODOS os selects obrigatórios (primeira opção real, ou 'TODAS'/'todos' quando existir), clica o
botão Excel e segue o `window.open` que a própria app gera — assim os params saem corretos para a
build 2025. Reporta a URL gerada e as colunas do XLS (mascarando nomes). Retry no OOM do Crystal.

Uso:  python sonda_upa_relatorio.py "<caminho da tela>" [DD/MM/AAAA]
  ex.: python sonda_upa_relatorio.py "/UPA24H/UPA/Relatorios/Relatorio.aspx?parrel=667&origem=5"
"""

from __future__ import annotations

import re
import sys
import time
from urllib.parse import urljoin

from klinikos.client import KlinikosSession, sopa, campos_todos, action_do_form, LIBERADOS, CAP
from paridade_upa import gate_porta_local, BIFF

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def _xls(s, url, tent=6):
    for _ in range(tent):
        r = s.get(url)
        if r.content[:8] == BIFF:
            return r.content, None
        txt = r.text
        if re.search(r"Not enough memory|OutOfMemory|COMException|Gateway Time-out", txt, re.I):
            time.sleep(4); continue
        return None, txt
    return None, "OOM persistente"


def colunas(b: bytes):
    import xlrd
    sh = xlrd.open_workbook(file_contents=b).sheet_by_index(0)
    for i in range(sh.nrows):
        v = [str(c).strip() for c in sh.row_values(i) if str(c).strip()]
        if len(v) >= 3 and any(re.search(r"Boletim|Paciente|Nome|Data|Hora|Cl[íi]nica|CID|Classifica", x) for x in v):
            return v[:14], sh.nrows
    return [], sh.nrows


def main(argv):
    if not argv:
        print(__doc__); return 2
    tela = argv[0]
    dia = argv[1] if len(argv) > 1 else "15/09/2026"
    s = KlinikosSession(); s.login(); gate_porta_local(s)
    r = s.get(tela); html = r.text; url = str(r.url)
    d = sopa(html)
    tit = d.title.get_text(strip=True) if d.title else ""
    print(f"tela: {tela}  -> {r.status_code} '{tit}'")
    # campos próprios
    selects = [(e.get("name"), [(o.get("value"), o.get_text(strip=True)[:20]) for o in e.find_all("option")])
               for e in d.find_all("select") if "contentCenterChild" in (e.get("name") or "")]
    print("selects:", [(n.split("$")[-1], len(o)) for n, o in selects])

    dados = campos_todos(html)
    for k in list(dados):
        if re.search(r"\$(rdp?Data\w*(Inicial|Final|Competencia))$", k):
            dados[k] = f"{dia[6:10]}-{dia[3:5]}-{dia[0:2]}"
            dados[k + "$dateInput"] = f"{dados[k]}-00-00-00"
            dados[k.replace('$', '_') + "_dateInput_text"] = dia
    # cada select obrigatório: prefere 'TODAS'/'todos'/'-1'; senão a 1a opção real
    for nome, ops in selects:
        vals = [v for v, _ in ops]
        todos = next((v for v, t in ops if re.search(r"todas|todos", t, re.I)), None)
        real = next((v for v in vals if v not in (None, "", "-1", "0")), None)
        escolha = todos if todos is not None else (real if real is not None else (vals[0] if vals else ""))
        if escolha:
            dados[nome] = escolha
    bt = next((i.get("name") for i in d.find_all("input", attrs={"type": "image"})
               if re.search(r"Excel$", i.get("name", ""))), None)
    if not bt:
        print("sem botão Excel"); s.close(); return 1
    dados[bt + ".x"] = "10"; dados[bt + ".y"] = "10"; LIBERADOS.update({bt + ".x", bt + ".y"})
    r2 = s.post(action_do_form(html, url), dados)
    sc = " ".join(x.get_text() for x in sopa(r2.text).find_all("script") if not x.get("src"))
    m = re.search(r"""window\.open\(["']([^"']*rpt[^"']*)["']""", sc, re.I)
    if not m:
        al = re.search(r"(Aten[çc][ãa]o|Informe|Selecione|obrigat)[^`'\"<]{0,90}", r2.text, re.I)
        print("sem window.open | alerta:", al.group(0)[:90] if al else "nenhum")
        s.close(); return 1
    print("params gerados (UPA):", m.group(1))
    b, err = _xls(s, urljoin(str(r2.url), m.group(1)))
    if b:
        cols, n = colunas(b)
        nome = re.sub(r"[^A-Za-z0-9]+", "_", tela)[:50]
        (CAP / f"upa_{nome}.xls").write_bytes(b)
        print(f"XLS: {len(b)//1024} KB, {n} linhas; colunas={cols}")
    else:
        print("não veio XLS:", re.sub(r"\s+", " ", (err or ""))[:120])
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
