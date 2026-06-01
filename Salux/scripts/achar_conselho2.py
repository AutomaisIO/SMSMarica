"""Valores de CD_CONSELHO / CD_CONSELHO_CATEGORIA + lookup do conselho."""
from conexao import executar_select

print("=== distinct CD_CONSELHO / CD_CONSELHO_CATEGORIA (top médicos) ===")
linhas, _ = executar_select(
    """
    SELECT cd_conselho, cd_conselho_categoria, COUNT(*) qtd
    FROM medico
    WHERE dt_exclusao IS NULL AND nm_medico IS NOT NULL
    GROUP BY cd_conselho, cd_conselho_categoria
    ORDER BY qtd DESC
    FETCH FIRST 20 ROWS ONLY
    """,
    modo="supervisor")
for l in linhas:
    print("  ", l)

print("\n=== tabelas com CONSELHO no nome (qualquer owner) ===")
tabs, _ = executar_select(
    "SELECT owner||'.'||table_name FROM all_tables WHERE table_name LIKE '%CONSELHO%' ORDER BY 1",
    modo="supervisor")
for t in tabs:
    print("  ", t[0])

print("\n=== amostra: conselho por profissional ===")
amostra, _ = executar_select(
    """
    SELECT * FROM (
      SELECT m.cd_medico, m.nm_medico, m.cd_conselho, m.cd_conselho_categoria, m.nr_crm,
             (SELECT LISTAGG(e.ds_especialidade,' / ') WITHIN GROUP (ORDER BY e.ds_especialidade)
                FROM medico_especialidade me JOIN especialidade e ON e.cd_especialidade=me.cd_especialidade
               WHERE me.cd_medico=m.cd_medico) esp
      FROM medico m WHERE m.nm_medico IS NOT NULL AND m.dt_exclusao IS NULL
      ORDER BY m.cd_medico DESC
    ) WHERE ROWNUM <= 12
    """,
    modo="supervisor")
for l in amostra:
    print("  ", l)
