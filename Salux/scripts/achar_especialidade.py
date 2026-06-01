"""Inspeciona MEDICO_ESPECIALIDADE + amostra de especialidades de um médico."""
from conexao import executar_select

print("=== MEDICO_ESPECIALIDADE colunas ===")
cols, _ = executar_select(
    "SELECT column_name||' ('||data_type||')' FROM all_tab_columns WHERE table_name='MEDICO_ESPECIALIDADE' ORDER BY column_id",
    modo="supervisor")
for c in cols: print("  ", c[0])

print("\n=== amostra: médicos com especialidade resolvida ===")
linhas, _ = executar_select(
    """
    SELECT * FROM (
      SELECT m.cd_medico, m.nm_medico, e.ds_especialidade
      FROM medico m
      JOIN medico_especialidade me ON me.cd_medico = m.cd_medico
      JOIN especialidade e ON e.cd_especialidade = me.cd_especialidade
      WHERE m.nm_medico IS NOT NULL
      ORDER BY m.cd_medico DESC
    ) WHERE ROWNUM <= 12
    """,
    modo="supervisor")
for l in linhas: print("  ", l)
