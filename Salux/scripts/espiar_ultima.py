"""Mostra a última e a atual SQL da sessão alvo. Bom pra confirmar canal."""
from __future__ import annotations

import os

from conexao import executar_select, texto_sql


def main() -> int:
    sid = int(os.environ["SALUX_SESSAO_SID"])
    serial = int(os.environ["SALUX_SESSAO_SERIAL"])

    linhas, colunas = executar_select(
        """
        SELECT sid, status, module, action, sql_id, prev_sql_id,
               TO_CHAR(sql_exec_start, 'HH24:MI:SS') AS exec_start
        FROM v$session
        WHERE sid = :s AND serial# = :se
        """,
        {"s": sid, "se": serial},
    )
    print("=== sessão alvo ===")
    print(" | ".join(colunas))
    for l in linhas:
        print(" | ".join(l))

    if not linhas:
        print("sessão não encontrada — pode ter desconectado")
        return 1

    sql_id_atual = linhas[0][colunas.index("SQL_ID")] or ""
    sql_id_prev = linhas[0][colunas.index("PREV_SQL_ID")] or ""

    for rotulo, sql_id in (("ATUAL", sql_id_atual), ("ANTERIOR", sql_id_prev)):
        if not sql_id:
            continue
        print(f"\n=== SQL {rotulo} ({sql_id}) ===")
        # Metadados (sem CLOB)
        linhas, colunas = executar_select(
            """
            SELECT executions, parse_calls,
                   TO_CHAR(last_active_time, 'HH24:MI:SS') AS last_active,
                   module, action
            FROM v$sql
            WHERE sql_id = :i
            """,
            {"i": sql_id},
        )
        for l in linhas:
            for c, v in zip(colunas, l):
                print(f"{c}: {v}")
        # Texto via V$SQLTEXT
        print("SQL_TEXT:")
        print(texto_sql(sql_id))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
