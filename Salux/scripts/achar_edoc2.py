"""Onde está o TEXTO do atendimento: evolução (BAA) e itens do EDOC. Só SELECT."""
from conexao import executar_select

print("=== EVOLUCAO_PACIENTE_BAA do paciente 13885 (tem texto?) ===")
r, _ = executar_select(
    """
    SELECT COUNT(*) total,
           COUNT(CASE WHEN bl_evolucao IS NOT NULL THEN 1 END) com_blob
    FROM infosaude.evolucao_paciente_baa e
    JOIN infosaude.baa b ON b.cd_hospital=e.cd_hospital AND b.dt_ano_baa=e.dt_ano_baa AND b.nr_baa=e.nr_baa
    WHERE b.cd_paciente=13885
    """,
    modo="supervisor")
print("  ", r)

print("\n=== EDOC_MOVIMENTO_ITEM colunas ===")
c, _ = executar_select(
    "SELECT column_name, data_type FROM all_tab_columns WHERE owner='INFOSAUDE' AND table_name='EDOC_MOVIMENTO_ITEM' ORDER BY column_id",
    modo="supervisor")
print("  ", ", ".join(f"{x[0]}({x[1][:3]})" for x in c))

print("\n=== EDOC_ITEM colunas (rótulos dos campos) ===")
c, _ = executar_select(
    "SELECT column_name, data_type FROM all_tab_columns WHERE owner='INFOSAUDE' AND table_name='EDOC_ITEM' ORDER BY column_id",
    modo="supervisor")
print("  ", ", ".join(f"{x[0]}({x[1][:3]})" for x in c))

print("\n=== um EDOC_MOVIMENTO do paciente 13885 + seus itens (amostra) ===")
r, _ = executar_select(
    """
    SELECT * FROM (
      SELECT cd_hospital, ano_movimento, id_movimento, cd_modelo, cd_documento,
             TO_CHAR(dt_episodio,'YYYY-MM-DD') dt
      FROM infosaude.edoc_movimento WHERE cd_paciente=13885 ORDER BY dt_episodio DESC NULLS LAST
    ) WHERE ROWNUM<=1
    """,
    modo="supervisor")
print("  movimento:", r)
