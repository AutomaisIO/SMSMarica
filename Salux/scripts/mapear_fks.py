"""Mapeia FKs entre tabelas-chave (INFOSAUDE)."""
from __future__ import annotations

from conexao import executar_select


_TABELAS_CORE = [
    "PACIENTE", "FIA", "BAA", "ATENDIMENTO_PACIENTE", "FICHA_PACIENTE",
    "PRESCRICAO_FIA", "PRESCRICAO_BAA", "PRESCRICAO_FIA_CONDUTA", "PRESCRICAO_BAA_CONDUTA",
    "EVOLUCAO_PACIENTE_FIA", "EVOLUCAO_PACIENTE_BAA",
    "SINAIS_VITAIS", "PLANO_TERAPEUTICO_MEDICO",
    "MEDICO", "CLINICA", "ESPECIALIDADE", "PLANO_SAUDE", "TIPO_PLANO",
    "FUNCIONARIO", "UNIDADE", "FIA_CLINICA", "FIA_TIPO_PLANO",
    "EDOC_MODELO", "EDOC_MOVIMENTO", "EDOC_MOVIMENTO_ITEM", "EDOC_ITEM", "EDOC_DOCUMENTO",
    "REQUISICAO",
]
_IN_LIST = ", ".join(f"'{t}'" for t in _TABELAS_CORE)


def main() -> int:
    sql = f"""
    SELECT ac.table_name        AS tabela_origem,
           ac.constraint_name   AS fk_nome,
           acc.column_name      AS col_origem,
           ar.table_name        AS tabela_destino,
           arc.column_name      AS col_destino
      FROM dba_constraints ac
      JOIN dba_constraints ar ON ar.owner = ac.r_owner AND ar.constraint_name = ac.r_constraint_name
      JOIN dba_cons_columns acc ON acc.owner = ac.owner AND acc.constraint_name = ac.constraint_name
      JOIN dba_cons_columns arc ON arc.owner = ar.owner AND arc.constraint_name = ar.constraint_name
                                AND arc.position = acc.position
     WHERE ac.owner = 'INFOSAUDE' AND ac.constraint_type = 'R'
       AND (ac.table_name IN ({_IN_LIST}) OR ar.table_name IN ({_IN_LIST}))
     ORDER BY ac.table_name, ac.constraint_name, acc.position
    """
    linhas, _ = executar_select(sql, timeout=180)
    # Ordem fixa do SELECT (sqlplus pode truncar headers):
    # 0 tabela_origem, 1 fk_nome, 2 col_origem, 3 tabela_destino, 4 col_destino
    from collections import defaultdict
    grupos: dict[tuple[str, str, str], list[tuple[str, str]]] = defaultdict(list)
    for l in linhas:
        chave = (l[0], l[1], l[3])
        grupos[chave].append((l[2], l[4]))

    for (origem, fk, destino), pares in sorted(grupos.items()):
        cols_orig = ", ".join(p[0] for p in pares)
        cols_dest = ", ".join(p[1] for p in pares)
        print(f"{origem}({cols_orig}) -> {destino}({cols_dest})")
    print(f"\n{len(grupos)} FKs distintas envolvendo tabelas-core")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
