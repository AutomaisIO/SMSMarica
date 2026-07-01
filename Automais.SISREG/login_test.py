"""Teste de login no SISREG III + dump da tela pós-login para análise dos menus.

Uso:
    pip install -r requirements.txt
    cp .env.example .env   # e preencha SISREG_USUARIO / SISREG_SENHA
    python login_test.py

Salva a resposta pós-login em capturas/pos_login.html (gitignored) e imprime um
resumo (status, cookies, título, e âncoras/menus encontrados).
"""

from __future__ import annotations

import os
import pathlib
import sys

from dotenv import load_dotenv

from sisreg import SisregClient, SisregLoginError

try:
    from bs4 import BeautifulSoup
except ImportError:
    BeautifulSoup = None


def main() -> int:
    load_dotenv()
    usuario = os.getenv("SISREG_USUARIO", "").strip()
    senha = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

    if not usuario or not senha:
        print("ERRO: preencha SISREG_USUARIO e SISREG_SENHA no arquivo .env", file=sys.stderr)
        return 2

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            estado = cli.conectar(usuario, senha)
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}")
            return 1
        print(f"sessão: {estado}")
        resp = cli.get("/cgi-bin/index")

        print(f"OK  status={resp.status_code}  url_final={resp.url}")
        print("Cookies de sessão:")
        for name, value in cli.cookies.items():
            shown = value if name.startswith("TS") else (value[:6] + "…") if value else "(vazio)"
            print(f"  - {name} = {shown}")

        out = cap / "pos_login.html"
        out.write_text(resp.text, encoding="utf-8")
        print(f"\nHTML pós-login salvo em: {out}")

        if BeautifulSoup is not None:
            soup = BeautifulSoup(resp.text, "html.parser")
            title = soup.title.string.strip() if soup.title and soup.title.string else "(sem título)"
            print(f"Título: {title}")
            frames = soup.find_all(["frame", "iframe"])
            if frames:
                print("Frames encontrados:")
                for f in frames:
                    print(f"  - {f.get('name') or '?'} -> {f.get('src')}")
            links = [(a.get_text(strip=True), a.get("href")) for a in soup.find_all("a", href=True)]
            if links:
                print(f"Âncoras/menus ({len(links)}):")
                for text, href in links[:40]:
                    print(f"  - {text!r} -> {href}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
