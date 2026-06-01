"""Viabilidade: dado um cd_paciente, quão barato é listar BAA/FIA/EDOC dele?
Pega um paciente com movimento e mede contagem por paciente.
"""
import time
from conexao import executar_select

# Acha um cd_paciente com bastante BAA (amostra recente).
linhas, _ = executar_select(
    """
    SELECT cd_paciente, COUNT(*) FROM (
      SELECT cd_paciente FROM infosaude.baa WHERE cd_paciente IS NOT NULL AND ROWNUM <= 50000
    ) GROUP BY cd_paciente ORDER BY COUNT(*) DESC FETCH FIRST 3 ROWS ONLY
    """,
    modo="supervisor")
print("cd_paciente candidatos (amostra):", linhas)
if not linhas:
    raise SystemExit("sem dados")

cd = linhas[0][0]
print(f"\nUsando cd_paciente={cd}\n")

for nome, sql in [
    ("BAA do paciente", f"SELECT COUNT(*) FROM infosaude.baa WHERE cd_paciente={cd}"),
    ("FIA do paciente", f"SELECT COUNT(*) FROM infosaude.fia WHERE cd_paciente={cd}"),
    ("EDOC do paciente", f"SELECT COUNT(*) FROM infosaude.edoc_movimento WHERE cd_paciente={cd}"),
]:
    t = time.time()
    r, _ = executar_select(sql, modo="supervisor")
    print(f"  {nome:<18} = {r[0][0]:<8} ({time.time()-t:.2f}s)")

# Amostra de um BAA (campos p/ Encounter)
print("\n=== amostra BAA (1) ===")
r, cols = executar_select(
    f"""
    SELECT dt_atendimento, dt_chegada, dt_saida, cd_medico, cd_especialidade,
           cd_cid, in_emergencia, cd_classificacao_risco
    FROM infosaude.baa WHERE cd_paciente={cd} AND ROWNUM<=1
    """,
    modo="supervisor")
print(" ", r)

# Existe lookup de CID?
print("\n=== tabela(s) de CID ===")
t, _ = executar_select(
    "SELECT owner||'.'||table_name FROM all_tables WHERE table_name IN ('CID','CID10','TB_CID') ORDER BY 1",
    modo="supervisor")
print(" ", [x[0] for x in t])
