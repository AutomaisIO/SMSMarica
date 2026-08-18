"""Registrar FollowUP numa solicitação do SER — protocolo lido do navegador em 18/08/2026.

Por que isto é diferente de todo o resto do laboratório: o FollowUP **não** mora no `form0`.
Clicar em "Registrar FollowUP" no menu da linha abre um modal que tem **form próprio**
(`j_idNNN`, posicional), e o botão Gravar dele é um **POST comum** — sem `AJAXREQUEST`, sem
`ajaxSingle`, sem ViewState do form0. A página inteira navega e volta com "FollowUp registrado!".

O POST é minúsculo (cinco campos):

    <form>            = <form>            # marcador do form, como sempre no JSF
    <form>:<textarea> = <observação>
    <form>:<gravar>   = Gravar
    autoScroll        =
    javax.faces.ViewState = <o ViewState DE DENTRO desse form>

Nada de `j_id` chumbado: os três ids do modal são posicionais e mudam quando a SES-RJ
recompila. Aqui eles são resolvidos na hora — o item do menu pelo TEXTO, o form pelo título
"Registrar FollowUP", o textarea e o Gravar por serem os únicos do form.

**"FollowUp registrado!" não é prova.** Foi essa a lição de 10/08/2026 com a Hipótese: o SER
respondeu sucesso e não gravou nada. Por isso esta sonda confere pelo **Histórico**, onde o
evento aparece como `FollowUP` com estado anterior IGUAL ao atual — foi assim que o registro de
18/08/2026 na 8196837 se confirmou (Agendada -> Agendada).

> Correção ao `docs/ser.md §6`, que diz "FollowUP é Em fila -> Em fila": o certo é que FollowUP
> **preserva a situação, qualquer que ela seja**. O caso medido era Agendada -> Agendada.

Por padrão é ENSAIO: abre o modal, mostra os ids resolvidos e PARA. Para registrar de verdade
é preciso `--gravar`.

Uso:
  python probe_followup.py 8196837 --texto "..."
  python probe_followup.py 8196837 --texto "..." --gravar
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys

import httpx

RAIZ = pathlib.Path(__file__).parent
sys.path.insert(0, str(RAIZ))

from probe_editar_solicitacao import (  # noqa: E402
    BASE, CAMPO_ID, CAMPO_SITUACAO, CAP, UA, URL_SOLIC,
    Ser, campos_do_form, mensagens, sopa, viewstate,
)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL_HISTORICO = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-historico.seam"


def pesquisar(ser: Ser, id_solic: str) -> str:
    """Busca por ID, sem filtro de situação — o FollowUP vale em qualquer situação viva."""
    tela = ser.c.get(URL_SOLIC).text
    d = sopa(tela)
    botao = d.find(attrs={"title": "Pesquisar"})
    if botao is None:
        raise SystemExit("tela de Solicitação sem botão Pesquisar — sessão derrubada?")
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    return ser.postar(acao, campos_do_form(d) | {
        "form0": "form0",
        botao.get("id"): botao.get("id"),
        CAMPO_ID: id_solic,
        CAMPO_SITUACAO: "",
        "AJAXREQUEST": "form0",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(tela),
    })


def acao_da_linha(html: str, id_solic: str, rotulo: str) -> tuple[int, str, list[str]]:
    """(índice, id da ação, células) — a ação é achada pelo TEXTO, nunca pelo j_id."""
    d = sopa(html)
    tab = d.find("table", id="form0:listagem")
    if tab is None:
        raise SystemExit("a busca não devolveu grade. " + (mensagens(html) or "(sem mensagem)"))

    linhas = [tr for tr in tab.select("tbody tr")
              if any(re.fullmatch(r"\d{5,}", td.get_text(strip=True)) for td in tr.find_all("td"))]
    if len(linhas) != 1:
        raise SystemExit(f"esperava 1 linha para o ID {id_solic}, vieram {len(linhas)}")

    tr = linhas[0]
    celulas = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all("td")]
    if id_solic not in celulas:
        raise SystemExit(f"a linha devolvida não é o ID {id_solic}: {celulas[:6]}")

    idx = int(re.search(r"form0:listagem:(\d+):", str(tr)).group(1))
    menu = d.find(id=re.compile(rf"^form0:listagem:{idx}:j_id\d+_menu$"))
    if menu is None:
        raise SystemExit("linha sem menu de Ação")

    alvo = next((a for a in menu.find_all("a")
                 if rotulo.casefold() in " ".join(a.get_text(" ", strip=True).split()).casefold()), None)
    if alvo is None:
        disponiveis = [" ".join(a.get_text(" ", strip=True).split()) for a in menu.find_all("a")]
        raise SystemExit(f"esta solicitação não oferece {rotulo!r}. Ações: {disponiveis}")
    return idx, alvo.get("id"), celulas


def abrir_modal(ser: Ser, html_busca: str, id_acao: str) -> str:
    d = sopa(html_busca)
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    return ser.postar(acao, campos_do_form(d) | {
        "form0": "form0",
        id_acao: id_acao,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(html_busca),
    })


def form_do_modal(html: str) -> tuple[str, str, str, str, str]:
    """(form_id, action, nome do textarea, nome do Gravar, viewstate) — tudo resolvido na hora."""
    d = sopa(html)
    for form in d.find_all("form"):
        ta = form.find("textarea")
        if ta is None or not ta.get("name"):
            continue
        gravar = next(
            (e for e in form.find_all(["a", "input"])
             if (e.get("title") or e.get("value") or e.get_text(strip=True) or "").strip().casefold() == "gravar"),
            None)
        if gravar is None:
            continue
        vs = ""
        if (i := form.find("input", attrs={"name": "javax.faces.ViewState"})):
            vs = i.get("value") or ""
        return (form.get("id") or "", form.get("action") or URL_SOLIC,
                ta["name"], gravar.get("name") or gravar.get("id") or "", vs)
    raise SystemExit("não achei o form do modal de FollowUP na resposta")


def historico(ser: Ser, html_busca: str, id_acao_hist: str) -> str:
    d = sopa(html_busca)
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    return ser.postar(acao, campos_do_form(d) | {
        "form0": "form0",
        id_acao_hist: id_acao_hist,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(html_busca),
    })


def eventos_do_historico(html: str) -> list[list[str]]:
    d = sopa(html)
    tab = d.find("table", id="form0:historicoList") or d.find(
        "table", id=re.compile(r"historico", re.I))
    if tab is None:
        return []
    saida = []
    for tr in tab.find_all("tr"):
        celulas = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all("td")]
        if any(celulas):
            saida.append(celulas)
    return saida


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("id_solicitacao")
    p.add_argument("--texto", required=True, help="a observação do FollowUP")
    p.add_argument("--gravar", action="store_true", help="registra de verdade (sem isso é ensaio)")
    a = p.parse_args()

    if not a.texto.strip():
        raise SystemExit("o texto do FollowUP não pode ser vazio")

    with httpx.Client(base_url=BASE, timeout=120, follow_redirects=True,
                      headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"}) as c:
        ser = Ser(c)
        ser.login()

        print(f"\nbuscando solicitação {a.id_solicitacao}...")
        busca = pesquisar(ser, a.id_solicitacao)
        idx, id_acao, celulas = acao_da_linha(busca, a.id_solicitacao, "followup")

        print("\n--- LINHA " + "-" * 64)
        for cel in celulas:
            if cel:
                print(f"    {cel[:100]}")
        print(f"\n    ação 'Registrar FollowUP' da linha {idx}: {id_acao}")

        modal = abrir_modal(ser, busca, id_acao)
        (CAP / f"followup_{a.id_solicitacao}_modal.html").write_text(modal, encoding="utf-8")
        form_id, acao, campo_texto, campo_gravar, vs = form_do_modal(modal)

        print("\n--- MODAL " + "-" * 64)
        print(f"    form      : {form_id}   (POST comum, sem AJAXREQUEST)")
        print(f"    action    : {acao}")
        print(f"    observação: {campo_texto}")
        print(f"    Gravar    : {campo_gravar}")
        print(f"    ViewState : {vs}")
        print(f"    texto     : {a.texto!r}")

        if not a.gravar:
            print("\n    ENSAIO — nada foi registrado. Rode com --gravar para valer.")
            return 0

        html = ser.postar(acao, {
            form_id: form_id,
            campo_texto: a.texto,
            campo_gravar: "Gravar",
            "autoScroll": "",
            "javax.faces.ViewState": vs,
        }, ajax=False)
        (CAP / f"followup_{a.id_solicitacao}_resposta.html").write_text(html, encoding="utf-8")
        print(f"\n    resposta: {len(html)} bytes")
        if (msg := mensagens(html)):
            print(f"    mensagem do SER: {msg}")

        # ---- CONFERÊNCIA: "registrado!" não é prova (lição de 10/08/2026 com a Hipótese).
        print("\n--- CONFERÊNCIA pelo Histórico " + "-" * 44)
        busca2 = pesquisar(ser, a.id_solicitacao)
        _, id_hist, _ = acao_da_linha(busca2, a.id_solicitacao, "historico")
        eventos = eventos_do_historico(historico(ser, busca2, id_hist))

        achou = False
        for ev in eventos:
            linha = " | ".join(ev)
            if "followup" in linha.casefold() and a.texto.strip()[:40] in linha:
                achou = True
                print(f"    {linha[:180]}")
        if achou:
            print("\n    REGISTRADO ✓ (confirmado por releitura do histórico)")
            return 0

        print(f"    !! não achei o FollowUP no histórico ({len(eventos)} evento(s) lidos).")
        for ev in eventos[-3:]:
            print(f"       {' | '.join(ev)[:150]}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
