"""Investiga como o Salux distingue o conselho profissional (CRM/COREN/CRN...).

Premissa: a tabela MEDICO guarda nr_crm/uf_cd_uf, mas nem todo profissional é
médico (há técnicos de enfermagem, nutricionistas...). Procuramos uma coluna de
categoria/conselho e seu lookup, e amostramos por especialidade.
"""
from conexao import executar_select

print("=== colunas de MEDICO com cara de conselho/categoria/registro ===")
cols, _ = executar_select(
    """
    SELECT column_name||' ('||data_type||')'
    FROM all_tab_columns
    WHERE table_name='MEDICO'
      AND (column_name LIKE '%CRM%' OR column_name LIKE '%CONSELHO%'
        OR column_name LIKE '%CATEGORIA%' OR column_name LIKE '%CBO%'
        OR column_name LIKE '%REGISTRO%' OR column_name LIKE '%COREN%'
        OR column_name LIKE '%CLASSE%' OR column_name LIKE '%PROFISS%')
    ORDER BY column_id
    """,
    modo="supervisor")
for c in cols:
    print("  ", c[0])

print("\n=== tabelas de lookup candidatas (categoria/conselho/cbo) ===")
tabs, _ = executar_select(
    """
    SELECT table_name FROM all_tables
    WHERE owner='SYBSA'
      AND (table_name LIKE '%CATEGORIA%' OR table_name LIKE '%CONSELHO%'
        OR table_name LIKE '%CBO%' OR table_name LIKE '%CLASSE%')
    ORDER BY table_name
    """,
    modo="supervisor")
for t in tabs:
    print("  ", t[0])

print("\n=== amostra: nr_crm/uf/categoria/cbo por especialidade ===")
linhas, _ = executar_select(
    """
    SELECT * FROM (
      SELECT m.cd_medico, m.nm_medico, m.nr_crm, m.uf_cd_uf,
             m.id_categoria, m.cd_cbo_smm,
             (SELECT LISTAGG(e.ds_especialidade, ', ') WITHIN GROUP (ORDER BY e.ds_especialidade)
                FROM medico_especialidade me JOIN especialidade e ON e.cd_especialidade=me.cd_especialidade
               WHERE me.cd_medico=m.cd_medico) AS especialidades
      FROM medico m
      WHERE m.nm_medico IS NOT NULL AND m.dt_exclusao IS NULL
      ORDER BY m.cd_medico DESC
    ) WHERE ROWNUM <= 12
    """,
    modo="supervisor")
for l in linhas:
    print("  ", l)
