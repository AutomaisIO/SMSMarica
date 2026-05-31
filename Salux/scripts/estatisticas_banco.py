"""Estatísticas gerais do schema INFOSAUDE."""
from __future__ import annotations

import sys

from conexao import executar_select


def secao(titulo: str) -> None:
    print(f"\n\n=== {titulo} ===")


def main() -> int:
    secao("Top 30 tabelas INFOSAUDE por num_rows")
    linhas, cols = executar_select(
        """
        SELECT table_name, num_rows
        FROM dba_tables
        WHERE owner = 'INFOSAUDE' AND num_rows > 0
        ORDER BY num_rows DESC
        FETCH FIRST 30 ROWS ONLY
        """, timeout=120,
    )
    print(" | ".join(cols))
    for l in linhas: print(" | ".join(l))

    secao("Top 30 EDOC_MODELO mais usados (via EDOC_MOVIMENTO último ano)")
    linhas, cols = executar_select(
        """
        SELECT m.cd_modelo, m.ds_modelo,
               (SELECT g.ds_grupo_modelo FROM edoc_grupo_modelo g WHERE g.cd_grupo_modelo = m.cd_grupo_modelo) AS grupo,
               (SELECT COUNT(*) FROM edoc_movimento mv WHERE mv.cd_modelo = m.cd_modelo AND mv.dt_inclusao > SYSDATE - 365) AS usos_12m
          FROM edoc_modelo m
         WHERE m.in_ativo = 'S'
         ORDER BY (SELECT COUNT(*) FROM edoc_movimento mv WHERE mv.cd_modelo = m.cd_modelo AND mv.dt_inclusao > SYSDATE - 365) DESC
         FETCH FIRST 30 ROWS ONLY
        """,
        modo="supervisor", timeout=180,
    )
    print(" | ".join(cols))
    for l in linhas: print(" | ".join(l))

    secao("Sessões ativas por module (SYBSA)")
    linhas, cols = executar_select(
        """
        SELECT module, COUNT(*) AS qtd, COUNT(DISTINCT machine) AS machines
        FROM v$session
        WHERE username = 'SYBSA'
        GROUP BY module
        ORDER BY qtd DESC
        """, timeout=60,
    )
    print(" | ".join(cols))
    for l in linhas: print(" | ".join(l))

    secao("Tabelas LIKE ATENDIMENTO/EXAME/LAUDO/REQUISICAO/ANEXO")
    for padrao in ("%ATENDIMENTO%", "%EXAME%", "%LAUDO%", "%REQUISICAO%", "%ANEXO%", "%LABORATORIO%"):
        print(f"\n-- {padrao}")
        linhas, _ = executar_select(
            "SELECT table_name, num_rows FROM dba_tables WHERE owner='INFOSAUDE' AND table_name LIKE :p AND num_rows>0 ORDER BY num_rows DESC FETCH FIRST 15 ROWS ONLY",
            {"p": padrao},
        )
        for l in linhas:
            print(" | ".join(l))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
