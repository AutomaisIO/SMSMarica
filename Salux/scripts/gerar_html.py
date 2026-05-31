"""Re-executa queries (como SUPERVISOR) e renderiza HTML.

Telas:
    cadastro-paciente   --paciente <cd>
    historico-paciente  --paciente <cd>
    ficha-paciente      --fia <nr>  [--ano 2026] [--hospital 1]
    prescricao-fia      --fia <nr>  [--ano 2026] [--hospital 1]
    sinais-vitais       --fia <nr>  [--ano 2026] [--hospital 1]
    plano-terapeutico   --fia <nr>  [--ano 2026] [--hospital 1]
    todas-do-paciente   --paciente <cd>          # cadastro + histórico
    todas-da-fia        --fia <nr> [--ano 2026]  # paciente + todas as do FIA
    indice                                       # gera index.html

PII: saídas em `saidas_html/` (gitignored).
"""
from __future__ import annotations

import argparse
import html
import pathlib
import sys

from conexao import executar_select

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_OUT = _RAIZ / "saidas_html"

# ============================================================================
# Estilo comum
# ============================================================================

_CSS = """
body { font-family: -apple-system, Segoe UI, sans-serif; padding: 2em; color: #222; max-width: 1200px; margin: 0 auto; }
h1 { color: #c00; border-bottom: 2px solid #c00; padding-bottom: 0.3em; }
h2 { color: #555; margin-top: 1.5em; border-bottom: 1px solid #ddd; padding-bottom: 0.2em; }
.ctx { background: #f5f5f5; padding: 0.5em 1em; border-radius: 4px; font-family: monospace; font-size: 13px; }
.grid { display: grid; grid-template-columns: 220px 1fr; gap: 6px 16px; margin: 1em 0; }
.grid .k { color: #888; font-size: 13px; text-align: right; padding-top: 4px; }
.grid .v { background: white; border: 1px solid #ddd; padding: 4px 8px; border-radius: 3px; min-height: 20px; }
.grid .v.empty { color: #bbb; font-style: italic; }
table { border-collapse: collapse; width: 100%; font-size: 13px; }
th { background: #c00; color: white; padding: 8px; text-align: left; font-weight: 600; }
td { padding: 6px 8px; border-bottom: 1px solid #eee; vertical-align: top; }
tr:nth-child(even) { background: #fafafa; }
.tag-F { background: #e74c3c; color: white; padding: 2px 8px; border-radius: 3px; font-weight: bold; }
.tag-B { background: #3498db; color: white; padding: 2px 8px; border-radius: 3px; font-weight: bold; }
.nodata { color: #999; font-style: italic; padding: 1em; }
.evolucao { background: #fff8e1; border-left: 4px solid #f39c12; padding: 1em; margin: 0.5em 0; border-radius: 0 4px 4px 0; }
.evolucao .meta { font-size: 12px; color: #666; margin-bottom: 0.5em; }
.evolucao .titulo { font-weight: bold; color: #444; margin-top: 0.5em; }
.evolucao .corpo { white-space: pre-wrap; margin-top: 0.2em; }
a.btn { display: inline-block; padding: 8px 16px; background: #c00; color: white; text-decoration: none; border-radius: 4px; margin: 4px; }
a.btn:hover { background: #900; }
"""


def _envelope(titulo: str, ctx: dict, corpo: str) -> str:
    ctx_str = " &middot; ".join(f"<b>{html.escape(k)}</b>={html.escape(str(v))}" for k, v in ctx.items())
    return (
        f"<!doctype html><html><head><meta charset='utf-8'><title>{html.escape(titulo)}</title>"
        f"<style>{_CSS}</style></head><body>"
        f"<h1>{html.escape(titulo)}</h1>"
        f"<div class='ctx'>{ctx_str}</div>"
        f"{corpo}"
        "</body></html>"
    )


def _render_tabela(linhas: list[list[str]], colunas: list[str]) -> str:
    if not linhas:
        return f"<p class='nodata'>Nenhum registro encontrado.</p>"
    out = ["<table><thead><tr>"]
    out += [f"<th>{html.escape(c)}</th>" for c in colunas]
    out.append("</tr></thead><tbody>")
    for linha in linhas:
        out.append("<tr>")
        for c, v in zip(colunas, linha):
            v_esc = html.escape(str(v)) if v else "—"
            if c == "ID_TIPO":
                cls = "tag-F" if v == "F" else "tag-B"
                rotulo = "FIA" if v == "F" else ("BAA" if v == "B" else v)
                out.append(f"<td><span class='{cls}'>{rotulo}</span></td>")
            else:
                out.append(f"<td>{v_esc}</td>")
        out.append("</tr>")
    out.append("</tbody></table>")
    return "".join(out)


# ============================================================================
# Tela: cadastro-paciente
# ============================================================================

_SQL_CADASTRO = """
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

_COLS_CADASTRO = [
    "CD_PACIENTE", "NM_PACIENTE", "SC_APELIDO", "SEXO", "DT_NASCIMENTO",
    "CPF_PACIENTE", "RG_PACIENTE", "CNS", "ESTADO_CIVIL",
    "NM_MAE", "NM_PAI", "NM_CONJUGE",
    "NM_LOGRADOURO", "NR_LOGRADOURO", "COMPL_LOGRADOURO", "BAIRRO",
    "CEP", "CD_UF", "CD_CIDADE",
    "NR_DDD_FONE", "NR_FONE", "NR_FONE_COMPL", "EMAIL",
    "PROFISSAO", "RELIGIAO",
    "PESO", "ALTURA",
    "IN_ATIVO", "DT_CADASTRO", "DT_OBITO",
    "CD_PRONT_SGH", "CD_PRONT_CEM",
    "NM_CIDADE",
]


def render_cadastro(cd_paciente: int) -> str:
    linhas, _ = executar_select(_SQL_CADASTRO, {"pl_cd_paciente": cd_paciente}, modo="supervisor")
    if not linhas:
        return _envelope(f"Cadastro {cd_paciente}", {"cd_paciente": cd_paciente},
                         "<p class='nodata'>Paciente não encontrado.</p>")
    dados = dict(zip(_COLS_CADASTRO, linhas[0]))

    def kv(rotulo: str, chave: str) -> str:
        valor = dados.get(chave, "")
        classe = "v" if valor else "v empty"
        v = html.escape(valor) if valor else "—"
        return f"<div class='k'>{html.escape(rotulo)}</div><div class='{classe}'>{v}</div>"

    secoes = [
        ("Identificação", [
            ("CD Paciente", "CD_PACIENTE"), ("Nome", "NM_PACIENTE"), ("Apelido", "SC_APELIDO"),
            ("Sexo", "SEXO"), ("Nascimento", "DT_NASCIMENTO"),
            ("CPF", "CPF_PACIENTE"), ("RG", "RG_PACIENTE"), ("CNS", "CNS"),
            ("Estado civil", "ESTADO_CIVIL"), ("Ativo", "IN_ATIVO"), ("Data óbito", "DT_OBITO"),
        ]),
        ("Filiação", [("Mãe", "NM_MAE"), ("Pai", "NM_PAI"), ("Cônjuge", "NM_CONJUGE")]),
        ("Endereço", [
            ("Logradouro", "NM_LOGRADOURO"), ("Número", "NR_LOGRADOURO"),
            ("Complemento", "COMPL_LOGRADOURO"), ("Bairro", "BAIRRO"),
            ("Cidade", "NM_CIDADE"), ("UF", "CD_UF"), ("CEP", "CEP"),
        ]),
        ("Contato", [
            ("DDD", "NR_DDD_FONE"), ("Telefone", "NR_FONE"),
            ("Complemento", "NR_FONE_COMPL"), ("E-mail", "EMAIL"),
        ]),
        ("Outros", [
            ("Profissão", "PROFISSAO"), ("Religião", "RELIGIAO"),
            ("Peso", "PESO"), ("Altura", "ALTURA"),
            ("Data cadastro", "DT_CADASTRO"),
            ("Prontuário SGH", "CD_PRONT_SGH"), ("Prontuário CEM", "CD_PRONT_CEM"),
        ]),
    ]
    body_parts = []
    for nome, campos in secoes:
        body_parts.append(f"<h2>{html.escape(nome)}</h2><div class='grid'>")
        for rot, ch in campos:
            body_parts.append(kv(rot, ch))
        body_parts.append("</div>")
    titulo = f"Cadastro do paciente {dados.get('NM_PACIENTE') or cd_paciente}"
    return _envelope(titulo, {"cd_paciente": cd_paciente}, "".join(body_parts))


# ============================================================================
# Tela: historico-paciente
# ============================================================================

_SQL_HISTORICO = """
SELECT fia.dt_baixa AS dt_entrada, fia.dt_ano_fia, fia.nr_fia,
       (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = fia.cd_medico) AS nm_medico_ent,
       fia.dt_alta,
       (SELECT c.sc_clinica FROM clinica c, fia_clinica fc
         WHERE c.cd_clinica = fc.cd_clinica AND fc.cd_hospital = fia.cd_hospital
           AND fc.dt_ano_fia = fia.dt_ano_fia AND fc.nr_fia = fia.nr_fia
           AND fc.dt_entra_clinica =
               (SELECT MAX(f.dt_entra_clinica) FROM fia_clinica f
                 WHERE f.cd_hospital = fc.cd_hospital AND f.nr_fia = fc.nr_fia
                   AND f.dt_ano_fia = fc.dt_ano_fia)
       ) AS ds_clinica,
       (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = fia.med_cd_medico) AS nm_medico_saida,
       fia.cd_cid, 'F' AS id_tipo, fia.cd_hospital
  FROM fia
 WHERE NVL(fia.cd_paciente_unificado, fia.cd_paciente) = :pl_cd_paciente
   AND fia.cd_hospital = :pl_cd_hospital
UNION ALL
SELECT baa.dt_atendimento, baa.dt_ano_baa, baa.nr_baa,
       (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = baa.cd_medico) AS nm_medico_ent,
       baa.dt_saida,
       (SELECT e.ds_especialidade FROM especialidade e WHERE e.cd_especialidade = baa.cd_especialidade) AS ds_clinica,
       (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = baa.cd_medico) AS nm_medico_saida,
       baa.cd_cid, 'B' AS id_tipo, baa.cd_hospital
  FROM baa
 WHERE NVL(baa.cd_paciente_unificado, baa.cd_paciente) = :pl_cd_paciente
   AND baa.cd_hospital = :pl_cd_hospital
ORDER BY 1 DESC, 5 DESC
"""

_COLS_HISTORICO = [
    "DT_ENTRADA", "DT_ANO", "NR_DOC", "NM_MEDICO_ENT",
    "DT_ALTA", "DS_CLINICA", "NM_MEDICO_SAIDA", "CD_CID", "ID_TIPO", "CD_HOSPITAL",
]


def render_historico(cd_paciente: int, cd_hospital: int = 1) -> str:
    linhas, _ = executar_select(
        _SQL_HISTORICO,
        {"pl_cd_paciente": cd_paciente, "pl_cd_hospital": cd_hospital},
        modo="supervisor",
    )
    corpo = _render_tabela(linhas, _COLS_HISTORICO)
    titulo = f"Histórico do paciente {cd_paciente}"
    return _envelope(titulo, {"cd_paciente": cd_paciente, "cd_hospital": cd_hospital}, corpo)


# ============================================================================
# Telas: por FIA
# ============================================================================


def _consultar_paciente_da_fia(nr_fia: int, ano: int, hospital: int):
    linhas, _ = executar_select(
        """
        SELECT NVL(f.cd_paciente_unificado, f.cd_paciente), p.nm_paciente
          FROM fia f, paciente p
         WHERE f.cd_hospital = :h AND f.dt_ano_fia = :a AND f.nr_fia = :n
           AND p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
        """,
        {"h": hospital, "a": ano, "n": nr_fia}, modo="supervisor",
    )
    if not linhas:
        return None, None
    return int(linhas[0][0]), linhas[0][1]


def render_ficha_paciente(nr_fia: int, ano: int, hospital: int) -> str:
    cd_pac, nome = _consultar_paciente_da_fia(nr_fia, ano, hospital)
    sql = """
    SELECT fp.cd_ficha_pac, fp.cd_perfil, fp.cd_funcionario, fp.cd_paciente,
           fp.cd_hospital, fp.dt_ano_fia, fp.nr_fia,
           fp.dt_preenchimento, fp.dt_abertura
      FROM ficha_paciente fp
     WHERE fp.cd_hospital = :h AND fp.dt_ano_fia = :a AND fp.nr_fia = :n
     ORDER BY fp.dt_preenchimento DESC
    """
    cols = ["CD_FICHA_PAC", "CD_PERFIL", "CD_FUNCIONARIO", "CD_PACIENTE",
            "CD_HOSPITAL", "DT_ANO_FIA", "NR_FIA",
            "DT_PREENCHIMENTO", "DT_ABERTURA"]
    linhas, _ = executar_select(sql, {"h": hospital, "a": ano, "n": nr_fia}, modo="supervisor")
    corpo = _render_tabela(linhas, cols)
    titulo = f"Ficha do paciente — FIA {nr_fia}/{ano}"
    if nome:
        titulo += f" — {nome}"
    return _envelope(titulo, {"nr_fia": nr_fia, "dt_ano_fia": ano, "cd_hospital": hospital, "cd_paciente": cd_pac}, corpo)


def render_prescricao_fia(nr_fia: int, ano: int, hospital: int) -> str:
    cd_pac, nome = _consultar_paciente_da_fia(nr_fia, ano, hospital)
    sql = """
    SELECT pr.nr_prescricao,
           pr.id_tipo_prescricao,
           pr.dt_prescricao, pr.dt_inicio, pr.dt_fim,
           pr.cd_medico,
           (SELECT m.nm_medico FROM medico m WHERE m.cd_medico = pr.cd_medico) AS nm_medico,
           pr.nm_resp_dispensacao, pr.nm_resp_conferencia,
           pr.peso, pr.altura,
           pr.in_ass_digital, pr.in_aprazamento, pr.in_presc_impressa, pr.in_gerou_requisicao,
           pr.historico_prescricao
      FROM prescricao_fia pr
     WHERE pr.cd_hospital = :h AND pr.dt_ano_fia = :a AND pr.nr_fia = :n
     ORDER BY pr.dt_prescricao DESC, pr.nr_prescricao DESC
    """
    cols = ["NR_PRESCRICAO", "TIPO", "DT_PRESCRICAO", "DT_INICIO", "DT_FIM",
            "CD_MEDICO", "NM_MEDICO", "RESP_DISPENSACAO", "RESP_CONFERENCIA",
            "PESO", "ALTURA", "ASS_DIG", "APRAZ", "IMPRESSA", "REQUISICAO",
            "HISTORICO_PRESCRICAO"]
    linhas, _ = executar_select(sql, {"h": hospital, "a": ano, "n": nr_fia}, modo="supervisor")
    legenda = (
        "<p style='font-size:13px;color:#666'>"
        "Tipos: <code>M</code>=médica · <code>E</code>=enfermagem · <code>L</code>=multiprofissional/livre · etc. "
        "(Heurística — confirmar via tabela de domínio.)"
        "</p>"
    )
    corpo = legenda + _render_tabela(linhas, cols)
    titulo = f"Prescrição médica — FIA {nr_fia}/{ano}"
    if nome:
        titulo += f" — {nome}"
    return _envelope(titulo, {"nr_fia": nr_fia, "dt_ano_fia": ano, "cd_hospital": hospital, "cd_paciente": cd_pac}, corpo)


def render_sinais_vitais(nr_fia: int, ano: int, hospital: int) -> str:
    cd_pac, nome = _consultar_paciente_da_fia(nr_fia, ano, hospital)
    sql = """
    SELECT sv.dthr_visita,
           sv.cd_funcionario,
           sv.peso, sv.altura,
           sv.vl_temp_aux       AS temp_axilar,
           sv.vl_pa_alta        AS pa_sist,
           sv.vl_pa_baixa       AS pa_diast,
           sv.vl_freq_cardio    AS fc,
           sv.vl_respiracao     AS fr,
           sv.vl_saturacao_oxigenio AS spo2,
           sv.vl_glicose        AS glicemia,
           sv.vl_pa_media       AS pa_media,
           sv.vl_via_oral       AS via_oral_ml,
           sv.vl_urina          AS urina_ml,
           sv.vl_soro           AS soro_ml,
           sv.id_tipo
      FROM sinais_vitais sv
     WHERE sv.cd_hospital = :h
       AND sv.dt_ano_fia_baa = :a
       AND sv.nr_fia_baa = :n
       AND sv.id_tipo = 'F'
     ORDER BY sv.dthr_visita DESC
    """
    cols = ["DTHR_VISITA", "CD_FUNC", "PESO", "ALTURA",
            "TEMP", "PA_SIST", "PA_DIAST", "FC", "FR", "SPO2",
            "GLICEMIA", "PA_MED", "VIA_ORAL", "URINA", "SORO", "ID_TIPO"]
    linhas, _ = executar_select(sql, {"h": hospital, "a": ano, "n": nr_fia}, modo="supervisor")
    corpo = _render_tabela(linhas, cols)
    titulo = f"Sinais vitais — FIA {nr_fia}/{ano}"
    if nome:
        titulo += f" — {nome}"
    return _envelope(titulo, {"nr_fia": nr_fia, "dt_ano_fia": ano, "cd_hospital": hospital, "cd_paciente": cd_pac}, corpo)


def render_plano_terapeutico(nr_fia: int, ano: int, hospital: int) -> str:
    cd_pac, nome = _consultar_paciente_da_fia(nr_fia, ano, hospital)
    sql = """
    SELECT pt.sq_plano_terapeutico_medico,
           pt.dt_evolucao, pt.dt_cadastro,
           pt.cd_func_cadastro,
           pt.hipotese_diagnostica,
           pt.investigacao_conduta,
           pt.in_ativo
      FROM plano_terapeutico_medico pt
     WHERE pt.cd_hospital = :h AND pt.dt_ano_fia = :a AND pt.nr_fia = :n
     ORDER BY pt.dt_evolucao DESC NULLS LAST, pt.dt_cadastro DESC
    """
    cols = ["SQ", "DT_EVOLUCAO", "DT_CADASTRO", "CD_FUNC",
            "HIPOTESE_DIAGNOSTICA", "INVESTIGACAO_CONDUTA", "ATIVO"]
    linhas, _ = executar_select(sql, {"h": hospital, "a": ano, "n": nr_fia}, modo="supervisor")
    if not linhas:
        corpo = "<p class='nodata'>Sem evolução médica registrada.</p>"
    else:
        partes = []
        for linha in linhas:
            d = dict(zip(cols, linha))
            ativo_badge = "" if d.get("ATIVO") == "S" else " <em>(inativo)</em>"
            partes.append(
                f"<div class='evolucao'>"
                f"<div class='meta'>SQ {html.escape(d.get('SQ',''))} · "
                f"evolução: {html.escape(d.get('DT_EVOLUCAO','—'))} · "
                f"cadastro: {html.escape(d.get('DT_CADASTRO','—'))} · "
                f"func: {html.escape(d.get('CD_FUNC','—'))}{ativo_badge}</div>"
                f"<div class='titulo'>Hipótese diagnóstica</div>"
                f"<div class='corpo'>{html.escape(d.get('HIPOTESE_DIAGNOSTICA') or '—')}</div>"
                f"<div class='titulo'>Investigação / conduta</div>"
                f"<div class='corpo'>{html.escape(d.get('INVESTIGACAO_CONDUTA') or '—')}</div>"
                f"</div>"
            )
        corpo = "".join(partes)
    titulo = f"Plano terapêutico / evolução médica — FIA {nr_fia}/{ano}"
    if nome:
        titulo += f" — {nome}"
    return _envelope(titulo, {"nr_fia": nr_fia, "dt_ano_fia": ano, "cd_hospital": hospital, "cd_paciente": cd_pac}, corpo)


# ============================================================================
# Índice
# ============================================================================


def render_indice() -> str:
    arquivos = sorted(_OUT.glob("*.html"))
    arquivos = [a for a in arquivos if a.name != "index.html"]
    if not arquivos:
        body = "<p class='nodata'>Nada gerado ainda.</p>"
    else:
        items = []
        for a in arquivos:
            items.append(f"<li><a href='{a.name}'>{html.escape(a.name)}</a></li>")
        body = "<ul>" + "".join(items) + "</ul>"
    return _envelope("Telas Salux renderizadas", {"qtd": len(arquivos)}, body)


# ============================================================================
# CLI
# ============================================================================


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("tela", choices=[
        "cadastro-paciente", "historico-paciente",
        "ficha-paciente", "prescricao-fia", "sinais-vitais", "plano-terapeutico",
        "todas-do-paciente", "todas-da-fia",
        "indice",
    ])
    p.add_argument("--paciente", type=int, help="cd_paciente")
    p.add_argument("--fia", type=int, help="nr_fia")
    p.add_argument("--ano", type=int, default=2026)
    p.add_argument("--hospital", type=int, default=1)
    args = p.parse_args()

    _OUT.mkdir(exist_ok=True)
    gerados: list[pathlib.Path] = []

    def gera(nome: str, conteudo: str) -> None:
        arq = _OUT / f"{nome}.html"
        arq.write_text(conteudo, encoding="utf-8")
        print(f"gerado: {arq}")
        gerados.append(arq)

    if args.tela == "indice":
        gera("index", render_indice())
        return 0

    if args.tela in ("cadastro-paciente", "todas-do-paciente") or args.tela == "todas-da-fia":
        pass  # validations below

    if args.tela == "cadastro-paciente":
        if not args.paciente: print("--paciente obrigatório"); return 2
        gera(f"cadastro-paciente-{args.paciente}", render_cadastro(args.paciente))
    elif args.tela == "historico-paciente":
        if not args.paciente: print("--paciente obrigatório"); return 2
        gera(f"historico-paciente-{args.paciente}", render_historico(args.paciente, args.hospital))
    elif args.tela == "ficha-paciente":
        if not args.fia: print("--fia obrigatório"); return 2
        gera(f"ficha-fia-{args.fia}-{args.ano}", render_ficha_paciente(args.fia, args.ano, args.hospital))
    elif args.tela == "prescricao-fia":
        if not args.fia: print("--fia obrigatório"); return 2
        gera(f"prescricao-fia-{args.fia}-{args.ano}", render_prescricao_fia(args.fia, args.ano, args.hospital))
    elif args.tela == "sinais-vitais":
        if not args.fia: print("--fia obrigatório"); return 2
        gera(f"sinais-vitais-fia-{args.fia}-{args.ano}", render_sinais_vitais(args.fia, args.ano, args.hospital))
    elif args.tela == "plano-terapeutico":
        if not args.fia: print("--fia obrigatório"); return 2
        gera(f"plano-terapeutico-fia-{args.fia}-{args.ano}", render_plano_terapeutico(args.fia, args.ano, args.hospital))
    elif args.tela == "todas-do-paciente":
        if not args.paciente: print("--paciente obrigatório"); return 2
        gera(f"cadastro-paciente-{args.paciente}", render_cadastro(args.paciente))
        gera(f"historico-paciente-{args.paciente}", render_historico(args.paciente, args.hospital))
    elif args.tela == "todas-da-fia":
        if not args.fia: print("--fia obrigatório"); return 2
        cd_pac, _ = _consultar_paciente_da_fia(args.fia, args.ano, args.hospital)
        if cd_pac:
            gera(f"cadastro-paciente-{cd_pac}", render_cadastro(cd_pac))
            gera(f"historico-paciente-{cd_pac}", render_historico(cd_pac, args.hospital))
        gera(f"ficha-fia-{args.fia}-{args.ano}", render_ficha_paciente(args.fia, args.ano, args.hospital))
        gera(f"prescricao-fia-{args.fia}-{args.ano}", render_prescricao_fia(args.fia, args.ano, args.hospital))
        gera(f"sinais-vitais-fia-{args.fia}-{args.ano}", render_sinais_vitais(args.fia, args.ano, args.hospital))
        gera(f"plano-terapeutico-fia-{args.fia}-{args.ano}", render_plano_terapeutico(args.fia, args.ano, args.hospital))

    # Regenera índice ao final
    gera("index", render_indice())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
