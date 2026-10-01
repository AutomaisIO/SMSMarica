"""
Converte em PERGUNTA DE LISTA as condições alternativas do manual que a importação gravou como
perguntas soltas — e aplica as correções revisadas junto (conversao-listas/REVISAO.md).

    python converter_listas.py                  # simula: mostra o que faria, não grava
    python converter_listas.py --gravar         # grava, numa transação só
    python converter_listas.py --desfazer <log> # volta exatamente o que um --gravar fez

Por que existe: o manual escreve "portadores das seguintes condições: A; B; C" querendo dizer
BASTA UMA. A importação (07/09/2026) gravou cada condição como pergunta própria, e o avaliador
soma as regras com E — para passar, o paciente precisaria ter todas.

O plano (conversao-listas/plano.json) foi revisado procedimento a procedimento contra o PDF. Este
script não decide nada clínico: só executa o plano, e RECUSA gravar se o banco não estiver como o
plano viu (regra mexida pela tela depois da exportação, coluna da migration ausente).

Itens do plano:
  - "lista": cria UMA regra NaoDedutivel com as opções e desativa as regras que ela substitui.
  - "versao": cria a versão seguinte de uma regra com os campos corrigidos e desativa a anterior
    (o mesmo que "Nova versão" na tela).
  - "desativar": desativa o que não é regra (cabeçalho solto, caco do extrator, duplicata, regra
    de outra seção do manual).

Nada é apagado. criado_por/atualizado_por ficam nulos — é assim que se distingue do que uma pessoa
fez pela tela. O --gravar escreve um log com os ids; o --desfazer lê esse log.
"""

import io
import json
import os
import sys
import uuid
from datetime import datetime, timezone

import psycopg2
import psycopg2.extras

RAIZ = os.path.dirname(os.path.abspath(__file__))
PLANO = os.path.join(RAIZ, "conversao-listas", "plano.json")

NAO_DEDUTIVEL = 2
RESPOSTA = {"Sim": 1, "Nao": 2}

COLUNAS = [
    "id", "procedimento_id", "procedimento_origem_id", "sistema", "tipo", "severidade",
    "descricao", "fonte", "idade_min_anos", "idade_max_anos", "sexo", "exige_cpf",
    "cids_permitidos_json", "cids_excluidos_json", "municipios_ibge_json", "expressao_json",
    "pergunta", "resposta_bloqueia", "nao_sei_vira", "opcoes_json", "documento_rotulo",
    "tipo_exame_id", "validade_dias", "obrigatorio", "ordem", "versao", "ativo", "criado_em",
    "criado_por", "atualizado_em", "atualizado_por", "excluido_em", "excluido_por",
]
JSONB = {c for c in COLUNAS if c.endswith("_json")}


def conectar():
    caminho = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(io.open(caminho, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(p.split("=", 1) for p in cs.split(";") if "=" in p)
    return psycopg2.connect(host=d["Host"], port=d["Port"], dbname=d["Database"],
                            user=d["Username"], password=d["Password"], sslmode="require")


def inserir(cur, regra):
    valores = []
    for c in COLUNAS:
        v = regra.get(c)
        if c in JSONB and v is not None and not isinstance(v, str):
            v = json.dumps(v, ensure_ascii=False)
        valores.append(v)
    marcadores = ", ".join("%s::jsonb" if c in JSONB else "%s" for c in COLUNAS)
    cur.execute(f"insert into smsmarica.regulacao_regra ({', '.join(COLUNAS)}) values ({marcadores})", valores)


def conferir(item, atuais, problemas):
    """O banco tem de estar como o plano viu: mesma versão, mesmo estado de ativa, não excluída."""
    for esperado in item["regras_esperadas"]:
        atual = atuais.get(esperado["id"])
        rotulo = f'{item["procedimento"]} — {esperado["id"][:8]}'
        if atual is None:
            problemas.append(f"{rotulo}: regra não existe mais")
        elif atual["excluido_em"] is not None:
            problemas.append(f"{rotulo}: regra foi excluída")
        elif atual["versao"] != esperado["versao"] or atual["ativo"] != esperado["ativo"]:
            problemas.append(
                f'{rotulo}: mudou desde a revisão (v{atual["versao"]}, ativa={atual["ativo"]}; '
                f'plano viu v{esperado["versao"]}, ativa={esperado["ativo"]})')


def montar(item, atuais, agora):
    """(regras a inserir, ids a desativar) de um item do plano."""
    tipo = item["tipo"]
    if tipo == "desativar":
        return [], [e["id"] for e in item["regras_esperadas"] if e["ativo"]]

    base = dict(atuais[item["regras_esperadas"][0]["id"]])

    if tipo == "versao":
        nova = {c: base.get(c) for c in COLUNAS}
        nova.update(item["campos"])
        nova.update(id=str(uuid.uuid4()), versao=base["versao"] + 1, ativo=base["ativo"],
                    criado_em=agora, criado_por=None, atualizado_em=None, atualizado_por=None,
                    excluido_em=None, excluido_por=None)
        return [nova], [str(base["id"])] if base["ativo"] else []

    if tipo == "lista":
        substituidas = [atuais[e["id"]] for e in item["regras_esperadas"]]
        # Converter não liga o que estava desligado: a lista vale se ao menos uma solta valia.
        ativa = any(r["ativo"] for r in substituidas)
        nova = {c: None for c in COLUNAS}
        nova.update(
            id=str(uuid.uuid4()), procedimento_id=base["procedimento_id"],
            procedimento_origem_id=base["procedimento_origem_id"], sistema=base["sistema"],
            tipo=NAO_DEDUTIVEL, severidade=1, descricao=item["descricao"],
            fonte=item.get("fonte") or base["fonte"], exige_cpf=False, pergunta=item["pergunta"],
            resposta_bloqueia=RESPOSTA[item.get("resposta_bloqueia", "Nao")], opcoes_json=item["opcoes"],
            obrigatorio=True, ordem=min(r["ordem"] for r in substituidas), versao=1, ativo=ativa,
            criado_em=agora)
        return [nova], [str(r["id"]) for r in substituidas if r["ativo"]]

    raise ValueError(f"tipo de item desconhecido: {tipo}")


def main():
    if "--desfazer" in sys.argv:
        return desfazer(sys.argv[sys.argv.index("--desfazer") + 1])
    gravar = "--gravar" in sys.argv

    plano = json.load(io.open(PLANO, encoding="utf-8"))
    cn = conectar()
    cur = cn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)

    cur.execute("select count(*) as n from information_schema.columns where table_schema = 'smsmarica' "
                "and table_name = 'regulacao_regra' and column_name = 'opcoes_json'")
    if cur.fetchone()["n"] == 0:
        print("ABORTADO: a coluna opcoes_json não existe — a migration PerguntaDeListaNasRegras não rodou.")
        return 1

    ids = [e["id"] for item in plano["itens"] for e in item["regras_esperadas"]]
    cur.execute("select * from smsmarica.regulacao_regra where id = any(%s::uuid[])", (ids,))
    atuais = {str(r["id"]): dict(r) for r in cur.fetchall()}

    problemas = []
    for item in plano["itens"]:
        conferir(item, atuais, problemas)
    if problemas:
        print("O banco não está como o plano espera — nada foi gravado:")
        for p in problemas:
            print("  -", p)
        return 1

    agora = datetime.now(timezone.utc)
    inserir_todas, desativar_todas = [], []
    for item in plano["itens"]:
        novas, desligar = montar(item, atuais, agora)
        inserir_todas += novas
        desativar_todas += desligar
        if item["tipo"] == "lista":
            print(f'[LISTA] {item["procedimento"]}: {len(item["opcoes"])} opções, substitui {len(desligar)}')
        elif item["tipo"] == "versao":
            print(f'[VERSÃO] {item["procedimento"]}: {item["campos"]}')
        else:
            print(f'[DESLIGA] {item["procedimento"]}: {item["motivo"][:80]}')

    assert len(set(desativar_todas)) == len(desativar_todas), "regra desativada duas vezes no plano"
    print(f"\nTOTAL: {len(inserir_todas)} regra(s) nova(s), {len(desativar_todas)} desativada(s).")

    if not gravar:
        print("\n(simulação — nada gravado; use --gravar)")
        return 0

    try:
        for n in inserir_todas:
            inserir(cur, n)
        cur.execute("update smsmarica.regulacao_regra set ativo = false, atualizado_em = %s, atualizado_por = null "
                    "where id = any(%s::uuid[]) and ativo", (agora, desativar_todas))
        if cur.rowcount != len(desativar_todas):
            raise RuntimeError(f"esperava desativar {len(desativar_todas)}, desativou {cur.rowcount}")
        cn.commit()
    except Exception:
        cn.rollback()
        raise

    log = os.path.join(RAIZ, "conversao-listas", f"log-{agora:%Y%m%d-%H%M%S}.json")
    json.dump({"gravado_em": agora.isoformat(), "criadas": [n["id"] for n in inserir_todas],
               "desativadas": desativar_todas}, io.open(log, "w", encoding="utf-8"), indent=1)
    print(f"\nGRAVADO. Log para desfazer: {log}")
    return 0


def desfazer(caminho):
    log = json.load(io.open(caminho, encoding="utf-8"))
    cn = conectar()
    cur = cn.cursor()
    agora = datetime.now(timezone.utc)
    try:
        # As criadas não se apagam (a resposta pendurada nelas tem FK Restrict, de propósito):
        # ficam inativas, e as substituídas voltam a valer.
        cur.execute("update smsmarica.regulacao_regra set ativo = false, atualizado_em = %s where id = any(%s::uuid[])",
                    (agora, log["criadas"]))
        cur.execute("update smsmarica.regulacao_regra set ativo = true, atualizado_em = %s where id = any(%s::uuid[])",
                    (agora, log["desativadas"]))
        cn.commit()
    except Exception:
        cn.rollback()
        raise
    print(f'Desfeito: {len(log["criadas"])} desligada(s), {len(log["desativadas"])} religada(s).')
    return 0


if __name__ == "__main__":
    sys.exit(main())
