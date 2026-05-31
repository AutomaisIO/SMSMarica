"""Lista TODAS as sessões não-internas, agrupadas, pra ajudar a identificar
qual conexão é do Salux desktop.
"""
from __future__ import annotations

from conexao import executar_select


def main() -> int:
    print("\n=== Sessões por usuário (top 30) ===")
    linhas, colunas = executar_select("""
    SELECT username, COUNT(*) AS qtd
    FROM v$session
    WHERE username IS NOT NULL
    GROUP BY username
    ORDER BY qtd DESC
    FETCH FIRST 30 ROWS ONLY
    """)
    if colunas:
        print(" | ".join(colunas))
        print("-" * 50)
    for l in linhas:
        print(" | ".join(l))

    print("\n=== Sessões recentes (últimas 30, qualquer usuário não-Oracle) ===")
    linhas, colunas = executar_select("""
    SELECT
        sid, serial#, username, program, machine, osuser, status,
        TO_CHAR(logon_time, 'YYYY-MM-DD HH24:MI:SS') AS logon
    FROM v$session
    WHERE username IS NOT NULL
      AND username NOT IN ('SYS','SYSTEM','DBSNMP','SYSMAN','XDB','CTXSYS','OUTLN')
    ORDER BY logon_time DESC
    FETCH FIRST 30 ROWS ONLY
    """)
    if colunas:
        print(" | ".join(colunas))
        print("-" * 120)
    for l in linhas:
        print(" | ".join(l))

    print("\n=== Programas distintos hoje ===")
    linhas, colunas = executar_select("""
    SELECT DISTINCT program, COUNT(*) AS qtd
    FROM v$session
    WHERE username IS NOT NULL
      AND logon_time > TRUNC(SYSDATE)
    GROUP BY program
    ORDER BY qtd DESC
    """)
    if colunas:
        print(" | ".join(colunas))
        print("-" * 80)
    for l in linhas:
        print(" | ".join(l))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
