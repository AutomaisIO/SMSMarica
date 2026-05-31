"""Monitor contínuo da sessão Salux alvo.

Polling em V$SQL a cada N segundos. Cada SQL_ID novo é gravado em
`capturas/historico.jsonl` (append-only, JSONL: 1 JSON por linha).

Rodar em background:
    Start-Process python -ArgumentList 'scripts\\monitor.py' -WindowStyle Hidden

Parar:
    Get-Process python | Where-Object {$_.CommandLine -like '*monitor.py*'} | Stop-Process
"""
from __future__ import annotations

import json
import os
import pathlib
import signal
import sys
import time
from datetime import datetime, timezone

from conexao import executar_select, texto_sql

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_HIST = _RAIZ / "capturas" / "historico.jsonl"
_INTERVALO = int(os.environ.get("SALUX_MONITOR_INTERVALO", "5"))


def _ja_vistos() -> set[str]:
    vistos: set[str] = set()
    if not _HIST.exists():
        return vistos
    for linha in _HIST.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            obj = json.loads(linha)
            if obj.get("tipo") == "query":
                vistos.add(obj["sql_id"])
        except Exception:
            continue
    return vistos


def _ciclo(sids_list: str, usuario: str, vistos: set[str]) -> int:
    sql = f"""
    SELECT
        sq.sql_id,
        MAX(sq.executions)                                        AS executions,
        MAX(sq.first_load_time)                                   AS first_load,
        TO_CHAR(MAX(sq.last_active_time), 'YYYY-MM-DD HH24:MI:SS') AS last_active,
        MAX(sq.module)                                            AS module,
        MAX(sq.action)                                            AS action
    FROM v$sql sq
    WHERE sq.parsing_schema_name = :u
      AND sq.sql_id IN (
          SELECT sql_id FROM v$open_cursor WHERE sid IN ({sids_list})
          UNION
          SELECT prev_sql_id FROM v$session WHERE sid IN ({sids_list}) AND prev_sql_id IS NOT NULL
          UNION
          SELECT sql_id FROM v$session WHERE sid IN ({sids_list}) AND sql_id IS NOT NULL
      )
    GROUP BY sq.sql_id
    ORDER BY MAX(sq.last_active_time)
    """
    linhas, colunas = executar_select(sql, {"u": usuario})
    novos = 0
    for linha in linhas:
        rec = dict(zip(colunas, linha))
        sql_id = rec.get("SQL_ID")
        if not sql_id or sql_id in vistos:
            continue
        vistos.add(sql_id)
        try:
            texto = texto_sql(sql_id)
        except Exception as e:
            texto = f"-- erro recuperando texto: {e}"
        # Binds (best-effort)
        try:
            blinhas, bcols = executar_select(
                "SELECT name, value_string, datatype_string "
                "FROM v$sql_bind_capture WHERE sql_id = :i ORDER BY position",
                {"i": sql_id},
            )
            binds = [dict(zip(bcols, b)) for b in blinhas]
        except Exception:
            binds = []
        entrada = {
            "tipo": "query",
            "ts_local": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "sql_id": sql_id,
            "executions": rec.get("EXECUTIONS"),
            "first_load": rec.get("FIRST_LOAD"),
            "last_active": rec.get("LAST_ACTIVE"),
            "module": rec.get("MODULE"),
            "action": rec.get("ACTION"),
            "sql_text": texto,
            "binds": binds,
        }
        with _HIST.open("a", encoding="utf-8") as f:
            f.write(json.dumps(entrada, ensure_ascii=False) + "\n")
        print(f"+ {sql_id} ({rec.get('LAST_ACTIVE')}) {texto[:80]}")
        novos += 1
    return novos


def main() -> int:
    sids_env = os.environ["SALUX_SESSAO_SID"]
    usuario = os.environ["SALUX_APP_USUARIO"]
    sids_list = ", ".join(str(int(x.strip())) for x in sids_env.split(","))

    _HIST.parent.mkdir(exist_ok=True)
    vistos = _ja_vistos()
    print(f"monitor iniciado | sids={sids_env} usuario={usuario} intervalo={_INTERVALO}s vistos={len(vistos)}")
    print(f"arquivo: {_HIST}")

    def _encerrar(signum, _frame):
        print(f"[sinal {signum}] encerrando monitor com {len(vistos)} sql_ids no acumulado.")
        sys.exit(0)
    signal.signal(signal.SIGINT, _encerrar)
    signal.signal(signal.SIGTERM, _encerrar)

    while True:
        try:
            n = _ciclo(sids_list, usuario, vistos)
            if n == 0:
                # Sinal de vida silencioso pra não poluir
                sys.stdout.write(".")
                sys.stdout.flush()
        except Exception as e:
            print(f"\n[erro no ciclo] {e}")
        time.sleep(_INTERVALO)


if __name__ == "__main__":
    raise SystemExit(main())
