"""Busca paciente por nome (LIKE)."""
from __future__ import annotations

import sys

from conexao import executar_select


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/buscar_paciente.py <nome ou parte>")
        return 2
    nome = " ".join(sys.argv[1:]).upper()
    linhas, _ = executar_select(
        """
        SELECT cd_paciente, nm_paciente, dt_nascimento, cpf_paciente, cns, in_ativo, dt_obito
        FROM paciente
        WHERE UPPER(nm_paciente) LIKE :n
        ORDER BY nm_paciente
        """,
        {"n": f"%{nome}%"}, modo="supervisor",
    )
    cols = ["CD", "NOME", "NASC", "CPF", "CNS", "ATIVO", "OBITO"]
    print(" | ".join(cols))
    print("-" * 90)
    if not linhas:
        print("(nenhum resultado)")
        return 0
    for l in linhas[:20]:
        print(" | ".join(l))
    if len(linhas) > 20:
        print(f"... e mais {len(linhas) - 20}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
