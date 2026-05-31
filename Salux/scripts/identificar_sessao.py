"""Lista as sessões da máquina-alvo (SALUX_APP_MACHINE_LIKE) conectadas como
SALUX_APP_USUARIO. É o que o Salux desktop produz.
"""
from __future__ import annotations

import os

from conexao import executar_select


def main() -> int:
    usuario = os.environ["SALUX_APP_USUARIO"]
    machine_like = os.environ["SALUX_APP_MACHINE_LIKE"]
    sql = f"""
    SELECT
        sid,
        serial#       AS serial,
        program,
        module,
        action,
        machine,
        osuser,
        status,
        TO_CHAR(logon_time, 'YYYY-MM-DD HH24:MI:SS') AS logon,
        sql_id,
        prev_sql_id
    FROM v$session
    WHERE username = :u
      AND machine LIKE :m
    ORDER BY logon_time DESC
    """
    linhas, colunas = executar_select(sql, {"u": usuario, "m": machine_like})
    if not linhas:
        print(f"Nenhuma sessão {usuario} de máquina LIKE '{machine_like}'.")
        print("Possíveis causas:")
        print(" - Salux desktop ainda não logou nessa máquina")
        print(" - O nome da máquina é outro — ajuste SALUX_APP_MACHINE_LIKE em .env")
        return 0
    print(" | ".join(colunas))
    print("-" * 120)
    for linha in linhas:
        print(" | ".join(linha))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
