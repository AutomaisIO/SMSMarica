"""Chama direto o visualizador de relatórios do Klinikos e descreve o que volta. SOMENTE LEITURA.

Medido em 16/09/2026: os botões Imprimir/Excel da tela de parâmetros NÃO devolvem o relatório —
devolvem a própria tela com um `window.open` para:

  Relatorios/rptview.aspx?parRel=631&parNum=5&par1=01/09/2026&par2=30/09/2026&par3=0005&par4=0109&par5=-1&Modulo=UPA
  Relatorios/rptviewXls.aspx?parRel=...  (mesmos parâmetros)

Ou seja: o relatório é um GET com parâmetros POSICIONAIS (`parNum` = quantos, `par1..parN`).
par3=0005 é a unidade (cookie `_codigo_unidade`), par4 a especialidade, par5 o profissional
(-1 = todos). Este script faz o GET e descreve: content-type, tamanho, se é xlsx/xls/HTML/PDF,
e para tabelas SÓ o cabeçalho e o nº de linhas (as células têm nome de paciente e ficam na
captura, que é gitignored).

Uso:
  python probe_rptview.py "rptviewXls.aspx?parRel=631&parNum=5&par1=01/09/2026&par2=30/09/2026&par3=0005&par4=0109&par5=-1&Modulo=UPA"
  python probe_rptview.py "rptview.aspx?parRel=629&parNum=1&par1=unidDef"
"""

from __future__ import annotations

import io
import re
import sys
import zipfile

from klinikos.client import CAP, KlinikosSession, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def descrever_xlsx(b: bytes) -> None:
    try:
        import openpyxl  # type: ignore
        wb = openpyxl.load_workbook(io.BytesIO(b), read_only=True)
        for ws in wb.worksheets:
            linhas = list(ws.iter_rows(values_only=True))
            cab = next((l for l in linhas if l and sum(1 for c in l if c) >= 2), None)
            print(f"     planilha {ws.title!r}: {len(linhas)} linhas; primeira linha com 2+ células = {[str(c)[:25] for c in (cab or []) if c][:16]}")
        return
    except ImportError:
        pass
    z = zipfile.ZipFile(io.BytesIO(b))
    print("     xlsx (sem openpyxl) — entradas:", [n for n in z.namelist()][:12])
    ss = z.read("xl/sharedStrings.xml").decode("utf-8", "replace") if "xl/sharedStrings.xml" in z.namelist() else ""
    strings = re.findall(r"<t[^>]*>([^<]*)</t>", ss)
    print(f"     sharedStrings: {len(strings)}; primeiras 20: {[s[:25] for s in strings[:20]]}")


def descrever_html(txt: str) -> None:
    d = sopa(txt)
    print("     título:", d.title.get_text(strip=True) if d.title else "(sem)")
    for fr in d.find_all(["iframe", "frame", "object", "embed"]):
        print("     frame/object:", fr.get("id"), (fr.get("src") or fr.get("data") or "")[:200])
    for i, t in enumerate(d.find_all("table")):
        linhas = t.find_all("tr")
        if len(linhas) < 2:
            continue
        cab = [th.get_text(" ", strip=True)[:25] for th in linhas[0].find_all(["th", "td"])]
        if len(cab) < 2:
            continue
        print(f"     tabela#{i}: {len(linhas)} linhas x {len(cab)} colunas; cabeçalho={cab[:16]}")
    scripts = " ".join(sc.get_text() for sc in d.find_all("script") if not sc.get("src"))
    for m in sorted(set(re.findall(r"""(?:window\.open|location\.href\s*=|\.src\s*=)\s*\(?["']([^"']+)["']""", scripts)))[:10]:
        print("     JS abre:", m[:200])
    texto = " ".join(d.get_text(" ", strip=True).split())
    if len(texto) < 400:
        print("     texto:", texto)
    # ReportViewer (Microsoft) / Crystal / Telerik Reporting
    for pista in ("ReportViewer", "CrystalReport", "Telerik.Reporting", "ReportSession", "rsRpt"):
        if pista in txt:
            print("     pista de viewer:", pista)


def main(args: list[str]) -> int:
    if not args:
        print(__doc__)
        return 2
    s = KlinikosSession()
    s.entrar()
    for alvo in args:
        url = alvo if alvo.startswith("/") else "/KlinikosNet/Relatorios/" + alvo
        r = s.get(url)
        ct = r.headers.get("content-type", "")
        b = r.content
        print("GET", url)
        print(f"  -> {r.status_code} {r.url}\n     content-type={ct} bytes={len(b)} disposition={r.headers.get('content-disposition')}")
        nome = re.sub(r"[^A-Za-z0-9]+", "_", alvo)[:90]
        if b[:2] == b"PK":
            (CAP / f"{nome}.xlsx").write_bytes(b); print(f"     formato: xlsx -> capturas/{nome}.xlsx"); descrever_xlsx(b)
        elif b[:4] == b"%PDF":
            (CAP / f"{nome}.pdf").write_bytes(b); print(f"     formato: pdf -> capturas/{nome}.pdf")
        elif b[:8] == b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1":
            (CAP / f"{nome}.xls").write_bytes(b); print(f"     formato: xls binário (BIFF) -> capturas/{nome}.xls")
        else:
            txt = r.text
            (CAP / f"{nome}.html").write_text(txt, encoding="utf-8")
            print(f"     formato: texto/html -> capturas/{nome}.html")
            descrever_html(txt)
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
