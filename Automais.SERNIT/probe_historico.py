"""Sonda 3: abre o "Historico da Solicitação" de uma linha e mapeia os dois blocos.

No SER-RJ (../../docs/ser.md §5) essa é a tela rica: dados do paciente (com os 3 telefones,
incluindo WhatsApp) + trilha de eventos (Solicitar/FollowUP/Pendenciar/Cancelar) com usuário e
IP. Aqui confirmamos se o SERNIT traz o mesmo. SOMENTE LEITURA (o item é "Historico", sem verbo
de escrita — passa pela trava; "Cancelar"/"Registrar FollowUP" seriam recusados).

Estrutura (rótulos/estrutura), não conteúdo: PII vai só para capturas/ (gitignored); no terminal
os valores saem mascarados.

Uso:  python probe_historico.py [ID_SOLICITACAO]   (sem id: usa a 1ª linha de EM_FILA)
"""

from __future__ import annotations

import re
import sys

from sernit.client import (CAP, URL_SOLIC, campos_todos, login_e_modulo,
                            seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ALVO = sys.argv[1] if len(sys.argv) > 1 else None


def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def mascara(t: str) -> str:
    t = re.sub(r"\d{7,15}", "«num»", t)
    t = re.sub(r"\b[A-ZÀ-Ú][A-ZÀ-Ú ]{6,}\b", "«NOME»", t)
    return t


def link_por_texto(tr, texto):
    for a in tr.find_all("a"):
        if texto.lower() in " ".join(a.get_text(" ", strip=True).split()).lower():
            return a.get("id")
    return None


def main() -> int:
    s = login_e_modulo("ambulatorial")
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    sit = select_situacao(d)
    btn = f.find("input", attrs={"value": re.compile("Pesquisar", re.I)})

    dados = campos_todos(d, "form0")
    if ALVO:
        dados["form0:idSolicitacao"] = ALVO
    else:
        dados[sit] = "EM_FILA"
    dados[btn.get("name")] = btn.get("value")
    dados |= {"AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
              "javax.faces.ViewState": viewstate(tela) or ""}
    html = seguir_redirect_a4j(s, s.post(act, dados, ajax=True).text)

    dd = sopa(html)
    grade = dd.find("table", id="form0:listagem")
    corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
    if not corpo:
        print("!! busca sem resultado — nada para abrir")
        return 1
    linha = corpo[0]
    id_txt = " ".join(linha.find("td").get_text(" ", strip=True).split())
    lid = link_por_texto(linha, "Historico")
    print(f"abrindo Histórico da 1ª linha (ID={id_txt}) — link {lid!r}")
    if not lid:
        print("!! link 'Historico' não achado na linha")
        return 1

    # A4J do item do menu (AJAXREQUEST=_viewRoot, como no SER-RJ §4.2)
    from sernit.client import hidden_do_form
    ph = hidden_do_form(html, "form0")
    ph |= {"AJAXREQUEST": "_viewRoot", lid: lid, "ajaxSingle": lid,
           "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(html) or ""}
    hist = seguir_redirect_a4j(s, s.post(act, ph, ajax=True).text)
    (CAP / "historico.html").write_text(hist, encoding="utf-8")
    print(f"histórico: {len(hist)} bytes -> capturas/historico.html")

    dh = sopa(hist)
    # bloco 1: campos readonly (rótulo do <td> anterior + valor)
    print("\n### BLOCO 1 — dados do paciente (rótulo → valor mascarado)")
    campos_vistos = 0
    for el in dh.find_all(["input", "select", "textarea"]):
        val = el.get("value") or (el.get_text(strip=True) if el.name != "input" else "")
        td = el.find_parent("td")
        rot = ""
        if td and td.find_previous_sibling("td"):
            rot = " ".join(td.find_previous_sibling("td").get_text(" ", strip=True).split())
        if rot:
            print(f"  {rot[:32]:32} → {mascara(val)[:40]!r}")
            campos_vistos += 1
    if not campos_vistos:
        print("  (nenhum campo com rótulo — layout diferente; ver captura)")

    # bloco 2: trilha de eventos (tabela com cabeçalhos Data/Evento/Estado/Usuario/IP…)
    print("\n### BLOCO 2 — trilha de eventos")
    tab = None
    for t in dh.find_all("table"):
        cab = " ".join(t.get_text(" ", strip=True).split()).lower()
        if "evento" in cab and ("usu" in cab or "estado" in cab or "observ" in cab):
            tab = t
            break
    if tab:
        ths = [x.get_text(" ", strip=True) for x in tab.find_all("th") if x.get_text(strip=True)]
        print("  colunas:", ths)
        rows = [tr for tr in tab.find_all("tr") if tr.find("td")]
        print(f"  {len(rows)} linha(s) de evento")
        if rows:
            cels = [" ".join(td.get_text(" ", strip=True).split()) for td in rows[0].find_all("td")]
            print("  1º evento (mascarado):", [mascara(c) for c in cels])
    else:
        print("  (tabela de eventos não localizada — ver captura)")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
