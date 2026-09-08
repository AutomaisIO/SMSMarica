"""Sonda 1: mapeia a tela de pesquisa de Solicitação (form0) do SERNIT.

Levanta os campos de filtro (Situação, Tipo, CPF/Nome/CNS, Id, datas), os botões
(Pesquisar / Exportar) e as colunas da grade — resolvendo tudo por rótulo/conteúdo,
porque os ids `j_idNN` do SERNIT diferem dos do SER-RJ. SOMENTE LEITURA (só abre a tela).

Uso:  python probe_pesquisa.py
"""

from __future__ import annotations

import re
import sys

from sernit.client import CAP, URL_SOLIC, login_e_modulo, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def rotulo_de(el) -> str:
    """Rótulo textual mais próximo de um campo: <label for>, ou texto do <td> anterior."""
    idv = el.get("id")
    if idv:
        lab = el.find_parent("form").find("label", attrs={"for": idv}) if el.find_parent("form") else None
        if lab:
            return " ".join(lab.get_text(" ", strip=True).split())
    td = el.find_parent("td")
    if td:
        prev = td.find_previous_sibling("td")
        if prev and prev.get_text(strip=True):
            return " ".join(prev.get_text(" ", strip=True).split())
    return ""


def main() -> int:
    s = login_e_modulo("ambulatorial")
    tela = s.get(URL_SOLIC).text
    (CAP / "pesquisar.html").write_text(tela, encoding="utf-8")
    d = sopa(tela)
    f = d.find("form", id="form0")
    if not f:
        print("!! form0 não veio na tela de pesquisa")
        return 1
    print("form0 action:", f.get("action"))
    print("=" * 78)

    print("\n### SELECTS (combos) — id, rótulo, opções")
    for sel in f.find_all("select"):
        opts = [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
                for o in sel.find_all("option")]
        print(f"\n  name={sel.get('name')!r}  rótulo={rotulo_de(sel)!r}")
        for v, t in opts[:20]:
            print(f"      {v!r:26} {t}")
        if len(opts) > 20:
            print(f"      ... (+{len(opts)-20} opções)")

    print("\n### CAMPOS DE TEXTO / DATA — id, rótulo")
    for i in f.find_all("input", attrs={"type": ["text"]}):
        print(f"  name={i.get('name')!r:34} id={i.get('id')!r:26} rótulo={rotulo_de(i)!r}")

    print("\n### BOTÕES / AÇÕES (a[title], a[onclick], input submit)")
    for a in f.find_all("a"):
        if a.get("title") or (a.get("onclick") and ("Submit" in a.get("onclick") or "jsfcljs" in a.get("onclick"))):
            oc = " ".join((a.get("onclick") or "").split())
            print(f"  a id={a.get('id')!r:24} title={a.get('title')!r:14} onclick={oc[:80]}")
    for i in f.find_all("input", attrs={"type": ["submit", "button", "image"]}):
        print(f"  input id={i.get('id')!r:24} name={i.get('name')!r:20} value={i.get('value')!r}")

    print("\n### GRADE — cabeçalhos da tabela de resultado")
    grade = f.find("table", id=re.compile(r"listagem|list|grid|tabela")) or f.find("table")
    if grade:
        print("  table id:", grade.get("id"))
        ths = grade.find_all("th")
        cols = [" ".join(th.get_text(" ", strip=True).split()) for th in ths if th.get_text(strip=True)]
        print(f"  {len(cols)} colunas:", cols)
    else:
        print("  (nenhuma tabela de grade — a grade só aparece após pesquisar)")

    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
