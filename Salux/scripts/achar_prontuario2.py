"""Colunas-chave de BAA, FIA, EDOC_MOVIMENTO, EDOC_DOCUMENTO_ITEM e ligação ao paciente."""
from conexao import executar_select

ALVOS = ["BAA", "FIA", "EDOC_MOVIMENTO", "EDOC_DOCUMENTO_ITEM", "EVOLUCAO_PACIENTE_BAA"]

for tab in ALVOS:
    print(f"\n=== INFOSAUDE.{tab} — colunas ===")
    cols, _ = executar_select(
        f"""
        SELECT column_name, data_type
        FROM all_tab_columns
        WHERE owner='INFOSAUDE' AND table_name='{tab}'
        ORDER BY column_id
        """,
        modo="supervisor")
    linha = ", ".join([f"{c[0]}({c[1][:3]})" for c in cols])
    print("  " + linha)
