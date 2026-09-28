"""Executa um relatório de PARÂMETROS do Klinikos como o operador faz e descreve o que volta.
SOMENTE LEITURA (gerar relatório não altera dado).

Replica a sequência medida no navegador do operador em 16/09/2026 para
`Relatorios/ParametroRelatorio.aspx?parrel=631&Modulo=UPA` ("Atendimentos em Andamento"):
1. GET da tela de parâmetros;
2. postback do `ddlEspecialidade` (AutoPostBack; carrega `ddlProfissional` em cascata);
3. clique em um dos botões-imagem: `ImageButton1` (visualizar), `btnImprimirExcel`, `btnImprimir`.

Descreve a resposta: URL final, título, iframes/objetos (ReportViewer?), tabelas (nº de linhas e
CABEÇALHOS — nunca as células, que têm nome de paciente), content-type (Excel/PDF?), e grava
tudo em capturas/relatorio_<parrel>_<botao>.<ext>.

Uso:
  python probe_relatorio.py 631 [--modulo UPA] [--de 2026-09-01] [--ate 2026-09-30]
                            [--especialidade 0109] [--botao ImageButton1|btnImprimirExcel|btnImprimir]
"""

from __future__ import annotations

import re
import sys

from klinikos.client import CAP, KlinikosSession, campos_todos, action_do_form, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
PREF = "ctl00$ctl00$contentCenter$contentCenterChild$"


def arg(args, nome, padrao=None):
    return args[args.index(nome) + 1] if nome in args else padrao


def descrever_resposta(r, rotulo: str) -> None:
    ct = r.headers.get("content-type", "")
    print(f"  -> {r.status_code} {r.url}")
    print(f"     content-type={ct} bytes={len(r.content)} disposition={r.headers.get('content-disposition')}")
    ext = "html" if "html" in ct else ("xls" if "excel" in ct or "spreadsheet" in ct else "pdf" if "pdf" in ct else "bin")
    (CAP / f"{rotulo}.{ext}").write_bytes(r.content)
    if ext != "html":
        print(f"     gravado capturas/{rotulo}.{ext}")
        return
    d = sopa(r.text)
    print("     título:", d.title.get_text(strip=True) if d.title else "(sem)")
    for fr in d.find_all(["iframe", "frame", "object", "embed"]):
        print("     frame/object:", fr.get("id"), (fr.get("src") or fr.get("data") or "")[:160])
    for i, t in enumerate(d.find_all("table")):
        linhas = t.find_all("tr")
        if len(linhas) < 3:
            continue
        cab = [th.get_text(" ", strip=True)[:25] for th in (linhas[0].find_all(["th", "td"]))]
        if len(cab) < 2:
            continue
        print(f"     tabela#{i}: {len(linhas)} linhas x {len(cab)} colunas; cabeçalho={cab[:14]}")
    grids = [g.get("id") for g in d.find_all(attrs={"class": re.compile(r"RadGrid|dataTable|GridView", re.I)})]
    if grids:
        print("     grids:", grids[:10])
    scripts = " ".join(sc.get_text() for sc in d.find_all("script") if not sc.get("src"))
    for m in sorted(set(re.findall(r"""(?:window\.open|location\.href\s*=|location\s*=)\s*\(?["']([^"']+)["']""", scripts))):
        print("     JS abre:", m[:160])
    print(f"     gravado capturas/{rotulo}.html")


def main(args: list[str]) -> int:
    if not args:
        print(__doc__)
        return 2
    parrel = args[0]
    modulo = arg(args, "--modulo", "UPA")
    de, ate = arg(args, "--de", "2026-09-01"), arg(args, "--ate", "2026-09-30")
    esp = arg(args, "--especialidade")
    botao = arg(args, "--botao", "ImageButton1")
    url = f"/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel={parrel}&Modulo={modulo}"

    s = KlinikosSession()
    s.entrar()
    r = s.get(url)
    html = r.text
    print("GET", url, "->", r.status_code, len(html), "bytes")

    def datas(d: dict) -> dict:
        for nome, v in (("rdpDataInicial", de), ("rdpDataFinal", ate)):
            if PREF + f"ctlParam${nome}" in d:
                dd, mm, aa = v[8:10], v[5:7], v[0:4]
                d[PREF + f"ctlParam${nome}"] = v
                d[PREF + f"ctlParam${nome}$dateInput"] = f"{v}-00-00-00"
                d[f"ctl00_ctl00_contentCenter_contentCenterChild_ctlParam_{nome}_dateInput_text"] = f"{dd}/{mm}/{aa}"
        return d

    if esp and PREF + "ctlParam$ddlEspecialidade" in campos_todos(html):
        r = s.postback(url, html, target=PREF + "ctlParam$ddlEspecialidade",
                       extra=datas({PREF + "ctlParam$ddlEspecialidade": esp}))
        html = r.text
        d = sopa(html)
        sel = d.find("select", attrs={"name": PREF + "ctlParam$ddlProfissional"})
        print("postback especialidade ->", r.status_code, "profissionais carregados:",
              len(sel.find_all("option")) if sel else "(select ausente)")

    dados = datas(campos_todos(html))
    if esp:
        dados[PREF + "ctlParam$ddlEspecialidade"] = esp
    dados[PREF + f"{botao}.x"] = "10"
    dados[PREF + f"{botao}.y"] = "10"
    from klinikos.client import LIBERADOS
    LIBERADOS.update({PREF + f"{botao}.x", PREF + f"{botao}.y"})
    print(f"POST {botao} ...")
    r = s.post(action_do_form(html, str(r.url)), dados)
    descrever_resposta(r, f"relatorio_{parrel}_{botao}")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
