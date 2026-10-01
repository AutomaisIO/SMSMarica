"""Carga, no banco de PRODUÇÃO, da lista oficial de faltas ANTES de 2025 lida por `coletar_faltas_historico.py`.

Nasceu em 01/10/2026 para o passado da ficha do paciente (Compareceu / Faltou / Em aberto). Grava SÓ:
  sisreg_falta              uma linha por (código, dia de execução) — ON CONFLICT DO NOTHING
  sisreg_indicador_coleta   uma janela por trecho contínuo de dias em que TODAS as executantes ativas
                            foram lidas — coletor 1 (Faltas), escopo "exec:rede", status Concluída. É a
                            janela que deixa a ficha afirmar "em aberto" (quem não está na lista num
                            dia LIDO não faltou); dia com unidade faltando não ganha janela.

Por que um carregador próprio, e não `carregar_indicadores_prod.py`: aquele relê o arquivo de 30/09
inteiro, remarca a conferência de judiciais do SER e sobrescreve janelas que a plataforma já coletou
depois. Este só toca o que o coletor histórico leu, e só janelas de escopo "exec:" (que a plataforma
não usa).

Fonte (gitignored — tem código de solicitação): capturas/indicadores/faltas_historico/progresso.json

    python carregar_faltas_historico_prod.py            # conferência, só leitura
    python carregar_faltas_historico_prod.py --aplicar  # grava numa transação
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
from coletar_faltas_historico import EXECUTANTES, ativa  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
COLETA = AQUI / "capturas" / "indicadores" / "faltas_historico" / "progresso.json"
COLETOR_FALTAS = 1
CONCLUIDA = 3
# Base de produção esperada: a carga de 30/09 deixou 85.121 faltas de 2025-01 a 2026-08. Abaixo disso
# é sinal de que a connection string aponta para outro banco.
MINIMO_FALTAS_EXISTENTES = 85_000


def dmy(s: str) -> dt.date:
    return dt.datetime.strptime(s, "%d/%m/%Y").date()


def corta(s, n):
    return None if s is None else str(s)[:n]


def montar():
    """Faltas lidas (todas — são verdade) e as janelas de COBERTURA COMPLETA.

    A ficha trata um dia como "lido" sem olhar a unidade: registrar a janela do Péricles num mês em
    que o Conde ainda não foi lido faria os pendentes do Conde aparecerem "em aberto" quando podem ser
    falta. Por isso a janela só cobre os dias em que TODAS as executantes ativas naquele mês (tabela
    do coletor) têm leitura — e vira uma janela por trecho contínuo, escopo "exec:rede".
    """
    dados = json.loads(COLETA.read_text(encoding="utf-8"))
    faltas: dict[tuple[str, dt.date], tuple[dict, dt.datetime]] = {}
    lidos: dict[str, list[tuple[dt.date, dt.date, int, dt.datetime]]] = {}
    for chave, v in dados.items():
        if "linhas" not in v:
            continue  # marca de janela partida: as metades é que carregam as linhas
        cnes, ini, fim = chave.split(":")
        lido_em = dt.datetime.fromisoformat(v["lido_em"])
        for f in v["linhas"]:
            faltas.setdefault((f["codigo"], dmy(f["data_execucao"])), (f, lido_em))
        lidos.setdefault(cnes, []).append(
            (dt.date.fromisoformat(ini), dt.date.fromisoformat(fim), len(v["linhas"]), lido_em))

    coletas: dict[tuple[dt.date, str], tuple[dt.date, int, dt.datetime]] = {}
    if not lidos:
        return faltas, coletas

    def coberto(cnes: str, dia: dt.date) -> bool:
        return any(a <= dia <= b for a, b, _, _ in lidos.get(cnes, []))

    dia = min(a for js in lidos.values() for a, _, _, _ in js)
    ultimo = max(b for js in lidos.values() for _, b, _, _ in js)
    trecho: list[dt.date] = []
    while dia <= ultimo + dt.timedelta(days=1):
        ativas = [c for c in EXECUTANTES if ativa(c, dia.year, dia.month)] if dia <= ultimo else []
        if ativas and all(coberto(c, dia) for c in ativas):
            trecho.append(dia)
        elif trecho:
            ini, fim = trecho[0], trecho[-1]
            n = sum(1 for (_, d) in faltas if ini <= d <= fim)
            quando = max(l for js in lidos.values() for a, b, _, l in js if a <= fim and b >= ini)
            coletas[(ini, "exec:rede")] = (fim, n, quando)
            trecho = []
        dia += dt.timedelta(days=1)
    return faltas, coletas


def main() -> int:
    aplicar = "--aplicar" in sys.argv
    faltas, coletas = montar()
    if not faltas:
        print("Nada coletado ainda.")
        return 0
    inicio = min(d for _, d in faltas)
    fim = max(d for _, d in faltas)
    print(f"coletado: {len(faltas)} faltas de {inicio} a {fim}")
    if coletas:
        for (ini, _), (fim_j, n, _) in sorted(coletas.items()):
            print(f"  cobertura completa (todas as executantes lidas): {ini}..{fim_j} — {n} faltas")
    else:
        print("  nenhum dia com TODAS as executantes lidas ainda: só as faltas entram, sem janela (sem 'em aberto')")
    if fim >= dt.date(2025, 1, 1):
        print("ABORTADO: há janela de 2025 em diante — esse período é da plataforma.")
        return 1

    codigos = sorted({c for c, _ in faltas})
    with db.conn() as c, c.cursor() as cur:
        cur.execute("set local statement_timeout = '180s'")
        cur.execute("select count(*) from smsmarica.sisreg_falta")
        existentes = cur.fetchone()[0]
        print(f"  hoje em sisreg_falta: {existentes}")
        if existentes < MINIMO_FALTAS_EXISTENTES:
            print("ABORTADO: o banco conectado não parece o de produção (sisreg_falta pequena demais).")
            return 1

        cur.execute("""select count(*) from smsmarica.sisreg_falta f
                        where f.codigo_solicitacao = any(%s) and f.data_execucao between %s and %s""",
                    (codigos, inicio, fim))
        print(f"  dessas, já carregadas: {cur.fetchone()[0]}")

        # O efeito na ficha: agendamento Agendada, sem chegada na recepção, que na linha guardada
        # estava PENDENTE e cai num dia lido — vira "Faltou" (está na lista) ou "Em aberto".
        cur.execute("""
            with lidas as (select * from unnest(%s::date[], %s::date[]) as j(ini, fim)),
                 lista as (select * from unnest(%s::text[], %s::date[]) as l(cod, dia)),
                 alvo as (
                   select s.codigo_solicitacao cod, (s.data_agendada at time zone 'America/Sao_Paulo')::date dia
                     from smsmarica.solicitacao s
                    where s.excluido_em is null and s.raw_sisreg is not null and s.status = 2
                      and s.autorizado_em is null
                      and s.codigo_solicitacao is not null and s.codigo_solicitacao <> '0000'
                      and upper(trim(split_part(s.raw_sisreg, ';', 35))) = 'PENDENTE'
                      and s.data_agendada >= %s::date and s.data_agendada < (%s::date + 1))
            select count(*) filter (where exists (select 1 from lista where lista.cod = a.cod and lista.dia = a.dia)),
                   count(*) filter (where exists (select 1 from lidas where a.dia between lidas.ini and lidas.fim)
                                      and not exists (select 1 from lista where lista.cod = a.cod and lista.dia = a.dia))
              from alvo a""",
            ([k[0] for k in coletas], [v[0] for v in coletas.values()],
             [k[0] for k in faltas], [k[1] for k in faltas], inicio, fim))
        faltou, em_aberto = cur.fetchone()
        print(f"  na ficha, pendentes que passam a Faltou: {faltou} (casam código + dia com a lista)")
        print(f"  na ficha, pendentes que passam a Em aberto: {em_aberto} (dia com cobertura completa, fora da lista)")

        if not aplicar:
            print("\nConferência apenas (nada gravado). Para gravar: --aplicar")
            return 0

        agora = dt.datetime.now(dt.timezone.utc)
        execute_values(cur, """
            insert into smsmarica.sisreg_falta (id, codigo_solicitacao, data_execucao, hora, procedimento, unidade_solicitante, lido_em)
            values %s on conflict (codigo_solicitacao, data_execucao) do nothing""",
            [(str(uuid.uuid4()), cod, data, corta(f.get("hora"), 10), corta(f.get("procedimento"), 300),
              corta(f.get("unidade_solicitante"), 200), lido_em) for (cod, data), (f, lido_em) in faltas.items()],
            page_size=2000)
        novas = cur.rowcount
        execute_values(cur, """
            insert into smsmarica.sisreg_indicador_coleta (id, coletor, janela_inicio, janela_fim, escopo, status, tentativas, linhas, iniciado_em, lido_em, criado_em)
            values %s on conflict (coletor, janela_inicio, escopo) do update
              set janela_fim = excluded.janela_fim, status = excluded.status, linhas = excluded.linhas,
                  lido_em = excluded.lido_em, erro = null
            where sisreg_indicador_coleta.escopo like 'exec:%%'""",
            [(str(uuid.uuid4()), COLETOR_FALTAS, ini, fim_j, esc, CONCLUIDA, 1, n, lido_em, lido_em, agora)
             for (ini, esc), (fim_j, n, lido_em) in coletas.items()], page_size=2000)
        c.commit()

        cur.execute("select count(*) from smsmarica.sisreg_falta")
        print(f"\nGRAVADO. sisreg_falta agora: {cur.fetchone()[0]} (inseridas nesta página final: {novas})")
        cur.execute("select count(*) from smsmarica.sisreg_indicador_coleta where escopo like 'exec:%%'")
        print(f"  janelas exec:* em sisreg_indicador_coleta: {cur.fetchone()[0]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
