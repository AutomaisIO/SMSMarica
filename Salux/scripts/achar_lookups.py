"""Descobre tabelas de lookup do Salux (cidade, uf, nacionalidade, cor, etnia,
instrução, estado civil) e suas colunas-chave, para resolver os códigos do
PACIENTE ao mapear para FHIR."""
from conexao import executar_select

candidatos = [
    "CIDADE", "MUNICIPIO", "UF", "ESTADO", "UNIDADE_FEDERACAO",
    "NACIONALIDADE", "PAIS", "COR", "RACA", "RACA_COR", "ETNIA", "ETNIA_INDIGENA",
    "INSTRUCAO", "GRAU_INSTRUCAO", "ESCOLARIDADE", "ESTADO_CIVIL",
    "BARREIRA_COMUNICACAO", "RELIGIAO", "TIPO_LOGRADOURO",
]
print("=== existência ===")
nomes = "','".join(candidatos)
linhas, _ = executar_select(
    f"SELECT table_name FROM all_tables WHERE table_name IN ('{nomes}') ORDER BY table_name",
    modo="supervisor")
existem = [l[0] for l in linhas]
print("  existem:", existem)

for t in existem:
    cols, _ = executar_select(
        f"SELECT column_name FROM all_tab_columns WHERE table_name='{t}' ORDER BY column_id",
        modo="supervisor")
    print(f"\n=== {t} ===")
    print("  " + ", ".join(c[0] for c in cols))
