"""Sonda o `gerenciador_solicitacao` para trazer a FILA DE ESPERA (nao agendados).

O que esta em teste, e por que importa:

1. `cmb_situacao=2` e "Solicitacao / Pendente / Fila de Espera" — exatamente quem
   esta aguardando vaga, que hoje nao existe no nosso banco (medido 05/09/2026:
   22.081 de 22.101 solicitacoes ja tinham dia marcado).

2. `qtd_itens_pag=0` e a opcao "TODOS" do combo. SE o servidor honrar, a fila de um
   mes inteiro sai em UMA requisicao. Isso e o que decide o custo: com janela de 31
   dias (o teto do `validaFormulario`), 5 anos de fila custam ~60 requisicoes.

3. TETO SILENCIOSO. O `expo_solicitacoes` corta em 700 registros sem avisar. Por isso
   esta sonda NAO confia na contagem de linhas: ela procura o total declarado na
   pagina e COMPARA com o que veio. Divergencia = teto, e a estrategia muda.

Regras obedecidas: somente LISTAR (nunca CANCELAR_SOLICITACAO/REENVIAR_REGULACAO),
teto rigido de requisicoes por execucao, e parada imediata se aparecer captcha — o
orcamento anti-robo e do OPERADOR e o estouro pausa a unidade por 24h.
"""
from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SESSAO = BASE / "capturas" / ".sessao_programador.json"

MARCAS_SESSAO_MORTA = (
    "logon em outra", "sess&#227;o foi finalizada", "sessao foi finalizada",
    "a sess&atilde;o expirou", "tempo de inatividade",
)
MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "diferencia&ccedil;&atilde;o entre computadores",
)

SITUACOES = {
    1: "Solicitacao / Pendente / Regulacao",
    2: "Solicitacao / Pendente / Fila de Espera",
    4: "Solicitacao / Devolvida",
    7: "Solicitacao / Agendada",
    9: "Solicitacao / Agendada / Fila de Espera",
}


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    if not caminho.exists():
        return d
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        if chave.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[chave.strip()] = valor.strip()
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


def consulta(situacao: int, d1: dt.date, d2: dt.date, por_pagina: int, pagina: int = 0,
             tipo_periodo: str = "S") -> dict:
    """Os campos exatamente como o formulario os envia (METHOD=GET)."""
    return {
        "etapa": "LISTAR_SOLICITACOES",
        "co_solicitacao": "",
        "cns_paciente": "",
        "no_usuario": "",
        "cnes_solicitante": "",
        "cnes_executante": "",
        "co_proc_unificado": "",
        "co_pa_interno": "",
        "ds_procedimento": "",
        "tipo_periodo": tipo_periodo,  # S = data de SOLICITACAO; A = data de AGENDAMENTO
        "dt_inicial": d1.strftime("%d/%m/%Y"),
        "dt_final": d2.strftime("%d/%m/%Y"),
        "cmb_situacao": str(situacao),
        "qtd_itens_pag": str(por_pagina),
        "co_seq_solicitacao": "",
        "ordenacao": "2",
        "pagina": str(pagina),
    }


def analisar(html: str) -> dict:
    """Linhas devolvidas x total declarado — a divergencia denuncia teto silencioso."""
    r: dict = {}

    linhas = len(re.findall(r"exibirFichaCompleta|exibirFichaReduzida|visualizaFicha", html))
    r["ancoras_ficha"] = linhas

    trs = re.findall(r"<tr[^>]*>", html, re.I)
    r["trs"] = len(trs)

    # O SISREG costuma anunciar "Total de registros: N" ou "N registro(s)".
    for padrao in (r"[Tt]otal\s*(?:de)?\s*[Rr]egistros?\s*[:\-]?\s*(\d+)",
                   r"(\d+)\s*registros?\s*encontrados?",
                   r"[Ee]ncontrad[ao]s?\s*(\d+)"):
        m = re.search(padrao, html)
        if m:
            r["total_declarado"] = int(m.group(1))
            break

    m = re.search(r"exibirPagina\(\s*\d+\s*,\s*(\d+)\s*\)", html)
    if m:
        r["paginas"] = int(m.group(1))

    for aviso in ("Nenhum registro", "nenhuma solicita", "N&atilde;o foram encontrados",
                  "Nao foram encontrados"):
        if aviso.lower() in html.lower():
            r["vazio"] = True

    return r


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True)
    ap.add_argument("--max-req", type=int, default=4,
                    help="teto rigido de requisicoes nesta execucao")
    ap.add_argument("--situacao", type=int, action="append",
                    help="situacao a sondar (repetivel); default 2")
    ap.add_argument("--de", help="dd/mm/aaaa inicial")
    ap.add_argument("--ate", help="dd/mm/aaaa final")
    ap.add_argument("--por-pagina", type=int, default=0,
                    help="qtd_itens_pag; 0=TODOS (pesado: ja deu ReadTimeout em situacao 7)")
    ap.add_argument("--timeout", type=float, default=120.0)
    ap.add_argument("--tipo-periodo", default="S",
                    help="S=solicitacao A=agendamento E=execucao P=confirmacao C=cancelamento")
    args = ap.parse_args(argv)
    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    env = credencial()
    if not env:
        print("!! credencial SISREG nao encontrada", file=sys.stderr)
        return 2

    hoje = dt.date.today()
    # Sondas do mais barato para o mais caro: primeiro provar que a consulta responde
    # e como e a forma; so entao esticar a janela ate o teto de 31 dias.
    d1 = dt.datetime.strptime(args.de, "%d/%m/%Y").date() if args.de else hoje - dt.timedelta(days=31)
    d2 = dt.datetime.strptime(args.ate, "%d/%m/%Y").date() if args.ate else hoje
    sondas = [(f"tp{args.tipo_periodo} sit{sit} {d1:%d/%m}-{d2:%d/%m}", sit, d1, d2, args.por_pagina)
              for sit in (args.situacao or [2])][: args.max_req]

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br",
                      timeout=args.timeout) as cli:
        try:
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[login] {env['SISREG_USUARIO']}")

        gastas = 0
        for rotulo, situacao, d1, d2, por_pag in sondas:
            params = consulta(situacao, d1, d2, por_pag, tipo_periodo=args.tipo_periodo)
            html = texto(cli.get("/cgi-bin/gerenciador_solicitacao", params=params))
            gastas += 1

            if any(m in html.lower() for m in MARCAS_SESSAO_MORTA):
                print("  ! sessao morta — refazendo login")
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
                html = texto(cli.get("/cgi-bin/gerenciador_solicitacao", params=params))
                gastas += 1

            if any(m in html.lower() for m in MARCAS_CAPTCHA):
                print("!! CAPTCHA — PARANDO AGORA.", file=sys.stderr)
                return 4

            nome = re.sub(r"[^a-z0-9]+", "_", rotulo.lower()).strip("_")
            (saida / f"ger_{nome}.html").write_text(html, encoding="utf-8")

            info = analisar(html)
            print(f"\n[{rotulo}]  situacao={situacao} ({SITUACOES.get(situacao, '?')})")
            print(f"   {d1:%d/%m/%Y} a {d2:%d/%m/%Y}  qtd_itens_pag={por_pag}")
            print(f"   {len(html)} bytes -> {info}")

        print(f"\n== {gastas} requisicoes gastas (teto {args.max_req}) ==")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
