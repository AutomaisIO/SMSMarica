"""Cria usuário Oracle `salux_obs` com SELECT_CATALOG_ROLE.

Roda via SSH como root no servidor de banco, su - oracle, sqlplus / as sysdba.
Idempotente: se já existir, só relata.
"""
from __future__ import annotations

import pathlib
import secrets
import string
import sys

from dotenv import set_key
from ssh_servidor import conectar, executar

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_ENV = _RAIZ / ".env"

_NOME = "salux_obs"


def _gerar_senha() -> str:
    alfa = string.ascii_letters + string.digits
    # 24 chars seguros, sem símbolos pra evitar problemas de quoting no shell/sqlplus.
    return "Obs_" + "".join(secrets.choice(alfa) for _ in range(20))


_SQL_CRIA = """
WHENEVER SQLERROR EXIT 1
DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM dba_users WHERE username = '{NOME_UPPER}';
    IF v_existe = 1 THEN
        EXECUTE IMMEDIATE 'ALTER USER {NOME} IDENTIFIED BY "{SENHA}"';
        DBMS_OUTPUT.PUT_LINE('USER_EXISTIA_RESETOU_SENHA');
    ELSE
        EXECUTE IMMEDIATE 'CREATE USER {NOME} IDENTIFIED BY "{SENHA}"';
        DBMS_OUTPUT.PUT_LINE('USER_CRIADO');
    END IF;
    EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO {NOME}';
    EXECUTE IMMEDIATE 'GRANT SELECT_CATALOG_ROLE TO {NOME}';
    EXECUTE IMMEDIATE 'ALTER USER {NOME} PROFILE DEFAULT';
END;
/
SET SERVEROUTPUT ON
EXEC NULL;
SELECT username, account_status, profile, default_tablespace
FROM dba_users WHERE username = '{NOME_UPPER}';
SELECT granted_role FROM dba_role_privs WHERE grantee = '{NOME_UPPER}';
SELECT privilege FROM dba_sys_privs WHERE grantee = '{NOME_UPPER}';
EXIT
"""


def main() -> int:
    senha = _gerar_senha()
    sql = _SQL_CRIA.format(NOME=_NOME, NOME_UPPER=_NOME.upper(), SENHA=senha)

    # Comando como oracle: setup env, escreve SQL em stdin do sqlplus
    cmd = (
        "su - oracle -c '"
        "export ORACLE_SID=ORASX01 && "
        "export ORACLE_HOME=/u01/app/oracle/product/12.2.0/dbhome_1 && "
        "export PATH=$ORACLE_HOME/bin:$PATH && "
        "$ORACLE_HOME/bin/sqlplus -L -S / as sysdba"
        "'"
    )

    cli = conectar()
    try:
        print(f"=== Criando/ajustando usuário {_NOME} via SYS ===")
        rc, out, err = executar(cli, cmd, stdin_text=sql, timeout=60)
        print(out)
        if err.strip():
            print(f"[stderr] {err}")
        print(f"[exit {rc}]")
        if rc != 0:
            print("FALHOU — abortando", file=sys.stderr)
            return rc
    finally:
        cli.close()

    # Grava senha no .env
    set_key(str(_ENV), "SALUX_OBS_USER", _NOME, quote_mode="never")
    set_key(str(_ENV), "SALUX_OBS_PASSWORD", senha, quote_mode="never")
    print(f"\nCredenciais salvas em {_ENV}: SALUX_OBS_USER, SALUX_OBS_PASSWORD")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
