"""Sonda 2: executa uma busca (EM_FILA) e mapeia a grade + o menu de Ação.

Confirma (medindo, não assumindo — lição do SER-RJ) se a conta já vem escopada a Maricá,
quantos registros a busca devolve, as colunas reais e os itens do menu *Ação/Opções* de uma
linha (Visualizar/Editar/Histórico/FollowUP). Verifica também se surge *Exportar* após buscar.

SOMENTE LEITURA — a busca é POST mas não altera dado. PII vai só para capturas/ (gitignored);
no terminal, nomes/CNS saem mascarados.

Uso:  python probe_grade.py [SITUACAO]   (default EM_FILA)
"""

from __future__ import annotations

import re
import sys

from sernit.client import (CAP, URL_SOLIC, campos_todos, hidden_do_form,
                            login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SIT = (sys.argv[1] if len(sys.argv) > 1 else "EM_FILA").upper()


def select_situacao(doc):
    """Id do <select> de Situação: o que oferece 'EM_FILA' — nunca por j_id fixo."""
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def mascara(txt: str) -> str:
    txt = re.sub(r"\d{11,15}", "«num»", txt)                 # CNS/CPF
    txt = re.sub(r"\b[A-ZÀ-Ú][A-ZÀ-Ú ]{6,}\b", "«NOME»", txt)  # nomes em caixa alta
    return txt


def main() -> int:
    s = login_e_modulo("ambulatorial")
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    sit = select_situacao(d)
    btn = (f.find("input", attrs={"value": re.compile("Pesquisar", re.I)})
           or f.find("input", attrs={"type": re.compile("submit", re.I)}))
    if not sit or not btn:
        print(f"!! não achei situação({sit!r}) ou botão({btn!r}) — layout mudou?")
        (CAP / "pesquisar_sembotao.html").write_text(tela, encoding="utf-8")
        return 1
    print(f"situacao select={sit!r}  pesquisar botao={btn.get('name')!r}")

    dados = campos_todos(d, "form0")
    dados[sit] = SIT
    dados[btn.get("name")] = btn.get("value") or "Pesquisar"
    dados["javax.faces.ViewState"] = viewstate(tela) or ""

    # 1ª tentativa: postback comum (o botão é <input submit>)
    r = s.post(act, dados)
    html = seguir_redirect_a4j(s, r.text)
    grade = sopa(html).find("table", id="form0:listagem")
    linhas = grade.find_all("tr") if grade else []
    corpo = [tr for tr in linhas if tr.find("td")]

    # se veio vazio, tenta como A4J (a armadilha AJAXREQUEST do §3.1)
    if not corpo:
        print("postback comum veio vazio — tentando como A4J (AJAXREQUEST)…")
        dados["AJAXREQUEST"] = "form0"
        dados["AJAX:EVENTS_COUNT"] = "1"
        r = s.post(act, dados, ajax=True)
        html = seguir_redirect_a4j(s, r.text)
        grade = sopa(html).find("table", id="form0:listagem")
        corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]

    (CAP / f"grade_{SIT}.html").write_text(html, encoding="utf-8")
    print(f"registros na 1ª página: {len(corpo)}  -> capturas/grade_{SIT}.html")

    # mensagens (avisos de corte etc.)
    msg = sopa(html).find(id=re.compile(r"messages"))
    if msg and msg.get_text(strip=True):
        print("MENSAGENS:", " ".join(msg.get_text(' ', strip=True).split())[:200])

    # colunas reais
    if grade:
        ths = [t.get_text(" ", strip=True) for t in grade.find_all("th") if t.get_text(strip=True)]
        print("colunas:", ths)

    # 1ª linha (mascarada) + menu de Ação
    if corpo:
        primeira = corpo[0]
        cels = [" ".join(td.get_text(" ", strip=True).split()) for td in primeira.find_all("td")]
        print("1ª linha (mascarada):", [mascara(c) for c in cels][:12])
        # itens do menu de ação (links dentro da última célula / com onclick de menu)
        acoes = []
        for a in primeira.find_all("a"):
            t = " ".join(a.get_text(" ", strip=True).split())
            if t:
                acoes.append((a.get("id"), t))
        print("menu de Ação (linha 0):", acoes or "(nenhum <a> textual na linha — menu pode ser popup)")

    # Exportar existe nesta tela?
    exp = sopa(html).find(lambda t: t.name in ("a", "input") and
                          ("export" in (t.get("id") or "").lower()
                           or "Exportar" in (t.get("value") or "")
                           or "jsfcljs" in (t.get("onclick") or "")))
    print("Exportar presente?", bool(exp), (exp.get("id") if exp else ""))

    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
