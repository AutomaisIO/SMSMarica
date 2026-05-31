"""Dado uma FIA (nr_fia, dt_ano_fia, cd_hospital), retorna cd_paciente."""
from __future__ import annotations

import sys

from conexao import executar_select


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/descobrir_paciente_fia.py <nr_fia> [dt_ano_fia=2026] [cd_hospital=1]")
        return 2
    nr_fia = int(sys.argv[1])
    dt_ano_fia = int(sys.argv[2]) if len(sys.argv) > 2 else 2026
    cd_hospital = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    linhas, colunas = executar_select(
        """
        SELECT f.nr_fia, f.dt_ano_fia, f.cd_paciente, f.cd_paciente_unificado,
               NVL(f.cd_paciente_unificado, f.cd_paciente) AS cd_efetivo,
               p.nm_paciente
        FROM fia f, paciente p
        WHERE f.cd_hospital = :h AND f.dt_ano_fia = :a AND f.nr_fia = :n
          AND p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
        """,
        {"h": cd_hospital, "a": dt_ano_fia, "n": nr_fia},
        modo="supervisor",
    )
    print(" | ".join(colunas))
    for l in linhas:
        print(" | ".join(l))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
