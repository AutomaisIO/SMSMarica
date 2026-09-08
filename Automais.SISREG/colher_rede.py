"""Colhe a REDE INTEIRA para a implantacao: todas as unidades, futuro e passado.

Roda por horas. Tudo aqui existe para que uma interrupcao -- queda, restart, Ctrl+C -- nao custe
nada: as fatias ja em disco sao puladas, e o consumo de requisicoes e persistido, entao religar
nao "esquece" o que ja foi gasto e nao estoura o teto por reinicio.

## Teto anti-robo

O orcamento e do OPERADOR, nao nosso: quem paga o estouro e a pessoa que usa o SISREG, com CAPTCHA
e pausa de 24 h na unidade. O alerta do SISREG e acima de 500/h; aqui o alvo e 300/h e o teto duro
350/h, medidos numa janela DESLIZANTE de 60 minutos (nao "por hora cheia", que permitiria 350 as
13:59 e mais 350 as 14:01).

## Janela de bloqueio

O `expo_solicitacoes` responde "Aplicativo bloqueado para uso de 8 as 15 horas". Dentro dela o
colhedor DORME ate as 15:00 em vez de sair -- uma colheita de dias nao pode exigir que alguem a
religue toda manha.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import sys
import time
from collections import deque

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from colher_implantacao import (  # noqa: E402
    BLOQUEIO_FIM, Colhedor, SESSAO, agora_brasilia, credencial, dentro_do_bloqueio,
)
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SAIDA = BASE / "capturas" / "implantacao"
UNIDADES = SAIDA / "_unidades.json"
GASTOS = SAIDA / "_requisicoes.json"

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)
except AttributeError:
    pass


def agora() -> float:
    return time.time()


class Governador:
    """Janela deslizante de 60 min. Persiste em disco: religar nao zera o consumo."""

    def __init__(self, alvo: int, teto: int):
        self.alvo, self.teto = alvo, teto
        self.marcas: deque[float] = deque()
        if GASTOS.exists():
            try:
                antigas = json.loads(GASTOS.read_text(encoding="utf-8"))
                corte = agora() - 3600
                self.marcas.extend(t for t in antigas if t > corte)
            except (ValueError, OSError):
                pass

    def _limpar(self) -> None:
        corte = agora() - 3600
        while self.marcas and self.marcas[0] <= corte:
            self.marcas.popleft()

    @property
    def na_hora(self) -> int:
        self._limpar()
        return len(self.marcas)

    def aguardar_vaga(self) -> None:
        """Bloqueia ate caber mais uma requisicao sem passar do alvo."""
        while True:
            self._limpar()
            if len(self.marcas) < self.alvo:
                return
            # Espera a marca mais antiga sair da janela, com folga de 1 s.
            espera = max(1.0, 3600 - (agora() - self.marcas[0]) + 1)
            print(f"    [taxa] {len(self.marcas)}/{self.alvo} na ultima hora — "
                  f"aguardando {espera/60:.1f} min")
            time.sleep(min(espera, 120))

    def registrar(self, quantas: int = 1) -> None:
        t = agora()
        for _ in range(quantas):
            self.marcas.append(t)
        try:
            GASTOS.write_text(json.dumps(list(self.marcas)), encoding="utf-8")
        except OSError:
            pass


def dormir_ate_abrir() -> None:
    a = agora_brasilia()
    abre = a.replace(hour=BLOQUEIO_FIM.hour, minute=BLOQUEIO_FIM.minute, second=0, microsecond=0)
    if abre <= a:
        abre += dt.timedelta(days=1)
    seg = (abre - a).total_seconds()
    print(f"  [bloqueio] {a:%H:%M} Brasilia — dormindo {seg/3600:.1f} h ate {abre:%H:%M}")
    while (agora_brasilia() < abre):
        time.sleep(min(300, max(30, (abre - agora_brasilia()).total_seconds())))


def janelas(hoje: dt.date, ultima_escala: dt.date):
    """Futuro primeiro (a operacao usa), depois o passado, recuando."""
    ini = hoje
    while ini <= ultima_escala:
        fim = min(ini + dt.timedelta(days=30), ultima_escala)
        yield ini, fim, "futuro"
        ini = fim + dt.timedelta(days=1)

    fim = hoje - dt.timedelta(days=1)
    while True:
        yield fim - dt.timedelta(days=30), fim, "passado"
        fim = fim - dt.timedelta(days=31)


def colher(c: Colhedor, gov: Governador, u: dict, vazias_passado: int, vazias_futuro: int,
           pausa: float) -> dict:
    cnes, nome = u["cnes"], u["nome"]
    ultima = dt.date.fromisoformat(u["ultima_escala"])
    hoje = agora_brasilia().date()

    r = {"cnes": cnes, "nome": nome, "ok": 0, "vazio": 0, "pulado": 0, "erro": 0,
         "registros": 0, "inicio": dt.datetime.now().isoformat(timespec="seconds")}

    # Contador POR FASE, com TETOS DIFERENTES -- e a assimetria e real, nao economia preguicosa:
    #
    #   FUTURO: mes vazio e conclusivo. Ninguem marca para 2028 se ja nao marca para 2027; a agenda
    #   rareia e acaba, nao volta. E o horizonte ja vem do ponteiro da escala, entao aqui bastam
    #   poucas vazias como rede de seguranca.
    #
    #   PASSADO: um vao pode ser hiato de verdade -- reforma, ferias coletivas, troca de sistema --
    #   com movimento antes dele. Cortar cedo apagaria o comeco da unidade.
    vazias = 0
    fase_atual = None
    tetos = {"futuro": vazias_futuro, "passado": vazias_passado}

    for inicio, fim, fase in janelas(hoje, ultima):
        if fase != fase_atual:
            fase_atual, vazias = fase, 0
        if vazias >= tetos[fase]:
            if fase == "passado":
                break
            continue

        alvo = c.saida / cnes / f"sisreg-unidade-{inicio:%Y%m%d}-{fim:%Y%m%d}.txt"
        if alvo.exists() and alvo.stat().st_size > 0:
            r["pulado"] += 1
            # Um arquivo so com cabecalho conta como vazio para a regra de parada.
            if sum(1 for _ in alvo.open(encoding="utf-8", errors="replace")) <= 1:
                vazias += 1
            else:
                vazias = 0
            continue

        while dentro_do_bloqueio(agora_brasilia()):
            dormir_ate_abrir()

        gov.aguardar_vaga()
        antes = c.requisicoes
        situacao, corpo = c.exportar(cnes, inicio, fim)
        gov.registrar(c.requisicoes - antes)

        if situacao == "bloqueado":
            dormir_ate_abrir()
            continue

        if situacao == "erro" or situacao.startswith("truncado"):
            r["erro"] += 1
            print(f"  !! {cnes} {inicio:%d/%m/%y}-{fim:%d/%m/%y} {situacao} — nao gravado")
            continue

        linhas = [l for l in corpo.splitlines() if l.strip()]
        n = max(0, len(linhas) - 1)
        c.salvar(cnes, inicio, fim, corpo)
        r[situacao] += 1
        r["registros"] += n

        if situacao == "vazio":
            vazias += 1
        else:
            vazias = 0

        print(f"  {cnes} {inicio:%d/%m/%y}-{fim:%d/%m/%y} {fase:7} {situacao:5} "
              f"{n:6,} reg   [taxa {gov.na_hora}/{gov.alvo}]")
        time.sleep(pausa)

    r["fim"] = dt.datetime.now().isoformat(timespec="seconds")
    (c.saida / cnes / "_resumo.json").write_text(
        json.dumps(r, indent=2, ensure_ascii=False), encoding="utf-8")
    return r


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--alvo", type=int, default=300, help="requisicoes/hora alvo")
    ap.add_argument("--teto", type=int, default=350, help="teto duro/hora")
    ap.add_argument("--vazias-para-parar", type=int, default=10,
                    help="vazias seguidas que encerram o PASSADO")
    ap.add_argument("--vazias-futuro", type=int, default=3,
                    help="vazias seguidas que encerram o FUTURO — no futuro, vazio e conclusivo")
    ap.add_argument("--pausa", type=float, default=1.0)
    ap.add_argument("--pular-cnes", action="append", default=[])
    args = ap.parse_args(argv)

    if not UNIDADES.exists():
        print(f"!! falta {UNIDADES}", file=sys.stderr)
        return 2

    unidades = json.loads(UNIDADES.read_text(encoding="utf-8"))
    unidades = [u for u in unidades if u["cnes"] not in args.pular_cnes]

    env = credencial()
    if not env:
        print("!! credencial SISREG nao encontrada", file=sys.stderr)
        return 2

    gov = Governador(args.alvo, args.teto)
    print(f"== colheita da rede: {len(unidades)} unidades ==")
    print(f"   taxa alvo {args.alvo}/h (teto {args.teto}/h), "
          f"ja gastas na ultima hora: {gov.na_hora}")

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br",
                      timeout=300.0) as cli:
        try:
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1

        c = Colhedor(cli, env["SISREG_USUARIO"], env["SISREG_SENHA"], SAIDA)
        geral = []

        for i, u in enumerate(unidades, 1):
            print()
            print(f"[{i}/{len(unidades)}] {u['cnes']}  {u['nome'][:52]}  "
                  f"(escala ate {u['ultima_escala']})")
            r = colher(c, gov, u, args.vazias_para_parar, args.vazias_futuro, args.pausa)
            geral.append(r)
            print(f"  -> {r['ok']} com dados, {r['vazio']} vazias, {r['pulado']} pulados, "
                  f"{r['erro']} erros, {r['registros']:,} registros")
            (SAIDA / "_geral.json").write_text(
                json.dumps(geral, indent=2, ensure_ascii=False), encoding="utf-8")

        print()
        print("=== FIM ===")
        print(f"  unidades: {len(geral)}")
        print(f"  registros: {sum(x['registros'] for x in geral):,}")
        print(f"  requisicoes nesta execucao: {c.requisicoes}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
