"""Mede o USO do Prime Saúde Mental por CAPS: nº de atendimentos do relatório *Atendimentos
Realizados* (tela única RelatoriosSaudeMental → Telerik Reporting → export CSV) por unidade e
janela, com primeira/última data. SOMENTE LEITURA. Imprime só contagens e datas (sem PII).

A unidade do relatório é a da SESSÃO (`hidUnidadeId`), então a sonda repassa pelo gate de unidade
(GET login.aspx autenticado devolve o gate) antes de cada CAPS.

Uso: python sondar_atividade.py DD/MM/AAAA DD/MM/AAAA
"""
from __future__ import annotations
import csv, io, json, re, sys, time, datetime as dt
from bs4 import BeautifulSoup
from saudemental.client import CAP, URL_LOGIN, SaudeMentalSession, campos_todos, action_do_form
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
URL = "/SaudeMental/Relatorios/RelatoriosSaudeMental.aspx"
P = "ctl00$DefaultContent$"

def atendimentos(s, ini, fim):
    r = s.get(URL); html = r.text
    d = campos_todos(html)
    for campo, br in (("rdpAtendimentosRealizadosInicio", ini), ("rdpAtendimentosRealizadosFim", fim)):
        iso = dt.datetime.strptime(br, "%d/%m/%Y").strftime("%Y-%m-%d"); v = iso + "-00-00-00"
        d[P + campo] = iso; d[P + campo + "$dateInput"] = br
        d["ctl00_DefaultContent_" + campo + "_dateInput_ClientState"] = json.dumps({
            "enabled": True, "emptyMessage": "", "validationText": v, "valueAsString": v,
            "minDateStr": "1980-01-01-00-00-00", "maxDateStr": "2099-12-31-00-00-00", "lastSetTextBoxValue": br})
    d.update({P + "imbAtendimentosRealizados.x": "10", P + "imbAtendimentosRealizados.y": "10",
              "__EVENTTARGET": "", "__EVENTARGUMENT": ""})
    r = s.post(action_do_form(html, str(r.url)), d)
    m = re.search(r"'/SaudeMental/Telerik\.ReportViewer\.axd',\s*'([0-9a-f]{32})'", r.text)
    if not m:
        return None
    e = s.get("/SaudeMental/Telerik.ReportViewer.axd", params={"instanceID": m.group(1), "optype": "Export", "ExportFormat": "CSV"})
    rows = list(csv.reader(io.StringIO(e.content.decode("utf-8-sig", errors="replace"))))
    if len(rows) < 2:
        return 0, None, None, ""
    c = rows[0]; ip, idt = c.index("txtPeriodo"), c.index("txtDataInicioGroup")
    ds = sorted(x[idt][:10] for x in rows[1:] if len(x) > idt)
    chave = lambda s_: dt.datetime.strptime(s_, "%d/%m/%Y")
    return len(rows) - 1, min(ds, key=chave), max(ds, key=chave), rows[1][ip]

def main(a):
    ini, fim = a[0], a[1]
    with SaudeMentalSession() as s:
        s.entrar()
        uns = {o["value"]: o.get_text(strip=True) for o in BeautifulSoup((CAP / "login_resposta.html").read_text(encoding="utf-8"), "html.parser")
               .find("select", attrs={"name": "LoginView1$ddlUnidade"}).find_all("option") if o["value"] != "-1"}
        for g, nome in uns.items():
            r = s.get(URL_LOGIN)
            s.selecionar_unidade(r, unidade=g)
            t = time.time(); res = atendimentos(s, ini, fim)
            print(f"{nome}: {res} ({time.time()-t:.1f}s)")
            time.sleep(0.5)
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
