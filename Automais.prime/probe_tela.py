"""Sonda genérica de tela: loga, faz GET de uma ou mais URLs e descreve cada uma. SOMENTE LEITURA.

Para cada URL imprime status, URL final, título, os campos do form (nome, tipo, opções de
select), os alvos `.aspx/.ashx/.asmx/.svc` referenciados e os scripts externos. Grava o HTML em
capturas/<nome>.html.

Uso:  python probe_tela.py "/Prime/Relatorios/RelatorioPacientesAtendidos.aspx" [...]
"""

from __future__ import annotations

import re
import sys

from prime.client import APP, CAP, PrimeSession, eh_sessao_expirada, links_aspx, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def descrever(s: PrimeSession, url: str) -> None:
    r = s.get(url)
    html = r.text
    nome = re.sub(r"[^A-Za-z0-9]+", "_", url.split(APP + "/")[-1])[:80]
    (CAP / f"{nome}.html").write_text(html, encoding="utf-8")
    d = sopa(html)
    print("=" * 100)
    print("GET", url)
    print("  ->", r.status_code, r.url, len(html), "bytes", "SESSÃO EXPIRADA" if eh_sessao_expirada(r) else "")
    print("  título:", d.title.get_text(strip=True) if d.title else "(sem)")
    for f in d.find_all("form"):
        print(f"  form id={f.get('id')} action={f.get('action')}")
        for i in f.find_all(["input", "select", "textarea", "button"]):
            n = i.get("name")
            if not n or n.startswith("__"):
                continue
            t = i.name if i.name != "input" else (i.get("type") or "text")
            extra = ""
            if i.name == "select":
                ops = [(o.get("value"), o.get_text(strip=True)) for o in i.find_all("option")]
                extra = f" opções={len(ops)} {ops[:8]}"
            elif i.get("value") and t not in ("password",):
                extra = f" value={i.get('value')[:60]!r}"
            print(f"    {t:10} {n}{extra}")
    alvos = [a for a in links_aspx(html, str(r.url)) if ".axd" not in a]
    print(f"  alvos ({len(alvos)}):")
    for a in alvos:
        print("    ", a)
    scripts = [sc.get("src") for sc in d.find_all("script") if sc.get("src") and ".axd" not in sc.get("src")]
    print(f"  scripts externos ({len(scripts)}): {scripts[:20]}")
    # chamadas AJAX inline: $.ajax / PageMethods / WebService
    inline = " ".join(sc.get_text() for sc in d.find_all("script") if not sc.get("src"))
    ajax = sorted(set(re.findall(r"""url\s*:\s*["']([^"']+)["']""", inline)))
    if ajax:
        print("  $.ajax url inline:", ajax)
    pm = sorted(set(re.findall(r"PageMethods\.(\w+)", inline)))
    if pm:
        print("  PageMethods:", pm)


def main(urls: list[str]) -> int:
    if not urls:
        print(__doc__)
        return 2
    with PrimeSession() as s:
        s.entrar()
        for u in urls:
            descrever(s, u)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
