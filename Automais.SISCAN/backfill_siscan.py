# -*- coding: utf-8 -*-
"""Lê as requisições que já foram lançadas À MÃO no SISCAN e prepara o backfill.

O problema: até 23/09/2026 a requisição de mamografia era digitada direto no
SISCAN pela unidade, enquanto a anamnese era preenchida aqui. Os dois lados nunca
se conheceram — a anamnese não sabe o número da requisição, e o nosso botão de
gerar, sem saber, criaria uma segunda.

A ponte é o **Cartão SUS**, que a grade do GERENCIAR EXAME mostra como coluna
(descoberto em 23/09/2026 — é o que torna este cruzamento possível sem abrir
requisição nenhuma).

    par = mesma paciente (CNS) + MESMO DIA da anamnese + única dos dois lados

"Mesmo dia" não é arbitrário: das 498 candidatas na mesma unidade, 483 caíam no
mesmo dia. Quem preenche a anamnese lança a requisição na sequência.

A unidade NÃO serve de chave: quem digita no SISCAN usa, com frequência,
"SECRETARIA MUNICIPAL DE SAUDE DE MARICA" ou outra USF. Serve de sinal.

ESTE SCRIPT NÃO ESCREVE NADA — nem aqui, nem no SISCAN. Ele produz o relatório
que o `aplicar_backfill.py` consome. A escrita em produção é decisão separada,
com OK explícito.

    python backfill_siscan.py --espelho <espelho.json> --saida <relatorio.json>

A saída tem dado clínico de paciente: SEMPRE fora do repositório.
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import json
import os
import re
import time
import unicodedata

import psycopg2

from siscan import exame
from siscan.client import SiscanClient

RE_SOL = re.compile(r"Solicita\w*:\s*(\d{2}/\d{2}/\d{4})")
RE_NUM = re.compile(r"listaExamePaginada:(\d+):")
RE_ANO_CIRURGIA = re.compile(r"^frm:ano(?!UltimaMamografia|Radioterapia)(.+?)(Direita|Esquerda)$")

# Os 13 tipos do SISCAN. A chave do nosso questionário foi escolhida para
# espelhar o nome do campo deles (`frm:anoMastectomiaDireita` -> `mastectomia`).
TIPOS_CIRURGIA = [
    "biopsiaCirurgicaIncisional", "biopsiaCirurgicaExcisional", "segmentectomia",
    "centralectomia", "dutectomia", "mastectomia", "mastectomiaPoupadoraPele",
    "mastectomiaPoupadoraPeleComplexoPapilar", "linfadenectomiaAxilar",
    "biopsiaLinfonodoSentinela", "reconstrucaoMamaria", "mastoplastiaRedutora",
    "inclusaoImplantes",
]
POR_NOME_SISCAN = {t[0].upper() + t[1:]: t for t in TIPOS_CIRURGIA}

# Os `name` são `j_idNN` auto-gerados e mudam quando o DATASUS recompila. Por
# isso cada pergunta é localizada pela LEGENDA do fieldset, nunca pelo id.
LEGENDAS = {
    "nodulo": "TEM NODULO OU CAROCO NA MAMA",
    "risco_elevado": "APRESENTA RISCO ELEVADO PARA",
    "mamas_examinadas": "ANTES DESTA CONSULTA, TEVE AS MAMAS EXAMINADAS",
    "fez_mamografia": "FEZ MAMOGRAFIA ALGUMA VEZ",
    "radioterapia": "FEZ RADIOTERAPIA NA MAMA",
    "cirurgia": "FEZ CIRURGIA DE MAMA",
    "tipo_mamografia": "TIPO DE MAMOGRAFIA",
}

UM = {"01": "sim", "02": "nao", "03": "naoSabe"}
LADO = {"01": "esquerda", "02": "direita", "03": "ambas"}


def asc(s: str) -> str:
    s = unicodedata.normalize("NFD", s or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


# ---------------------------------------------------------------- ler a tela
def ler_requisicao(doc) -> dict:
    """A requisição como ela está no SISCAN: respostas por pergunta + textos."""
    lido: dict = {"_campos": {}, "_perguntas": {}}

    for i in doc.find_all("input"):
        nome, valor = i.get("name"), i.get("value")
        if not nome:
            continue
        if i.has_attr("checked"):
            lido["_campos"].setdefault(nome, [])
            if isinstance(lido["_campos"][nome], list):
                lido["_campos"][nome].append(valor or "")
        elif i.get("type") == "text" and (valor or "").strip():
            lido["_campos"][nome] = valor.strip()

    for fs in doc.find_all("fieldset"):
        lg = fs.find("legend")
        if not lg:
            continue
        texto = asc(lg.get_text(" ", strip=True))
        chave = next((k for k, pref in LEGENDAS.items() if texto.startswith(pref)), None)
        if chave:
            marcados = [i.get("value") for i in fs.find_all("input") if i.has_attr("checked")]
            if marcados:
                lido["_perguntas"][chave] = marcados
        # O lado da radioterapia só existe quando a resposta é Sim, e vem num
        # fieldset à parte.
        if "LOCAL" in texto:
            m = [i.get("value") for i in fs.find_all("input") if i.has_attr("checked")]
            if m:
                lido["_perguntas"]["radioterapia_lado"] = m

    cirurgias = []
    for nome, valor in lido["_campos"].items():
        m = RE_ANO_CIRURGIA.match(nome)
        if m and isinstance(valor, str) and valor.strip():
            tipo = POR_NOME_SISCAN.get(m.group(1))
            if tipo:
                cirurgias.append({"tipo": tipo, "lado": m.group(2).lower(), "ano": valor.strip()})
    lido["_cirurgias"] = cirurgias
    return lido


# ------------------------------------------------------------------ o merge
def merge(conteudo: dict, lido: dict) -> tuple[dict, list[str], list[str]]:
    """Preenche SÓ as lacunas. Nunca sobrescreve o que a enfermeira respondeu aqui.

    Devolve (conteúdo novo, o que foi preenchido, divergências). Divergência não
    vira escrita: vira linha de relatório para olho humano. Sobrescrever uma
    resposta daqui com a de lá seria trocar uma verdade por outra sem ninguém ver.
    """
    novo = json.loads(json.dumps(conteudo))
    feitos: list[str] = []
    diverg: list[str] = []
    p, campos = lido["_perguntas"], lido["_campos"]

    def por(chave):
        v = p.get(chave)
        return v[0] if v else None

    sis = novo.setdefault("siscan", {})

    mex = por("mamas_examinadas")
    if not sis.get("mamasExaminadasAntes") and mex in ("01", "02"):
        sis["mamasExaminadasAntes"] = "sim" if mex == "01" else "nunca"
        feitos.append("siscan.mamasExaminadasAntes")

    ano = campos.get("frm:anoUltimaMamografia")
    if isinstance(ano, str) and ano.strip() and not (sis.get("anoUltimaMamografia") or "").strip():
        sis["anoUltimaMamografia"] = ano.strip()
        feitos.append("siscan.anoUltimaMamografia")

    radio = sis.setdefault(
        "radioterapia", {"resposta": None, "lado": None, "anoDireita": "", "anoEsquerda": ""})
    rt = por("radioterapia")
    if not radio.get("resposta") and rt in UM:
        radio["resposta"] = UM[rt]
        feitos.append("siscan.radioterapia")
        lado = por("radioterapia_lado")
        if rt == "01" and lado in LADO:
            radio["lado"] = LADO[lado]
        for chave, campo in (("anoDireita", "frm:anoRadioterapiaDireita"),
                             ("anoEsquerda", "frm:anoRadioterapiaEsquerda")):
            v = campos.get(campo)
            if isinstance(v, str) and v.strip():
                radio[chave] = v.strip()

    if not sis.get("cirurgias") and lido["_cirurgias"]:
        sis["cirurgias"] = lido["_cirurgias"]
        feitos.append("siscan.cirurgias (%d)" % len(lido["_cirurgias"]))

    sis.setdefault("responsavel", None)

    # --- o que nós JÁ respondemos: confere, não escreve
    hc = novo.get("historicoClinico") or {}

    nossa_mamo = (hc.get("jaRealizouMamografia") or {}).get("resposta")
    deles = por("fez_mamografia")
    if deles in ("01", "02") and nossa_mamo is not None:
        if (deles == "01") != bool(nossa_mamo):
            diverg.append("fez mamografia: nossa=%s · SISCAN=%s"
                          % (nossa_mamo, "Sim" if deles == "01" else "Nao"))

    nossa_cir = (hc.get("jaRealizouCirurgiaMamaria") or {}).get("resposta")
    protese = (hc.get("possuiProteseMamaria") or {}).get("resposta")
    deles_cir = por("cirurgia")
    if deles_cir in ("S", "N") and nossa_cir is not None:
        nosso = bool(nossa_cir) or bool(protese)
        if (deles_cir == "S") != nosso:
            diverg.append("fez cirurgia: nossa=%s · SISCAN=%s" % (nosso, deles_cir))

    nod = p.get("nodulo") or []
    q = ((novo.get("queixas") or {}).get("sintomas") or {}).get("noduloPalpavel") or {}
    if nod and "04" not in nod:
        for cod, lado_nosso in (("01", "direita"), ("02", "esquerda")):
            if cod in nod and not q.get(lado_nosso):
                diverg.append("nodulo %s: SISCAN marcou, nossa anamnese nao" % lado_nosso)

    return novo, feitos, diverg


# ---------------------------------------------------------------- o par
def conectar():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    mm = {k.strip().lower(): v.strip()
          for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=mm["host"], port=mm.get("port", 25060), dbname=mm["database"],
                            user=mm.get("username") or mm.get("user id"),
                            password=mm["password"], sslmode="require")


def pares_do_banco(espelho: list[dict]):
    por_cns = collections.defaultdict(list)
    for x in espelho:
        m = RE_SOL.search(x.get("Datas") or "")
        x["_d"] = dt.datetime.strptime(m.group(1), "%d/%m/%Y").date() if m else None
        por_cns[re.sub(r"\D", "", x.get("Cartão SUS") or "")].append(x)

    cur = conectar().cursor()
    cur.execute("""
      select e.id, e.accession_number, a.id, a.versao, a.conteudo_json,
             p.cns, p.cns_todos, p.nome,
             (a.criado_em at time zone 'America/Sao_Paulo')::date
      from smsmarica.anamnese a
      join smsmarica.exame_imagem e on e.id = a.exame_imagem_id
      join smsmarica.solicitacao s on s.id = e.solicitacao_id
      join fhir.patient p on p.id = s.paciente_id
      where a.excluido_em is null and e.excluido_em is null and e.siscan_protocolo is null
      order by 9""")

    pares, motivos, excecoes = [], collections.Counter(), []
    for eid, acc, aid, versao, conteudo, cns, todos, pnome, data in cur.fetchall():
        chaves = [re.sub(r"\D", "", k) for k in ([cns] + list(todos or [])) if k]
        cands = list({x["Protocolo"]: x for k in chaves for x in por_cns.get(k, [])}.values())
        dia = [x for x in cands if x["_d"] == data]

        if len(dia) != 1:
            # O que não casou não some: vira lista de trabalho. Backfill que
            # engole a dúvida em silêncio é pior que backfill nenhum.
            excecoes.append({
                "accession": acc, "paciente": pnome, "data_anamnese": data.isoformat(),
                "motivo": ("sem CNS" if not chaves
                           else "2+ requisicoes no mesmo dia" if len(dia) > 1
                           else "requisicao em outro dia" if cands
                           else "nenhuma requisicao no periodo"),
                "candidatas": [{"protocolo": x["Protocolo"], "numero": x["_numero_exame"],
                                "data": x["_d"].isoformat() if x["_d"] else None,
                                "dias": (x["_d"] - data).days if x["_d"] else None,
                                "unidade": x["Unidade Requisitante"], "status": x["Status"]}
                               for x in sorted(cands, key=lambda y: y["_d"] or dt.date.min)],
            })

        if not chaves:
            motivos["sem CNS"] += 1
        elif len(dia) == 1:
            motivos["par"] += 1
            pares.append({
                "exame_id": str(eid), "accession": acc, "anamnese_id": str(aid), "versao": versao,
                "conteudo": conteudo if isinstance(conteudo, dict) else json.loads(conteudo or "{}"),
                "paciente_nome": pnome,
                "cns": chaves[0], "cns_todos": chaves, "data": data.isoformat(),
                "protocolo": dia[0]["Protocolo"], "numero_exame": dia[0]["_numero_exame"],
                "status": dia[0]["Status"], "unidade_siscan": dia[0]["Unidade Requisitante"],
                "paciente_siscan": dia[0]["Paciente"], "_status_filtro": dia[0]["_status_filtro"]})
        elif len(dia) > 1:
            motivos["2+ no mesmo dia (humano)"] += 1
        elif cands:
            motivos["requisicao em outro dia (humano)"] += 1
        else:
            motivos["nenhuma requisicao (ainda nao foi)"] += 1

    # Um protocolo não pode servir a dois exames nossos.
    uso = collections.Counter(x["protocolo"] for x in pares)
    limpos = [x for x in pares if uso[x["protocolo"]] == 1]
    if len(limpos) != len(pares):
        motivos["protocolo disputado (descartado)"] += len(pares) - len(limpos)
    return limpos, motivos, excecoes


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--espelho", required=True)
    ap.add_argument("--saida", required=True)
    ap.add_argument("--limite", type=int, default=0, help="para ensaiar com poucas")
    ap.add_argument("--excecoes", help="onde gravar o que NAO casou (lista de trabalho humana)")
    args = ap.parse_args()

    espelho = json.load(open(args.espelho, encoding="utf-8"))
    pares, motivos, excecoes = pares_do_banco(espelho)
    print("cruzamento:")
    for k, v in motivos.most_common():
        print("  %-36s %4d" % (k, v))
    if args.excecoes:
        with open(args.excecoes, "w", encoding="utf-8") as fh:
            json.dump(excecoes, fh, ensure_ascii=False, indent=1)
        print("\n%d excecao(oes) -> %s" % (len(excecoes), args.excecoes))
    if args.limite:
        pares = pares[: args.limite]
    print("\nvou ler %d requisicao(oes) no SISCAN\n" % len(pares))

    relatorio, falhas = [], 0
    with SiscanClient(capturar=False) as c:
        t0 = time.time()
        c.login()
        doc = exame.abrir(c)
        print("login + menu: %.0fs" % (time.time() - t0))

        for n, par in enumerate(pares, 1):
            try:
                data_br = dt.date.fromisoformat(par["data"]).strftime("%d/%m/%Y")
                doc = exame.pesquisar(c, exame.Filtro(
                    mamografia=True, status=par["_status_filtro"],
                    data_inicio=data_br, data_fim=data_br,
                    numero_protocolo=par["protocolo"]), doc)
                _, linhas = exame.grade(doc)
                if len(linhas) != 1:
                    par["erro"] = "pesquisa por protocolo devolveu %d linha(s)" % len(linhas)
                    par.pop("conteudo", None)
                    relatorio.append(par)
                    falhas += 1
                    continue

                num = RE_NUM.search(list(linhas[0].acoes.values())[0]).group(1)
                req = exame.abrir_acao(c, doc, num, exame.ACAO_ALTERAR_REQUISICAO, "req")
                lido = ler_requisicao(req)
                doc = c.sopa(c.post_form(req, "frm",
                                         {"frm:botaoVoltar": "frm:botaoVoltar"}, "voltar"))

                cns_lido = re.sub(r"\D", "", str(lido["_campos"].get("frm:cartaoSUS", "")))
                novo, feitos, diverg = merge(par["conteudo"], lido)

                par.update({
                    "numero_exame": num,
                    "prontuario_no_siscan": lido["_campos"].get("frm:prontuario", ""),
                    "cns_confere": cns_lido in par.get("cns_todos", [par["cns"]]),
                    "cnes_unidade_siscan": lido["_campos"].get("frm:cnesUnidade", ""),
                    "data_solicitacao_siscan": lido["_campos"].get("frm:dataSolicitacaoInputDate", ""),
                    "lido": lido["_perguntas"],
                    "preenche": feitos,
                    "divergencias": diverg,
                    "conteudo_novo": novo,
                })
                par.pop("conteudo", None)
                relatorio.append(par)
            except Exception as e:  # noqa: BLE001 — uma falha não derruba o lote
                par["erro"] = "%s: %s" % (type(e).__name__, e)
                par.pop("conteudo", None)
                relatorio.append(par)
                falhas += 1

            if n % 25 == 0 or n == len(pares):
                print("  %d/%d · %.0fs · falhas: %d" % (n, len(pares), time.time() - t0, falhas),
                      flush=True)

    with open(args.saida, "w", encoding="utf-8") as fh:
        json.dump(relatorio, fh, ensure_ascii=False, indent=1)
    print("\n%d linha(s) -> %s" % (len(relatorio), args.saida))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
