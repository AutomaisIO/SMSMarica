"""Relê a CHEGADA do paciente (coluna 34 do Arquivo de Agendamentos) de um período passado e grava só ela.

Nasceu em 01/10/2026 para agosto/2026: a varredura diária passou a reler os últimos 31 dias, e agosto
ficou de fora com 1.958 agendamentos "pendentes" lidos ANTES do atendimento (25 unidades). Os meses
anteriores não precisam — a carga do histórico os leu 30+ dias depois do fato (medido no banco).

Três passos, separados de propósito:
  --coletar   exporta o TXT da UNIDADE INTEIRA (`cpf=0` + `procedimento=0`, 1 requisição por unidade
              e fatia de até 31 dias) das unidades que têm pendente no período. Fora das 08h–15h
              (o `expo_solicitacoes` fica bloqueado). Usa o PROGRAMADOR-BERNARDO: só com OK.
  (padrão)    conferência: quantas chegadas o arquivo traz e quantas solicitações mudariam. Só leitura.
  --aplicar   grava, com o MESMO UPDATE de `ChegadasSisregService.GravarAsync` (casa por código E dia;
              não regrava confirmado que segue confirmado; carimba `chegada_sisreg_lida_em`).

Não importa nada, não muda data, procedimento nem status, não avisa paciente.
Os TXT têm PII e ficam em capturas/chegadas/ (gitignored).

Uso:
  python reler_chegadas_periodo.py --de 2026-08-01 --ate 2026-08-31 --coletar
  python reler_chegadas_periodo.py --de 2026-08-01 --ate 2026-08-31
  python reler_chegadas_periodo.py --de 2026-08-01 --ate 2026-08-31 --aplicar
"""
from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import sys

BASE = pathlib.Path(__file__).parent
RAIZ = BASE.parent
sys.path.insert(0, str(BASE))
sys.path.insert(0, str(RAIZ / "Aprendizados e Scratchpads" / "ferramentas"))
import db  # noqa: E402
from colher_implantacao import Colhedor, SESSAO, agora_brasilia, credencial, dentro_do_bloqueio  # noqa: E402
from sisreg import SisregClient  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SAIDA = BASE / "capturas" / "chegadas"
LOTE = 2000

# Colunas do TXT (mesmos índices de AgendaTxtParser no .NET).
COL_CODIGO, COL_DATA, COL_CHEGADA, TOTAL_CAMPOS = 0, 6, 34, 38


def unidades_com_pendente(de: dt.date, ate: dt.date) -> list[tuple[str, str, int]]:
    """(CNES, nome, pendentes) das executantes com agendamento passado ainda 'pendente' no período."""
    with db.conn() as c, c.cursor() as cur:
        cur.execute("""
            select u.cnes, u.nome, count(*)
              from smsmarica.solicitacao s
              join smsmarica.unidade u on u.id = s.unidade_executante_id
             where s.excluido_em is null and s.raw_sisreg is not null and s.status = 2
               and s.autorizado_em is null and s.chegada_confirmada_sisreg is distinct from true
               and left(ltrim(s.raw_sisreg), 1) <> '{'
               and upper(trim(split_part(s.raw_sisreg, ';', 35))) = 'PENDENTE'
               and (s.data_agendada at time zone 'America/Sao_Paulo')::date between %s and %s
               and u.cnes ~ '^[0-9]{7}$'
             group by u.cnes, u.nome order by 3 desc""", (de, ate))
        return cur.fetchall()


def fatias(de: dt.date, ate: dt.date) -> list[tuple[dt.date, dt.date]]:
    out, ini = [], de
    while ini <= ate:
        fim = min(ate, ini + dt.timedelta(days=30))
        out.append((ini, fim))
        ini = fim + dt.timedelta(days=1)
    return out


def arquivo(cnes: str, ini: dt.date, fim: dt.date) -> pathlib.Path:
    return SAIDA / cnes / f"chegadas-{ini:%Y%m%d}-{fim:%Y%m%d}.txt"


def coletar(de: dt.date, ate: dt.date, max_req: int) -> int:
    if dentro_do_bloqueio(agora_brasilia()):
        print("O expo_solicitacoes fica bloqueado das 08h às 15h (Brasília). Rode depois das 15h.")
        return 3
    alvo = unidades_com_pendente(de, ate)
    print(f"{len(alvo)} unidades com pendente em {de}..{ate}")
    env = credencial()
    cli = SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br", timeout=170)
    cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
    cli.salvar_sessao(SESSAO)
    col = Colhedor(cli, env["SISREG_USUARIO"], env["SISREG_SENHA"], SAIDA)
    col.requisicoes = 2
    try:
        for cnes, nome, pend in alvo:
            for ini, fim in fatias(de, ate):
                destino = arquivo(cnes, ini, fim)
                if destino.exists() and destino.stat().st_size > 0:
                    continue
                if col.requisicoes >= max_req:
                    print(f"teto de {max_req} requisições — rode de novo para continuar")
                    return 0
                if dentro_do_bloqueio(agora_brasilia()):
                    print("entrou na janela de bloqueio (08h–15h) — parando")
                    return 3
                situacao, corpo = col.exportar(cnes, ini, fim)
                if situacao == "captcha":
                    print("!! CAPTCHA — PARANDO. Um humano precisa abrir o SISREG no navegador com esse operador.")
                    return 4
                if situacao in ("ok", "vazio"):
                    destino.parent.mkdir(parents=True, exist_ok=True)
                    destino.write_text(corpo, encoding="utf-8")
                    linhas = max(0, len([l for l in corpo.splitlines() if l.strip()]) - 1)
                    print(f"  {nome[:40]:40} {ini}..{fim}: {linhas:5} agendamentos ({pend} pendentes nossos)")
                else:
                    print(f"  ! {nome[:40]:40} {ini}..{fim}: {situacao} — não gravado")
    finally:
        try:
            cli.logout()
            col.requisicoes += 1
        except Exception:
            pass
        print(f"[fim] requisições gastas: {col.requisicoes}")
    return 0


def ler_chegadas(de: dt.date, ate: dt.date, hoje: dt.date) -> dict[tuple[str, dt.date], bool]:
    """(código, dia) -> confirmada, só de dias já passados. Repetido na fronteira: basta um CONFIRMADO."""
    lidas: dict[tuple[str, dt.date], bool] = {}
    for txt in sorted(SAIDA.glob("*/chegadas-*.txt")):
        ini, fim = (dt.datetime.strptime(x, "%Y%m%d").date() for x in txt.stem.split("-")[1:3])
        if fim < de or ini > ate:
            continue
        for linha in txt.read_text(encoding="utf-8").splitlines()[1:]:
            c = linha.split(";")
            if len(c) < TOTAL_CAMPOS or not c[COL_CODIGO].strip().isdigit():
                continue
            try:
                dia = dt.datetime.strptime(c[COL_DATA].strip(), "%d.%m.%Y").date()
            except ValueError:
                continue
            chegada = c[COL_CHEGADA].strip().upper()
            if chegada not in ("CONFIRMADO", "PENDENTE") or dia > hoje or not de <= dia <= ate:
                continue
            k = (c[COL_CODIGO].strip(), dia)
            lidas[k] = lidas.get(k, False) or chegada == "CONFIRMADO"
    return lidas


# O UPDATE de ChegadasSisregService.GravarAsync, palavra por palavra (só os parâmetros mudam de forma).
UPDATE = """
    update smsmarica.solicitacao s
       set chegada_confirmada_sisreg = x.c, chegada_sisreg_lida_em = %(agora)s
      from unnest(%(codigos)s::text[], %(dias)s::date[], %(confirmadas)s::boolean[]) as x(cod, dia, c)
     where s.codigo_solicitacao = x.cod
       and s.codigo_solicitacao is not null and s.codigo_solicitacao <> '0000' and s.excluido_em is null
       and (s.data_agendada at time zone 'America/Sao_Paulo')::date = x.dia
       and (s.chegada_confirmada_sisreg is distinct from x.c or not x.c)
"""


def gravar(de: dt.date, ate: dt.date, aplicar: bool) -> int:
    hoje = agora_brasilia().date()
    lidas = ler_chegadas(de, ate, hoje)
    confirmadas = sum(1 for v in lidas.values() if v)
    print(f"no arquivo: {len(lidas)} agendamentos passados · {confirmadas} CONFIRMADO · {len(lidas) - confirmadas} PENDENTE")
    if not lidas:
        return 0
    itens = list(lidas.items())
    with db.conn() as c, c.cursor() as cur:
        cur.execute("set local statement_timeout = '300s'")
        # Quantos dos nossos pendentes viram "Compareceu".
        cur.execute("""
            select count(*) from smsmarica.solicitacao s
              join unnest(%s::text[], %s::date[], %s::boolean[]) as x(cod, dia, c)
                on s.codigo_solicitacao = x.cod
               and (s.data_agendada at time zone 'America/Sao_Paulo')::date = x.dia
             where s.codigo_solicitacao is not null and s.codigo_solicitacao <> '0000' and s.excluido_em is null
               and s.status = 2 and s.autorizado_em is null and x.c
               and upper(trim(split_part(s.raw_sisreg, ';', 35))) = 'PENDENTE'
               and s.chegada_confirmada_sisreg is distinct from true""",
            ([k[0] for k, _ in itens], [k[1] for k, _ in itens], [v for _, v in itens]))
        viram = cur.fetchone()[0]
        print(f"  pendentes nossos que passam a 'Compareceu': {viram}")
        if not aplicar:
            print("\nConferência apenas (nada gravado). Para gravar: --aplicar")
            return 0
        agora = dt.datetime.now(dt.timezone.utc)
        total = 0
        for i in range(0, len(itens), LOTE):
            lote = itens[i:i + LOTE]
            cur.execute(UPDATE, {"agora": agora, "codigos": [k[0] for k, _ in lote],
                                 "dias": [k[1] for k, _ in lote], "confirmadas": [v for _, v in lote]})
            total += cur.rowcount
        c.commit()
        print(f"\nGRAVADO: {total} solicitação(ões) com a chegada relida (lida_em {agora:%d/%m/%Y %H:%M} UTC).")
    return 0


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--de", required=True, type=dt.date.fromisoformat)
    ap.add_argument("--ate", required=True, type=dt.date.fromisoformat)
    ap.add_argument("--coletar", action="store_true")
    ap.add_argument("--aplicar", action="store_true")
    ap.add_argument("--max-req", type=int, default=60)
    a = ap.parse_args(argv)
    if a.coletar:
        return coletar(a.de, a.ate, a.max_req)
    return gravar(a.de, a.ate, a.aplicar)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
