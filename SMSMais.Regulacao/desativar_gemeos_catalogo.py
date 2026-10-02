"""
Desativa os "gêmeos" do catálogo canônico — procedimentos com o MESMO nome de outro, sem origem viva.

    python desativar_gemeos_catalogo.py                  # simula, não grava
    python desativar_gemeos_catalogo.py --gravar         # grava numa transação só
    python desativar_gemeos_catalogo.py --desfazer <log> # reativa o que um --gravar desativou

DE ONDE VIERAM: até 01/10/2026 a sincronização do catálogo tratava o número do combo do SER/SERNIT
como identidade. Quando a SES renumerou o combo, cada número novo ganhou um procedimento NOVO com o
nome de um recurso que já tinha o seu — o gêmeo. O reparo (reparar_catalogo_posicional.py) devolveu
cada origem ao procedimento original; os gêmeos ficaram sem origem e aparecem em dobro na busca de
procedimento.

O QUE CONTA COMO GÊMEO (e só isso é desativado): procedimento ATIVO, sem nenhuma origem ativa, cujo
nome normalizado é igual ao de outro procedimento que TEM origem ativa, e que não é usado por regra,
solicitação, estratégia de fila nem versão de formulário. Desativar não apaga nada: a linha fica, só
some das listas que filtram por ativo.
"""

import io
import json
import os
import sys
from datetime import datetime, timezone

import psycopg2
import psycopg2.extras

RAIZ = os.path.dirname(os.path.abspath(__file__))

CONSULTA = """
with viva as (select distinct procedimento_id from smsmarica.regulacao_procedimento_origem where ativo),
n as (select p.id, p.ativo, p.nome_canonico, upper(smsmarica.unaccent(p.nome_canonico)) nn
      from smsmarica.regulacao_procedimento p)
select n.id, n.nome_canonico,
       exists (select 1 from smsmarica.regulacao_analise_espelho a where a.procedimento_id = n.id) as em_parecer
from n
where n.ativo
  and n.id not in (select procedimento_id from viva)
  and exists (select 1 from n n2 join viva v on v.procedimento_id = n2.id where n2.nn = n.nn and n2.id <> n.id)
  and not exists (select 1 from smsmarica.regulacao_regra r where r.procedimento_id = n.id)
  and not exists (select 1 from smsmarica.regulacao_solicitacao s where s.procedimento_id = n.id)
  and not exists (select 1 from smsmarica.estrategia_fila e where e.regulacao_procedimento_id = n.id)
  and not exists (select 1 from smsmarica.regulacao_formulario_versao f where f.procedimento_id = n.id)
order by n.nome_canonico
"""


def conectar():
    caminho = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(io.open(caminho, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(p.split("=", 1) for p in cs.split(";") if "=" in p)
    return psycopg2.connect(host=d["Host"], port=d["Port"], dbname=d["Database"],
                            user=d["Username"], password=d["Password"], sslmode="require")


def main():
    if "--desfazer" in sys.argv:
        return desfazer(sys.argv[sys.argv.index("--desfazer") + 1])
    gravar = "--gravar" in sys.argv

    cn = conectar()
    cur = cn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)
    cur.execute(CONSULTA)
    gemeos = [dict(r) for r in cur.fetchall()]

    print(f"Gêmeos a desativar: {len(gemeos)} "
          f"({sum(1 for g in gemeos if g['em_parecer'])} citados em parecer antigo da análise — refeito na próxima passada)")
    for g in gemeos[:15]:
        print(f"  - {g['nome_canonico']}")
    if len(gemeos) > 15:
        print(f"  … e mais {len(gemeos) - 15}")

    if not gravar or not gemeos:
        print("\n(simulação — nada gravado; use --gravar)" if not gravar else "\nNada a fazer.")
        return 0

    agora = datetime.now(timezone.utc)
    ids = [str(g["id"]) for g in gemeos]
    try:
        cur.execute("update smsmarica.regulacao_procedimento set ativo = false, atualizado_em = %s "
                    "where id = any(%s::uuid[]) and ativo", (agora, ids))
        if cur.rowcount != len(ids):
            raise RuntimeError(f"esperava desativar {len(ids)}, desativou {cur.rowcount}")
        cn.commit()
    except Exception:
        cn.rollback()
        raise

    caminho = os.path.join(RAIZ, "conversao-listas", f"log-gemeos-{agora:%Y%m%d-%H%M%S}.json")
    json.dump({"gravado_em": agora.isoformat(), "desativados": ids}, io.open(caminho, "w", encoding="utf-8"), indent=1)
    print(f"\nGRAVADO: {len(ids)} gêmeos desativados. Log: {caminho}")
    return 0


def desfazer(caminho):
    log = json.load(io.open(caminho, encoding="utf-8"))
    cn = conectar()
    cur = cn.cursor()
    try:
        cur.execute("update smsmarica.regulacao_procedimento set ativo = true where id = any(%s::uuid[])",
                    (log["desativados"],))
        cn.commit()
    except Exception:
        cn.rollback()
        raise
    print(f'Desfeito: {len(log["desativados"])} reativados.')
    return 0


if __name__ == "__main__":
    sys.exit(main())
