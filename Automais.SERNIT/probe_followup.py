"""Sonda 7: Registrar FollowUP — MAPA APENAS, NUNCA ENVIA (ensaio).

No SER-RJ (../../docs/ser.md §9) o FollowUP não mora no `form0`: o item do menu abre um MODAL
com form PRÓPRIO, e o Gravar dele é POST comum, com o ViewState de DENTRO desse form. Aqui só
abrimos o modal (A4J de leitura) e mapeamos o form (textarea, Gravar, Cancelar, ViewState,
campos). **O Gravar do FollowUP nunca é postado.**

Uso:  python probe_followup.py [ID_SOLICITACAO]   (sem id: 1ª linha EM_FILA)
"""

from __future__ import annotations

import re
import sys

from sernit.client import (CAP, URL_SOLIC, campos_todos, hidden_do_form,
                           login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ALVO = next((a for a in sys.argv[1:] if not a.startswith("--")), None)


def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def tela_pesquisa(s):
    for _ in range(4):
        h = s.get(URL_SOLIC).text
        d = sopa(h)
        if d.find("input", attrs={"value": re.compile("Pesquisar", re.I)}) and d.find("form", id="form0"):
            return h, d
        s.ativar_modulo("ambulatorial")
    raise SystemExit("não consegui a tela de pesquisa")


def main() -> int:
    s = login_e_modulo("ambulatorial")
    tela, d = tela_pesquisa(s)
    btn = d.find("input", attrs={"value": re.compile("Pesquisar", re.I)})
    dados = campos_todos(d, "form0")
    if ALVO:
        dados["form0:idSolicitacao"] = ALVO
    else:
        dados[select_situacao(d)] = "EM_FILA"
    dados[btn.get("name")] = btn.get("value")
    dados |= {"AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(tela) or ""}
    g = seguir_redirect_a4j(s, s.post(URL_SOLIC, dados, ajax=True).text)

    dd = sopa(g)
    grade = dd.find("table", id="form0:listagem")
    corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
    if not corpo:
        raise SystemExit("busca sem resultado")
    linha = corpo[0]
    id_txt = " ".join(linha.find("td").get_text(" ", strip=True).split())
    lid = next((a.get("id") for a in linha.find_all("a")
                if "followup" in " ".join(a.get_text(" ", strip=True).split()).lower().replace(" ", "")), None)
    if not lid:
        raise SystemExit("item 'Registrar FollowUP' não achado na linha")
    print(f"solicitação {id_txt} — abrindo modal FollowUP (item {lid}) [LEITURA]")

    # A4J de leitura: abre o modal (não posta nada de escrita)
    ph = hidden_do_form(g, "form0")
    ph |= {"AJAXREQUEST": "_viewRoot", lid: lid, "ajaxSingle": lid,
           "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(g) or ""}
    resp = seguir_redirect_a4j(s, s.post(URL_SOLIC, ph, ajax=True).text)
    (CAP / "followup_modal.html").write_text(resp, encoding="utf-8")
    dm = sopa(resp)

    # acha o form do modal: o que contém um <textarea> e um botão Gravar
    modal_form, textarea, gravar, cancelar = None, None, None, None
    for f in dm.find_all("form"):
        ta = f.find("textarea")
        gv = None
        for el in f.find_all(["input", "a", "button"]):
            rot = el.get("value") or (el.get_text(" ", strip=True) if el.name != "input" else "")
            if re.fullmatch(r"\s*gravar\s*", rot or "", re.I):
                gv = el
        if ta and gv:
            modal_form, textarea, gravar = f, ta, gv
            for el in f.find_all(["input", "a", "button"]):
                rot = el.get("value") or (el.get_text(" ", strip=True) if el.name != "input" else "")
                if re.fullmatch(r"\s*cancelar\s*", rot or "", re.I):
                    cancelar = el
            break

    if not modal_form:
        print("!! modal do FollowUP não localizado na resposta — ver capturas/followup_modal.html")
        print("   (pode vir como modalPanel com id próprio; inspecionar)")
        s.close()
        return 1

    print(f"\n### MODAL FollowUP (form próprio) -> capturas/followup_modal.html")
    print("form id:", modal_form.get("id"), "| action:", modal_form.get("action"))
    print("textarea:", textarea.get("name"))
    print("Gravar:  ", gravar.get("id"), "(NÃO SERÁ POSTADO)")
    print("Cancelar:", cancelar.get("id") if cancelar else "(?)")
    print("ViewState do modal:", viewstate(str(modal_form)))
    print("\ncampos do form do modal:")
    for i in modal_form.find_all(["input", "select", "textarea"]):
        t = i.name if i.name != "input" else (i.get("type") or "text")
        print(f"  {t:8} name={i.get('name')!r}")
    print("\n>>> ENSAIO: modal mapeado, Gravar do FollowUP NÃO foi enviado.")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
