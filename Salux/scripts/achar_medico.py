"""Inspeciona MEDICO + MEDICO_X_FUNCIONARIO."""
from conexao import executar_select

for tab in ("MEDICO", "MEDICO_X_FUNCIONARIO"):
    print(f"=== {tab} ===")
    cols, _ = executar_select(
        f"SELECT column_name||' ('||data_type||')' FROM all_tab_columns WHERE table_name='{tab}' ORDER BY column_id",
        modo="supervisor")
    for c in cols:
        print("  ", c[0])
    cnt, _ = executar_select(f"SELECT COUNT(*) FROM {tab}", modo="supervisor")
    print(f"  ROWS: {cnt[0][0] if cnt else '?'}\n")
