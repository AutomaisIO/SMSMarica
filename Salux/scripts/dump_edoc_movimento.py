"""Dump 1 movimento eDoc completo (incluindo todos os itens) — pra entender shape EAV."""
from __future__ import annotations

import pathlib

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas"
_ARQ = _SAIDA / "investig_edoc_movimento_completo.txt"


def secao(header: str, sql: str, binds=None, modo="supervisor", timeout=60):
    print(f"\n=== {header} ===")
    linhas, cols = executar_select(sql, binds, modo=modo, timeout=timeout)
    with _ARQ.open("a", encoding="utf-8") as f:
        f.write(f"\n# {header}\n")
        f.write(" | ".join(cols) + "\n")
        f.write("-" * 80 + "\n")
        for l in linhas:
            f.write(" | ".join(l) + "\n")
        f.write(f"({len(linhas)} linhas)\n")
    print(f"OK {len(linhas)} linhas")
    return linhas


def main() -> int:
    _ARQ.write_text("", encoding="utf-8")

    # 1) Achar um movimento recente da Escala de Glasgow (cd_modelo=10029, pequeno e estruturado)
    linhas = secao(
        "Achar um EDOC_MOVIMENTO recente da Escala de Glasgow (cd_modelo=10029)",
        """
        SELECT cd_hospital, ano_movimento, id_movimento, cd_modelo, cd_documento,
               cd_paciente, dt_inclusao, in_status, fia_cd_hospital, dt_ano_fia, nr_fia,
               baa_cd_hospital, dt_ano_baa, nr_baa
        FROM edoc_movimento
        WHERE cd_modelo = 10029
          AND in_status = 'D'
          AND in_ativo = 'S'
          AND ROWNUM <= 1
        ORDER BY dt_inclusao DESC
        """, timeout=180,
    )
    if not linhas:
        print("Nenhum movimento Glasgow encontrado, abortando")
        return 1
    # parse os campos pela posição
    cd_hosp, ano_mov, id_mov = int(linhas[0][0]), int(linhas[0][1]), int(linhas[0][2])
    cd_modelo = int(linhas[0][3])
    print(f"Selecionado: hosp={cd_hosp} ano={ano_mov} id={id_mov} modelo={cd_modelo}")

    # 2) Dump todos os items + JOIN com EDOC_ITEM pra ver o label do campo
    secao(
        f"EDOC_MOVIMENTO_ITEM (todos) do movimento {id_mov}/{ano_mov}, com label",
        f"""
        SELECT mi.cd_item, mi.cd_documento, mi.seq_docto, mi.cd_item_grupo,
               ei.ds_item, ei.cd_tipo_item,
               (SELECT ti.ds_tipo_item FROM edoc_tipo_item ti WHERE ti.cd_tipo_item = ei.cd_tipo_item) AS tipo_item,
               SUBSTR(mi.ds_resposta, 1, 200) AS resposta_inicio
        FROM edoc_movimento_item mi
        LEFT JOIN edoc_item ei ON ei.cd_item = mi.cd_item
        WHERE mi.cd_hospital = {cd_hosp}
          AND mi.ano_movimento = {ano_mov}
          AND mi.id_movimento = {id_mov}
        ORDER BY mi.seq_docto, mi.cd_item
        """,
    )

    # 3) Dump headers/cabecalho do modelo (definição)
    secao(
        f"EDOC_DOCUMENTO_ITEM (definição) do modelo {cd_modelo}",
        f"""
        SELECT di.cd_documento, di.cd_item, ei.ds_item, ei.cd_tipo_item,
               (SELECT ti.ds_tipo_item FROM edoc_tipo_item ti WHERE ti.cd_tipo_item = ei.cd_tipo_item) AS tipo,
               ei.in_obrigatorio, ei.in_visivel_lista
        FROM edoc_documento_item di
        LEFT JOIN edoc_item ei ON ei.cd_item = di.cd_item
        WHERE di.cd_documento IN (
            SELECT cd_documento FROM edoc_documento WHERE cd_modelo = {cd_modelo}
        )
        ORDER BY di.cd_documento, di.cd_item
        """, timeout=120,
    )

    print(f"\nGravado em {_ARQ}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
