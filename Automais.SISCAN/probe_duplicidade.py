"""Como perguntar ao SISCAN "essa paciente já tem requisição?" — medido, não suposto.

Duas perguntas diferentes, que viram duas críticas no nosso lado:

  1. Já existe requisição com o NOSSO Nº do Prontuário (o AccessionNumber)?
     -> é nossa: não criar outra, e trazer os números para a tela.
  2. A paciente tem QUALQUER requisição no período (hoje+10d recuando 1 ano)?
     -> não é nossa: não criar, e mandar resolver no SISCAN.

O que esta sonda mede: se a pesquisa por Cartão SUS funciona, se o Status é mesmo
obrigatório (a tela marca com *) e o que a grade devolve em cada caso.

    python probe_duplicidade.py --cns <CNS> [--prontuario <ACCESSION>]

SOMENTE LEITURA.
"""

from __future__ import annotations

import argparse
import sys
from datetime import date, timedelta

from siscan import exame
from siscan.client import SiscanClient, mensagens


def janela(hoje: date | None = None) -> tuple[str, str]:
    """A janela da crítica: 10 dias à frente, recuando um ano.

    Os 10 dias à frente existem porque a requisição pode ter sido lançada com data
    de solicitação futura; o ano para trás é o intervalo em que uma segunda
    mamografia da mesma paciente é suspeita."""
    fim = (hoje or date.today()) + timedelta(days=10)
    inicio = fim - timedelta(days=365)
    return inicio.strftime("%d/%m/%Y"), fim.strftime("%d/%m/%Y")


def pesquisar(c, doc, **kwargs):
    filtro = exame.Filtro(mamografia=True, **kwargs)
    resultado = exame.pesquisar(c, filtro, doc)
    msgs = list(dict.fromkeys(m for m in mensagens(resultado) if len(m) < 200))
    cab, linhas = exame.grade(resultado)
    return msgs, linhas


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cns", required=True)
    ap.add_argument("--prontuario")
    args = ap.parse_args()

    de, ate = janela()
    print(f"janela da crítica: {de} .. {ate}\n")

    with SiscanClient() as c:
        c.login()

        # 1) O Status é mesmo obrigatório? A tela marca com *, mas o que importa é o
        #    comportamento: filtro ignorado em silêncio é pior que erro.
        doc = exame.abrir(c)
        msgs, linhas = pesquisar(c, doc, cartao_sus=args.cns, data_inicio=de, data_fim=ate)
        print(f"SEM status  -> {len(linhas)} linha(s) · mensagens: {msgs[:2]}")

        for status, rotulo in exame.STATUS_ROTULO.items():
            doc = exame.abrir(c)
            msgs, linhas = pesquisar(
                c, doc, cartao_sus=args.cns, status=status, data_inicio=de, data_fim=ate)
            print(f"status {status} ({rotulo:<13}) -> {len(linhas)} linha(s)"
                  + (f" · mensagens: {msgs[:1]}" if msgs else ""))
            for l in linhas:
                cols = [x for x in l.colunas if x]
                if cols:
                    cols[0] = cols[0][:3] + "…"          # nome
                    if len(cols) > 1:
                        cols[1] = cols[1][:4] + "…" + cols[1][-3:]   # CNS
                print("      ", " | ".join(cols))

        if args.prontuario:
            doc = exame.abrir(c)
            msgs, linhas = pesquisar(
                c, doc, numero_prontuario=args.prontuario, status=exame.REQUISITADO,
                data_inicio=de, data_fim=ate)
            print(f"\npor prontuário {args.prontuario} -> {len(linhas)} linha(s)")
            for l in linhas:
                print("      protocolo:", l.colunas[4] if len(l.colunas) > 4 else "?",
                      "· ações:", list(l.acoes.values())[:1])
    return 0


if __name__ == "__main__":
    sys.exit(main())
