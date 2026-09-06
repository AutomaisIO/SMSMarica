"""Captura o menu real do SISREG a partir da RESPOSTA DO LOGIN (frameset).

`/cgi-bin/avisos` e so o mural de avisos (505 KB de comunicados) e `/cgi-bin/index`
e a tela de login. O menu com os endpoints de verdade vem no frameset devolvido
pelo POST de login. Sem ele, achar o nome certo de "Solicitacoes Pendentes na Fila
de Espera" viraria adivinhacao de sufixo — e cada palpite custa uma requisicao do
orcamento anti-robo do OPERADOR.

SOMENTE LEITURA (1 login + os frames do proprio menu).
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SESSAO = BASE / "capturas" / ".sessao_programador.json"


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


def endpoints(html: str) -> dict[str, str]:
    """Todo /cgi-bin/<algo> citado, com o rotulo do <a> quando houver."""
    achados: dict[str, str] = {}
    for m in re.finditer(
            r'href\s*=\s*["\']([^"\']*?cgi-bin/([a-z_0-9.]+))[^"\']*["\'][^>]*>(.*?)</a>',
            html, re.I | re.S):
        rotulo = re.sub(r"<[^>]+>", " ", m.group(3))
        achados.setdefault(m.group(2), re.sub(r"\s+", " ", rotulo).strip())
    for m in re.finditer(r"cgi-bin/([a-z_0-9.]+)", html, re.I):
        achados.setdefault(m.group(1), "")
    return achados


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True)
    args = ap.parse_args(argv)
    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    env = credencial()
    if not env:
        print("!! credencial SISREG nao encontrada", file=sys.stderr)
        return 2

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        try:
            resp = cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
            cli.salvar_sessao(SESSAO)
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1

        html = texto(resp)
        (saida / "pos_login.html").write_text(html, encoding="utf-8")
        print(f"[1] resposta do login -> {len(html)} bytes (url={resp.url})")

        todos = endpoints(html)
        print(f"    {len(todos)} endpoints citados no frameset")

        # Os frames do frameset costumam trazer o menu de verdade.
        frames = re.findall(r'(?:frame|iframe)[^>]+src\s*=\s*["\']([^"\']+)["\']', html, re.I)
        print(f"[2] frames: {frames}")
        gets = 0
        for src in frames:
            if not src.startswith("/"):
                src = "/" + src.lstrip("./")
            if "barra-brasil" in src:
                continue
            h = texto(cli.get(src))
            gets += 1
            nome = re.sub(r"[^a-z0-9]+", "_", src.lower()).strip("_")[:40]
            (saida / f"frame_{nome}.html").write_text(h, encoding="utf-8")
            achados = endpoints(h)
            print(f"    {src} -> {len(h)} bytes, {len(achados)} endpoints")
            for k, v in achados.items():
                if v or k not in todos:
                    todos[k] = v or todos.get(k, "")

        print(f"\n=== {len(todos)} endpoints ===")
        for alvo, rotulo in sorted(todos.items()):
            baixo = (alvo + " " + rotulo).lower()
            marca = "  <<<" if any(p in baixo for p in ("fila", "pendent", "espera", "solicit")) else ""
            print(f"  {alvo:34} {rotulo[:48]:50}{marca}")

        print(f"\n== 1 login + {gets} GETs ==")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
