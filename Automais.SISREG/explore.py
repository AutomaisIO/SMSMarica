"""Explorador de telas do SISREG (autenticado).

Loga com a credencial do .env e faz GET de um ou mais caminhos, salvando o HTML
em capturas/ e imprimindo um resumo (título, formulários + campos, selects,
tabelas) para a gente entender os parâmetros de cada tela.

Uso:
    python explore.py /cgi-bin/cons_agendas
    python explore.py /cgi-bin/cons_agendas /cgi-bin/expo_solicitacoes
"""

from __future__ import annotations

import os
import pathlib
import re
import sys

from dotenv import load_dotenv
from bs4 import BeautifulSoup

from sisreg import SisregClient, SisregLoginError


def _slug(path: str) -> str:
    return re.sub(r"[^a-zA-Z0-9]+", "_", path).strip("_") or "root"


def resumir(html: str) -> None:
    soup = BeautifulSoup(html, "html.parser")
    title = soup.title.string.strip() if soup.title and soup.title.string else "(sem título)"
    print(f"  título: {title}")

    for i, form in enumerate(soup.find_all("form")):
        action = form.get("action") or "(mesma URL)"
        method = (form.get("method") or "get").upper()
        print(f"  form[{i}] {method} action={action}")
        for inp in form.find_all(["input", "select", "textarea"]):
            tag = inp.name
            name = inp.get("name")
            if not name:
                continue
            if tag == "select":
                opts = [o.get("value", o.get_text(strip=True)) for o in inp.find_all("option")]
                extra = f" opções={opts[:8]}{'…' if len(opts) > 8 else ''}"
            else:
                extra = f" type={inp.get('type', 'text')} value={inp.get('value', '')!r}"
            print(f"      - {tag} {name}{extra}")

    tables = soup.find_all("table")
    if tables:
        print(f"  tabelas: {len(tables)}")
        for i, t in enumerate(tables[:3]):
            rows = t.find_all("tr")
            head = rows[0].get_text(" | ", strip=True)[:160] if rows else ""
            print(f"      table[{i}] linhas={len(rows)} 1a='{head}'")

    # iframes/frames (SISREG usa muito)
    for f in soup.find_all(["frame", "iframe"]):
        if f.get("src"):
            print(f"  frame: {f.get('name') or '?'} -> {f.get('src')}")


def main(argv: list[str]) -> int:
    if not argv:
        print("uso: python explore.py <path> [<path> ...]", file=sys.stderr)
        return 2

    load_dotenv()
    usuario = os.getenv("SISREG_USUARIO", "").strip()
    senha = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            estado = cli.conectar(usuario, senha)
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"sessão: {estado}\n")

        for path in argv:
            print(f"== GET {path} ==")
            r = cli.get(path)
            print(f"  status={r.status_code} url_final={r.url} len={len(r.text)}")
            out = cap / f"tela_{_slug(path)}.html"
            out.write_text(r.text, encoding="utf-8")
            print(f"  salvo: {out}")
            resumir(r.text)
            print()

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
