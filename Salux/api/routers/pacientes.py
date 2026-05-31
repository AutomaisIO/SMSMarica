"""Endpoints de paciente."""
from __future__ import annotations

from typing import Any

from fastapi import APIRouter, HTTPException, Query

from conexao import executar_select  # type: ignore  # injetado via sys.path em main.py

router = APIRouter()


def _row_to_dict(linha: list[str], cols: list[str]) -> dict[str, Any]:
    return {c.lower(): (v if v else None) for c, v in zip(cols, linha)}


@router.get("/buscar")
def buscar(
    nome: str = Query("", min_length=0, description="parte do nome (ILIKE)"),
    cpf: str = Query("", description="CPF (11 dígitos)"),
    limite: int = Query(20, ge=1, le=100),
) -> dict:
    """Busca paciente por nome (LIKE) e/ou CPF exato."""
    if not nome and not cpf:
        raise HTTPException(400, "informe nome ou cpf")

    where = []
    binds = {}
    if nome:
        where.append("UPPER(nm_paciente) LIKE :n")
        binds["n"] = f"%{nome.upper()}%"
    if cpf:
        where.append("cpf_paciente = :c")
        binds["c"] = cpf.replace(".", "").replace("-", "").strip()

    sql = f"""
    SELECT *
      FROM (
        SELECT cd_paciente, nm_paciente, dt_nascimento, sexo,
               cpf_paciente, cns, in_ativo, dt_obito
          FROM paciente
         WHERE {' AND '.join(where)}
         ORDER BY nm_paciente
      )
     WHERE ROWNUM <= :lim
    """
    binds["lim"] = limite

    colunas = ["CD_PACIENTE", "NM_PACIENTE", "DT_NASCIMENTO", "SEXO",
               "CPF_PACIENTE", "CNS", "IN_ATIVO", "DT_OBITO"]
    try:
        linhas, _ = executar_select(sql, binds, modo="supervisor")
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"resultados": [_row_to_dict(l, colunas) for l in linhas]}


@router.get("/{cd_paciente}")
def cadastro(cd_paciente: int) -> dict:
    """Cadastro completo do paciente (dados de identificação + endereço + contato)."""
    sql = """
    SELECT
        p.cd_paciente, p.nm_paciente, p.sc_apelido, p.sexo, p.dt_nascimento,
        p.cpf_paciente, p.rg_paciente, p.cns, p.estado_civil,
        p.nm_mae, p.nm_pai, p.nm_conjuge,
        p.nm_logradouro, p.nr_logradouro, p.compl_logradouro, p.bairro,
        p.cep, p.cd_uf, p.cd_cidade,
        p.nr_ddd_fone, p.nr_fone, p.nr_fone_compl, p.email,
        p.profissao, p.religiao,
        p.peso, p.altura,
        p.in_ativo, p.dt_cadastro, p.dt_obito,
        p.cd_pront_sgh, p.cd_pront_cem,
        (SELECT cidade.ds_cidade FROM cidade
          WHERE cidade.cd_uf = p.cd_uf AND cidade.cd_cidade = p.cd_cidade) AS nm_cidade
      FROM paciente p
     WHERE p.cd_paciente = :pl_cd_paciente
    """
    colunas = ["CD_PACIENTE", "NM_PACIENTE", "SC_APELIDO", "SEXO", "DT_NASCIMENTO",
               "CPF_PACIENTE", "RG_PACIENTE", "CNS", "ESTADO_CIVIL",
               "NM_MAE", "NM_PAI", "NM_CONJUGE",
               "NM_LOGRADOURO", "NR_LOGRADOURO", "COMPL_LOGRADOURO", "BAIRRO",
               "CEP", "CD_UF", "CD_CIDADE",
               "NR_DDD_FONE", "NR_FONE", "NR_FONE_COMPL", "EMAIL",
               "PROFISSAO", "RELIGIAO",
               "PESO", "ALTURA",
               "IN_ATIVO", "DT_CADASTRO", "DT_OBITO",
               "CD_PRONT_SGH", "CD_PRONT_CEM",
               "NM_CIDADE"]
    try:
        linhas, _ = executar_select(sql, {"pl_cd_paciente": cd_paciente}, modo="supervisor")
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    if not linhas:
        raise HTTPException(404, f"paciente {cd_paciente} não encontrado")
    return _row_to_dict(linhas[0], colunas)


@router.get("/{cd_paciente}/alergias")
def alergias(cd_paciente: int) -> dict:
    """Lista de alergias do paciente + medicamentos associados."""
    sql = """
    SELECT pa.cd_alergia, a.ds_alergia, am.cd_medicamento, m.ds_material,
           pa.ds_descritivo, a.in_ativo
      FROM paciente_x_alergia pa
      JOIN alergia a ON pa.cd_alergia = a.cd_alergia
      LEFT JOIN alergia_x_medicamento am ON pa.cd_alergia = am.cd_alergia
      LEFT JOIN matmed m ON am.cd_medicamento = m.cd_material
     WHERE pa.cd_paciente = :pl_cd_paciente
    """
    colunas = ["CD_ALERGIA", "DS_ALERGIA", "CD_MEDICAMENTO", "DS_MATERIAL",
               "DS_DESCRITIVO", "IN_ATIVO"]
    try:
        linhas, _ = executar_select(sql, {"pl_cd_paciente": cd_paciente}, modo="supervisor")
    except Exception as e:
        raise HTTPException(500, f"erro no Oracle: {e}")
    return {"alergias": [_row_to_dict(l, colunas) for l in linhas]}
