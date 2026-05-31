"""Checa o que SUPERVISOR consegue ler das V$ views críticas pra captura."""
from __future__ import annotations

from conexao import executar_select


def testa(nome: str, sql: str) -> None:
    try:
        linhas, _ = executar_select(sql)
        print(f"OK   {nome}  ({len(linhas)} linha(s))")
    except Exception as e:
        msg = str(e).split("\n", 1)[0][:120]
        print(f"FAIL {nome}  -> {msg}")


def main() -> int:
    print("=== Acesso a V$ views ===")
    for nome, sql in {
        "v$session": "SELECT 1 FROM v$session WHERE ROWNUM <= 1",
        "v$sql": "SELECT 1 FROM v$sql WHERE ROWNUM <= 1",
        "v$sqlarea": "SELECT 1 FROM v$sqlarea WHERE ROWNUM <= 1",
        "v$sqltext": "SELECT 1 FROM v$sqltext WHERE ROWNUM <= 1",
        "v$sql_bind_capture": "SELECT 1 FROM v$sql_bind_capture WHERE ROWNUM <= 1",
        "v$open_cursor": "SELECT 1 FROM v$open_cursor WHERE ROWNUM <= 1",
        "v$active_session_history": "SELECT 1 FROM v$active_session_history WHERE ROWNUM <= 1",
        "dba_hist_sqltext": "SELECT 1 FROM dba_hist_sqltext WHERE ROWNUM <= 1",
        "gv$sql": "SELECT 1 FROM gv$sql WHERE ROWNUM <= 1",
        "sys.v_$sql": "SELECT 1 FROM sys.v_$sql WHERE ROWNUM <= 1",
    }.items():
        testa(nome, sql)

    print("\n=== Roles do usuário SUPERVISOR ===")
    linhas, colunas = executar_select(
        "SELECT * FROM session_roles ORDER BY role"
    )
    if colunas:
        print(" | ".join(colunas))
    for l in linhas:
        print(" | ".join(l))

    print("\n=== System privs ===")
    linhas, colunas = executar_select(
        "SELECT * FROM session_privs WHERE privilege LIKE 'SELECT%' OR privilege LIKE '%CATALOG%' ORDER BY privilege"
    )
    if colunas:
        print(" | ".join(colunas))
    for l in linhas:
        print(" | ".join(l))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
