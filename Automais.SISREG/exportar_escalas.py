"""Download da grade de ESCALAS ambulatoriais (`cons_escalas`) — sem criterio.

A tela `cons_escalas` (CONSULTA DE ESCALAS AMBULATORIAIS) e onde vivem as VAGAS
— profissional x unidade executante x procedimento — e nao os pacientes. E a
fonte para a estrutura de "horarios" da plataforma.

Mecanica (lida do JS do proprio formulario, ver docs/APRENDIZADOS.md):
- Form unico `formulario`, POST para a propria URL `/cgi-bin/cons_escalas`.
- `consultarEscalas()` -> `etapa=EXIBIR_ESCALAS` (lista paginada em HTML).
- `exportarEscalas()`  -> `etapa=EXPORTAR_ESCALAS` (arquivo).
- Campos: ups, radioFiltro(cpf|pa), cpf, pa, status, dataInicial, dataFinal,
  qtd_itens_pag, pagina, ibge=330270, ordenacao, clas_lista, coluna.
- NENHUMA validacao no cliente exige ups/cpf/pa — so coerencia das datas. Se o
  recorte amplo passar no servidor, uma requisicao traz a rede inteira.

SOMENTE LEITURA. Custo: 1 login (reaproveitado) + 1 GET + 2 POST.
A saida pode conter CPF de profissional -> gravar FORA do repositorio.

Uso:
    python exportar_escalas.py --saida <pasta fora do repo>
"""
from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import re
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
ENV_SISCAN = BASE.parent / "Automais.SISCAN" / ".env"
SESSAO = BASE / "capturas" / ".sessao_programador.json"
IBGE_MARICA = "330270"
PAUSA = 0.5

MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "diferencia&ccedil;&atilde;o entre computadores",
)
MARCAS_SESSAO_MORTA = ("logon em outra", "sess&#227;o foi finalizada", "sisreg_erro")


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


def checar_captcha(corpo: str, onde: str) -> None:
    if any(m in corpo.lower() for m in MARCAS_CAPTCHA):
        raise CaptchaExigido(f"CAPTCHA em {onde} — parar. Um humano resolve no navegador.")


def sessao_morta(corpo: str) -> bool:
    return any(m in corpo.lower() for m in MARCAS_SESSAO_MORTA)


def campos(**over) -> dict[str, str]:
    """Form completo com os defaults do HTML; `over` sobrescreve o que interessa."""
    d = {
        "ups": "", "radioFiltro": "cpf", "cpf": "", "pa": "", "status": "",
        "dataInicial": "", "dataFinal": "", "qtd_itens_pag": "50",
        "pagina": "0", "ibge": IBGE_MARICA, "ordenacao": "", "clas_lista": "ASC",
        "coluna": "", "etapa": "",
    }
    d.update(over)
    return d


def diagnosticar_lista(html: str) -> str:
    """Resumo da tela EXIBIR_ESCALAS sem despejar o HTML."""
    baixo = html.lower()
    if "nenhum" in baixo and "resultado" in baixo:
        return "SEM RESULTADO (servidor recusou o recorte amplo ou nao ha escalas)"
    linhas = len(re.findall(r"<tr[^>]*class=['\"]?coord_(?:im)?par_tr", html, re.I))
    tot = re.search(r"(\d+)\s*(?:registro|escala)", baixo)
    exportar = "exportarEscalas" in html
    return (f"lista com {linhas} linha(s) na pagina"
            + (f"; total citado: {tot.group(1)}" if tot else "")
            + f"; botao Exportar {'PRESENTE' if exportar else 'ausente'}")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True, help="pasta FORA do repo (saida pode ter CPF)")
    ap.add_argument("--status", default="", help="'' todos, A ativas, I inativas, E expiradas, X excluidas")
    args = ap.parse_args(argv)

    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)
    carimbo = dt.datetime.now().strftime("%Y%m%d-%H%M%S")

    env = ler_env(ENV_SISCAN)
    usuario, senha = env.get("SISREG_USUARIO", ""), env.get("SISREG_SENHA", "")
    if not usuario or not senha:
        print("!! credencial SISREG nao encontrada no .env do SISCAN", file=sys.stderr)
        return 2

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        def relogar() -> None:
            cli.login(usuario, senha)
            cli.salvar_sessao(SESSAO)

        try:
            estado = "reaproveitada"
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                relogar()
                estado = "novo_login"
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[1] login: operador={usuario} sessao={estado}")

        # `esta_logado()` da falso positivo quando outro logon derrubou a sessao:
        # o SISREG devolve 200 com a pagina sisreg_erro.c. So o conteudo denuncia.
        html = texto(cli.get("/cgi-bin/cons_escalas"))
        if sessao_morta(html):
            print("[1a] sessao derrubada por outro logon — refazendo login")
            relogar()
            html = texto(cli.get("/cgi-bin/cons_escalas"))
        checar_captcha(html, "GET do formulario")
        if "name='ups'" not in html and 'name="ups"' not in html:
            print(f"!! formulario nao veio ({len(html)} bytes) — nao gastar POST.", file=sys.stderr)
            return 3
        print(f"[2] formulario aberto ({len(html)} bytes)")

        # --- sonda: o recorte SEM criterio passa no servidor? ---
        time.sleep(PAUSA)
        lista = texto(cli.post("/cgi-bin/cons_escalas",
                               data=campos(etapa="EXIBIR_ESCALAS", status=args.status,
                                           qtd_itens_pag="10")))
        checar_captcha(lista, "EXIBIR_ESCALAS")
        if sessao_morta(lista):
            print("!! sessao morreu no meio — rode de novo.", file=sys.stderr)
            return 3
        (saida / f"escalas_lista_{carimbo}.html").write_text(lista, encoding="utf-8")
        print(f"[3] EXIBIR_ESCALAS sem criterio -> {diagnosticar_lista(lista)}")

        # --- export ---
        time.sleep(PAUSA)
        resp = cli.post("/cgi-bin/cons_escalas",
                        data=campos(etapa="EXPORTAR_ESCALAS", status=args.status))
        corpo = texto(resp)
        checar_captcha(corpo, "EXPORTAR_ESCALAS")

        ctype = resp.headers.get("content-type", "?")
        cdisp = resp.headers.get("content-disposition", "-")
        ehtml = "<html" in corpo[:2000].lower()
        nome = re.search(r'filename="?([^";]+)', cdisp)
        arq = saida / (nome.group(1) if nome else f"escalas_{carimbo}.{'html' if ehtml else 'csv'}")
        arq.write_bytes(resp.content)

        print(f"[4] EXPORTAR_ESCALAS -> HTTP {resp.status_code} | {ctype} | disposition: {cdisp}")
        print(f"    {len(resp.content)} bytes -> {arq}")
        if ehtml:
            print("    !! veio HTML, nao arquivo:", diagnosticar_lista(corpo))
        else:
            linhas = [l for l in corpo.splitlines() if l.strip()]
            print(f"    {len(linhas)} linha(s). Cabecalho: {linhas[0][:300] if linhas else '(vazio)'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except CaptchaExigido as e:
        print(f"\n!! {e}", file=sys.stderr)
        raise SystemExit(4) from e
