"""FKs por tabela individual (evita o timeout do mapper global)."""
from __future__ import annotations

import pathlib

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas"
_ARQ = _SAIDA / "investig_fks.txt"


_TABELAS = [
    "PACIENTE", "FIA", "BAA", "FIA_LEITO", "FIA_MEDICO", "FIA_TIPO_PLANO",
    "FIA_CLINICA", "ATENDIMENTO_PACIENTE", "FICHA_PACIENTE",
    "PRESCRICAO_FIA", "PRESCRICAO_BAA",
    "EVOLUCAO_PACIENTE_FIA", "EVOLUCAO_PACIENTE_BAA",
    "SINAIS_VITAIS", "PLANO_TERAPEUTICO_MEDICO",
    "EDOC_MODELO", "EDOC_MOVIMENTO", "EDOC_MOVIMENTO_ITEM", "EDOC_ITEM", "EDOC_DOCUMENTO",
    "TRIAGEM", "ACOLHIMENTO", "CHAMADA_PACIENTE",
    "LEITO", "QUARTO", "UNIDADE_HOSPITALAR", "SALA_AMBULATORIO", "CENTRO_CUSTO",
    "SETOR_FLUXO", "MEDICO", "FUNCIONARIO", "CLINICA", "ESPECIALIDADE",
    "PLANO_SAUDE", "TIPO_PLANO", "PROCEDIMENTO",
    "SOL_EXAME", "SOL_EXAME_ITEM", "LAUDO",
    "AIH", "REQUISICAO", "MATMED", "ALERGIA",
]


def main() -> int:
    _ARQ.write_text("", encoding="utf-8")
    print(f"Mapeando FKs de {len(_TABELAS)} tabelas...")
    for i, t in enumerate(_TABELAS, 1):
        try:
            # Concatena tudo em UMA coluna pra evitar problemas de parsing do COLSEP.
            linhas, _ = executar_select(
                """
                SELECT acc.column_name || ' -> ' || ar.table_name || '.' || arc.column_name AS fk_desc
                  FROM dba_constraints ac
                  JOIN dba_constraints ar ON ar.owner = ac.r_owner
                                          AND ar.constraint_name = ac.r_constraint_name
                  JOIN dba_cons_columns acc ON acc.owner = ac.owner
                                            AND acc.constraint_name = ac.constraint_name
                  JOIN dba_cons_columns arc ON arc.owner = ar.owner
                                            AND arc.constraint_name = ar.constraint_name
                                            AND arc.position = acc.position
                 WHERE ac.owner = 'INFOSAUDE'
                   AND ac.constraint_type = 'R'
                   AND ac.table_name = :t
                 ORDER BY ac.constraint_name, acc.position
                """, {"t": t}, timeout=60,
            )
            with _ARQ.open("a", encoding="utf-8") as f:
                f.write(f"\n# {t} (FKs saindo)\n")
                if not linhas:
                    f.write("(nenhuma)\n")
                for l in linhas:
                    val = l[0] if l else "?"
                    f.write(f"  {val}\n")
            print(f"  {i:3d}/{len(_TABELAS)}  {t:<28}  {len(linhas)} FKs")
        except Exception as e:
            err = str(e).split(chr(10), 1)[0][:80]
            print(f"  {i:3d}/{len(_TABELAS)}  {t:<28}  FALHOU: {err}")
            with _ARQ.open("a", encoding="utf-8") as f:
                f.write(f"\n# {t}: FALHOU ({err})\n")
    print(f"\nGravado em {_ARQ}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
