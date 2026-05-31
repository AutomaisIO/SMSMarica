"""Lista todas as colunas da tabela PACIENTE (SYBSA) para mapear ao FHIR."""
from conexao import executar_select

linhas, _ = executar_select(
    """
    SELECT column_name || ' (' || data_type || ')'
    FROM all_tab_columns
    WHERE table_name = 'PACIENTE'
    ORDER BY column_id
    """,
    modo="supervisor",
)
for l in linhas:
    print(l[0])
print(f"\nTOTAL: {len(linhas)} colunas")
