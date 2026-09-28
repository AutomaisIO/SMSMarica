"""Sonda 0: login no Prime Saúde Mental + retrato da página onde o login cai. SOMENTE LEITURA.

Imprime a cadeia de redirecionamento, cookies, título, texto visível, forms/campos (para achar
gate de local/unidade, como o GravaCookie do Klinikos) e todos os alvos `.aspx` referenciados.

Uso:  python probe_login.py
"""

from __future__ import annotations

import sys

from saudemental.client import CAP, SaudeMentalSession, links_aspx, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    s = SaudeMentalSession()
    r = s.login()
    print(">>> LOGIN OK — caiu em", r.url, r.status_code)
    print("redirecionamentos:", [f"{h.status_code} {h.url}" for h in r.history])
    print("cookies:", sorted(s.c.cookies.keys()))

    html = r.text
    (CAP / "pos_login.html").write_text(html, encoding="utf-8")
    d = sopa(html)
    print("título:", d.title.get_text(strip=True) if d.title else "(sem título)")
    body = d.find("body")
    print("texto (800c):", " ".join((body.get_text(" ", strip=True) if body else "").split())[:800])
    for f in d.find_all("form"):
        print("form:", f.get("id"), f.get("action"))
        for i in f.find_all(["input", "select", "textarea"]):
            t = i.get("type") or i.name
            if t == "hidden":
                continue
            ops = [o.get_text(strip=True) for o in i.find_all("option")][:15] if i.name == "select" else ""
            print("   ", t, i.get("name"), i.get("value", "")[:40] if t in ("submit", "button") else "", ops)
    alvos = links_aspx(html, str(r.url))
    print(f"alvos .aspx/.ashx/.asmx/.svc: {len(alvos)}")
    for a in alvos:
        print("  ", a)
    frames = [(f.get("id"), f.get("src")) for f in d.find_all(["iframe", "frame"])]
    if frames:
        print("frames:", frames)
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
