"""Sonda um relatório da tela única Relatorios/RelatoriosSaudeMental.aspx. SOMENTE LEITURA.

A tela tem ~40 relatórios; cada um é um ImageButton `imb<Nome>` com seus próprios campos de data
(`rdp<Nome>Inicio/Fim`). Replica o clique (postback `.x/.y`) com o período dado e descreve o que
volta: arquivo (CSV/PDF), `window.open` para uma URL de relatório, ou a própria tela com alerta.

Uso: python probe_relatorio.py <botao> <campoInicio> <campoFim> DD/MM/AAAA DD/MM/AAAA [CSV|PDF]
ex.: python probe_relatorio.py imbAtendimentosRealizados rdpAtendimentosRealizadosInicio rdpAtendimentosRealizadosFim 09/09/2026 15/09/2026
"""
from __future__ import annotations
import re, sys, datetime as dt
from saudemental.client import CAP, SaudeMentalSession, campos_todos, action_do_form, sopa
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
URL = "/SaudeMental/Relatorios/RelatoriosSaudeMental.aspx"
P = "ctl00$DefaultContent$"

def main(a):
    botao, cini, cfim, ini, fim = a[:5]
    fmt = (a[5] if len(a) > 5 else "CSV").upper()
    iso = lambda s: dt.datetime.strptime(s, "%d/%m/%Y").strftime("%Y-%m-%d")
    with SaudeMentalSession() as s:
        s.entrar()
        r = s.get(URL); html = r.text
        d = campos_todos(html)
        # Rádio do formato (rblTipoRelatorios?) não é o CSV/PDF: CSV/PDF são rbImprimir/rbImprimirPDF (RadButton
        # que só guarda a escolha no client state). Mandamos o formato no texto do botão para registrar o que volta.
        if cini:
            # RadDateInput 2011+: o valor que o servidor lê vem do ClientState JSON do dateInput
            # (validationText/valueAsString "AAAA-MM-DD-00-00-00"); só o texto não basta (medido:
            # sem isso o relatório sai com "Período:  a <hoje>", histórico inteiro).
            import json as _j
            for campo, br in ((cini, ini), (cfim, fim)):
                v = iso(br) + "-00-00-00"
                d[P + campo] = iso(br)
                d[P + campo + "$dateInput"] = br
                d["ctl00_DefaultContent_" + campo + "_dateInput_ClientState"] = _j.dumps({
                    "enabled": True, "emptyMessage": "", "validationText": v, "valueAsString": v,
                    "minDateStr": "1980-01-01-00-00-00", "maxDateStr": "2099-12-31-00-00-00",
                    "lastSetTextBoxValue": br})
        d.update({P + botao + ".x": "10", P + botao + ".y": "10", "__EVENTTARGET": "", "__EVENTARGUMENT": ""})
        t = dt.datetime.now()
        r = s.post(action_do_form(html, str(r.url)), d)
        ct = r.headers.get("content-type", ""); cd = r.headers.get("content-disposition", "")
        print(r.status_code, ct, cd, len(r.content), "bytes", f"{(dt.datetime.now()-t).total_seconds():.1f}s", r.url)
        nome = f"rel_{botao}_{ini.replace('/','')}_{fim.replace('/','')}"
        if "html" in ct:
            (CAP / f"{nome}.html").write_text(r.text, encoding="utf-8")
            achados = re.findall(r"window\.open\([^;]{0,300}|location\.href\s*=[^;]{0,200}|radalert\([^;]{0,200}|swal\.fire\([^;]{0,200}|Relatorio\w*\.aspx\?[^'\"\s]{0,200}", r.text)
            vistos = [x for x in dict.fromkeys(achados) if "manuais" not in x and "helpindicadores" not in x and "mailto" not in x]
            print("html ->", nome + ".html"); [print("  ", x[:300]) for x in vistos[:10]]
            # Telerik Reporting 7.2 (medido 16/09/2026): o postback cria uma instância do relatório
            # na sessão; o conteúdo sai por GET no handler, sem ViewState:
            #   Telerik.ReportViewer.axd?instanceID=<id>&optype=Export&ExportFormat=<CSV|XLS|PDF…>
            m = re.search(r"'/SaudeMental/Telerik\.ReportViewer\.axd',\s*'([0-9a-f]{32})'", r.text)
            if not m:
                print("  (sem instância de ReportViewer na resposta)"); return 0
            inst = m.group(1)
            pg = s.get("/SaudeMental/Telerik.ReportViewer.axd", params={"instanceID": inst, "optype": "Report"})
            periodo = re.findall(r"Per[íi]odo:[^<]{0,40}", pg.text)
            print(f"  página 1: {pg.status_code} {len(pg.content)} bytes; {periodo[:1]}")
            if "--forcar" not in a and periodo and ini not in periodo[0]:
                print("  período NÃO aplicado — export abortado (use --forcar para exportar assim mesmo)"); return 0
            t = dt.datetime.now()
            e = s.get("/SaudeMental/Telerik.ReportViewer.axd", params={"instanceID": inst, "optype": "Export", "ExportFormat": fmt})
            ect = e.headers.get("content-type", "")
            print(f"  export {fmt}: {e.status_code} {ect} {e.headers.get('content-disposition','')} {len(e.content)} bytes {(dt.datetime.now()-t).total_seconds():.1f}s")
            ext = "html" if "html" in ect else fmt.lower()
            (CAP / f"{nome}.{ext}").write_bytes(e.content)
            if fmt == "CSV" and "html" not in ect:
                linhas = e.content.decode("utf-8-sig", errors="replace").splitlines()
                print("  linhas:", len(linhas), "| cabeçalho:", linhas[0][:500] if linhas else "")
            elif "html" in ect:
                print("  html:", " ".join(sopa(e.text).get_text(" ", strip=True).split())[:300])
        else:
            (CAP / f"{nome}.bin").write_bytes(r.content)
            if "csv" in ct or "text" in ct:
                linhas = r.content.decode("utf-8-sig", errors="replace").splitlines()
                print("linhas:", len(linhas), "| cabeçalho:", linhas[0][:400] if linhas else "")
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
