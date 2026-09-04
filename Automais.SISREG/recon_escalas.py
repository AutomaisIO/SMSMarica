"""Recon da tela Consulta de Escalas Ambulatoriais (`cons_escalas`).

SOMENTE LEITURA: 1 login (reaproveitado) + 1 GET do formulario. Nenhum POST.
Objetivo: descobrir os campos do form, as etapas (EXIBIR_ESCALAS/EXPORTAR_ESCALAS)
e o formato do export, ANTES de gastar POST no orcamento anti-robo do operador.

O HTML cru vai para --saida (fora do repo). Aqui so imprimimos estrutura.
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
ENV_SISCAN = BASE.parent / "Automais.SISCAN" / ".env"
SESSAO = BASE / "capturas" / ".sessao_programador.json"

MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "diferencia&ccedil;&atilde;o entre computadores",
)


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


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True)
    args = ap.parse_args(argv)
    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    env = ler_env(ENV_SISCAN)
    usuario, senha = env.get("SISREG_USUARIO", ""), env.get("SISREG_SENHA", "")
    if not usuario or not senha:
        print("!! credencial nao encontrada", file=sys.stderr)
        return 2

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

        html = texto(cli.get("/cgi-bin/cons_escalas"))
        # `esta_logado()` da falso positivo quando a sessao foi derrubada por outro logon:
        # o SISREG responde 200 com a pagina sisreg_erro.c. So o conteudo denuncia.
        if "logon em outra" in html.lower() or "sess&#227;o foi finalizada" in html.lower():
            print("[2a] sessao derrubada por outro logon — refazendo login")
            cli.login(usuario, senha)
            cli.salvar_sessao(SESSAO)
            html = texto(cli.get("/cgi-bin/cons_escalas"))
        if any(m in html.lower() for m in MARCAS_CAPTCHA):
            print("!! CAPTCHA na tela de escalas — parando.", file=sys.stderr)
            return 4
        (saida / "cons_escalas_form.html").write_text(html, encoding="utf-8")
        print(f"[2] GET /cgi-bin/cons_escalas -> {len(html)} bytes  (salvo em cons_escalas_form.html)")

        # --- estrutura do formulario ---
        for f in re.finditer(r"<form[^>]*>", html, re.I):
            print("    FORM:", re.sub(r"\s+", " ", f.group(0)))
        print("\n[3] campos (input/select/textarea):")
        for m in re.finditer(r"<(input|select|textarea)\b([^>]*)>", html, re.I):
            tag, attrs = m.group(1).lower(), m.group(2)
            nome = re.search(r'name\s*=\s*["\']?([^"\'\s>]+)', attrs, re.I)
            tipo = re.search(r'type\s*=\s*["\']?([^"\'\s>]+)', attrs, re.I)
            val = re.search(r'value\s*=\s*["\']([^"\']*)', attrs, re.I)
            print(f"    {tag:8} name={nome.group(1) if nome else '-':22} "
                  f"type={tipo.group(1) if tipo else '-':10} value={(val.group(1)[:40] if val else '-')}")

        print("\n[4] etapas / acoes citadas no JS:")
        for e in sorted(set(re.findall(r"['\"]([A-Z_]{6,})['\"]", html))):
            print("   ", e)

        print("\n[5] funcoes JS:")
        for fn in sorted(set(re.findall(r"function\s+(\w+)\s*\(", html))):
            print("   ", fn)

        print("\n[6] tamanho dos selects (options):")
        for s in re.finditer(r'<select[^>]*name\s*=\s*["\']?(\w+)[^>]*>(.*?)</select>', html, re.I | re.S):
            nome, corpo = s.group(1), s.group(2)
            opts = re.findall(r'<option[^>]*value\s*=\s*["\']?([^"\'>]*)["\']?[^>]*>([^<]*)', corpo, re.I)
            print(f"    {nome}: {len(opts)} options; 3 primeiras: {opts[:3]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
