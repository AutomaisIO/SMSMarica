"""Lista todos os EDOC_MODELO com categoria/grupo."""
from __future__ import annotations

from conexao import executar_select


def main() -> int:
    linhas, _ = executar_select(
        """
        SELECT m.cd_modelo,
               m.ds_modelo,
               m.in_ativo,
               m.in_tipo,
               m.in_tipo_reg,
               m.cd_grupo_modelo,
               (SELECT g.ds_grupo_modelo FROM edoc_grupo_modelo g WHERE g.cd_grupo_modelo = m.cd_grupo_modelo) AS grupo,
               m.cd_categoria,
               (SELECT c.ds_categoria FROM edoc_categoria c WHERE c.cd_categoria = m.cd_categoria) AS categoria
          FROM edoc_modelo m
         ORDER BY m.cd_categoria, m.cd_grupo_modelo, m.cd_modelo
        """,
        modo="supervisor",
    )
    cols = ["CD", "DS_MODELO", "ATIVO", "TIPO", "TIPO_REG", "GRUPO_CD", "GRUPO", "CAT_CD", "CATEGORIA"]
    print(" | ".join(cols))
    print("-" * 140)
    for l in linhas:
        print(" | ".join(l))
    print(f"\nTotal: {len(linhas)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
