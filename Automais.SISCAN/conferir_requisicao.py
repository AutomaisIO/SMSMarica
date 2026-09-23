"""Confere, RELENDO o SISCAN, uma requisição criada por nós. Somente leitura.

"Registro salvo com sucesso" não é prova — a prova é a releitura. Esta sonda
procura a requisição pelo Nº do Prontuário (onde gravamos o nosso AccessionNumber)
e imprime o que a grade mostra, inclusive o Nº do Exame, que não aparece no modal
de protocolo: ele vem embutido no id das ações da linha.

    python conferir_requisicao.py --prontuario 260827047 --de 01/06/2026 --ate 22/09/2026
"""

from __future__ import annotations

import argparse
import sys

from siscan import exame
from siscan.client import SiscanClient, mensagens


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--prontuario", required=True)
    ap.add_argument("--de", required=True)
    ap.add_argument("--ate", required=True)
    ap.add_argument("--status", default=exame.REQUISITADO)
    args = ap.parse_args()

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        filtro = exame.Filtro(mamografia=True, status=args.status,
                              numero_prontuario=args.prontuario,
                              data_inicio=args.de, data_fim=args.ate)
        doc = exame.pesquisar(c, filtro)
        msgs = list(dict.fromkeys(m for m in mensagens(doc) if len(m) < 200))
        if msgs:
            print("mensagens:", msgs[:3])
        cab, linhas = exame.grade(doc)
        print("COLUNAS:", cab)
        print("linhas encontradas:", len(linhas))
        for l in linhas:
            print("\n  ", " | ".join(x for x in l.colunas if x))
            for titulo, ident in l.acoes.items():
                print(f"      {titulo}  ->  {ident}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
