"""Preenche fhir.patient.cns_todos a partir do `identifier` do documento.

## Por que fora da migration

Sao 344 mil fichas com expansao de jsonb -- minutos de execucao. Migration roda dentro do
AutoMigrate no deploy, e neste projeto o AutoMigrate FALHA CALADO: um timeout ali deixaria a coluna
vazia com o codigo ja contando com ela, e o sintoma so apareceria quando a busca por um CNS antigo
parasse de achar o paciente -- criando ficha duplicada em silencio.

## O que preenche

Todos os CNS do paciente, nao so o oficial. Um cidadao acumula numeros: o provisorio (faixa 898)
vira definitivo quando o cadastro se regulariza, e cadastros feitos em lugares diferentes geram
outros. Medido na implantacao de 07/09/2026: 3.815 conversoes provisorio->definitivo e 577 pessoas
com dois CNS definitivos, so entre os pacientes que o SISREG trouxe.

O identificador antigo continua sendo chave de busca valida -- todo o legado (solicitacao, exame,
laudo) aponta para ele.

## Seguranca

Somente UPDATE de uma coluna de projecao; o documento canonico (`content`) nao e tocado. Idempotente:
recalcula do proprio content, entao rodar de novo da o mesmo resultado. Em lotes com ponteiro, para
que uma queda no meio nao desfaca o que ja passou.
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import sys
import time

import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

# `--reconciliar` alcanca tambem quem esta DESATUALIZADO, nao so quem esta nulo. Precisou existir
# no primeiro uso: uma ficha escrita pelo codigo antigo durante a janela do deploy ficou com
# `cns_todos = []` tendo CNS no documento, e o filtro "IS NULL" nao a alcancava. Enquanto houver
# escrita por versao antiga -- deploy, rollback, outro conector -- a coluna pode divergir do
# documento, e divergencia silenciosa aqui significa paciente que some da busca.
SQL_LOTE = r"""
WITH alvo AS (
    SELECT id FROM fhir.patient
    WHERE (cns_todos IS NULL OR %s) AND id > %s
    ORDER BY id
    LIMIT %s
),
calc AS (
    SELECT a.id,
           array_agg(DISTINCT regexp_replace(e->>'value', '\D', '', 'g')) AS cns
    FROM alvo a
    JOIN fhir.patient p ON p.id = a.id,
         LATERAL jsonb_array_elements(p.content->'identifier') e
    WHERE e->>'system' LIKE '%%/cns'
      AND regexp_replace(e->>'value', '\D', '', 'g') <> ''
    GROUP BY a.id
)
UPDATE fhir.patient p
SET cns_todos = COALESCE(c.cns, ARRAY[]::text[])
FROM alvo a LEFT JOIN calc c ON c.id = a.id
WHERE p.id = a.id
RETURNING p.id
"""


def conexao(cs: str):
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--lote", type=int, default=5000)
    ap.add_argument("--reconciliar", action="store_true",
                    help="recalcula TODAS as fichas, nao so as nulas — para depois de deploy")
    ap.add_argument("--conexao", help="connection string; default = user-secrets do SMSMais.Api")
    args = ap.parse_args(argv)

    cs = args.conexao
    if not cs:
        p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
        cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]

    cx = conexao(cs)
    cx.autocommit = True          # lote a lote: queda no meio nao desfaz o que ja passou
    cur = cx.cursor()

    if args.reconciliar:
        # Divergente = o que a projecao diz difere do que o documento tem.
        cur.execute(r"""
            select count(*) from fhir.patient p
            where p.cns_todos is null
               or p.cns_todos is distinct from (
                    select coalesce(array_agg(distinct regexp_replace(e->>'value','\D','','g')),
                                    array[]::text[])
                    from jsonb_array_elements(p.content->'identifier') e
                    where e->>'system' like '%%/cns'
                      and regexp_replace(e->>'value','\D','','g') <> '')""")
    else:
        cur.execute("select count(*) from fhir.patient where cns_todos is null")
    faltam = cur.fetchone()[0]
    print(f"a preencher: {faltam:,}")
    if not faltam:
        return 0

    ultimo, feitos, t0 = "00000000-0000-0000-0000-000000000000", 0, time.monotonic()
    while True:
        cur.execute(SQL_LOTE, (args.reconciliar, ultimo, args.lote))
        ids = [r[0] for r in cur.fetchall()]
        if not ids:
            break
        ultimo = max(str(i) for i in ids)
        feitos += len(ids)
        seg = time.monotonic() - t0
        print(f"  {feitos:,}/{faltam:,}  ({feitos*100//faltam}%)  {feitos/max(seg,1):.0f}/s")

    print(f"\nconcluido em {(time.monotonic()-t0)/60:.1f} min")

    cur.execute("""select count(*) filter (where cns_todos is null) nulos,
                          count(*) filter (where array_length(cns_todos,1) > 1) com_varios,
                          count(*) total from fhir.patient""")
    nulos, varios, total = cur.fetchone()
    print(f"  ainda nulos          : {nulos:,}")
    print(f"  com MAIS DE UM CNS   : {varios:,}")
    print(f"  total                : {total:,}")

    # Conferencia: o oficial tem de estar entre os todos. Se nao estiver, a projecao esta errada.
    cur.execute("""select count(*) from fhir.patient
                   where cns is not null and cns <> '' and cns_todos is not null
                     and not (cns = any(cns_todos))""")
    print(f"  oficial FORA da lista: {cur.fetchone()[0]}  (tem de ser 0)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
