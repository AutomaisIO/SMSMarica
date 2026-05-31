"""Captura queries SUPERVISOR executadas entre o marcador e agora.

Uso:
    python marcar.py inicio
    [usuário executa ação no Salux desktop]
    python capturar.py busca-paciente-por-cpf

Saída: capturas/<rotulo>.json (todas queries) + docs/queries/<rotulo>.md (resumo).
Filtra parsing_schema_name=SUPERVISOR. Se a SID da sua sessão for conhecida
(via SALUX_SESSAO_SID/SERIAL no .env), também filtra por SID via V$OPEN_CURSOR.
"""
from __future__ import annotations

import json
import os
import pathlib
import sys
from datetime import datetime

from conexao import executar_select, texto_sql

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_MARCADOR = _RAIZ / "capturas" / "marcador.txt"
_PASTA_CAPTURA = _RAIZ / "capturas"
_PASTA_DOCS = _RAIZ / "docs" / "queries"


def _carregar_marcador() -> tuple[str, str]:
    if not _MARCADOR.exists():
        raise FileNotFoundError("rode `python marcar.py inicio` antes")
    linhas = _MARCADOR.read_text(encoding="utf-8").splitlines()
    return linhas[0], (linhas[1] if len(linhas) > 1 else "")


def _capturar(desde: str, sid: str | None, usuario_app: str) -> list[dict]:
    # SQL_FULLTEXT é CLOB e não pode entrar em DISTINCT — pegamos só metadados
    # aqui e usamos texto_sql(sql_id) pra recuperar texto via V$SQLTEXT depois.
    if sid:
        sids = ", ".join(str(int(x.strip())) for x in sid.split(","))
        sql = f"""
        SELECT
            sq.sql_id,
            MAX(sq.executions)    AS executions,
            MAX(sq.parse_calls)   AS parse_calls,
            MAX(sq.first_load_time)                                    AS first_load,
            TO_CHAR(MAX(sq.last_active_time), 'YYYY-MM-DD HH24:MI:SS') AS last_active,
            MAX(sq.module)        AS module,
            MAX(sq.action)        AS action
        FROM v$sql sq
        WHERE sq.parsing_schema_name = :u
          AND sq.last_active_time >= TO_TIMESTAMP_TZ(:desde, 'YYYY-MM-DD"T"HH24:MI:SS.FF6 TZH:TZM')
          AND sq.sql_id IN (
              SELECT sql_id FROM v$open_cursor WHERE sid IN ({sids})
              UNION
              SELECT prev_sql_id FROM v$session WHERE sid IN ({sids}) AND prev_sql_id IS NOT NULL
              UNION
              SELECT sql_id FROM v$session WHERE sid IN ({sids}) AND sql_id IS NOT NULL
          )
        GROUP BY sq.sql_id
        ORDER BY MAX(sq.last_active_time)
        """
        binds = {"desde": desde, "u": usuario_app}
    else:
        sql = """
        SELECT
            sql_id,
            MAX(executions)    AS executions,
            MAX(parse_calls)   AS parse_calls,
            MAX(first_load_time)                                    AS first_load,
            TO_CHAR(MAX(last_active_time), 'YYYY-MM-DD HH24:MI:SS') AS last_active,
            MAX(module)        AS module,
            MAX(action)        AS action
        FROM v$sql
        WHERE parsing_schema_name = :u
          AND last_active_time >= TO_TIMESTAMP_TZ(:desde, 'YYYY-MM-DD"T"HH24:MI:SS.FF6 TZH:TZM')
        GROUP BY sql_id
        ORDER BY MAX(last_active_time)
        """
        binds = {"desde": desde, "u": usuario_app}
    linhas, colunas = executar_select(sql, binds)
    resultados = [dict(zip(colunas, l)) for l in linhas]
    # Anexa texto completo via V$SQLTEXT
    for q in resultados:
        try:
            q["SQL_FULLTEXT"] = texto_sql(q["SQL_ID"])
        except Exception as e:
            q["SQL_FULLTEXT"] = f"-- erro recuperando texto: {e}"
    return resultados


def _binds_de(sql_ids: list[str]) -> dict[str, list[dict]]:
    if not sql_ids:
        return {}
    # IN list construída via inline (binds não suportam lista com sqlplus subprocess).
    lista = ", ".join(f"'{s}'" for s in sql_ids)
    sql = f"""
    SELECT sql_id, name, value_string, datatype_string,
           TO_CHAR(last_captured, 'YYYY-MM-DD HH24:MI:SS') AS quando
    FROM v$sql_bind_capture
    WHERE sql_id IN ({lista})
    ORDER BY sql_id, position
    """
    linhas, colunas = executar_select(sql)
    out: dict[str, list[dict]] = {}
    for linha in linhas:
        d = dict(zip(colunas, linha))
        out.setdefault(d["SQL_ID"], []).append(d)
    return out


def _salvar(rotulo: str, queries: list[dict], binds: dict[str, list[dict]]) -> None:
    _PASTA_CAPTURA.mkdir(exist_ok=True)
    _PASTA_DOCS.mkdir(parents=True, exist_ok=True)

    json_path = _PASTA_CAPTURA / f"{rotulo}.json"
    json_path.write_text(
        json.dumps({"queries": queries, "binds": binds}, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    md_path = _PASTA_DOCS / f"{rotulo}.md"
    with md_path.open("w", encoding="utf-8") as f:
        f.write(f"# Queries: {rotulo}\n\n")
        f.write(f"Capturado em {datetime.now().isoformat(timespec='seconds')}\n\n")
        f.write(f"Total: {len(queries)} queries únicas\n\n")
        for i, q in enumerate(queries, 1):
            f.write(f"## {i}. SQL_ID `{q.get('SQL_ID')}`\n\n")
            f.write(f"- last_active: {q.get('LAST_ACTIVE')}\n")
            f.write(f"- executions: {q.get('EXECUTIONS')}\n")
            f.write(f"- module: {q.get('MODULE')}\n")
            f.write(f"- action: {q.get('ACTION')}\n\n")
            f.write("```sql\n")
            f.write((q.get("SQL_FULLTEXT") or "").rstrip())
            f.write("\n```\n\n")
            if q.get("SQL_ID") in binds:
                f.write("**Binds capturados:**\n\n")
                for b in binds[q["SQL_ID"]]:
                    nome = b.get("NAME", "?")
                    valor = b.get("VALUE_STRING", "")
                    tipo = b.get("DATATYPE_STRING", "?")
                    f.write(f"- `:{nome}` = `{valor}` ({tipo})\n")
                f.write("\n")
    print(f"salvo: {json_path}")
    print(f"salvo: {md_path}")


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python capturar.py <rotulo-da-acao>")
        return 2
    rotulo = sys.argv[1]
    sid = os.environ.get("SALUX_SESSAO_SID")
    usuario_app = os.environ["SALUX_APP_USUARIO"]

    desde, rotulo_marcador = _carregar_marcador()
    print(f"capturando desde {desde} (marcador: {rotulo_marcador})")
    print(f"filtrando parsing_schema_name={usuario_app}", end="")
    if sid:
        print(f" + sid={sid}")
    else:
        print()

    queries = _capturar(desde, sid, usuario_app)
    print(f"{len(queries)} queries únicas")
    if not queries:
        print("nada novo — talvez a ação rode rápido demais (V$SQL agrega) ou outra sessão.")
        return 0
    binds = _binds_de([q["SQL_ID"] for q in queries])
    _salvar(rotulo, queries, binds)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
