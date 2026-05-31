"""Pega SQLs SELECT reais de TODAS as sessões SYBSA com last_active_time recente.

Filtra:
- parsing_schema_name = 'SYBSA'
- command_type = 3 (SELECT) ou 6 (UPDATE)... configurável
- last_active_time > SYSDATE - N hours
- texto não começa com SELECT COUNT (descarta os counts de existência)

Saída: capturas/snapshot_sql_<horas>h.jsonl
"""
from __future__ import annotations

import json
import os
import pathlib
import sys
from datetime import datetime, timezone

from conexao import executar_select, texto_sql

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_SAIDA_DIR = _RAIZ / "capturas"


def main() -> int:
    horas = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    apenas_select = "--all" not in sys.argv

    # COMMAND_TYPE 3=SELECT, 2=INSERT, 6=UPDATE, 7=DELETE, 47=PLSQL_EXECUTE
    if apenas_select:
        filtro_cmd = "AND command_type = 3"
    else:
        filtro_cmd = ""

    limite = int(os.environ.get("SALUX_SNAPSHOT_LIMIT", "200"))
    sql = f"""
    SELECT * FROM (
        SELECT sql_id,
               MAX(executions)         AS execs,
               MAX(parse_calls)        AS parses,
               TO_CHAR(MAX(last_active_time),'YYYY-MM-DD HH24:MI:SS') AS last_active,
               MAX(first_load_time)    AS first_load,
               MAX(module)             AS module,
               MAX(command_type)       AS cmd_type
        FROM v$sql
        WHERE parsing_schema_name = 'SYBSA'
          AND last_active_time > SYSDATE - :horas/24
          {filtro_cmd}
        GROUP BY sql_id
        ORDER BY MAX(last_active_time) DESC
    ) WHERE ROWNUM <= {limite}
    """

    print(f"buscando até {limite} SQL_IDs SYBSA ativos nas últimas {horas}h (apenas_select={apenas_select})...")
    linhas, colunas = executar_select(sql, {"horas": horas}, timeout=120)
    print(f"{len(linhas)} sql_ids candidatos")

    arquivo = _SAIDA_DIR / f"snapshot_sql_{horas}h.jsonl"
    _SAIDA_DIR.mkdir(exist_ok=True)

    n_count = 0
    n_select = 0
    n_outros = 0
    with arquivo.open("w", encoding="utf-8") as f:
        for i, linha in enumerate(linhas, 1):
            rec = dict(zip(colunas, linha))
            sql_id = rec["SQL_ID"]
            try:
                texto = texto_sql(sql_id)
            except Exception as e:
                texto = f"-- erro recuperando: {e}"
            stripped = texto.lstrip().lower()
            if stripped.startswith("select count"):
                n_count += 1
                continue  # pular counts de existência
            if stripped.startswith("select"):
                n_select += 1
            else:
                n_outros += 1
            entrada = {
                "ts_local": datetime.now(timezone.utc).isoformat(timespec="seconds"),
                "sql_id": sql_id,
                "executions": rec.get("EXECS"),
                "parse_calls": rec.get("PARSES"),
                "last_active": rec.get("LAST_ACTIVE"),
                "first_load": rec.get("FIRST_LOAD"),
                "module": rec.get("MODULE"),
                "cmd_type": rec.get("CMD_TYPE"),
                "sql_text": texto,
            }
            f.write(json.dumps(entrada, ensure_ascii=False) + "\n")
            if i % 50 == 0:
                print(f"  ... {i}/{len(linhas)}")

    print(f"\nresumo: select={n_select} count_filtrados={n_count} outros={n_outros}")
    print(f"gravado em {arquivo}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
