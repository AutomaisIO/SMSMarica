"""Espelho das requisições de mamografia já existentes no SISCAN.

Varre EXAME -> GERENCIAR EXAME por período e devolve TODAS as linhas — com o
**Cartão SUS**, que é a chave para cruzar com as nossas anamneses e descobrir
quais já foram lançadas na mão pela unidade.

Duas medidas que fazem a varredura ser barata (22-23/09/2026):

  * `frm:tamanhoPagina` aceita até **300** por página — o padrão é 10;
  * o rodapé diz "Mostrando 1 a N de **T** registro(s)". Quando T passa do
    tamanho da página, a janela é PARTIDA AO MEIO e refeita, em vez de paginar.
    Paginar é um datascroller RichFaces (`rich:datascroller:onscroll`) e exige
    engenharia reversa; partir a janela é aritmética e não mente.

  * a página de resultado É a tela de pesquisa — pesquisa-se de novo nela, sem
    reabrir o menu (o clique de menu custa dezenas de segundos).

SOMENTE LEITURA. A saída tem nome e CNS de paciente: escreva SEMPRE fora do
repositório (o padrão aponta para o scratchpad).

    python espelho_requisicoes.py --de 01/06/2026 --ate 30/09/2026 --saida <arquivo.json>
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import date, datetime, timedelta

from siscan import exame
from siscan.client import SiscanClient

TAMANHO_PAGINA = "300"
RE_TOTAL = re.compile(r"de\s+([\d.]+)\s+registro", re.IGNORECASE)
# O Nº do Exame NÃO é coluna: mora no id das ações da linha
# (`frm:listaExamePaginada:139257485:j_id173`). É ele que abre o
# "Incluir Resultado do Exame" — perder isso é perder metade do par.
RE_NUMERO = re.compile(r"listaExamePaginada:(\d+):")


def _data(s: str) -> date:
    return datetime.strptime(s, "%d/%m/%Y").date()


def _br(d: date) -> str:
    return d.strftime("%d/%m/%Y")


def total_da_grade(doc) -> int:
    """Quantos registros a pesquisa diz ter — não quantos vieram nesta página."""
    t = doc.find("table", id="frm:listaExamePaginada")
    rodape = t.find("tfoot") if t else None
    m = RE_TOTAL.search(rodape.get_text(" ", strip=True)) if rodape else None
    return int(m.group(1).replace(".", "")) if m else 0


def varrer(c, doc, status: str, de: date, ate: date, achadas: list, profundidade=0):
    """Pesquisa a janela; se estourar a página, parte ao meio e refaz."""
    filtro = exame.Filtro(mamografia=True, status=status,
                          data_inicio=_br(de), data_fim=_br(ate))
    doc = pesquisar_com_pagina(c, filtro, doc)
    cab, linhas = exame.grade(doc)
    total = total_da_grade(doc)
    indent = "  " * profundidade
    print(f"{indent}status {status} · {_br(de)}..{_br(ate)} -> {total} registro(s), "
          f"{len(linhas)} na página", flush=True)

    if total > len(linhas):
        if de == ate:
            print(f"{indent}  !! {total} num único dia e a página só traz {len(linhas)}; "
                  f"não dá para partir mais — paginação seria necessária.", file=sys.stderr)
            return doc
        meio = de + (ate - de) // 2
        doc = varrer(c, doc, status, de, meio, achadas, profundidade + 1)
        doc = varrer(c, doc, status, meio + timedelta(days=1), ate, achadas, profundidade + 1)
        return doc

    for l in linhas:
        numero = next((m.group(1) for m in
                       (RE_NUMERO.search(v) for v in l.acoes.values()) if m), "")
        achadas.append(dict(zip(cab, l.colunas)) | {"_numero_exame": numero,
                                                    "_status_filtro": status,
                                                    "_acoes": sorted(l.acoes)})
    return doc


def pesquisar_com_pagina(c, filtro: exame.Filtro, doc):
    """`exame.pesquisar` + tamanho de página de 300 (o padrão de 10 pagina tudo)."""
    if filtro.data_inicio:
        exame.commit_data_inicial(c, doc, filtro.data_inicio)
    extras = filtro.para_campos()
    extras.pop("frm:j_id47", None)
    nome = exame.campo_por_rotulo(doc, "Mamografia")
    if not nome:
        raise RuntimeError("checkbox 'Mamografia' ausente — tela não inicializada?")
    extras[nome] = exame.MAMOGRAFIA
    extras["frm:tamanhoPagina"] = TAMANHO_PAGINA
    extras["frm:botaoPesquisarExame"] = "frm:botaoPesquisarExame"
    return c.sopa(c.post_form(doc, "frm", extras, "espelho",
                              referer=c._url(exame.CAMINHO)))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--de", required=True)
    ap.add_argument("--ate", required=True)
    ap.add_argument("--saida", required=True)
    ap.add_argument("--status", default="01,02,03")
    args = ap.parse_args()

    de, ate = _data(args.de), _data(args.ate)
    achadas: list[dict] = []

    with SiscanClient() as c:
        c.login()
        doc = exame.abrir(c)
        for status in args.status.split(","):
            doc = varrer(c, doc, status.strip(), de, ate, achadas)

    with open(args.saida, "w", encoding="utf-8") as fh:
        json.dump(achadas, fh, ensure_ascii=False, indent=1)

    print(f"\n{len(achadas)} linha(s) -> {args.saida}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
