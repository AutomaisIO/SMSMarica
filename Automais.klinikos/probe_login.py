"""Sonda 0: login no Klinikos + retrato da home. SOMENTE LEITURA.

Valida o cliente ponta a ponta (POST WebForms com __VIEWSTATE/__EVENTVALIDATION) e imprime
para onde o login redireciona, o título da home, quem está logado e todos os alvos `.aspx`
referenciados (o começo do mapa de endpoints).

Uso:  python probe_login.py
"""

from __future__ import annotations

import sys

from klinikos.client import CAP, URL_HOME, KlinikosSession, links_aspx, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    s = KlinikosSession()
    r = s.login()  # sonda 0 força login de verdade (as demais usam s.entrar(), que retoma a sessão)
    print(">>> LOGIN OK — caiu em", r.url)
    print("cookies:", sorted(s.c.cookies.keys()))

    home = s.get(URL_HOME)
    html = home.text
    (CAP / "home.html").write_text(html, encoding="utf-8")
    d = sopa(html)
    print("home:", home.url, home.status_code, len(html), "bytes -> capturas/home.html")
    print("título:", (d.title.get_text(strip=True) if d.title else "(sem título)"))

    body = d.find("body")
    texto = " ".join((body.get_text(" ", strip=True) if body else "").split())
    print("texto (600c):", texto[:600])

    alvos = links_aspx(html, str(home.url))
    print(f"alvos .aspx/.ashx/.asmx/.svc na home: {len(alvos)}")
    for a in alvos:
        print("  ", a)

    frames = [(f.get("id"), f.get("src")) for f in d.find_all(["iframe", "frame"])]
    if frames:
        print("frames:", frames)
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
