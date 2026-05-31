"""Inspeciona mecanismos de sincronização incremental disponíveis no Salux.

Cobre:
- Estrutura de H_PACIENTE / H_PLANO_SAUDE (shadow tables)
- Colunas DT_* em PACIENTE, FIA, BAA
- Estrutura de LOG_BAA, LOG_AIH, EDOC_MOVIMENTO_LOG, RASTREABILIDADE_ATENDIMENTO
- ROWDEPENDENCIES nas tabelas core
"""
from __future__ import annotations

import pathlib

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas" / "investig_sincronizacao.txt"


def dump(header: str, sql: str, binds=None, modo="obs", timeout=60):
    print(f"\n=== {header} ===")
    try:
        linhas, cols = executar_select(sql, binds, modo=modo, timeout=timeout)
    except Exception as e:
        print(f"FALHOU: {e}")
        with _SAIDA.open("a", encoding="utf-8") as f:
            f.write(f"\n# {header}\nFALHOU: {e}\n")
        return
    with _SAIDA.open("a", encoding="utf-8") as f:
        f.write(f"\n# {header}\n")
        f.write(" | ".join(cols) + "\n")
        f.write("-" * 80 + "\n")
        for l in linhas:
            f.write(" | ".join(l) + "\n")
        f.write(f"({len(linhas)} linhas)\n")
    print(f"OK {len(linhas)} linhas")


def main() -> int:
    _SAIDA.write_text("", encoding="utf-8")

    # 1) Estrutura H_PACIENTE
    dump("H_PACIENTE colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='H_PACIENTE' ORDER BY column_id")

    # 2) H_PLANO_SAUDE
    dump("H_PLANO_SAUDE colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='H_PLANO_SAUDE' ORDER BY column_id")

    # 3) Listar TODAS tabelas H_* (audit shadow tables) com tamanho
    dump("Tabelas H_* em INFOSAUDE",
         "SELECT table_name, num_rows FROM dba_tables "
         "WHERE owner='INFOSAUDE' AND table_name LIKE 'H\\_%' ESCAPE '\\' "
         "ORDER BY num_rows DESC NULLS LAST")

    # 4) Colunas DT_* em PACIENTE
    dump("DT_* / IN_ATIVO / colunas de update em PACIENTE",
         "SELECT column_name, data_type FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='PACIENTE' "
         "AND (column_name LIKE 'DT\\_%' ESCAPE '\\' "
         "  OR column_name LIKE '%ALT%' "
         "  OR column_name = 'IN_ATIVO') ORDER BY column_id")

    # 5) Colunas DT_* em FIA
    dump("DT_* / colunas de update em FIA",
         "SELECT column_name, data_type FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='FIA' "
         "AND (column_name LIKE 'DT\\_%' ESCAPE '\\' OR column_name LIKE '%ALT%') "
         "ORDER BY column_id")

    # 6) Colunas DT_* em BAA
    dump("DT_* / colunas de update em BAA",
         "SELECT column_name, data_type FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='BAA' "
         "AND (column_name LIKE 'DT\\_%' ESCAPE '\\' OR column_name LIKE '%ALT%') "
         "ORDER BY column_id")

    # 7) Estrutura LOG_BAA
    dump("LOG_BAA colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='LOG_BAA' ORDER BY column_id")

    # 8) Estrutura EDOC_MOVIMENTO_LOG
    dump("EDOC_MOVIMENTO_LOG colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='EDOC_MOVIMENTO_LOG' ORDER BY column_id")

    # 9) Estrutura RASTREABILIDADE_ATENDIMENTO
    dump("RASTREABILIDADE_ATENDIMENTO colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='RASTREABILIDADE_ATENDIMENTO' ORDER BY column_id")

    # 10) Tabelas LOG_* 10 maiores
    dump("Tabelas LOG_* top 15",
         "SELECT table_name, num_rows FROM dba_tables "
         "WHERE owner='INFOSAUDE' AND table_name LIKE 'LOG\\_%' ESCAPE '\\' "
         "AND num_rows > 0 ORDER BY num_rows DESC FETCH FIRST 15 ROWS ONLY")

    # 11) Tabelas RAST*
    dump("Tabelas RAST*",
         "SELECT table_name, num_rows FROM dba_tables "
         "WHERE owner='INFOSAUDE' AND table_name LIKE 'RAST%' AND num_rows > 0 ORDER BY num_rows DESC")

    # 12) ROWDEPENDENCIES nas tabelas core
    dump("ROWDEPENDENCIES (suporta ORA_ROWSCN) — core tables",
         "SELECT table_name, dependencies FROM dba_tables "
         "WHERE owner='INFOSAUDE' "
         "AND table_name IN ('PACIENTE','FIA','BAA','EDOC_MOVIMENTO','PRESCRICAO_FIA','PRESCRICAO_BAA','SINAIS_VITAIS') "
         "ORDER BY table_name")

    # 13) Triggers nas tabelas core (mostra quem popula H_*)
    dump("Triggers ativos em PACIENTE/FIA/BAA",
         "SELECT table_name, trigger_name, triggering_event, status "
         "FROM dba_triggers WHERE table_owner='INFOSAUDE' "
         "AND table_name IN ('PACIENTE','FIA','BAA') ORDER BY table_name, trigger_name")

    print(f"\nGravado em {_SAIDA}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
