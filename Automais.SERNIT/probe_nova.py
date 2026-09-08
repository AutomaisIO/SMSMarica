"""Sonda 8: abre a aba "Editar/Nova" (formulário de nova solicitação) e mapeia a estrutura.

A aba de criação é um rich:tab do SERNIT, aberta por `form0:editar_server_submit` (mesmo mecanismo
do SER-RJ). Aqui levantamos o bloco fixo (Tipo, Recurso, "É ambulatório estadual?", CADSUS/CNS,
médico, classificação de risco, hipótese/CID, unidade de origem) e localizamos o container de
campos dinâmicos e o suggestionbox de CID. SOMENTE LEITURA — abrir a aba e trocar combos só
re-renderiza a view; o Gravar nunca é acionado.

Uso:  python probe_nova.py
"""

from __future__ import annotations

import re
import sys

from sernit.client import (CAP, URL_SOLIC, campos_todos, hidden_do_form,
                           login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def abrir_aba_nova(s):
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    dados = campos_todos(d, "form0") | {
        "form0": "form0",
        "form0:editar_server_submit": "form0:editar_server_submit",
        "javax.faces.ViewState": viewstate(tela) or "",
    }
    r = s.post(act, dados)
    return seguir_redirect_a4j(s, r.text)


def main() -> int:
    s = login_e_modulo("ambulatorial")
    html = abrir_aba_nova(s)
    (CAP / "nova.html").write_text(html, encoding="utf-8")
    d = sopa(html)
    f = d.find("form", id="form0")
    print(f"aba Nova aberta: form0 presente={bool(f)}  ({len(html)} bytes) -> capturas/nova.html")
    if not f:
        print("!! form0 não veio — a aba não abriu como esperado")
        print("trecho:", " ".join(html[:400].split()))
        return 1

    print("\n### SELECTS (combos) — name, rótulo aproximado, nº de opções")
    for sel in f.find_all("select"):
        nome = sel.get("name")
        opts = sel.find_all("option")
        amostra = [" ".join(o.get_text(" ", strip=True).split())[:30] for o in opts[:4]]
        print(f"  {nome:34} opcoes={len(opts):4}  ex.: {amostra}")

    print("\n### RADIOS / CHECKBOX (name → valores)")
    radios = {}
    for i in f.find_all("input", attrs={"type": ["radio", "checkbox"]}):
        radios.setdefault(i.get("name"), []).append(i.get("value"))
    for nome, vals in radios.items():
        print(f"  {nome:40} {vals}")

    print("\n### CAMPOS DE TEXTO / TEXTAREA (name, id, rótulo irmão)")
    for el in f.find_all(["input", "textarea"]):
        t = (el.get("type") or el.name).lower()
        if t not in ("text", "textarea"):
            continue
        # rótulo: label no mesmo td ou td anterior
        td = el.find_parent("td")
        rot = ""
        if td:
            lab = td.find("label")
            if not lab and td.find_previous_sibling("td"):
                lab = td.find_previous_sibling("td").find("label") or td.find_previous_sibling("td")
            rot = " ".join(lab.get_text(" ", strip=True).split())[:30] if lab else ""
        print(f"  {(el.get('name') or ''):34} id={(el.get('id') or ''):26} [{t:8}] rot={rot!r}")

    print("\n### CONTAINERS DE CAMPOS DINÂMICOS (padrões conhecidos)")
    dins = set()
    for el in f.find_all(id=re.compile(r"dinamic|container_dinamico|campos", re.I)):
        dins.add(el.get("id"))
    print("  ids:", sorted(dins) or "(nenhum ainda — aparecem após escolher o Recurso)")

    print("\n### SUGGESTIONBOX / CID (procedimento)")
    sb = re.findall(r"new RichFaces\.Suggestion\([^)]*\)", html)
    for m in sb[:4]:
        print("  ", " ".join(m.split())[:160])
    proc = f.find(attrs={"name": re.compile("procedimento", re.I)})
    print("  campo procedimento:", proc.get("name") if proc else "(não achado)",
          "| alt=", proc.get("alt") if proc else "")

    print("\n### BOTÃO GRAVAR (nunca acionado)")
    g = f.find("input", attrs={"value": re.compile(r"^\s*Gravar\s*$", re.I)})
    print("  Gravar:", g.get("id") if g else "(não achado)")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
