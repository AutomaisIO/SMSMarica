"""Relê requisições no SISCAN pelo PROTOCOLO e grava, por requisição, TODAS as respostas da etapa 2
em JSON — para comparar com o que o nosso modelo (SiscanRequisicaoMapper) geraria da anamnese.

Pergunta (16/10/2026): validar o modelo contra o que a operação lançou à mão, por faixa de idade.
Somente leitura: pesquisa, abre "Alterar/Visualizar" e volta — nunca Salva.

Não grava nem imprime nome/CNS: só respostas (códigos da tela) e anos. A saída vai para o arquivo
de --saida, fora do repositório.

    python reler_requisicoes.py --de 01/03/2026 --ate 16/10/2026 --saida <arq.json> 140267119 141072673
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import unicodedata

from siscan import exame
from siscan.client import SiscanClient, mensagens

RE_NUM = re.compile(r"listaExamePaginada:(\d+):")
RE_ANO = re.compile(r"^frm:ano\w+$")

# Pergunta → começo da legenda (sem acento, caixa alta). Os j_idNN mudam; a legenda não.
LEGENDAS = {
    "risco": "APRESENTA RISCO ELEVADO",
    "fez_mamografia": "FEZ MAMOGRAFIA ALGUMA VEZ",
    "radioterapia": "FEZ RADIOTERAPIA",
    "cirurgia": "FEZ CIRURGIA DE MAMA",
    "tipo": "TIPO DE MAMOGRAFIA",
}
# Campos de nome estável lidos direto.
ESTAVEIS = {"nodulo": "frm:localizacaoNodulo", "mamas_examinadas": "frm:mamasExaminadas",
            "populacao": "frm:tipoMamografiaRastreamento"}
# A diagnóstica: indicações + sub-campos (FLUXO §6b-2).
RE_DIAGNOSTICA = re.compile(
    r"^frm:(achadosExameClinico|controleRadiologicoLesao|lesaoDiagnosticoCancer|avaliacaoRespostaQuimioterapia"
    r"|revisaoMamografia|controleLesao|lesaoPapilar\w+|descargaPapilar\w*|noduloLocalizacao\w+"
    r"|espessamentoLcalizacao\w+|linfonodoPalpavel\w+|radiologico\w+|lesaoCancer\w+|controleLesao\w+"
    r"|avaliacaoRespostaQuimiNeo|revisaoCategoria\w+)$")


def asc(s: str) -> str:
    s = unicodedata.normalize("NFD", s or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def marcados(inputs) -> list[str]:
    return [i.get("value") or "on" for i in inputs if i.has_attr("checked")]


def ler(doc) -> dict:
    f = doc.find("form", id="frm") or doc
    lido: dict = {}
    for chave, nome in ESTAVEIS.items():
        lido[chave] = marcados(f.find_all("input", attrs={"name": nome}))

    for fs in f.find_all("fieldset"):
        lg = fs.find("legend")
        if not lg:
            continue
        texto = asc(lg.get_text(" ", strip=True))
        chave = next((k for k, p in LEGENDAS.items() if texto.startswith(p)), None)
        if not chave or chave in lido:
            continue
        radios = fs.find_all("input", attrs={"type": "radio"})
        if not radios:
            continue
        principal = radios[0].get("name")
        lido[chave] = marcados(r for r in radios if r.get("name") == principal)
        if chave == "radioterapia":
            # A localização é outro grupo de radios dentro do mesmo fieldset.
            lido["radioterapia_local"] = marcados(r for r in radios if r.get("name") != principal)

    lido["anos"] = {i["name"]: i.get("value").strip() for i in f.find_all("input", attrs={"type": "text"})
                    if RE_ANO.match(i.get("name") or "") and (i.get("value") or "").strip()}
    lido["diagnostica"] = sorted(
        f"{i['name']}={i.get('value') or 'on'}" for i in f.find_all("input")
        if RE_DIAGNOSTICA.match(i.get("name") or "") and i.get("type") in ("checkbox", "radio")
        and i.has_attr("checked"))
    return lido


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("protocolos", nargs="+")
    ap.add_argument("--de", required=True)
    ap.add_argument("--ate", required=True)
    ap.add_argument("--saida", required=True)
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    saida: dict[str, dict] = {}
    with SiscanClient(somente_leitura=True, capturar=False) as c:
        c.login()
        doc = exame.abrir(c)
        for protocolo in args.protocolos:
            linhas = []
            for status in (exame.REQUISITADO, exame.COM_RESULTADO, exame.LIBERADO):
                doc = exame.pesquisar(c, exame.Filtro(
                    mamografia=True, status=status, numero_protocolo=protocolo,
                    data_inicio=args.de, data_fim=args.ate), doc)
                _, linhas = exame.grade(doc)
                if linhas:
                    break
            if len(linhas) != 1:
                saida[protocolo] = {"erro": f"{len(linhas)} linha(s)", "mensagens": list(dict.fromkeys(mensagens(doc)))[:2]}
                print(f"{protocolo}: {len(linhas)} linha(s)")
                continue

            num = RE_NUM.search(list(linhas[0].acoes.values())[0]).group(1)
            req = exame.abrir_acao(c, doc, num, exame.ACAO_ALTERAR_REQUISICAO, "reler")
            saida[protocolo] = ler(req)
            doc = c.sopa(c.post_form(req, "frm", {"frm:botaoVoltar": "frm:botaoVoltar"}, "voltar"))
            print(f"{protocolo}: ok")

    with open(args.saida, "w", encoding="utf-8") as fh:
        json.dump(saida, fh, ensure_ascii=False, indent=1)
    print(f"{len(saida)} relidas → {args.saida}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
