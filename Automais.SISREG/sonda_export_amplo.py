"""O `expo_solicitacoes` aceita recorte MAIS AMPLO que um par profissional x procedimento?

A pergunta vale muito: o CDT tem 272 combinacoes habilitadas = 272 requisicoes por varredura,
e o CAPTCHA anti-robo chega por volta de 700 no operador. Se o export aceitar "procedimento=0"
(a option-sentinela "Selecione o Procedimento" do proprio formulario), o custo cai para ~99
(um por profissional). Se aceitar tambem "cpf=0", cai para um punhado.

O JS do formulario nao valida cpf nem procedimento -- so as datas e o range de 31 dias. Mas o
precedente da tela vizinha e NEGATIVO: no `cons_agendas`, `ups` preenchido com `cpf`/`pa` vazios
devolve "A pesquisa nao retornou nenhum resultado" (os filtros sao obrigatorios NO SERVIDOR).
Por isso isto e uma SONDA, nao uma suposicao.

SOMENTE LEITURA: login + GET do form + POSTs com etapa=exportar (que e leitura -- gera o TXT).
Nunca Confirma/Falta/qualquer escrita. Para na hora se detectar CAPTCHA ou sessao morta.

CUSTO: 1 login + 1 GET + 4 POSTs = ~6 requisicoes do orcamento anti-robo do operador.

O corpo das respostas tem PII (nome/CNS/telefone de paciente) e por isso NAO e impresso: so
contagens e o cabecalho do TXT (CNES;unidade;dt_ini;dt_fim;total). Os arquivos crus vao para
--saida, que deve ficar FORA do repositorio.

Uso:
    python sonda_export_amplo.py --saida "C:\\caminho\\scratchpad"
"""
from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
ENV_SISCAN = BASE.parent / "Automais.SISCAN" / ".env"
SESSAO = BASE / "capturas" / ".sessao_programador.json"

PAUSA = 0.4

# Mesmas marcas do teste_export_cmi.py: "recaptcha" cru da falso positivo (a tela saudavel
# carrega o api.js do Google no <head>). A PAREDE redireciona para ./recaptcha?cod= .
MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "class='g-recaptcha'",
    "diferencia&ccedil;&atilde;o entre computadores",
)
MARCAS_SESSAO_MORTA = ("sisreg_erro", "erro ao carregar a sessao", "logon em outra", "foi finalizada")
MARCA_BLOQUEIO = "bloquead"  # alert('Aplicativo bloqueado para uso de 8 as 15 horas.')

# Alvo: lido do NOSSO banco (zero requisicao ao SISREG para descobri-lo). E a combinacao mais
# produtiva do CDT -- 327 registros na ultima varredura -- entao "veio vazio" nao tem como ser
# "essa agenda nao tem movimento".
CNES = "3132358"          # CDT DR ALBERTO LUIS MACHADO BORGES
CPF_PROF = "08594208766"  # VIVIANE VIEIRA FERREIRA DE ANDRADE
PA_CONTROLE = "1402000"   # GRUPO - ULTRASONOGRAFIA


class CaptchaExigido(RuntimeError):
    pass


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        if chave.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[chave.strip()] = valor.strip()
    return d


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


def checar(corpo: str, onde: str) -> None:
    baixo = corpo.lower()
    if any(m in baixo for m in MARCAS_CAPTCHA):
        raise CaptchaExigido(f"CAPTCHA em {onde} — parando. Um humano precisa resolver no navegador.")


def diagnostico(corpo: str) -> str:
    """Classifica a resposta SEM expor PII."""
    baixo = corpo.lower()
    if any(m in baixo for m in MARCAS_SESSAO_MORTA):
        return "SESSAO MORTA"
    if MARCA_BLOQUEIO in baixo:
        return "BLOQUEIO DE HORARIO (08h-15h)"
    if "<html" in baixo:
        return "HTML (nao e o TXT do export)"
    return "TXT"


def resumo(corpo: str) -> tuple[int, str]:
    """(linhas de dados, cabecalho). O cabecalho e CNES;unidade;dt_ini;dt_fim;total — sem PII."""
    linhas = [l for l in corpo.splitlines() if l.strip()]
    if not linhas:
        return 0, "(vazio)"
    cab = linhas[0]
    # Cabecalho de unidade tem 5 campos; se a 1a linha ja for dado (38 campos), nao ha cabecalho.
    tem_cabecalho = cab.count(";") == 4
    return (len(linhas) - 1 if tem_cabecalho else len(linhas)), (cab if tem_cabecalho else "(sem cabecalho)")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True, help="pasta FORA do repo para os TXT crus (tem PII)")
    ap.add_argument("--dias", type=int, default=29, help="janela a frente (default 29, a de producao)")
    args = ap.parse_args(argv)

    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    hoje = dt.date.today()
    data1 = hoje.strftime("%d/%m/%Y")
    data2 = (hoje + dt.timedelta(days=args.dias)).strftime("%d/%m/%Y")

    env = ler_env(ENV_SISCAN)
    usuario, senha = env.get("SISREG_USUARIO", ""), env.get("SISREG_SENHA", "")
    if not usuario or not senha:
        print("!! credencial SISREG nao encontrada no .env do SISCAN", file=sys.stderr)
        return 2

    # Cada cenario e (rotulo, cpf, procedimento). O CONTROLE vem primeiro de proposito: se ele
    # vier vazio, a agenda e que esta vazia e nenhum outro resultado significa nada.
    cenarios = [
        ("CONTROLE  cpf=prof  proc=1402000", CPF_PROF, PA_CONTROLE),
        ("A  cpf=prof  proc=0 (sentinela)", CPF_PROF, "0"),
        ("B  cpf=prof  proc=(vazio)", CPF_PROF, ""),
        ("C  cpf=0     proc=0 (unidade)", "0", "0"),
    ]

    print(f"janela: {data1} a {data2}  ({args.dias} dias)   unidade CNES={CNES}")
    print(f"custo previsto: 1 login + 1 GET + {len(cenarios)} POST = {len(cenarios) + 2} requisicoes\n")

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        try:
            estado = "reaproveitada"
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(usuario, senha)
                cli.salvar_sessao(SESSAO)
                estado = "novo_login"
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[1] login: operador={usuario} sessao={estado}")

        # O form REAL tem ~9,5 KB e os dois selects. Uma resposta curta e um redirect de sessao
        # morta -- e `esta_logado()` ja deu falso positivo aqui. Sem esta checagem, os 4 POSTs
        # sairiam contra uma sessao morta e as quatro respostas vazias seriam lidas como
        # "o SISREG recusou o recorte amplo", que e a conclusao errada pelo motivo errado.
        def abrir_form() -> str:
            time.sleep(PAUSA)
            html = texto(cli.get("/cgi-bin/expo_solicitacoes"))
            checar(html, "GET do formulario")
            return html

        formhtml = abrir_form()
        if 'name="cpf"' not in formhtml:
            print(f"[2] formulario nao veio ({len(formhtml)} bytes) — sessao morta. Relogando…")
            cli.login(usuario, senha)
            cli.salvar_sessao(SESSAO)
            formhtml = abrir_form()

        if MARCA_BLOQUEIO in formhtml.lower():
            print("!! BLOQUEIO DE HORARIO: o expo_solicitacoes so abre fora da faixa 08h-15h. "
                  "Rode depois das 15h (e antes das 07:30).", file=sys.stderr)
            return 3
        if 'name="cpf"' not in formhtml:
            print(f"!! o formulario continua sem os campos esperados ({len(formhtml)} bytes) — "
                  "nao vale gastar POSTs contra isso.", file=sys.stderr)
            return 3
        print(f"[2] formulario aberto ({len(formhtml)} bytes), com os selects, sem bloqueio\n")

        print(f"{'cenario':38} {'tipo':32} {'linhas':>7}  cabecalho")
        print("-" * 118)
        for i, (rotulo, cpf, proc) in enumerate(cenarios, start=3):
            time.sleep(PAUSA)
            resp = cli.post("/cgi-bin/expo_solicitacoes", data={
                "data1": data1, "data2": data2,
                "cpf": cpf, "procedimento": proc,
                "tp_arquivo": "0",      # TXT, o mesmo formato que o motor consome
                "etapa": "exportar",
                "unidade": CNES,
            })
            corpo = texto(resp)
            checar(corpo, rotulo)

            tipo = diagnostico(corpo)
            linhas, cab = resumo(corpo) if tipo == "TXT" else (0, "-")
            arq = saida / f"sonda_{i:02d}_{rotulo.split()[0].lower()}.txt"
            arq.write_text(corpo, encoding="utf-8")
            print(f"{rotulo:38} {tipo:32} {linhas:>7}  {cab[:60]}")

        print("\nArquivos crus (TEM PII, nao commitar) em:", saida)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except CaptchaExigido as e:
        print(f"\n!! {e}", file=sys.stderr)
        raise SystemExit(4) from e
