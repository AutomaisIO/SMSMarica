"""Extrai o que está legível dentro de um `__VIEWSTATE` do Klinikos (base64 LOS, sem cifra).

O ViewState do Klinikos não é cifrado — é o formato LOS do WebForms em base64 com MAC. Dentro
dele o Telerik RadMenu serializa **todo o menu do módulo** (rótulo + URL de cada item), o que dá
o mapa de telas de um módulo sem clicar em nada. Este script decodifica e imprime:

- as URLs `.aspx` encontradas (com query string), únicas e ordenadas;
- os pares rótulo → URL quando aparecem adjacentes (menu);
- opcionalmente todas as strings legíveis (`--tudo`).

Uso:
  python decodificar_viewstate.py capturas/<pagina>.html            # lê o __VIEWSTATE do HTML
  python decodificar_viewstate.py --raw "<base64>"                   # valor cru
  python decodificar_viewstate.py capturas/x.html --tudo             # todas as strings

Nunca imprime dado de paciente: o ViewState de uma tela de PARÂMETROS só tem menu, rótulos e
opções de filtro. Em tela de RESULTADO pode ter grade com nome — não rodar `--tudo` nessas.
"""

from __future__ import annotations

import base64
import re
import sys
from urllib.parse import unquote

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def viewstate_de_html(html: str) -> str:
    m = re.search(r'id="__VIEWSTATE"[^>]*value="([^"]*)"', html)
    if not m:
        raise SystemExit("__VIEWSTATE não encontrado no HTML")
    return m.group(1)


def strings_legiveis(blob: bytes, minimo: int = 3) -> list[str]:
    """LOS guarda strings como UTF-8 com prefixo de tamanho; extrai sequências imprimíveis."""
    txt = blob.decode("utf-8", errors="replace")
    return [s for s in re.findall(r"[\w\sÀ-ÿ/\.\?\=\&\-\:\,\(\)\%\'\"\[\]]{%d,}" % minimo, txt)]


def main(args: list[str]) -> int:
    tudo = "--tudo" in args
    args = [a for a in args if a != "--tudo"]
    if not args:
        print(__doc__)
        return 2
    if args[0] == "--raw":
        vs = unquote(args[1])
    else:
        vs = viewstate_de_html(open(args[0], encoding="utf-8", errors="replace").read())
    blob = base64.b64decode(vs + "=" * (-len(vs) % 4))
    print(f"viewstate: {len(vs)} chars base64 -> {len(blob)} bytes")

    txt = blob.decode("utf-8", errors="replace")
    urls = sorted(set(re.findall(r"/KlinikosNet/[\w\-/\.]+\.aspx(?:\?[\w\-=&%\.]*)?", txt)))
    print(f"\n== URLs .aspx no ViewState: {len(urls)} ==")
    for u in urls:
        print("  ", u)

    # Pares rótulo→URL do RadMenu: no LOS o rótulo (Text) vem logo antes da NavigateUrl.
    print("\n== menu (rótulo -> URL) ==")
    vistos = set()
    for m in re.finditer(r"([A-Za-zÀ-ÿ][\w\sÀ-ÿ/\.\-\,\(\)]{2,80}?)\x1f\x06\x05[\x00-\xff]?(/KlinikosNet/[\w\-/\.]+\.aspx(?:\?[\w\-=&%\.]*)?)", txt):
        rot, url = m.group(1).strip(), m.group(2)
        if (rot, url) in vistos:
            continue
        vistos.add((rot, url))
        print(f"  {rot!r:60} {url}")
    if not vistos:
        print("  (padrão rótulo/URL não casou — use --tudo e olhe o layout)")

    if tudo:
        print("\n== todas as strings ==")
        for s in strings_legiveis(blob):
            s = s.strip()
            if s:
                print("  ", s[:200])
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
