"""Discovery EDOC: o que é DOC_ASSINADO, tamanho, formato e ligação ao atendimento.
Somente SELECT. NÃO commitar saída (PII/clinico)."""
from conexao import executar_select

PAC = [13885, 20269]
inlist = ",".join(str(x) for x in PAC)

print("=== EDOC_MOVIMENTO desses pacientes: total e com DOC_ASSINADO ===")
r, _ = executar_select(
    f"""
    SELECT cd_paciente,
           COUNT(*) total,
           COUNT(CASE WHEN doc_assinado IS NOT NULL THEN 1 END) com_pdf,
           COUNT(CASE WHEN nr_baa IS NOT NULL THEN 1 END) com_baa,
           COUNT(CASE WHEN nr_fia IS NOT NULL THEN 1 END) com_fia
    FROM infosaude.edoc_movimento
    WHERE cd_paciente IN ({inlist})
    GROUP BY cd_paciente
    """,
    modo="supervisor")
for x in r:
    print("  ", x)

print("\n=== amostra de docs (tipo, data, tamanho do blob, primeiros bytes) ===")
r, _ = executar_select(
    f"""
    SELECT * FROM (
      SELECT cd_paciente, cd_modelo, cd_documento,
             TO_CHAR(dt_episodio,'YYYY-MM-DD') dt,
             nr_baa, nr_fia, in_status,
             CASE WHEN doc_assinado IS NULL THEN 0 ELSE DBMS_LOB.GETLENGTH(doc_assinado) END tam,
             CASE WHEN doc_assinado IS NULL THEN NULL
                  ELSE RAWTOHEX(DBMS_LOB.SUBSTR(doc_assinado, 8, 1)) END magic
      FROM infosaude.edoc_movimento
      WHERE cd_paciente IN ({inlist}) AND doc_assinado IS NOT NULL
      ORDER BY dt_episodio DESC NULLS LAST
    ) WHERE ROWNUM <= 8
    """,
    modo="supervisor")
for x in r:
    print("  ", x)

print("\n=== tabela de modelo de documento (nome do tipo de doc) ===")
t, _ = executar_select(
    "SELECT owner||'.'||table_name FROM all_tables WHERE table_name LIKE 'EDOC_MODELO%' OR table_name='MODELO_DOCUMENTO' ORDER BY 1 FETCH FIRST 10 ROWS ONLY",
    modo="supervisor")
print("  tabelas modelo:", [x[0] for x in t])
