"""
Repara o catálogo canônico depois do deslize dos números do combo do SER/SERNIT (01/10/2026).

    python reparar_catalogo_posicional.py                  # simula, não grava
    python reparar_catalogo_posicional.py --gravar         # grava numa transação só
    python reparar_catalogo_posicional.py --desfazer <log> # volta o que um --gravar fez

O QUE ACONTECEU: o `value` do combo de recursos do SER (e do SERNIT) é posicional — quando a SES
acrescenta um recurso, os números dos que vêm depois deslizam (22/09 e 30/09/2026: 1030 -> 1067).
A sincronização tratava o número como identidade: gravava o rótulo novo no número antigo e
deixava a origem ligada ao procedimento antigo. Resultado medido: 320 origens do SER e 54 do
SERNIT ligadas ao procedimento errado, e os campos/lista de CID do espelho presos ao recurso que
ocupava aquele número antes. A causa foi corrigida no código (RegulacaoCatalogoService.
RealinharPosicionaisAsync e os dois *CatalogoSyncService); este script conserta o que já ficou.

O QUE ELE FAZ:
  1. Cada origem automática do SER/SERNIT cujo rótulo não bate com o procedimento volta a apontar
     para o procedimento DAQUELE rótulo. O procedimento certo é o que nasceu com aquele rótulo no
     mesmo sistema/ramo/tipo — ele existe, só perdeu a origem para o vizinho.
  2. Os recursos do espelho (ser_catalogo_recurso / sernit_catalogo_recurso) dessas origens ficam
     com campos e lista de CID invalidados: a próxima sincronização do catálogo relê.
  3. A sugestão de pareamento dessas origens é limpa (recalculada no próximo sync).
  4. As regras de elegibilidade passam a apontar para a origem certa do procedimento delas.
  Origem CONFIRMADA por pessoa nunca é tocada. Nada é apagado.
"""

import io
import json
import os
import re
import sys
import unicodedata
import uuid
from collections import defaultdict
from datetime import datetime, timezone

import psycopg2
import psycopg2.extras

RAIZ = os.path.dirname(os.path.abspath(__file__))
SER, SERNIT = 2, 3
ESPELHO = {SER: "ser_catalogo_recurso", SERNIT: "sernit_catalogo_recurso"}
COLUNA_ESPELHO = {SER: "ser_catalogo_recurso_id", SERNIT: "sernit_catalogo_recurso_id"}


def conectar():
    caminho = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(io.open(caminho, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(p.split("=", 1) for p in cs.split(";") if "=" in p)
    return psycopg2.connect(host=d["Host"], port=d["Port"], dbname=d["Database"],
                            user=d["Username"], password=d["Password"], sslmode="require")


def norm(t):
    """A mesma chave do ChaveRotulo.Normalizar do backend."""
    t = unicodedata.normalize("NFD", t or "")
    t = "".join(c for c in t if unicodedata.category(c) != "Mn")
    t = "".join(c for c in t if c.isalnum() or c == " ").upper().strip()
    return re.sub(" +", " ", t)


def normalizado(t):
    """Como SugestaoSigtap.Normalizar (nome_normalizado do procedimento): pontuação vira espaço."""
    t = unicodedata.normalize("NFD", (t or "").strip().upper())
    t = "".join(c if c.isalnum() else " " for c in t if unicodedata.category(c) != "Mn")
    return re.sub(" +", " ", t).strip()


def grupo(o):
    return (o["sistema"], o["ramo"], o["chave_externa"].split("|")[0])


def main():
    if "--desfazer" in sys.argv:
        return desfazer(sys.argv[sys.argv.index("--desfazer") + 1])
    gravar = "--gravar" in sys.argv

    cn = conectar()
    cur = cn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)
    cur.execute("""
        select o.id, o.sistema, o.ramo, o.chave_externa, o.rotulo_externo, o.procedimento_id, o.vinculo,
               o.confirmado_em, o.ativo, o.ser_catalogo_recurso_id, o.sernit_catalogo_recurso_id,
               o.sugerido_procedimento_id, o.sugerido_score, p.nome_canonico, p.tipo as proc_tipo,
               p.criado_em as proc_criado_em,
               (select count(*) from smsmarica.regulacao_regra g
                 where g.procedimento_id = p.id and g.ativo and g.excluido_em is null) as proc_regras
        from smsmarica.regulacao_procedimento_origem o
        join smsmarica.regulacao_procedimento p on p.id = o.procedimento_id
        where o.sistema in (2, 3)""")
    origens = [dict(r) for r in cur.fetchall()]

    # Recursos do espelho que estavam no combo na ÚLTIMA listagem (mesma regra do backend:
    # RegulacaoCatalogoService.SoDaUltimaListagem). O resto é número que o SER já renumerou.
    cur.execute("""
        select r.id from smsmarica.ser_catalogo_recurso r
        where r.sincronizado_em >= (select max(x.sincronizado_em) from smsmarica.ser_catalogo_recurso x
                                    where x.tipo = r.tipo and x.ambulatorio_estadual = r.ambulatorio_estadual) - interval '1 hour'
        union all
        select r.id from smsmarica.sernit_catalogo_recurso r
        where r.sincronizado_em >= (select max(x.sincronizado_em) from smsmarica.sernit_catalogo_recurso x
                                    where x.tipo = r.tipo) - interval '1 hour'""")
    frescos = {str(r["id"]) for r in cur.fetchall()}

    def viva(o):
        ref = o[COLUNA_ESPELHO[o["sistema"]]]
        return ref is not None and str(ref) in frescos

    # Origem ativa presa a número que saiu do combo: o recurso dela não existe mais com aquele
    # número — inativa (a próxima sincronização, já corrigida, também faria isso).
    inativar = [o for o in origens if o["ativo"] and not viva(o)]
    for o in inativar:
        o["ativo"] = False

    # O procedimento certo de cada rótulo, por sistema/ramo/tipo. A sincronização cria o canônico
    # com o rótulo da origem, então o nome do procedimento é o rótulo com que ele nasceu. Quando o
    # mesmo nome tem DOIS procedimentos no grupo (o "gêmeo" que a sincronização quebrada criou
    # depois, para um número novo), vale o que tem regras e, em empate, o mais antigo — é o do
    # pareamento original, onde as regras do manual foram penduradas em 07/09.
    candidatos = defaultdict(dict)
    for o in origens:
        atual = candidatos[grupo(o)].get(norm(o["nome_canonico"]))
        chave = (-(1 if o["proc_regras"] else 0), o["proc_criado_em"])
        if atual is None or chave < atual[0]:
            candidatos[grupo(o)][norm(o["nome_canonico"])] = (chave, o["procedimento_id"])
    nascido = {g: {nome: v[1] for nome, v in d.items()} for g, d in candidatos.items()}

    # Toda origem viva cujo procedimento não é o certo do rótulo dela — inclusive a que tem o nome
    # "certo" mas está no gêmeo sem regras.
    erradas, reparos, sem_par = [], [], []
    for o in origens:
        if not o["ativo"] or o["confirmado_em"] is not None:
            continue
        alvo = nascido.get(grupo(o), {}).get(norm(o["rotulo_externo"]))
        if alvo == o["procedimento_id"]:
            continue
        erradas.append(o)
        if alvo is None:
            sem_par.append(o)
        else:
            reparos.append((o, alvo))

    # Fallback para quem não achou o procedimento no próprio grupo: um canônico com o mesmo nome e
    # o mesmo tipo que não esteja preso a outra origem deste sistema/ramo.
    if sem_par:
        cur.execute("select id, nome_canonico, tipo from smsmarica.regulacao_procedimento where ativo")
        por_nome = defaultdict(list)
        for p in cur.fetchall():
            por_nome[norm(p["nome_canonico"])].append(p)
        ocupados = defaultdict(set)
        for o in origens:
            ocupados[(o["sistema"], o["ramo"])].add(o["procedimento_id"])
        ainda = []
        for o in sem_par:
            livres = [p for p in por_nome.get(norm(o["rotulo_externo"]), [])
                      if p["tipo"] == o["proc_tipo"] and p["id"] not in ocupados[(o["sistema"], o["ramo"])]]
            if len(livres) == 1:
                reparos.append((o, livres[0]["id"]))
            else:
                ainda.append(o)
        sem_par = ainda

    # O que sobrou é recurso NOVO no combo que nunca ganhou procedimento: a sincronização antiga
    # renomeou uma origem velha em vez de criar. Ganha procedimento próprio, como a corrigida faria.
    criados = []
    for o in sem_par:
        novo = {"id": str(uuid.uuid4()), "nome_canonico": o["rotulo_externo"],
                "nome_normalizado": normalizado(o["rotulo_externo"]),
                "tipo": 1 if o["chave_externa"].split("|")[0] == "1" else 2}
        criados.append(novo)
        reparos.append((o, novo["id"]))
    sem_par = []

    # Depois do reparo, nenhum procedimento pode ter duas origens do mesmo sistema/ramo/tipo.
    final = {o["id"]: o["procedimento_id"] for o in origens}
    for o, alvo in reparos:
        final[o["id"]] = alvo
    contagem = defaultdict(list)
    for o in origens:
        if o["ativo"]:
            contagem[(grupo(o), final[o["id"]])].append(o["rotulo_externo"])
    colisoes = {k: v for k, v in contagem.items() if len(v) > 1}

    print(f"Origens SER/SERNIT: {len(origens)} — presas a número que saiu do combo (a inativar): {len(inativar)}")
    print(f"Origens vivas ligadas ao procedimento errado: {len(erradas)}")
    print(f"  reparáveis: {len(reparos)}   sem procedimento certo achado: {len(sem_par)}   colisões: {len(colisoes)}")
    for o, alvo in reparos[:8]:
        print(f'    {o["rotulo_externo"][:55]:55}  (estava em "{o["nome_canonico"][:40]}")')
    for c in criados:
        print(f'  PROCEDIMENTO NOVO: {c["nome_canonico"]}')
    for (g, proc), rotulos in list(colisoes.items())[:10]:
        print(f"  COLISÃO {g}: {rotulos}")

    ids_reparo = [str(o["id"]) for o, _ in reparos]
    espelho_ids = {s: [str(o[COLUNA_ESPELHO[s]]) for o, _ in reparos if o["sistema"] == s and o[COLUNA_ESPELHO[s]]]
                   for s in (SER, SERNIT)}

    # Regras cujo ponteiro de origem não é de uma origem do próprio procedimento (depois do reparo).
    cur.execute("""select r.id, r.procedimento_id, r.sistema, r.procedimento_origem_id, o.ramo
                   from smsmarica.regulacao_regra r
                   left join smsmarica.regulacao_procedimento_origem o on o.id = r.procedimento_origem_id
                   where r.procedimento_origem_id is not null and r.sistema in (2, 3)""")
    regras = [dict(r) for r in cur.fetchall()]
    por_proc = defaultdict(list)
    for o in origens:
        por_proc[(final[o["id"]], o["sistema"])].append(o)
    regras_reparo = []
    for r in regras:
        if final.get(r["procedimento_origem_id"]) == r["procedimento_id"]:
            continue
        candidatas = por_proc.get((r["procedimento_id"], r["sistema"]), [])
        certa = next((c for c in candidatas if c["ramo"] == r["ramo"] and c["ativo"]), None) \
            or next((c for c in candidatas if c["ativo"]), None)
        regras_reparo.append((r, certa["id"] if certa else None))

    # Solicitações abertas (rascunho/devolvida) com destino SER/SERNIT cujo formulário foi montado
    # com o recurso errado — só identifica; quem refaz é a unidade.
    procs_afetados = list({str(o["procedimento_id"]) for o, _ in reparos} | {str(a) for _, a in reparos})
    cur.execute("""select numero_local, status, sistema_destino from smsmarica.regulacao_solicitacao
                   where excluido_em is null and status in (1, 4) and sistema_destino in (2, 3)
                     and procedimento_id = any(%s::uuid[]) order by numero_local""", (procs_afetados,))
    abertas = cur.fetchall()

    print(f"\nEspelho a reler (campos + lista de CID): SER {len(espelho_ids[SER])}, SERNIT {len(espelho_ids[SERNIT])}")
    print(f"Regras com ponteiro de origem a corrigir: {len(regras_reparo)} "
          f"({sum(1 for _, c in regras_reparo if c is None)} ficam sem origem)")
    print(f"Solicitações abertas para SER/SERNIT montadas com formulário possivelmente errado: "
          f"{[(a['numero_local'], a['status']) for a in abertas]}")

    if colisoes:
        print("\nABORTADO: há colisões — o reparo deixaria duas origens no mesmo procedimento. Revisar antes.")
        return 1
    if not gravar:
        print("\n(simulação — nada gravado; use --gravar)")
        return 0

    agora = datetime.now(timezone.utc)
    log = {"gravado_em": agora.isoformat(),
           "inativadas": [str(o["id"]) for o in inativar],
           "procedimentos_criados": [c["id"] for c in criados],
           "origens": [{"id": str(o["id"]), "procedimento_id": str(o["procedimento_id"]),
                        "sugerido_procedimento_id": str(o["sugerido_procedimento_id"]) if o["sugerido_procedimento_id"] else None,
                        "sugerido_score": o["sugerido_score"]} for o, _ in reparos],
           "regras": [{"id": str(r["id"]), "procedimento_origem_id": str(r["procedimento_origem_id"])} for r, _ in regras_reparo],
           "espelho": {str(k): v for k, v in espelho_ids.items()}}
    try:
        for c in criados:
            cur.execute("""insert into smsmarica.regulacao_procedimento (id, nome_canonico, nome_normalizado, tipo, ativo, criado_em)
                           values (%s, %s, %s, %s, true, %s)""", (c["id"], c["nome_canonico"], c["nome_normalizado"], c["tipo"], agora))
        if inativar:
            cur.execute("""update smsmarica.regulacao_procedimento_origem set ativo = false, atualizado_em = %s
                           where id = any(%s::uuid[]) and ativo""", (agora, [str(o["id"]) for o in inativar]))
        for o, alvo in reparos:
            cur.execute("""update smsmarica.regulacao_procedimento_origem
                           set procedimento_id = %s, sugerido_procedimento_id = null, sugerido_score = null, atualizado_em = %s
                           where id = %s and confirmado_em is null""", (alvo, agora, o["id"]))
            assert cur.rowcount == 1
        for s, ids in espelho_ids.items():
            if ids:
                cur.execute(f"""update smsmarica.{ESPELHO[s]} set campos_lidos = false, cid_lista_id = null, cid_assinatura = null
                                where id = any(%s::uuid[])""", (ids,))
        for r, certa in regras_reparo:
            cur.execute("update smsmarica.regulacao_regra set procedimento_origem_id = %s where id = %s", (certa, r["id"]))
        cn.commit()
    except Exception:
        cn.rollback()
        raise

    caminho = os.path.join(RAIZ, "conversao-listas", f"log-reparo-catalogo-{agora:%Y%m%d-%H%M%S}.json")
    json.dump(log, io.open(caminho, "w", encoding="utf-8"), indent=1, default=str)
    print(f"\nGRAVADO: {len(inativar)} inativadas, {len(criados)} procedimentos novos, {len(reparos)} origens realinhadas, {len(regras_reparo)} regras, espelho invalidado. Log: {caminho}")
    return 0


def desfazer(caminho):
    """Volta origens e ponteiros das regras. O espelho invalidado não volta: reler é inofensivo."""
    log = json.load(io.open(caminho, encoding="utf-8"))
    cn = conectar()
    cur = cn.cursor()
    try:
        if log.get("procedimentos_criados"):
            cur.execute("update smsmarica.regulacao_procedimento set ativo = false where id = any(%s::uuid[])",
                        (log["procedimentos_criados"],))
        if log.get("inativadas"):
            cur.execute("update smsmarica.regulacao_procedimento_origem set ativo = true where id = any(%s::uuid[])",
                        (log["inativadas"],))
        for o in log["origens"]:
            cur.execute("""update smsmarica.regulacao_procedimento_origem
                           set procedimento_id = %s, sugerido_procedimento_id = %s, sugerido_score = %s where id = %s""",
                        (o["procedimento_id"], o["sugerido_procedimento_id"], o["sugerido_score"], o["id"]))
        for r in log["regras"]:
            cur.execute("update smsmarica.regulacao_regra set procedimento_origem_id = %s where id = %s",
                        (r["procedimento_origem_id"], r["id"]))
        cn.commit()
    except Exception:
        cn.rollback()
        raise
    print(f'Desfeito: {len(log["origens"])} origens, {len(log["regras"])} regras.')
    return 0


if __name__ == "__main__":
    sys.exit(main())
