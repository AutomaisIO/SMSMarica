"""Colhedor de IMPLANTACAO: baixa o TXT bruto do `expo_solicitacoes`, unidade por unidade.

Por que existe fora do .NET: a carga inicial e um trabalho de DIAS, e requisitos de carga sao
opostos aos do incremento diario. O motor do servidor cede a vez a todo mundo, anda uma fatia por
tick e morre a cada deploy -- foi exatamente isso que impediu a implantacao de andar. Aqui o
processo e proprio, o estado mora em disco e um restart nao perde nada.

O que este script NAO faz: importar. A importacao continua no .NET, pelo caminho que ja existe
(upload manual de TXT), que sabe resolver paciente por CNS, marcar identidade incompleta
(ADR-0041), criar procedimento e profissional extintos e guardar o RAW. Duplicar isso em Python
seria refazer meses de armadilhas -- e as duas copias divergiriam na primeira correcao.

SOMENTE LEITURA: `etapa=exportar` apenas gera o arquivo.

Custos medidos em 05/09/2026: uma fatia de 31 dias da unidade inteira sai em 1 requisicao
(`cpf=0` + `procedimento=0`). Rede toda: ~144 fatias para cobrir o futuro e ~1.224 para tres anos
de passado.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SESSAO = BASE / "capturas" / ".sessao_programador.json"

CAMINHO = "/cgi-bin/expo_solicitacoes"
SEM_FILTRO = "0"   # a option-sentinela: devolve a UNIDADE INTEIRA numa requisicao

# O SISREG bloqueia a exportacao neste intervalo (hora de Brasilia). Disparar dentro dele gasta a
# requisicao para receber o alerta de aplicativo bloqueado.
BLOQUEIO_INICIO, BLOQUEIO_FIM = dt.time(8, 0), dt.time(15, 0)

# A sessao morre de varios jeitos e cada um responde diferente. O `expo_solicitacoes` devolve 125
# bytes de <SCRIPT> com redirect -- sem <html>, sem mensagem legivel. Detectar so as marcas de
# pagina de erro deixava isso passar como TXT valido e VAZIO.
MARCAS_SESSAO_MORTA = (
    "logon em outra", "sess&#227;o foi finalizada", "sessao foi finalizada",
    "a sess&atilde;o expirou", "tempo de inatividade",
    "sisreg_erro", "logout=1", "carregar a sessao do operador",
    "carregar a sess&atilde;o do operador",
)
MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "diferencia&ccedil;&atilde;o entre computadores",
)
# Mensagem exata medida em 06/09/2026 as 12:35: o SISREG devolve 165 bytes de <SCRIPT> com um
# alert, sem <html>. Precisa ser "bloqueado" e nao "erro": erro manda tentar de novo, bloqueio
# manda ESPERAR ate as 15h -- confundir os dois faria o colhedor martelar a janela fechada.
MARCAS_BLOQUEIO = ("aplicativo bloqueado", "bloqueado para uso")


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    if not caminho.exists():
        return d
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        k, v = linha.split("=", 1)
        if k.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[k.strip()] = v.strip()
    return d


def credencial() -> dict[str, str]:
    for p in (BASE / ".env", BASE.parent / "Automais.SISCAN" / ".env"):
        env = ler_env(p)
        if env.get("SISREG_USUARIO") and env.get("SISREG_SENHA"):
            return env
    return {}


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


# Brasilia FIXO em UTC-3, sem horario de verao -- e a regra unica de fuso do repositorio. Nao usar
# `datetime.now()`: aqui a maquina esta em Brasilia por acaso, mas o servidor roda em UTC e a
# checagem erraria por 3 horas, gastando requisicao dentro do bloqueio ou recusando fora dele.
BRASILIA = dt.timezone(dt.timedelta(hours=-3))


def agora_brasilia() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc).astimezone(BRASILIA)


def dentro_do_bloqueio(agora: dt.datetime) -> bool:
    return BLOQUEIO_INICIO <= agora.time() < BLOQUEIO_FIM


class Colhedor:
    def __init__(self, cli: SisregClient, usuario: str, senha: str, saida: pathlib.Path):
        self.cli, self.usuario, self.senha = cli, usuario, senha
        self.saida = saida
        self.requisicoes = 0

    def _relogar(self) -> None:
        self.cli.login(self.usuario, self.senha)
        self.cli.salvar_sessao(SESSAO)

    def exportar(self, cnes: str, inicio: dt.date, fim: dt.date) -> tuple[str, str]:
        """Devolve (situacao, conteudo). Situacao: ok | vazio | captcha | bloqueado | erro."""
        campos = {
            "data1": inicio.strftime("%d/%m/%Y"),
            "data2": fim.strftime("%d/%m/%Y"),
            "cpf": SEM_FILTRO,
            "procedimento": SEM_FILTRO,
            "tp_arquivo": "0",     # 0 = TXT
            "etapa": "exportar",
            "unidade": cnes,
        }

        corpo = texto(self.cli.post(CAMINHO, data=campos))
        self.requisicoes += 1

        if any(m in corpo.lower() for m in MARCAS_SESSAO_MORTA):
            self._relogar()
            corpo = texto(self.cli.post(CAMINHO, data=campos))
            self.requisicoes += 1

        baixo = corpo.lower()
        if any(m in baixo for m in MARCAS_CAPTCHA):
            return "captcha", corpo
        if any(m in baixo for m in MARCAS_BLOQUEIO):
            return "bloqueado", corpo

        # RECONHECER a forma, nunca assumir. O TXT valido comeca por um cabecalho
        # "CNES;Nome;dt_ini;dt_fim;total". Qualquer outra coisa -- HTML, <SCRIPT> de redirect,
        # pagina de erro -- e recusa, e chamar isso de "vazio" seria gravar um buraco em disco e
        # concluir depois que a unidade nao tinha movimento naquele mes.
        linhas = [l for l in corpo.splitlines() if l.strip()]
        if not linhas or not self._parece_cabecalho(linhas[0]):
            return "erro", corpo

        # CONFERENCIA DE INTEGRIDADE, de graca: o cabecalho declara o total e o SISREG cumpre.
        # Medido em 06/09/2026 no CDT -- declarou 3711, entregou 3711. E a defesa contra corte
        # silencioso: baixar 700 de 3.711 e gravar como se fosse o mes inteiro produziria um buraco
        # que ninguem teria como perceber depois.
        declarado = self._total_declarado(linhas[0])
        entregues = len(linhas) - 1
        if declarado is not None and declarado != entregues:
            return f"truncado:{entregues}/{declarado}", corpo

        return ("vazio" if len(linhas) == 1 else "ok"), corpo

    @staticmethod
    def _total_declarado(cabecalho: str) -> int | None:
        campos = cabecalho.split(";")
        if len(campos) < 5:
            return None
        return int(campos[4]) if campos[4].strip().isdigit() else None

    @staticmethod
    def _parece_cabecalho(linha: str) -> bool:
        """CNES;Nome;dt_ini;dt_fim;total — o CNES sao 7 digitos."""
        campos = linha.split(";")
        return len(campos) >= 4 and campos[0].strip().isdigit() and len(campos[0].strip()) == 7

    def salvar(self, cnes: str, inicio: dt.date, fim: dt.date, conteudo: str) -> pathlib.Path:
        pasta = self.saida / cnes
        pasta.mkdir(parents=True, exist_ok=True)
        alvo = pasta / f"sisreg-unidade-{inicio:%Y%m%d}-{fim:%Y%m%d}.txt"
        alvo.write_text(conteudo, encoding="utf-8")
        return alvo


def colher_unidade(c: "Colhedor", cnes: str, ultima_escala: dt.date,
                   vazios_para_parar: int, pausa_s: float) -> dict:
    """Frente e tras a partir de hoje, em fatias de 31 dias.

    Idempotente: fatia com arquivo valido em disco e pulada, entao rodar de novo depois de uma
    interrupcao continua de onde parou sem gastar requisicao.
    """
    hoje = agora_brasilia().date()
    resumo = {"ok": 0, "vazio": 0, "pulado": 0, "requisicoes": 0, "registros": 0, "fatias": []}

    def uma(inicio: dt.date, fim: dt.date) -> str:
        alvo = c.saida / cnes / f"sisreg-unidade-{inicio:%Y%m%d}-{fim:%Y%m%d}.txt"
        if alvo.exists() and alvo.stat().st_size > 0:
            resumo["pulado"] += 1
            print(f"  {inicio:%d/%m/%Y}-{fim:%d/%m/%Y}  ja em disco, pulando")
            return "pulado"

        if dentro_do_bloqueio(agora_brasilia()):
            print("  !! entrou na janela de bloqueio (08:00-15:00) — parando por aqui")
            return "bloqueado"

        situacao, corpo = c.exportar(cnes, inicio, fim)
        if situacao == "bloqueado":
            print("  !! SISREG bloqueado por horario — parando por aqui")
            return "bloqueado"
        if situacao.startswith("truncado"):
            print(f"  !! {inicio:%d/%m/%Y}-{fim:%d/%m/%Y}  TRUNCADO ({situacao}) — nao gravado")
            return "erro"
        if situacao == "erro":
            print(f"  !! {inicio:%d/%m/%Y}-{fim:%d/%m/%Y}  recusado pelo SISREG — nao gravado")
            return "erro"

        linhas = [l for l in corpo.splitlines() if l.strip()]
        n = max(0, len(linhas) - 1)
        c.salvar(cnes, inicio, fim, corpo)
        resumo[situacao] += 1
        resumo["registros"] += n
        resumo["fatias"].append({"de": f"{inicio}", "ate": f"{fim}", "registros": n})
        print(f"  {inicio:%d/%m/%Y}-{fim:%d/%m/%Y}  {situacao:5}  {n:6,} registros")
        time.sleep(pausa_s)
        return situacao

    print()
    print(f"== FUTURO: de hoje ate {ultima_escala:%d/%m/%Y} ==")
    ini = hoje
    while ini <= ultima_escala:
        fim = min(ini + dt.timedelta(days=30), ultima_escala)
        if uma(ini, fim) == "bloqueado":
            return resumo
        ini = fim + dt.timedelta(days=1)

    print()
    print(f"== PASSADO: para depois de {vazios_para_parar} fatias vazias seguidas ==")
    vazios = 0
    fim = hoje - dt.timedelta(days=1)
    while vazios < vazios_para_parar:
        ini = fim - dt.timedelta(days=30)
        r = uma(ini, fim)
        if r == "bloqueado":
            return resumo
        if r == "vazio":
            vazios += 1
            print(f"     ({vazios}/{vazios_para_parar} vazias seguidas)")
        elif r == "ok":
            vazios = 0
        fim = ini - dt.timedelta(days=1)

    print()
    print(f"== fim do passado: {vazios_para_parar} fatias seguidas sem registro ==")
    return resumo


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cnes", required=True)
    ap.add_argument("--de", help="dd/mm/aaaa (fatia unica)")
    ap.add_argument("--ate", help="dd/mm/aaaa (fatia unica)")
    ap.add_argument("--completa", action="store_true",
                    help="unidade inteira: frente ate a ultima escala e tras ate N vazias")
    ap.add_argument("--ultima-escala", help="dd/mm/aaaa ate onde ir no futuro")
    ap.add_argument("--vazias-para-parar", type=int, default=10)
    ap.add_argument("--pausa", type=float, default=2.0)
    ap.add_argument("--saida", default=str(BASE / "capturas" / "implantacao"))
    ap.add_argument("--ignorar-bloqueio", action="store_true")
    args = ap.parse_args(argv)

    if not args.completa and (not args.de or not args.ate):
        print("!! use --completa ou --de/--ate", file=sys.stderr)
        return 2

    inicio = dt.datetime.strptime(args.de, "%d/%m/%Y").date() if args.de else agora_brasilia().date()
    fim = dt.datetime.strptime(args.ate, "%d/%m/%Y").date() if args.ate else inicio
    if not args.completa and (fim - inicio).days > 30:
        print("!! o SISREG recusa intervalo maior que 31 dias", file=sys.stderr)
        return 2

    agora = agora_brasilia()
    if dentro_do_bloqueio(agora) and not args.ignorar_bloqueio:
        print(f"!! {agora:%H:%M} (Brasilia) esta na janela de bloqueio "
              f"({BLOQUEIO_INICIO:%H:%M}-{BLOQUEIO_FIM:%H:%M}); a requisicao seria desperdicada.",
              file=sys.stderr)
        return 3

    env = credencial()
    if not env:
        print("!! credencial SISREG nao encontrada", file=sys.stderr)
        return 2

    saida = pathlib.Path(args.saida)
    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br",
                      timeout=180.0) as cli:
        try:
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1

        c = Colhedor(cli, env["SISREG_USUARIO"], env["SISREG_SENHA"], saida)
        print(f"[login] {env['SISREG_USUARIO']}")

        if args.completa:
            if not args.ultima_escala:
                print("!! --completa exige --ultima-escala", file=sys.stderr)
                return 2
            ate = dt.datetime.strptime(args.ultima_escala, "%d/%m/%Y").date()
            print(f"[unidade] CNES {args.cnes} — completa (futuro ate {ate:%d/%m/%Y}, "
                  f"passado ate {args.vazias_para_parar} vazias)")
            t0 = time.monotonic()
            r = colher_unidade(c, args.cnes, ate, args.vazias_para_parar, args.pausa)
            print()
            print(f"=== RESUMO CNES {args.cnes} ===")
            print(f"  fatias com dados : {r['ok']}")
            print(f"  fatias vazias    : {r['vazio']}")
            print(f"  ja em disco      : {r['pulado']}")
            print(f"  registros        : {r['registros']:,}")
            print(f"  requisicoes      : {c.requisicoes}")
            print(f"  tempo            : {(time.monotonic() - t0)/60:.1f} min")
            (saida / args.cnes / "_resumo.json").write_text(
                json.dumps(r, indent=2, ensure_ascii=False), encoding="utf-8")
            return 0

        print(f"[fatia] CNES {args.cnes}: {inicio:%d/%m/%Y} a {fim:%d/%m/%Y}")
        t0 = time.monotonic()
        situacao, conteudo = c.exportar(args.cnes, inicio, fim)
        seg = time.monotonic() - t0
        print(f"[resultado] {situacao}  ({len(conteudo):,} bytes, {seg:.1f}s, "
              f"{c.requisicoes} requisicao(oes))")

        if situacao in ("ok", "vazio"):
            alvo = c.salvar(args.cnes, inicio, fim, conteudo)
            linhas = [l for l in conteudo.splitlines() if l.strip()]
            print(f"[arquivo] {alvo}")
            print(f"[cabecalho] {linhas[0][:120] if linhas else '(vazio)'}")
            print(f"[linhas de dados] {max(0, len(linhas) - 1):,}")
        else:
            print("--- inicio da resposta ---")
            print(conteudo[:400])

        return 0 if situacao in ("ok", "vazio") else 4


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
