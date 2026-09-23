"""CRIA uma requisição de mamografia no SISCAN — a partir de dados já existentes.

**ESTA FERRAMENTA ESCREVE EM PRODUÇÃO FEDERAL.** Por padrão ela é um ensaio: monta
tudo, imprime o POST exato e **não envia**. Só com `--confirmar` ela clica Salvar, e
só então a trava do cliente libera a palavra "salvar" — todo o resto continua barrado.

Cada execução com `--confirmar` exige OK explícito do Bernardo para AQUELE caso.

    python criar_requisicao.py --caso 675273965              # ensaio
    python criar_requisicao.py --caso 675273965 --confirmar  # cria de verdade

O caso vem de `casos.py` (fora do git — tem PII). Nenhum dado de paciente mora aqui.
"""

from __future__ import annotations

import argparse
import sys
from datetime import date

from siscan import exame, requisicao as req
from siscan.client import SiscanClient, mensagens
from siscan.inspecao import titulos


def montar(c: SiscanClient, caso: dict):
    """Percorre o assistente até a requisição preenchida, sem salvar."""
    doc = exame.abrir(c)
    doc = req.novo_exame(c, doc)

    doc = req.digitar_cartao_sus(c, doc, caso["cns"], "cria-cns")
    nome = doc.find(attrs={"name": "frm:nome"})
    if not (nome and nome.get("value")):
        raise RuntimeError("o CADSUS não trouxe o paciente — CNS errado?")
    print(f"   paciente ...........: {nome.get('value')}")

    doc = req.marcar_tipo_exame(c, doc, "01", "cria-tipo")
    idx_unidade = req.unidade_por_cnes(doc, caso["cnes_unidade"])
    print(f"   unidade CNES {caso['cnes_unidade']} .: índice {idx_unidade} "
          f"({dict(req.unidades(doc))[idx_unidade]})")

    doc = req.avancar(c, doc, idx_unidade, "01", nome="cria-avancar")
    if "SOLICITAR" not in " ".join(titulos(doc)).upper():
        raise RuntimeError(f"não cheguei na etapa 2 — tela: {titulos(doc)}")

    tipo_mamo = req.tipo_mamografia_por_idade(caso["nascimento"])
    doc = req.marcar_tipo_mamografia(c, doc, tipo_mamo, "cria-mamo")
    print(f"   tipo de mamografia .: {tipo_mamo} ({req.TIPO_MAMOGRAFIA[tipo_mamo]}) "
          f"— pela idade em {date.today():%d/%m/%Y}")

    idx_resp = req.responsavel_por_cns(doc, caso["cns_responsavel"])
    if idx_resp is None:
        raise RuntimeError(
            f"o responsável (CNS {caso['cns_responsavel']}) NÃO está na lista desta "
            f"unidade para {req.TIPO_MAMOGRAFIA[tipo_mamo].lower()}. "
            f"Opções: {req.responsaveis(doc)}")
    doc = req.escolher_responsavel(c, doc, idx_resp, "cria-resp")
    conselho = doc.find(attrs={"name": "frm:conselho"})
    print(f"   responsável ........: índice {idx_resp} "
          f"({dict(req.responsaveis(doc)).get(idx_resp)}) → conselho derivado "
          f"{conselho.get('value')!r}")
    return doc, tipo_mamo


def payload(caso: dict, tipo_mamo: str) -> dict[str, str]:
    """O que vai no POST do Salvar.

    Todo valor que só foi ao bean por A4J precisa ser REENVIADO aqui — ver a
    armadilha do radio em FLUXO-NOVA-REQUISICAO.md §5. `frm:conselho` NÃO entra:
    é derivado e vem disabled, então o navegador também não posta.
    """
    p = {
        "frm:prontuario": caso["prontuario"],
        "frm:localizacaoNodulo": caso["nodulo"],
        "frm:j_id91": caso["risco_elevado"],
        "frm:mamasExaminadas": caso["mamas_examinadas"],
        "frm:j_id105": caso["fez_mamografia"],
        "frm:j_id121": caso["radioterapia"],
        "frm:j_id151": caso["cirurgia"],
        "frm:j_id242": tipo_mamo,
        "frm:dataSolicitacaoInputDate": caso["data_solicitacao"],
    }
    if tipo_mamo == "02":
        p["frm:tipoMamografiaRastreamento"] = caso["populacao_rastreamento"]
    if caso.get("ano_ultima_mamografia"):
        p["frm:anoUltimaMamografia"] = caso["ano_ultima_mamografia"]
    return p


ROTULOS = {
    "frm:prontuario": "Nº do Prontuário (nosso AccessionNumber — a ponte de volta)",
    "frm:localizacaoNodulo": "TEM NÓDULO OU CAROÇO NA MAMA? (01 Sim D · 02 Sim E · 04 Não)",
    "frm:j_id91": "APRESENTA RISCO ELEVADO? (01 Sim · 02 Não · 03 Não Sabe)",
    "frm:mamasExaminadas": "MAMAS JÁ EXAMINADAS ANTES? (01 Sim · 02 Nunca · 03 Não Sabe)",
    "frm:j_id105": "FEZ MAMOGRAFIA ALGUMA VEZ? (01 Sim · 02 Não · 03 Não Sabe)",
    "frm:j_id121": "FEZ RADIOTERAPIA? (01 Sim · 02 Não · 03 Não Sabe)",
    "frm:j_id151": "FEZ CIRURGIA DE MAMA? (S · N)",
    "frm:j_id242": "TIPO DE MAMOGRAFIA (01 Diagnóstica · 02 Rastreamento)",
    "frm:tipoMamografiaRastreamento": "População (01 alvo · 02 risco familiar · 03 já tratada)",
    "frm:dataSolicitacaoInputDate": "Data da Solicitação",
    "frm:anoUltimaMamografia": "Ano da última mamografia",
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--caso", required=True)
    ap.add_argument("--confirmar", action="store_true",
                    help="CLICA SALVAR. Cria a requisição em produção federal.")
    args = ap.parse_args()

    import casos
    caso = casos.CASOS[args.caso]

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        print(f"== montando a requisição do caso {args.caso}")
        doc, tipo_mamo = montar(c, caso)

        extras = payload(caso, tipo_mamo)
        print("\n== POST do Salvar — o que vai exatamente:")
        for k, v in extras.items():
            print(f"   {k:<36} = {v!r:<14} {ROTULOS.get(k, '')}")
        print(f"   {'frm:btSalvar':<36} = (clique)")

        if not args.confirmar:
            print("\nENSAIO — nada foi enviado. Repita com --confirmar para criar.")
            return 0

        extras["frm:btSalvar"] = "frm:btSalvar"
        r = c.post_form(doc, "frm", extras, "SALVOU", permitir=("salvar",))
        resposta = c.sopa(r)
        print(f"\n== RESPOSTA (HTTP {r.status_code})")
        print("   títulos:", titulos(resposta))
        for m in dict.fromkeys(m for m in mensagens(resposta) if len(m) < 300):
            print("   !", m)
        return 0


if __name__ == "__main__":
    sys.exit(main())
