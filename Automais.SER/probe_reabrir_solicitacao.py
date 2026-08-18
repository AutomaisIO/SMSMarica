"""RECONHECIMENTO de uma solicitação CANCELADA — o que o SER oferece para reabrir/reenviar.

Antes de escrever qualquer coisa, esta sonda responde três perguntas que só a tela viva
responde, e que decidem se "reenviar um cancelado" é sequer um caminho:

  1. Quais AÇÕES o menu da linha oferece quando a situação é Cancelada? (`Editar` pode
     simplesmente não estar lá — é o que acontece em `Alta`.)
  2. Se a aba Editar abre, ela vem preenchida e EDITÁVEL, ou só para consulta?
  3. Que BOTÕES a tela de edição expõe (`Gravar` e o que mais houver) — lidos da página,
     nunca chumbados: `j_id` é posicional e muda quando a SES-RJ recompila.

SOMENTE LEITURA. Os dois únicos POSTs são `Pesquisar` e a ação `Editar` da linha — os mesmos
que `probe_editar_solicitacao.py` já exercita. Nada é gravado: nenhum botão com verbo de
escrita é acionado, e a trava abaixo recusa o POST se algum escapar.

Uso:
  python probe_reabrir_solicitacao.py 3968654
  python probe_reabrir_solicitacao.py 3968654 --situacao CANCELADA
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

# A sonda inteira é leitura. Espelha a trava do motor .NET (`SerWebSessao.GarantirLeitura`)
# para que um POST de escrita não saia daqui por descuido enquanto se mapeia a tela.
ESCRITA = re.compile(
    r"(salvar|gravar|confirmar|inserir|incluir|alterar|atualizar|excluir|remover|deletar|"
    r"apagar|cancelar|agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|"
    r"executar|efetivar|finalizar|aprovar|reprovar|transferir|registrar|followup|pendenciar|"
    r"reabrir|reativar|reenviar)", re.I)

LIBERADOS = {"form0:editar_server_submit", "form0:pesquisar_server_submit"}


def guardar(dados: dict) -> dict:
    for chave in dados:
        if chave not in LIBERADOS and ESCRITA.search(chave):
            raise SystemExit(f"TRAVA: POST recusado — parâmetro de escrita: {chave!r}")
    return dados


def opcoes_situacao(html: str) -> list[tuple[str, str]]:
    """O combo de situação, lido da tela — para --situacao aceitar valor OU rótulo."""
    d = sopa(html)
    sel = d.find("select", attrs={"name": CAMPO_SITUACAO})
    if sel is None:
        return []
    return [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
            for o in sel.find_all("option")]


def pesquisar(ser: Ser, id_solic: str, situacao: str | None) -> tuple[str, list]:
    """Tela nova a cada busca — ViewState de tela anterior já contaminou busca antes."""
    tela = ser.c.get(URL_SOLIC).text
    d = sopa(tela)
    botao = d.find(attrs={"title": "Pesquisar"})
    if botao is None:
        raise SystemExit("tela de Solicitação sem botão Pesquisar — sessão derrubada?")
    acao = d.find("form", id="form0").get("action") or URL_SOLIC

    dados = campos_do_form(d) | {
        "form0": "form0",
        botao.get("id"): botao.get("id"),
        CAMPO_ID: id_solic,
        "AJAXREQUEST": "form0",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(tela),
    }
    if situacao:
        dados[CAMPO_SITUACAO] = situacao
    return ser.postar(acao, guardar(dados)), opcoes_situacao(tela)


def linha_e_acoes(html: str, id_solic: str) -> tuple[int, str, list[str], dict[str, str]]:
    """(índice, cabeçalho do menu, células da linha, {rótulo da ação: id}).

    Diferente de `probe_editar_solicitacao.linha_unica`, esta NÃO exige que exista `Editar`:
    o que interessa aqui é justamente o cardápio inteiro da linha cancelada.
    """
    d = sopa(html)
    tab = d.find("table", id="form0:listagem")
    if tab is None:
        raise SystemExit("a busca não devolveu grade. " + (mensagens(html) or "(sem mensagem)"))

    linhas = [tr for tr in tab.select("tbody tr")
              if any(re.fullmatch(r"\d{5,}", td.get_text(strip=True)) for td in tr.find_all("td"))]
    if len(linhas) != 1:
        raise SystemExit(f"esperava exatamente 1 linha para o ID {id_solic}, vieram {len(linhas)}")

    tr = linhas[0]
    celulas = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all("td")]
    if id_solic not in celulas:
        raise SystemExit(f"a linha devolvida não é o ID {id_solic}: {celulas[:6]}")

    idx_m = re.search(r"form0:listagem:(\d+):", str(tr))
    if not idx_m:
        raise SystemExit("não consegui identificar o índice da linha na grade")
    idx = int(idx_m.group(1))

    menu = d.find(id=re.compile(rf"^form0:listagem:{idx}:j_id\d+_menu$"))
    if menu is None:
        raise SystemExit("linha sem menu de Ação — a situação não oferece ação nenhuma?")

    cabecalho = ""
    if (cab := menu.find("div", class_=re.compile("rich-menu-item-disabled"))):
        cabecalho = " ".join(cab.get_text(" ", strip=True).split())

    acoes = {}
    for a in menu.find_all("a"):
        rotulo = " ".join(a.get_text(" ", strip=True).split())
        if rotulo:
            acoes[rotulo] = a.get("id") or "(sem id)"
    return idx, cabecalho, celulas, acoes


def abrir_acao(ser: Ser, html_busca: str, id_acao: str) -> str:
    """Aciona uma ação A4J da linha. Só chamada para ações de LEITURA (Editar/Visualizar)."""
    d = sopa(html_busca)
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    dados = campos_do_form(d) | {
        "form0": "form0",
        id_acao: id_acao,
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(html_busca),
    }
    return ser.postar(acao, guardar(dados))


def rotulo_de(el) -> str:
    """Rótulo visível de um campo: o <label> irmão, ou o <td> anterior sem controle dentro."""
    if el.parent and (lab := el.parent.find("label")):
        t = " ".join(lab.get_text(" ", strip=True).split())
        if t:
            return t[:52]
    td = el.find_parent("td")
    while td is not None:
        ant = td.find_previous_sibling("td")
        while ant is not None:
            if not ant.find(["input", "select", "textarea"]):
                t = " ".join(ant.get_text(" ", strip=True).split())
                if t:
                    return t[:52]
            ant = ant.find_previous_sibling("td")
        td = td.find_parent("td")
    return ""


def dump_formulario(html: str) -> None:
    d = sopa(html)
    f = d.find("form", id="form0")
    if f is None:
        print("!! a resposta não tem form0 — não é a tela de edição.")
        return

    print("\n" + "=" * 78)
    print("CAMPOS DA TELA  (T=travado/disabled — não viaja no POST, o SER já tem do lado dele)")
    print("=" * 78)
    for el in f.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or nome in {"form0", "autoScroll", "javax.faces.ViewState"}:
            continue
        tipo = el.name if el.name != "input" else (el.get("type") or "text").lower()
        if tipo == "hidden":
            continue
        if el.name == "select":
            sel = el.find("option", selected=True)
            rot_sel = sel.get_text(strip=True)[:34] if sel else ""
            valor = f"{(sel.get('value') if sel else '')!r} ({rot_sel})"
            valor += f"  [{len(el.find_all('option'))} opcoes]"
        elif el.name == "textarea":
            valor = repr((el.get_text() or "")[:60])
        else:
            valor = repr((el.get("value") or "")[:60])
        trava = "T" if el.has_attr("disabled") else " "
        print(f" {trava} {nome:40} {tipo:9} {rotulo_de(el)[:34]:36} = {valor}")

    dins = list(d.find_all(id=re.compile(r"^form0:container_dinamico_id_\d+$")))
    print(f"\n--- bloco DINÂMICO: {len(dins)} campo(s)")
    for cont in dins:
        texto = " ".join(cont.get_text(" ", strip=True).split())
        el = cont.find(["input", "select", "textarea"])
        nome = el.get("name") if el is not None else "(sem controle)"
        num = cont["id"].rsplit("_", 1)[-1]
        obrig = "*" if "*" in texto else " "
        print(f"     {num:>4}  {obrig} {texto.replace(' *', '')[:52]:54} {nome}")

    print("\n" + "=" * 78)
    print("BOTÕES / AÇÕES DA TELA  (lidos da página — nenhum foi acionado)")
    print("=" * 78)
    vistos = set()
    for a in d.find_all("a"):
        titulo = a.get("title") or ""
        texto = " ".join(a.get_text(" ", strip=True).split())
        rotulo = titulo or texto
        if not rotulo or a.get("id") is None:
            continue
        chave = (a["id"], rotulo)
        if chave in vistos:
            continue
        vistos.add(chave)
        if ESCRITA.search(rotulo) or titulo:
            marca = "!" if ESCRITA.search(rotulo) else " "
            print(f" {marca} {a['id']:44} {rotulo[:44]}")
    for i in d.find_all("input", attrs={"type": re.compile("submit|button|image", re.I)}):
        rotulo = i.get("value") or i.get("title") or i.get("alt") or ""
        if rotulo:
            marca = "!" if ESCRITA.search(rotulo) else " "
            print(f" {marca} {(i.get('name') or i.get('id') or '?'):44} {rotulo[:44]}")


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("id_solicitacao")
    p.add_argument("--situacao", default="CANCELADA",
                   help="valor ou rótulo do combo de situação (padrão CANCELADA)")
    a = p.parse_args()

    with httpx.Client(base_url=BASE, timeout=120, follow_redirects=True,
                      headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"}) as c:
        ser = Ser(c)
        ser.login()

        print(f"\nbuscando solicitação {a.id_solicitacao} (situação {a.situacao})...")
        busca, opcoes = pesquisar(ser, a.id_solicitacao, a.situacao)
        if opcoes:
            print("    combo de situação na tela: "
                  + ", ".join(f"{v}={t}" for v, t in opcoes if v)[:200])
        try:
            idx, cab, celulas, acoes = linha_e_acoes(busca, a.id_solicitacao)
        except SystemExit as e:
            print(f"  ({e}) — tentando sem filtro de situação")
            busca, _ = pesquisar(ser, a.id_solicitacao, None)
            idx, cab, celulas, acoes = linha_e_acoes(busca, a.id_solicitacao)

        (CAP / f"reabrir_{a.id_solicitacao}_busca.html").write_text(busca, encoding="utf-8")

        print("\n" + "=" * 78)
        print(f"LINHA {idx}  —  {cab}")
        print("=" * 78)
        for cel in celulas:
            if cel:
                print(f"    {cel[:104]}")

        print("\n" + "=" * 78)
        print(f"AÇÕES OFERECIDAS PELA LINHA  ({len(acoes)})")
        print("=" * 78)
        for rotulo, ident in acoes.items():
            marca = "!" if ESCRITA.search(rotulo) else " "
            print(f" {marca} {rotulo[:44]:46} {ident}")

        leitura = next((r for r in acoes if r.lower() in {"editar", "visualizar", "consultar"}), None)
        if leitura is None:
            print("\n>> A linha NÃO oferece Editar/Visualizar. Reabrir por esta tela não é caminho —")
            print("   as ações acima são tudo que existe. Nada mais a sondar sem escrever.")
            return 0

        print(f"\nabrindo '{leitura}' ({acoes[leitura]})...")
        tela = abrir_acao(ser, busca, acoes[leitura])
        destino = CAP / f"reabrir_{a.id_solicitacao}_{leitura.lower()}.html"
        destino.write_text(tela, encoding="utf-8")
        print(f"    {len(tela)} bytes -> capturas/{destino.name}")
        if (msg := mensagens(tela)):
            print(f"    mensagem do SER: {msg}")

        dump_formulario(tela)
        print("\nNADA FOI GRAVADO. As linhas marcadas com ! são as candidatas a escrita.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
