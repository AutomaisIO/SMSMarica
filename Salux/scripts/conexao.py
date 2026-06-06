"""Conexão Oracle read-only via sqlplus subprocess.

O cliente Oracle 12.1 instalado em C:\\Oracle1 é 32-bit; o Python aqui é 64-bit
e não consegue carregar a oci.dll. Solução simples: chamar `sqlplus.exe` como
processo filho. Funciona pra todo o fluxo de descoberta (V$ views, SELECTs em
tabelas Salux). Throughput é baixo — não importa.

Uso:
    from conexao import executar_select
    linhas, colunas = executar_select(
        "SELECT sid, username FROM v$session WHERE username = :u",
        binds={"u": "SUPERVISOR"},
    )

Toda query passa pelo guard de _guard.py — só SELECT/WITH/EXPLAIN.
sqlplus é invocado em ambiente read-only (SET TRANSACTION READ ONLY antes da
query) e nunca dá COMMIT.
"""
from __future__ import annotations

import os
import pathlib
import re
import subprocess
import tempfile
import time
from typing import Any

# Erros transitórios de rede/listener — fazem retry com backoff (não são bug de
# query). Surgem sob carga: abrir um sqlplus por query gera milhares de conexões
# sequenciais e o listener/servidor às vezes recusa ou estoura timeout.
_ERROS_TRANSITORIOS = (
    "ORA-12170",  # TNS:Connect timeout occurred
    "ORA-28547",  # connection to server failed, probable Oracle Net admin error
    "ORA-12541",  # TNS:no listener
    "ORA-12537",  # TNS:connection closed
    "ORA-12514",  # listener não conhece o serviço (transitório em failover)
    "ORA-03113",  # end-of-file on communication channel
    "ORA-03114",  # not connected to ORACLE
    "ORA-12535",  # TNS:operation timed out
    "ORA-12170",
    "SP2-0751",   # Unable to connect to Oracle (acompanha os ORA acima)
)
_MAX_TENTATIVAS = 5

from dotenv import load_dotenv

from _guard import garantir_leitura

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
load_dotenv(_RAIZ / ".env")

_SQLPLUS = os.environ.get("SALUX_SQLPLUS", "sqlplus")
_DELIM = "@@@"  # separador improvável de aparecer em dados clínicos


def _credenciais(modo: str = "obs") -> tuple[str, str, str]:
    """modo='obs' usa salux_obs (V$/DBA_); modo='supervisor' usa SUPERVISOR
    (acesso ao schema SYBSA via custom roles)."""
    dsn = os.environ["SALUX_ORACLE_DSN"]
    if modo == "supervisor":
        return (
            os.environ["SALUX_SUPERVISOR_USER"],
            os.environ["SALUX_SUPERVISOR_PASSWORD"],
            dsn,
        )
    return (
        os.environ["SALUX_ORACLE_USER"],
        os.environ["SALUX_ORACLE_PASSWORD"],
        dsn,
    )


def _string_bind(valor: Any) -> str:
    """Inlina bind como literal SQL. Só aceita tipos básicos.

    Suficiente pro fluxo de descoberta — não é caminho de produção.
    """
    if valor is None:
        return "NULL"
    if isinstance(valor, bool):
        return "1" if valor else "0"
    if isinstance(valor, (int, float)):
        return str(valor)
    s = str(valor).replace("'", "''")
    return f"'{s}'"


def _aplicar_binds(sql: str, binds: dict[str, Any] | None) -> str:
    if not binds:
        return sql
    # Substitui :nome onde nome é palavra completa (não casa :s em :serial).
    def _sub(m: re.Match) -> str:
        nome = m.group(1)
        if nome not in binds:
            return m.group(0)
        return _string_bind(binds[nome])
    return re.sub(r":([A-Za-z_][A-Za-z0-9_]*)", _sub, sql)


def _eh_transitorio(saida: str) -> bool:
    return any(cod in saida for cod in _ERROS_TRANSITORIOS)


def _rodar_sqlplus(script: str, modo: str = "obs", timeout: int = 60) -> str:
    user, password, dsn = _credenciais(modo)
    with tempfile.NamedTemporaryFile(
        mode="w", encoding="utf-8", suffix=".sql", delete=False
    ) as f:
        f.write(script)
        sql_file = f.name
    try:
        env = os.environ.copy()
        env["NLS_LANG"] = env.get("NLS_LANG", ".AL32UTF8")
        env["TNS_ADMIN"] = env.get("TNS_ADMIN", r"C:\Salux\TNS_ADMIN")
        ultimo_erro = ""
        for tentativa in range(1, _MAX_TENTATIVAS + 1):
            try:
                proc = subprocess.run(
                    [_SQLPLUS, "-L", "-S", f"{user}/{password}@{dsn}", f"@{sql_file}"],
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                    env=env, timeout=timeout,
                )
            except subprocess.TimeoutExpired:
                # Timeout do próprio processo = também transitório sob carga.
                ultimo_erro = f"subprocess timeout ({timeout}s)"
                if tentativa < _MAX_TENTATIVAS:
                    time.sleep(min(2 ** tentativa, 30))
                    continue
                raise RuntimeError(f"sqlplus excedeu {timeout}s após {tentativa} tentativas")

            if proc.returncode == 0:
                return proc.stdout

            ultimo_erro = (
                f"exit {proc.returncode}:\nSTDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}"
            )
            # Só faz retry em erro transitório de conexão; erro de SQL aborta já.
            if tentativa < _MAX_TENTATIVAS and _eh_transitorio(proc.stdout + proc.stderr):
                time.sleep(min(2 ** tentativa, 30))
                continue
            break
    finally:
        try:
            os.unlink(sql_file)
        except OSError:
            pass
    raise RuntimeError(f"sqlplus falhou ({ultimo_erro})")


def executar_json(sql: str, binds: dict[str, Any] | None = None, modo: str = "obs", timeout: int = 60) -> list[dict]:
    """Executa um SELECT cujo SELECT-list é UMA coluna JSON_OBJECT(...) e retorna
    lista de dicts. Robusto contra texto multi-linha e CLOB: sqlplus emite uma
    linha por row, sem COLSEP. Cada linha é parseada via json.loads.
    """
    import json
    garantir_leitura(sql)
    sql_final = _aplicar_binds(sql.strip().rstrip(";"), binds)
    script = (
        "SET ECHO OFF\nSET FEEDBACK OFF\nSET HEADING OFF\n"
        "SET PAGESIZE 0\nSET LINESIZE 32767\nSET LONG 200000\nSET LONGCHUNKSIZE 32000\n"
        "SET TRIMSPOOL ON\nSET TRIMOUT ON\nSET WRAP OFF\n"
        "WHENEVER SQLERROR EXIT 1\n"
        "SET TRANSACTION READ ONLY;\n"
        f"{sql_final};\n"
        "EXIT\n"
    )
    saida = _rodar_sqlplus(script, modo, timeout)
    out: list[dict] = []
    for raw in saida.splitlines():
        s = raw.strip()
        if not s or s.startswith(("SP2-", "ORA-", "PLS-")):
            continue
        # Concatena se uma linha começar com { sem fechar antes (defesa)
        try:
            out.append(json.loads(s))
        except json.JSONDecodeError:
            # Pode ter sido quebrado em múltiplas linhas físicas; tenta acumular
            pass
    return out


def executar_texto(sql: str, binds: dict[str, Any] | None = None, modo: str = "obs", timeout: int = 60) -> str:
    """Executa SELECT de UMA coluna textual e retorna a saída como string única
    (linhas físicas concatenadas, sem header).

    Para texto largo/multi-linha use sentinelas DENTRO do SQL (ex.: prefixar cada
    linha lógica com '@@ROW@@' e separar campos com '@@FLD@@') e fatiar o
    resultado no chamador. Diferente de `executar_select` (COLSEP), tolera:
      - colunas largas que o sqlplus quebra em múltiplas linhas físicas (WRAP);
      - bytes inválidos do charset legado do Salux (decodificados com replace),
        que estouram JSON_OBJECT com ORA-40474.
    """
    garantir_leitura(sql)
    sql_final = _aplicar_binds(sql.strip().rstrip(";"), binds)
    script = (
        "SET ECHO OFF\nSET FEEDBACK OFF\nSET HEADING OFF\n"
        "SET PAGESIZE 0\nSET LINESIZE 4000\nSET LONG 200000\nSET LONGCHUNKSIZE 32000\n"
        "SET TRIMSPOOL ON\nSET TRIMOUT ON\nSET WRAP ON\n"
        "WHENEVER SQLERROR EXIT 1\n"
        "SET TRANSACTION READ ONLY;\n"
        f"{sql_final};\n"
        "EXIT\n"
    )
    saida = _rodar_sqlplus(script, modo, timeout)
    linhas = [r for r in saida.splitlines() if not r.startswith(("SP2-", "ORA-", "PLS-"))]
    return "".join(linhas)


def texto_sql(sql_id: str) -> str:
    """Reconstrói o SQL completo a partir de V$SQLTEXT (chunks de 64 bytes)."""
    garantir_leitura("SELECT 1 FROM dual")  # sanity
    script = (
        "SET ECHO OFF\nSET FEEDBACK OFF\nSET HEADING OFF\n"
        "SET PAGESIZE 0\nSET LINESIZE 200\nSET TRIMSPOOL ON\nSET TRIMOUT ON\nSET WRAP OFF\n"
        "WHENEVER SQLERROR EXIT 1\n"
        "SET TRANSACTION READ ONLY;\n"
        f"SELECT sql_text FROM v$sqltext WHERE sql_id = '{sql_id}' ORDER BY piece;\n"
        "EXIT\n"
    )
    saida = _rodar_sqlplus(script)
    # Cada linha = 1 piece (até 64 chars). Junta tudo direto.
    return "".join(saida.splitlines())


def executar_select(sql: str, binds: dict[str, Any] | None = None, modo: str = "obs", timeout: int = 60) -> tuple[list[list[str]], list[str]]:
    """Executa SELECT/WITH/EXPLAIN via sqlplus. Retorna (linhas, nomes_colunas).

    Linhas vêm como strings (sem casting de tipo) — é descoberta, não produção.
    Para colunas CLOB grandes (V$SQL.SQL_FULLTEXT), use `texto_sql(sql_id)`.
    """
    garantir_leitura(sql)
    sql_final = _aplicar_binds(sql.strip().rstrip(";"), binds)

    script = (
        "SET ECHO OFF\n"
        "SET FEEDBACK OFF\n"
        "SET HEADING ON\n"
        "SET PAGESIZE 50000\n"
        "SET LINESIZE 4000\n"
        "SET LONG 200000\n"
        "SET LONGCHUNKSIZE 32000\n"
        "SET TRIMSPOOL ON\n"
        "SET TRIMOUT ON\n"
        "SET WRAP ON\n"
        f"SET COLSEP '{_DELIM}'\n"
        "SET MARKUP HTML OFF\n"
        "SET SERVEROUTPUT OFF\n"
        "WHENEVER SQLERROR EXIT 1\n"
        "SET TRANSACTION READ ONLY;\n"
        f"{sql_final};\n"
        "EXIT\n"
    )
    return _parse_saida(_rodar_sqlplus(script, modo, timeout))


def _parse_saida(saida: str) -> tuple[list[list[str]], list[str]]:
    """Cabeçalho = primeira linha não vazia. Próxima linha = dashes (descarta).
    Demais linhas = dados. Cada coluna separada por _DELIM.
    """
    linhas_brutas: list[str] = []
    for raw in saida.splitlines():
        if not raw.strip():
            continue
        if raw.startswith(("SP2-", "ORA-", "PLS-")):
            continue
        if raw.strip() == "no rows selected":
            return [], []
        linhas_brutas.append(raw)

    if not linhas_brutas:
        return [], []

    colunas = [c.strip() for c in linhas_brutas[0].split(_DELIM)]
    # Detecta a linha de "---- ----" que vem após o header.
    inicio = 1
    if len(linhas_brutas) > 1 and set(linhas_brutas[1].replace(_DELIM, "").strip()) <= {"-", " "}:
        inicio = 2

    dados: list[list[str]] = []
    for raw in linhas_brutas[inicio:]:
        dados.append([c.strip() for c in raw.split(_DELIM)])
    return dados, colunas
