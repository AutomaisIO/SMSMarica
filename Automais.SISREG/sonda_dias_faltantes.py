"""Os dias 27-28/08 e 02-03/09 do CDT existem no SISREG, ou foram mesmo cancelados?

Contexto: a varredura de 06/09 (janela 05/08-04/09) declarou AUSENTES 100% da agenda desses
quatro dias (177, 125, 193 e 207 marcacoes) enquanto os dias vizinhos ficaram em 0-3,5%. Os dois
intervalos sao exatamente nos-folha da recursao de `ExportarComTetoAsync`, o que aponta para duas
fatias que voltaram vazias e foram aceitas em silencio -- nao para cancelamento.

Esta sonda pergunta ao SISREG so isso. SOMENTE LEITURA (etapa=exportar gera o TXT).

CUSTO: 1 login (ou sessao reaproveitada) + 1 GET + 3 POSTs = ~5 requisicoes do orcamento
anti-robo do OPERADOR (limite ~700, compartilhado com varredura/mapeamento/CADSUS).

O CONTROLE vem primeiro de proposito: 25-26/08 sao dias que a varredura leu bem. Se ele vier
vazio, o canal e que esta quebrado e nenhum outro resultado significa nada.

PII: os TXT tem nome/CNS/telefone de paciente. Nada de corpo e impresso -- so contagem e o
cabecalho (CNES;unidade;dt_ini;dt_fim;total). Os crus vao para --saida, FORA do repositorio.

Uso:
    python sonda_dias_faltantes.py --saida "<pasta fora do repo>"
"""
from __future__ import annotations

import argparse
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SESSAO = BASE / "capturas" / ".sessao_programador.json"
PAUSA = 0.6

MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "class='g-recaptcha'",
    "diferencia&ccedil;&atilde;o entre computadores",
)
MARCAS_SESSAO_MORTA = ("sisreg_erro", "erro ao carregar a sessao", "logon em outra", "foi finalizada")
MARCA_BLOQUEIO = "bloquead"

CNES = "3132358"  # CDT DR ALBERTO LUIS MACHADO BORGES

# (rotulo, data1, data2, esperado_pelo_nosso_banco)
CENARIOS = [
    ("CONTROLE 25-26/08 (lidos ok)", "25/08/2026", "26/08/2026", 369),
    ("SUSPEITA 27-28/08 (100% ausente)", "27/08/2026", "28/08/2026", 302),
    ("SUSPEITA 02-03/09 (100% ausente)", "02/09/2026", "03/09/2026", 400),
]


class CaptchaExigido(RuntimeError):
    pass


def ler_env() -> dict[str, str]:
    for caminho in (BASE.parent / "Automais.SISCAN" / ".env", BASE / ".env"):
        if not caminho.exists():
            continue
        d = {}
        for linha in caminho.read_text(encoding="utf-8").splitlines():
            linha = linha.strip()
            if linha.startswith("#") or "=" not in linha:
                continue
            k, v = linha.split("=", 1)
            if k.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
                d[k.strip()] = v.strip()
        if d.get("SISREG_USUARIO") and d.get("SISREG_SENHA"):
            return d
    return {}


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


def checar(corpo: str, onde: str) -> None:
    if any(m in corpo.lower() for m in MARCAS_CAPTCHA):
        raise CaptchaExigido(f"CAPTCHA em {onde} - parando. Um humano precisa resolver no navegador.")


def diagnostico(corpo: str) -> str:
    baixo = corpo.lower()
    if any(m in baixo for m in MARCAS_SESSAO_MORTA):
        return "SESSAO MORTA"
    if MARCA_BLOQUEIO in baixo:
        return "BLOQUEIO DE HORARIO"
    if "<html" in baixo:
        return "HTML (nao e o TXT)"
    return "TXT"


def resumo(corpo: str) -> tuple[int, str]:
    linhas = [l for l in corpo.splitlines() if l.strip()]
    if not linhas:
        return 0, "(vazio)"
    cab = linhas[0]
    tem_cab = cab.count(";") == 4
    return (len(linhas) - 1 if tem_cab else len(linhas)), (cab if tem_cab else "(sem cabecalho)")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True)
    ap.add_argument("--so-suspeitas", action="store_true")
    ap.add_argument("--apenas", default=None, help="filtra cenarios pelo rotulo")
    ap.add_argument("--timeout", type=float, default=30.0)
    args = ap.parse_args(argv)
    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    env = ler_env()
    if not env:
        print("!! credencial SISREG nao encontrada no .env", file=sys.stderr)
        return 2

    cenarios = [c for c in CENARIOS if not args.so_suspeitas or "SUSPEITA" in c[0]]
    if args.apenas:
        cenarios = [c for c in cenarios if args.apenas.lower() in c[0].lower()]
    print(f"unidade CNES={CNES} (CDT)   recorte: cpf=0 procedimento=0 (unidade inteira)")
    print(f"custo previsto: 1 login + 1 GET + {len(cenarios)} POST = {len(cenarios) + 2} requisicoes\n")

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br",
                      timeout=args.timeout) as cli:
        try:
            estado = "reaproveitada"
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
                estado = "novo_login"
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[1] login: operador={env['SISREG_USUARIO']} sessao={estado}")

        time.sleep(PAUSA)
        formhtml = texto(cli.get("/cgi-bin/expo_solicitacoes"))
        checar(formhtml, "GET do formulario")
        if 'name="cpf"' not in formhtml:
            print(f"[2] formulario nao veio ({len(formhtml)} bytes) - relogando...")
            cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
            cli.salvar_sessao(SESSAO)
            time.sleep(PAUSA)
            formhtml = texto(cli.get("/cgi-bin/expo_solicitacoes"))
            checar(formhtml, "GET do formulario (2a)")
        if MARCA_BLOQUEIO in formhtml.lower():
            print("!! BLOQUEIO DE HORARIO (08h-15h). Rode depois das 15h.", file=sys.stderr)
            return 3
        if 'name="cpf"' not in formhtml:
            print(f"!! formulario sem os campos ({len(formhtml)} bytes) - nao vale gastar POSTs.",
                  file=sys.stderr)
            return 3
        print(f"[2] formulario aberto ({len(formhtml)} bytes), sem bloqueio\n")

        print(f"{'cenario':36} {'tipo':18} {'linhas':>7} {'esperado':>9}  cabecalho")
        print("-" * 116)
        for i, (rotulo, d1, d2, esperado) in enumerate(cenarios, start=3):
            time.sleep(PAUSA)
            resp = cli.post("/cgi-bin/expo_solicitacoes", data={
                "data1": d1, "data2": d2,
                "cpf": "0", "procedimento": "0",
                "tp_arquivo": "0", "etapa": "exportar", "unidade": CNES,
            })
            corpo = texto(resp)
            checar(corpo, rotulo)
            tipo = diagnostico(corpo)
            linhas, cab = resumo(corpo) if tipo == "TXT" else (0, "-")
            (saida / f"sonda_dias_{i:02d}.txt").write_text(corpo, encoding="utf-8")
            print(f"{rotulo:36} {tipo:18} {linhas:>7} {esperado:>9}  {cab[:52]}")

    print(f"\nTXT crus (com PII) em: {saida}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except CaptchaExigido as e:
        print(f"\n!! {e}", file=sys.stderr)
        raise SystemExit(4)
