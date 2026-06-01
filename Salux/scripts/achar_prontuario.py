"""Discovery: tabelas de prontuário/atendimento/documentos do Salux (BAA, FIA, Edocs).
Busca em TODOS os owners (Salux usa SYBSA + INFOSAUDE + outros). Somente SELECT.
NÃO commitar saída — pode ter PII.
"""
from conexao import executar_select

print("=== owners (schemas) com mais tabelas ===")
owners, _ = executar_select(
    """
    SELECT owner, COUNT(*) FROM all_tables
    WHERE owner NOT IN ('SYS','SYSTEM','XDB','MDSYS','CTXSYS','OLAPSYS','WMSYS','DBSNMP','APEX_040200','ORDDATA','GSMADMIN_INTERNAL','OUTLN','AUDSYS','LBACSYS','DVSYS','APPQOSSYS')
    GROUP BY owner ORDER BY COUNT(*) DESC FETCH FIRST 15 ROWS ONLY
    """,
    modo="supervisor")
for o in owners:
    print(f"  {o[0]:<25} {o[1]} tabelas")

PADROES = ["BAA", "FIA", "FAA", "%EDOC%", "%DOCUMENTO%", "%PRONTUARIO%",
           "%ATENDIMENTO%", "%EVOLUCAO%", "%ANAMNESE%", "%RECEITA%",
           "%PRESCRICAO%", "%DIAGNOSTICO%", "%INTERNACAO%", "%CONSULTA%", "%BOLETIM%"]
cond = " OR ".join([f"table_name LIKE '{p}'" for p in PADROES])

print("\n=== tabelas clínicas (qualquer owner), por nº de linhas ===")
tabs, _ = executar_select(
    f"""
    SELECT owner, table_name, num_rows FROM all_tables
    WHERE ({cond})
      AND owner NOT IN ('SYS','SYSTEM')
    ORDER BY num_rows DESC NULLS LAST FETCH FIRST 40 ROWS ONLY
    """,
    modo="supervisor")
for t in tabs:
    print(f"  {t[0]}.{t[1]:<38} ~{t[2]}")
print(f"\nTotal candidatas: {len(tabs)}")
