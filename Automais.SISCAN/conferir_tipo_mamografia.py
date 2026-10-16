"""Relê no SISCAN requisições criadas pela plataforma e mostra o que ficou GRAVADO no bloco do
tipo de mamografia. Somente leitura: pesquisa, abre "Alterar/Visualizar" e volta — nunca Salva.

Pergunta que responde (16/10/2026): a operação relatou requisição "indo sem ligar a flag" e sem
os dados de rastreamento/diagnóstica. Aqui se vê, por requisição, se o radio TIPO DE MAMOGRAFIA
está marcado, se a população do rastreamento está marcada e quais indicações da diagnóstica.

Não imprime nome nem CNS — só o prontuário (nosso accession) e as respostas.

    python conferir_tipo_mamografia.py --de 01/10/2026 --ate 26/10/2026 2610051103 2610051297
    python conferir_tipo_mamografia.py --por protocolo --de 01/05/2026 --ate 16/10/2026 140267119

Por PROTOCOLO serve também para as requisições lançadas À MÃO (o prontuário delas não é o nosso
accession). A diagnóstica sai com o detalhe: toda marcação dentro do bloco (indicação + sub-campos).
"""

from __future__ import annotations

import argparse
import re
import sys
import unicodedata

from siscan import exame
from siscan.client import SiscanClient, mensagens

RE_NUM = re.compile(r"listaExamePaginada:(\d+):")

# Legenda do fieldset (sem acento, caixa alta) → rótulo curto do relatório.
PERGUNTAS = {
    "TIPO DE MAMOGRAFIA": "tipo",
    "MAMOGRAFIA DE RASTREAMENTO": "populacao_rastreamento",
    "DIAGNOSTICA": "diagnostica",
    "TEM NODULO OU CAROCO": "nodulo",
    "APRESENTA RISCO ELEVADO": "risco_elevado",
    "FEZ MAMOGRAFIA ALGUMA VEZ": "fez_mamografia",
}
TIPO = {"01": "Diagnóstica", "02": "Rastreamento"}
POPULACAO = {"01": "População alvo", "02": "Risco elevado (hist. familiar)", "03": "Já tratada"}


def asc(s: str) -> str:
    s = unicodedata.normalize("NFD", s or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def ler(doc) -> dict[str, list[str]]:
    """Por pergunta, os valores MARCADOS dentro do fieldset — só o primeiro fieldset de cada."""
    lido: dict[str, list[str]] = {}
    for fs in doc.find_all("fieldset"):
        lg = fs.find("legend")
        if not lg:
            continue
        texto = asc(lg.get_text(" ", strip=True))
        chave = next((v for k, v in PERGUNTAS.items() if texto.startswith(k)), None)
        if not chave or chave in lido:
            continue
        if chave == "tipo":
            # O fieldset do tipo CONTÉM os da população/diagnóstica: aqui só o radio dele.
            radios = fs.find_all("input", attrs={"type": "radio"})
            nome = radios[0].get("name") if radios else None
            lido[chave] = [i.get("value") for i in radios if i.get("name") == nome and i.has_attr("checked")]
        elif chave == "diagnostica":
            # Indicação + sub-campos (lado, quadrante, achado, BI-RADS) — tudo o que está marcado.
            lido[chave] = [i.get("name", "").replace("frm:", "")
                           + ("" if (i.get("value") or "on") in ("on", "S") else f"={i.get('value')}")
                           for i in fs.find_all("input") if i.get("type") in ("checkbox", "radio")
                           and i.has_attr("checked")]
        else:
            lido[chave] = [i.get("value") for i in fs.find_all("input") if i.has_attr("checked")]
    return lido


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("chaves", nargs="+", help="prontuários (nosso accession) ou protocolos")
    ap.add_argument("--por", choices=("prontuario", "protocolo"), default="prontuario")
    ap.add_argument("--de", required=True, help="dd/mm/aaaa — data da solicitação no SISCAN")
    ap.add_argument("--ate", required=True)
    args = ap.parse_args()

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        doc = exame.abrir(c)
        for pront in args.chaves:
            linhas = []
            for status in (exame.REQUISITADO, exame.COM_RESULTADO, exame.LIBERADO):
                doc = exame.pesquisar(c, exame.Filtro(
                    mamografia=True, status=status,
                    numero_prontuario=pront if args.por == "prontuario" else None,
                    numero_protocolo=pront if args.por == "protocolo" else None,
                    data_inicio=args.de, data_fim=args.ate), doc)
                _, linhas = exame.grade(doc)
                if linhas:
                    break
            if len(linhas) != 1:
                print(f"\n{pront}: pesquisa devolveu {len(linhas)} linha(s) — "
                      f"{list(dict.fromkeys(mensagens(doc)))[:2]}")
                continue

            num = RE_NUM.search(list(linhas[0].acoes.values())[0]).group(1)
            req = exame.abrir_acao(c, doc, num, exame.ACAO_ALTERAR_REQUISICAO, f"tipo-{pront}")
            lido = ler(req)
            doc = c.sopa(c.post_form(req, "frm", {"frm:botaoVoltar": "frm:botaoVoltar"}, "voltar"))

            tipo = lido.get("tipo") or []
            pop = lido.get("populacao_rastreamento")
            print(f"\n{pront} (nº exame {num})")
            print(f"   tipo de mamografia ....: {', '.join(TIPO.get(v, v) for v in tipo) or 'NENHUM MARCADO'}")
            print(f"   população rastreamento : "
                  f"{'(bloco ausente)' if pop is None else ', '.join(POPULACAO.get(v, v) for v in pop) or 'NENHUMA MARCADA'}")
            dg = lido.get("diagnostica")
            print(f"   indicação diagnóstica  : "
                  f"{'(bloco ausente)' if dg is None else ', '.join(dg) or 'NENHUMA MARCADA'}")
            print(f"   nódulo={lido.get('nodulo')} risco={lido.get('risco_elevado')} "
                  f"fez_mamo={lido.get('fez_mamografia')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
