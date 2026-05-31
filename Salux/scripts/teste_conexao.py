"""Smoke test: roda algumas SELECTs em metadados e imprime resultado.

Sem dados clínicos. Só identidade da sessão e privilégios.
"""
from __future__ import annotations

from conexao import executar_select


def imprime(titulo: str, sql: str, binds=None) -> None:
    print(f"\n=== {titulo} ===")
    try:
        linhas, colunas = executar_select(sql, binds)
        if colunas:
            print(" | ".join(colunas))
            print("-" * 60)
        if not linhas:
            print("(sem linhas)")
            return
        for linha in linhas:
            print(" | ".join(linha))
    except Exception as e:
        print(f"FALHOU: {e}")


def main() -> int:
    imprime(
        "identidade",
        """
        SELECT
            SYS_CONTEXT('USERENV','DB_NAME')        AS db_name,
            SYS_CONTEXT('USERENV','INSTANCE_NAME')  AS instance,
            SYS_CONTEXT('USERENV','SESSION_USER')   AS usuario,
            SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AS schema_atual,
            SYS_CONTEXT('USERENV','SID')            AS sid
        FROM dual
        """,
    )
    imprime("versao", "SELECT banner FROM v$version WHERE ROWNUM <= 5")
    imprime(
        "privilegio v$session",
        "SELECT COUNT(*) AS qtd FROM v$session",
    )
    imprime(
        "sessoes SUPERVISOR ativas",
        """
        SELECT sid, serial#, program, machine, status, logon_time
        FROM v$session
        WHERE username = 'SUPERVISOR'
        ORDER BY logon_time DESC
        """,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
