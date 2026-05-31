"""Endpoints de atendimento (FIA + BAA): histórico, sinais vitais, prescrição, evolução, eDocs."""
from __future__ import annotations

from typing import Any

from fastapi import APIRouter, HTTPException, Query

from conexao import executar_select, executar_json  # type: ignore

router = APIRouter()


def _row_to_dict(linha: list[str], cols: list[str]) -> dict[str, Any]:
    return {c.lower(): (v if v else None) for c, v in zip(cols, linha)}


@router.get("/fia/{nr_fia}/{ano}")
def fia_header(nr_fia: int, ano: int, cd_hospital: int = Query(1)) -> dict:
    """Header de uma FIA (internação) específica."""
    sql = """
    SELECT
        f.cd_hospital, f.dt_ano_fia, f.nr_fia,
        f.cd_paciente, NVL(f.cd_paciente_unificado, f.cd_paciente) AS cd_paciente_efetivo,
        (SELECT nm_paciente FROM paciente WHERE cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)) AS nm_paciente,
        f.dt_baixa, f.dt_alta, f.dt_previsao_alta,
        f.cd_medico,
        (SELECT nm_medico FROM medico WHERE cd_medico = f.cd_medico) AS nm_medico_entrada,
        f.med_cd_medico AS cd_medico_alta,
        (SELECT nm_medico FROM medico WHERE cd_medico = f.med_cd_medico) AS nm_medico_alta,
        f.cd_cid, f.cid2_cd_cid AS cd_cid2, f.cid3_cd_cid AS cd_cid3,
        f.id_internacao, f.cd_carater_internacao,
        f.nm_responsavel, f.cpf_responsavel,
        f.nr_dias_internacao,
        (SELECT fl.cd_quarto || ' - ' || fl.cd_leito
           FROM fia_leito fl
          WHERE fl.cd_hospital = f.cd_hospital
            AND fl.dt_ano_fia = f.dt_ano_fia
            AND fl.nr_fia = f.nr_fia
            AND fl.dt_transferencia = (SELECT MAX(dt_transferencia) FROM fia_leito
                                        WHERE cd_hospital = f.cd_hospital
                                          AND dt_ano_fia = f.dt_ano_fia
                                          AND nr_fia = f.nr_fia)
        ) AS leito_atual
      FROM fia f
     WHERE f.cd_hospital = :h AND f.dt_ano_fia = :a AND f.nr_fia = :n
    """
    cols = ["CD_HOSPITAL", "DT_ANO_FIA", "NR_FIA",
            "CD_PACIENTE", "CD_PACIENTE_EFETIVO", "NM_PACIENTE",
            "DT_BAIXA", "DT_ALTA", "DT_PREVISAO_ALTA",
            "CD_MEDICO", "NM_MEDICO_ENTRADA",
            "CD_MEDICO_ALTA", "NM_MEDICO_ALTA",
            "CD_CID", "CD_CID2", "CD_CID3",
            "ID_INTERNACAO", "CD_CARATER_INTERNACAO",
            "NM_RESPONSAVEL", "CPF_RESPONSAVEL",
            "NR_DIAS_INTERNACAO", "LEITO_ATUAL"]
    try:
        linhas, _ = executar_select(sql, {"h": cd_hospital, "a": ano, "n": nr_fia}, modo="supervisor")
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    if not linhas:
        raise HTTPException(404, f"FIA {nr_fia}/{ano} não encontrada")
    return _row_to_dict(linhas[0], cols)


@router.get("/baa/{nr_baa}/{ano}")
def baa_header(nr_baa: int, ano: int, cd_hospital: int = Query(1)) -> dict:
    """Header de um BAA (ambulatório) específico."""
    sql = """
    SELECT
        b.cd_hospital, b.dt_ano_baa, b.nr_baa,
        b.cd_paciente, NVL(b.cd_paciente_unificado, b.cd_paciente) AS cd_paciente_efetivo,
        (SELECT nm_paciente FROM paciente WHERE cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)) AS nm_paciente,
        b.dt_atendimento, b.dt_chegada, b.dt_saida,
        b.cd_medico,
        (SELECT nm_medico FROM medico WHERE cd_medico = b.cd_medico) AS nm_medico,
        b.cd_cid,
        b.cd_especialidade,
        (SELECT ds_especialidade FROM especialidade WHERE cd_especialidade = b.cd_especialidade) AS ds_especialidade,
        b.id_tp_consulta, b.in_emergencia, b.id_destino,
        b.cd_classificacao_risco,
        (SELECT ds_classificacao_risco FROM classificacao_risco WHERE cd_classificacao_risco = b.cd_classificacao_risco) AS ds_classif_risco,
        b.in_baa_atendido,
        b.cd_setor,
        (SELECT ds_setor FROM setor_fluxo WHERE cd_setor = b.cd_setor) AS ds_setor,
        b.nro_senha
      FROM baa b
     WHERE b.cd_hospital = :h AND b.dt_ano_baa = :a AND b.nr_baa = :n
    """
    cols = ["CD_HOSPITAL", "DT_ANO_BAA", "NR_BAA",
            "CD_PACIENTE", "CD_PACIENTE_EFETIVO", "NM_PACIENTE",
            "DT_ATENDIMENTO", "DT_CHEGADA", "DT_SAIDA",
            "CD_MEDICO", "NM_MEDICO",
            "CD_CID",
            "CD_ESPECIALIDADE", "DS_ESPECIALIDADE",
            "ID_TP_CONSULTA", "IN_EMERGENCIA", "ID_DESTINO",
            "CD_CLASSIFICACAO_RISCO", "DS_CLASSIF_RISCO",
            "IN_BAA_ATENDIDO",
            "CD_SETOR", "DS_SETOR",
            "NRO_SENHA"]
    try:
        linhas, _ = executar_select(sql, {"h": cd_hospital, "a": ano, "n": nr_baa}, modo="supervisor")
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    if not linhas:
        raise HTTPException(404, f"BAA {nr_baa}/{ano} não encontrado")
    return _row_to_dict(linhas[0], cols)


@router.get("/baa/{nr_baa}/{ano}/edocs")
def edocs_do_baa(nr_baa: int, ano: int, cd_hospital: int = Query(1)) -> dict:
    """Lista eDocs preenchidos vinculados a um BAA."""
    sql = """
    SELECT mv.id_movimento, mv.cd_modelo, m.ds_modelo,
           (SELECT g.ds_grupo_modelo FROM edoc_grupo_modelo g WHERE g.cd_grupo_modelo = m.cd_grupo_modelo) AS grupo,
           (SELECT c.ds_categoria FROM edoc_categoria c WHERE c.cd_categoria = m.cd_categoria) AS categoria,
           mv.dt_inclusao, mv.cd_funcionario_inc,
           (SELECT nm_funcionario FROM funcionario WHERE cd_funcionario = mv.cd_funcionario_inc) AS nm_funcionario,
           mv.in_status, mv.in_ativo
      FROM edoc_movimento mv
      JOIN edoc_modelo m ON m.cd_modelo = mv.cd_modelo
     WHERE mv.baa_cd_hospital = :h AND mv.dt_ano_baa = :a AND mv.nr_baa = :n
       AND mv.in_ativo = 'S'
     ORDER BY mv.dt_inclusao DESC
    """
    cols = ["ID_MOVIMENTO", "CD_MODELO", "DS_MODELO", "GRUPO", "CATEGORIA",
            "DT_INCLUSAO", "CD_FUNCIONARIO_INC", "NM_FUNCIONARIO", "IN_STATUS", "IN_ATIVO"]
    try:
        linhas, _ = executar_select(sql, {"h": cd_hospital, "a": ano, "n": nr_baa}, modo="supervisor", timeout=120)
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"edocs": [_row_to_dict(l, cols) for l in linhas]}


@router.get("/pacientes/{cd_paciente}/historico")
def historico(
    cd_paciente: int,
    cd_hospital: int = Query(1),
) -> dict:
    """Histórico de atendimentos do paciente (UNION FIA + BAA)."""
    sql = """
    SELECT fia.dt_baixa AS dt_entrada, fia.dt_ano_fia AS dt_ano, fia.nr_fia AS nr_doc,
           (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = fia.cd_medico) AS nm_medico,
           fia.dt_alta,
           (SELECT c.sc_clinica FROM clinica c, fia_clinica fc
             WHERE c.cd_clinica = fc.cd_clinica AND fc.cd_hospital = fia.cd_hospital
               AND fc.dt_ano_fia = fia.dt_ano_fia AND fc.nr_fia = fia.nr_fia
               AND fc.dt_entra_clinica =
                   (SELECT MAX(f.dt_entra_clinica) FROM fia_clinica f
                     WHERE f.cd_hospital = fc.cd_hospital AND f.nr_fia = fc.nr_fia
                       AND f.dt_ano_fia = fc.dt_ano_fia)
           ) AS ds_clinica,
           fia.cd_cid, 'F' AS id_tipo, fia.cd_hospital
      FROM fia
     WHERE NVL(fia.cd_paciente_unificado, fia.cd_paciente) = :pl_cd_paciente
       AND fia.cd_hospital = :pl_cd_hospital
    UNION ALL
    SELECT baa.dt_atendimento, baa.dt_ano_baa, baa.nr_baa,
           (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = baa.cd_medico) AS nm_medico,
           baa.dt_saida,
           (SELECT e.ds_especialidade FROM especialidade e WHERE e.cd_especialidade = baa.cd_especialidade) AS ds_clinica,
           baa.cd_cid, 'B' AS id_tipo, baa.cd_hospital
      FROM baa
     WHERE NVL(baa.cd_paciente_unificado, baa.cd_paciente) = :pl_cd_paciente
       AND baa.cd_hospital = :pl_cd_hospital
    ORDER BY 1 DESC
    """
    colunas = ["DT_ENTRADA", "DT_ANO", "NR_DOC", "NM_MEDICO", "DT_ALTA",
               "DS_CLINICA", "CD_CID", "ID_TIPO", "CD_HOSPITAL"]
    try:
        linhas, _ = executar_select(
            sql, {"pl_cd_paciente": cd_paciente, "pl_cd_hospital": cd_hospital},
            modo="supervisor",
        )
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"atendimentos": [_row_to_dict(l, colunas) for l in linhas]}


@router.get("/fia/{nr_fia}/{ano}/sinais-vitais")
def sinais_vitais(nr_fia: int, ano: int, cd_hospital: int = Query(1)) -> dict:
    sql = """
    SELECT sv.dthr_visita,
           sv.cd_funcionario,
           sv.peso, sv.altura,
           sv.vl_temp_aux AS temp,
           sv.vl_pa_alta AS pa_sist,
           sv.vl_pa_baixa AS pa_diast,
           sv.vl_freq_cardio AS fc,
           sv.vl_respiracao AS fr,
           sv.vl_saturacao_oxigenio AS spo2,
           sv.vl_glicose AS glicemia
      FROM sinais_vitais sv
     WHERE sv.cd_hospital = :h AND sv.dt_ano_fia_baa = :a AND sv.nr_fia_baa = :n
       AND sv.id_tipo = 'F'
     ORDER BY sv.dthr_visita DESC
    """
    colunas = ["DTHR_VISITA", "CD_FUNCIONARIO", "PESO", "ALTURA",
               "TEMP", "PA_SIST", "PA_DIAST", "FC", "FR", "SPO2", "GLICEMIA"]
    try:
        linhas, _ = executar_select(
            sql, {"h": cd_hospital, "a": ano, "n": nr_fia}, modo="supervisor",
        )
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"aferições": [_row_to_dict(l, colunas) for l in linhas]}


@router.get("/fia/{nr_fia}/{ano}/edocs")
def edocs_da_fia(nr_fia: int, ano: int, cd_hospital: int = Query(1)) -> dict:
    """Lista eDocs preenchidos vinculados a uma FIA."""
    sql = """
    SELECT mv.id_movimento, mv.cd_modelo, m.ds_modelo,
           (SELECT g.ds_grupo_modelo FROM edoc_grupo_modelo g WHERE g.cd_grupo_modelo = m.cd_grupo_modelo) AS grupo,
           (SELECT c.ds_categoria FROM edoc_categoria c WHERE c.cd_categoria = m.cd_categoria) AS categoria,
           mv.dt_inclusao, mv.cd_funcionario_inc,
           (SELECT nm_funcionario FROM funcionario WHERE cd_funcionario = mv.cd_funcionario_inc) AS nm_funcionario,
           mv.in_status, mv.in_ativo
      FROM edoc_movimento mv
      JOIN edoc_modelo m ON m.cd_modelo = mv.cd_modelo
     WHERE mv.fia_cd_hospital = :h AND mv.dt_ano_fia = :a AND mv.nr_fia = :n
       AND mv.in_ativo = 'S'
     ORDER BY mv.dt_inclusao DESC
    """
    colunas = ["ID_MOVIMENTO", "CD_MODELO", "DS_MODELO", "GRUPO", "CATEGORIA",
               "DT_INCLUSAO", "CD_FUNCIONARIO_INC", "NM_FUNCIONARIO", "IN_STATUS", "IN_ATIVO"]
    try:
        linhas, _ = executar_select(
            sql, {"h": cd_hospital, "a": ano, "n": nr_fia}, modo="supervisor", timeout=120,
        )
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"edocs": [_row_to_dict(l, colunas) for l in linhas]}


@router.get("/edocs/movimento/{cd_hospital}/{ano_movimento}/{id_movimento}")
def edoc_movimento(cd_hospital: int, ano_movimento: int, id_movimento: int) -> dict:
    """Retorna um eDoc preenchido completo. Estratégia: cada row vem como UMA
    string concatenada com separador interno improvável (`@|@`). Evita problemas
    de COLSEP do sqlplus com texto longo/multilinha e ORA-40474 do JSON_OBJECT.
    """
    sql = """
    SELECT
        NVL(TO_CHAR(mi.cd_item), '') || '@|@' ||
        NVL(REGEXP_REPLACE(SUBSTR(ei.ds_item, 1, 250), '[[:cntrl:]]', ' '), '') || '@|@' ||
        NVL((SELECT ti.ds_tipo_item FROM edoc_tipo_item ti WHERE ti.cd_tipo_item = ei.cd_tipo_item), '') || '@|@' ||
        NVL(TO_CHAR(mi.seq_docto), '') || '@|@' ||
        NVL(REGEXP_REPLACE(SUBSTR(mi.ds_resposta, 1, 1500), '[[:cntrl:]]', ' '), '') AS rec
      FROM edoc_movimento_item mi
      LEFT JOIN edoc_item ei ON ei.cd_item = mi.cd_item
     WHERE mi.cd_hospital = :h AND mi.ano_movimento = :a AND mi.id_movimento = :i
     ORDER BY mi.seq_docto, mi.cd_item
    """
    try:
        linhas, _ = executar_select(
            sql, {"h": cd_hospital, "a": ano_movimento, "i": id_movimento},
            modo="supervisor",
        )
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")

    itens = []
    for linha in linhas:
        joined = linha[0] if linha else ""
        partes = joined.split("@|@")
        while len(partes) < 5:
            partes.append("")
        itens.append({
            "cd_item": partes[0].strip() or None,
            "ds_item": partes[1].strip() or None,
            "tipo": partes[2].strip() or None,
            "seq_docto": partes[3].strip() or None,
            "ds_resposta": partes[4].strip() or None,
        })
    return {"itens": itens}
