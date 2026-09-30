"""Carga, no banco de PRODUÇÃO, das coletas oficiais feitas pelo laboratório em 30/09/2026 para os
Indicadores de Regulação (tela Regulação → Indicadores, migration IndicadoresRegulacao).

Fontes (gitignored — têm código de solicitação):
  Automais.SISREG/capturas/indicadores/serie/progresso.json  (coletar_serie_indicadores.py)
  Automais.SER/capturas/judicial_ids.json                     (sonda_judicial.py)

O que grava:
  sisreg_falta                  lista oficial de faltas (rel_amb_faltas_sol), por semana
  sisreg_ppi_cota               cotas PPI por competência
  sisreg_solicitacao_desfecho   devolvidas (4), negadas (6), canceladas antes de agendar (3), por unidade
  sisreg_indicador_coleta       o que foi lido: janelas de faltas (coletor 1), total declarado de
                                canceladas por mês (coletor 2), unidades lidas nos desfechos (coletor 3)
  ser_solicitacao               mandado_judicial = true nos 169 IDs do filtro "Somente com mandado judicial"
  ser_/sernit_solicitacao       mandado_judicial_verificado_em = data da conferência (SERNIT: zero judiciais)

NÃO grava sisreg_marcacao_cancelada: a amostra de motivos coletada não guardou o código da
solicitação (a tabela exige). Os motivos do SISREG entram pelo coletor da plataforma.

Idempotente (ON CONFLICT DO NOTHING / UPDATE da coleta). Sem --aplicar só lê e mostra o que faria.
    python carregar_indicadores_prod.py            # conferência, só leitura
    python carregar_indicadores_prod.py --aplicar  # grava numa transação; rollback se algo divergir
"""
from __future__ import annotations

import datetime as dt
import json
import sys
import uuid
from pathlib import Path

from psycopg2.extras import execute_values

AQUI = Path(__file__).resolve().parent
RAIZ = AQUI.parent
sys.path.insert(0, str(RAIZ / "Aprendizados e Scratchpads" / "ferramentas"))
import db  # noqa: E402  (connection string dos user-secrets da API)

COLETA = AQUI / "capturas" / "indicadores" / "serie" / "progresso.json"
JUDICIAL_SER = RAIZ / "Automais.SER" / "capturas" / "judicial_ids.json"
TETO_JUDICIAL = 400  # o filtro devolveu 169; muito acima disso é sinal de arquivo errado


def dmy(s: str | None) -> dt.date | None:
    return dt.datetime.strptime(s, "%d/%m/%Y").date() if s else None


def iso(s: str) -> dt.date:
    return dt.date.fromisoformat(s)


def corta(s, n):
    return None if s is None else str(s)[:n]


def montar():
    dados = json.loads(COLETA.read_text(encoding="utf-8"))
    lido_em = dt.datetime.fromtimestamp(COLETA.stat().st_mtime, dt.timezone.utc)
    faltas, coletas, desf, ppi = {}, {}, {}, {}
    desf_por_unidade: dict[tuple[str, dt.date, dt.date], dict[int, int]] = {}
    for chave, v in dados.items():
        p = chave.split(":")
        if p[0] == "faltas":
            for f in v:
                faltas[(f["codigo"], dmy(f["data_execucao"]))] = f
            coletas[(1, iso(p[1]), "")] = (iso(p[2]), len(v))
        elif p[0] == "canc":
            ini = iso(p[1] + "-01")
            fim = (ini.replace(day=28) + dt.timedelta(days=4)).replace(day=1) - dt.timedelta(days=1)
            coletas[(2, ini, "")] = (fim, int(v))
        elif p[0] == "desf":
            sit, cnes, ini, fim = int(p[1]), p[2], iso(p[3]), iso(p[4])
            for l in v["linhas"]:
                desf[(l["codigo"], sit)] = (dmy(l.get("data_solicitacao")), l.get("procedimento"), cnes)
            desf_por_unidade.setdefault((cnes, ini, fim), {})[sit] = len(v["linhas"])
        elif p[0] == "ppi":
            comp = iso(p[1] + "-01")
            for c in v:
                ppi[(comp, c["co_interno"], c.get("tipo") or "")] = c
    # desfechos: uma linha de coleta por unidade (as três situações juntas)
    for (cnes, ini, fim), porsit in desf_por_unidade.items():
        coletas[(3, ini, cnes)] = (fim, sum(porsit.values()))
    completas = sum(1 for s in desf_por_unidade.values() if set(s) >= {3, 4, 6})
    return lido_em, faltas, coletas, desf, ppi, len(desf_por_unidade), completas


def judiciais():
    d = json.loads(JUDICIAL_SER.read_text(encoding="utf-8"))
    ids = sorted({str(x["id"]) for v in d.values() for x in v})
    quando = dt.datetime.fromtimestamp(JUDICIAL_SER.stat().st_mtime, dt.timezone.utc)
    return ids, quando


def main() -> int:
    aplicar = "--aplicar" in sys.argv
    lido_em, faltas, coletas, desf, ppi, unidades, completas = montar()
    jud, jud_em = judiciais()
    print(f"coleta lida em {lido_em:%d/%m/%Y %H:%M} UTC")
    print(f"  faltas {len(faltas)} · janelas de faltas {sum(1 for k in coletas if k[0] == 1)}"
          f" · meses de canceladas {sum(1 for k in coletas if k[0] == 2)}")
    print(f"  desfechos {len(desf)} em {unidades} unidades ({completas} com as 3 situações) · cotas PPI {len(ppi)}")
    print(f"  SER judiciais {len(jud)} (conferidos em {jud_em:%d/%m/%Y})")
    if not (0 < len(jud) <= TETO_JUDICIAL):
        print("ABORTADO: quantidade de judiciais fora do esperado.")
        return 1

    with db.conn() as c, c.cursor() as cur:
        cur.execute("set local statement_timeout = '120s'")
        for t in ("sisreg_falta", "sisreg_ppi_cota", "sisreg_solicitacao_desfecho", "sisreg_indicador_coleta"):
            cur.execute(f"select count(*) from smsmarica.{t}")
            print(f"  hoje em {t}: {cur.fetchone()[0]}")
        cur.execute("select count(*) from smsmarica.ser_solicitacao where excluido_em is null and id_ser = any(%s)", (jud,))
        casam = cur.fetchone()[0]
        print(f"  judiciais que casam com ser_solicitacao ativa: {casam} de {len(jud)}")
        if not aplicar:
            print("\nConferência apenas (nada gravado). Para gravar: --aplicar")
            return 0

        agora = dt.datetime.now(dt.timezone.utc)
        execute_values(cur, """
            insert into smsmarica.sisreg_falta (id, codigo_solicitacao, data_execucao, hora, procedimento, unidade_solicitante, lido_em)
            values %s on conflict (codigo_solicitacao, data_execucao) do nothing""",
            [(str(uuid.uuid4()), cod, data, corta(f.get("hora"), 10), corta(f.get("procedimento"), 300),
              corta(f.get("unidade_solicitante"), 200), lido_em) for (cod, data), f in faltas.items()], page_size=2000)
        execute_values(cur, """
            insert into smsmarica.sisreg_ppi_cota (id, competencia, codigo_interno, codigo_unificado, procedimento, total, usada, saldo, tipo, lido_em)
            values %s on conflict (competencia, codigo_interno, tipo) do nothing""",
            [(str(uuid.uuid4()), comp, corta(ci, 20), corta(c.get("co_unificado"), 20), corta(c.get("procedimento"), 300),
              int(c["total"]), int(c["usada"]), c.get("saldo"), corta(tipo, 30), lido_em) for (comp, ci, tipo), c in ppi.items()],
            page_size=2000)
        execute_values(cur, """
            insert into smsmarica.sisreg_solicitacao_desfecho (id, codigo_solicitacao, situacao, data_solicitacao, procedimento, unidade_solicitante_cnes, origem, lido_em)
            values %s on conflict (codigo_solicitacao, situacao) do nothing""",
            [(str(uuid.uuid4()), cod, sit, ds, corta(proc, 300), corta(cnes, 10), 1, lido_em)
             for (cod, sit), (ds, proc, cnes) in desf.items()], page_size=2000)
        execute_values(cur, """
            insert into smsmarica.sisreg_indicador_coleta (id, coletor, janela_inicio, janela_fim, escopo, status, tentativas, linhas, iniciado_em, lido_em, criado_em)
            values %s on conflict (coletor, janela_inicio, escopo) do update
              set janela_fim = excluded.janela_fim, status = 3, linhas = excluded.linhas, lido_em = excluded.lido_em, erro = null""",
            [(str(uuid.uuid4()), col, ini, fim, esc, 3, 1, linhas, lido_em, lido_em, agora)
             for (col, ini, esc), (fim, linhas) in coletas.items()], page_size=2000)
        cur.execute("update smsmarica.ser_solicitacao set mandado_judicial = true where excluido_em is null and id_ser = any(%s)", (jud,))
        n_jud = cur.rowcount
        if n_jud != casam:
            c.rollback()
            print(f"ROLLBACK: marcaria {n_jud} judiciais, esperado {casam}.")
            return 1
        cur.execute("update smsmarica.ser_solicitacao set mandado_judicial_verificado_em = %s", (jud_em,))
        cur.execute("update smsmarica.sernit_solicitacao set mandado_judicial_verificado_em = %s", (jud_em,))
        c.commit()

        print(f"\nGRAVADO (judiciais marcados {n_jud}).")
        for t in ("sisreg_falta", "sisreg_ppi_cota", "sisreg_solicitacao_desfecho", "sisreg_indicador_coleta"):
            cur.execute(f"select count(*) from smsmarica.{t}")
            print(f"  agora em {t}: {cur.fetchone()[0]}")
        cur.execute("select count(*) from smsmarica.ser_solicitacao where mandado_judicial")
        print(f"  ser_solicitacao com mandado judicial: {cur.fetchone()[0]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
