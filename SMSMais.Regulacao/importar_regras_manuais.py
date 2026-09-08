"""
Importa as regras dos manuais CRECE/REUNI direto no banco, já limpas.

Por que fora do importador da tela: a tela importa o CSV como ele veio do spike e, e o CSV
carrega 288 linhas que não são regra — 280 delas cacos de uma frase só ("Observar os critérios
… de cada prestador e faixa etária"), que o extrator estilhaçou em três. Importadas, virariam
280 perguntas sem sentido na tela de quem abre a solicitação. Este script aplica a triagem que
o importador não tem como fazer sozinho.

Uso:
    python importar_regras_manuais.py            # simula, não grava
    python importar_regras_manuais.py --gravar   # grava, em transação única

Só grava se a tabela estiver vazia: a importação não é idempotente e rodar duas vezes
duplicaria tudo.
"""

import csv
import io
import json
import os
import re
import sys
import unicodedata
import uuid
from collections import Counter
from datetime import datetime, timezone

import psycopg2

RAIZ = os.path.dirname(os.path.abspath(__file__))
CSV = os.path.join(RAIZ, "revisoes", "spike-e-manual-regras.csv")

SER = 2                      # SistemaRegulacao.Ser
BLOQUEIA = 1                 # SeveridadeRegraRegulacao.Bloqueia
TIPO = {"Dedutivel": 1, "NaoDedutivel": 2, "Documental": 3, "Informativa": 4}
RESPOSTA = {"Sim": 1, "Nao": 2}

# A frase global do encaminhamento. É requisito de todo pedido, não regra de um procedimento —
# no CSV ela aparece 179 vezes descartada pelo extrator e 10 vezes escapada. Entra uma vez por
# procedimento porque `procedimento_id` é obrigatório: não existe regra global no schema.
DOC_ENCAMINHAMENTO = (
    "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER."
)


def normalizar(texto):
    """Mesma chave do importador em C#: sem acento, sem pontuação, caixa alta, espaço único."""
    n = unicodedata.normalize("NFD", texto or "")
    n = "".join(c for c in n if unicodedata.category(c) != "Mn")
    n = "".join(c for c in n if c.isalnum() or c == " ").upper().strip()
    return re.sub(" +", " ", n)


def motivo_de_descarte(texto):
    """
    O que NÃO é regra. Cada padrão aqui foi conferido contra os PDFs originais antes de virar
    filtro — nenhum é palpite sobre o formato.
    """
    n = normalizar(texto)

    if re.search(r"RECURSOS? REQUISITOS NECESS", n):
        return "cabeçalho da tabela do manual"

    if re.search(r"\b\d\.\d{1,2}(\.\d{1,2})?\.\s+[A-ZÁÉÍÓÚÂÊÔÃÕÇ]", texto):
        return "número de seção do manual"

    if "INSERIR NO SER O ENCAMINHAMENTO" in n:
        return "boilerplate do encaminhamento (entra uma vez por procedimento)"

    # "Observar os critérios de inclusão do paciente de acordo com cada prestador (unidade
    # executante)" e a gêmea de exclusão: o extrator as parte em três pedaços.
    if n in ("OBSERVAR OS", "OBSERVAR OS CRITERIOS", "OBSERVAR"):
        return 'caco de "Observar os critérios do prestador"'
    if re.match(r"^(DO|DE|DA|AO|E|OU) ", n) and re.search(
        r"PRESTADOR|FAIXA ETARIA|UNIDADE EXECUTANTE|ATENDIMENTO", n
    ):
        return 'caco de "Observar os critérios do prestador"'
    if re.match(r"^OBSERVAR (OS )?CRITERIOS", n):
        return 'instrução ao avaliador, não requisito de quem solicita'

    return None


def conectar():
    caminho = os.path.expandvars(
        r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json"
    )
    cs = json.load(io.open(caminho, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(p.split("=", 1) for p in cs.split(";") if "=" in p)
    return psycopg2.connect(
        host=d["Host"], port=d["Port"], dbname=d["Database"],
        user=d["Username"], password=d["Password"], sslmode="require",
    )


def main():
    gravar = "--gravar" in sys.argv
    cn = conectar()
    cur = cn.cursor()

    cur.execute("select count(*) from smsmarica.regulacao_regra")
    ja_existem = cur.fetchone()[0]
    if ja_existem and gravar:
        print(f"ABORTADO: já existem {ja_existem} regras. A importação não é idempotente.")
        return 1

    cur.execute(
        "select id, procedimento_id, rotulo_externo, ramo "
        "from smsmarica.regulacao_procedimento_origem where sistema = %s", (SER,)
    )
    indice = {}
    for origem_id, proc_id, rotulo, ramo in cur.fetchall():
        indice.setdefault(f"{normalizar(rotulo)}|{ramo}", (origem_id, proc_id))

    linhas = list(csv.DictReader(io.open(CSV, encoding="utf-8-sig"), delimiter=";"))

    agora = datetime.now(timezone.utc)
    regras, descartes, sem_par = [], Counter(), 0
    procedimentos = {}

    for r in linhas:
        rotulo = r["recurso_catalogo"].strip() or r["recurso"].strip()
        ramo = "AE" if r["ramo_ser"] == "AmbulatorioEstadual" else "NAO_AE"
        alvo = indice.get(f"{normalizar(rotulo)}|{ramo}")
        if not alvo:
            sem_par += 1
            continue

        motivo = motivo_de_descarte(r["texto_original"])
        if motivo:
            descartes[motivo] += 1
            continue

        origem_id, proc_id = alvo
        procedimentos[proc_id] = (origem_id, rotulo)

        tipo = TIPO[r["tipo"]]
        exclusao = r["secao"].lower() == "exclusao"
        texto = r["texto_original"].strip()

        regras.append((
            str(uuid.uuid4()), str(proc_id), str(origem_id), SER, tipo, BLOQUEIA,
            texto[:2000],
            (r["fonte"] or None),
            int(r["idade_min"]) if r["idade_min"].strip().isdigit() else None,
            int(r["idade_max"]) if r["idade_max"].strip().isdigit() else None,
            r["sexo"] if r["sexo"] in ("M", "F") else None,
            False, None, None, None, None,
            (r["pergunta"].strip() or texto)[:500] if tipo == TIPO["NaoDedutivel"] else None,
            (RESPOSTA["Sim"] if exclusao else RESPOSTA["Nao"]) if tipo == TIPO["NaoDedutivel"] else None,
            None,
            (r["documento"].strip() or texto)[:200] if tipo == TIPO["Documental"] else None,
            None, None, True, 0, 1,
            # TUDO entra inativo, dedutíveis inclusive.
            #
            # Cheguei a planejar ativar as 20 dedutíveis, por avaliarem idade e sexo pelo
            # cadastro sem perguntar nada. Olhando uma a uma, não dá: o motor soma as regras com
            # E — cada falha barra —, e o manual lista faixas etárias como critérios
            # ALTERNATIVOS. "Consulta em alergologia - pediatria" tem três: 2 a 12 anos, 2 a 12
            # anos e até 2 anos. Ativadas juntas, nenhuma criança passa nas três, e o
            # procedimento inteiro ficaria intransitável. Pior, há sexo inferido errado — a
            # regra de plaquetas da hematologia veio marcada como feminina e barraria todos os
            # homens.
            #
            # Ativar é decisão clínica, uma a uma, na tela de curadoria.
            False,
            agora, None, None, None, None, None,
        ))

    # Uma regra documental do encaminhamento por procedimento atingido.
    for proc_id, (origem_id, _) in procedimentos.items():
        regras.append((
            str(uuid.uuid4()), str(proc_id), str(origem_id), SER, TIPO["Documental"], BLOQUEIA,
            DOC_ENCAMINHAMENTO, "CRECE/REUNI — requisito global",
            None, None, None, False, None, None, None, None,
            None, None, None, DOC_ENCAMINHAMENTO[:200], None, None,
            True, 0, 1, False, agora, None, None, None, None, None,
        ))

    ativas = sum(1 for x in regras if x[25])
    print(f"CSV lido:            {len(linhas)}")
    print(f"sem par no catálogo: {sem_par}")
    for motivo, n in descartes.most_common():
        print(f"descartadas:  {n:4}  {motivo}")
    print(f"\nA GRAVAR:            {len(regras)}  ({ativas} ativas, {len(regras)-ativas} inativas)")
    print(f"procedimentos:       {len(procedimentos)}")

    if not gravar:
        print("\n(simulação — nada gravado; use --gravar)")
        cn.close()
        return 0

    colunas = (
        "id, procedimento_id, procedimento_origem_id, sistema, tipo, severidade, descricao, "
        "fonte, idade_min_anos, idade_max_anos, sexo, exige_cpf, cids_permitidos_json, "
        "cids_excluidos_json, municipios_ibge_json, expressao_json, pergunta, resposta_bloqueia, "
        "nao_sei_vira, documento_rotulo, tipo_exame_id, validade_dias, obrigatorio, ordem, "
        "versao, ativo, criado_em, criado_por, atualizado_em, atualizado_por, excluido_em, "
        "excluido_por"
    )
    marcadores = "(" + ",".join(["%s"] * 32) + ")"
    try:
        cur.executemany(
            f"insert into smsmarica.regulacao_regra ({colunas}) values {marcadores}", regras
        )
        cn.commit()
        cur.execute("select count(*) filter (where ativo), count(*) from smsmarica.regulacao_regra")
        print("\nGRAVADO. ativas/total na tabela:", cur.fetchone())
    except Exception:
        cn.rollback()
        raise
    finally:
        cn.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
