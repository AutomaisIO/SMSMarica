"""Sonda: relatório Pacientes Atendidos (Relatorios/RelatorioPacientesAtendidos.aspx) em CSV/XLS
para um período, na unidade da sessão. SOMENTE LEITURA (postback do botão Gerar Relatório).
Saída em capturas/ (tem PII); imprime só contagens e cabeçalho.

Uso: python probe_atendidos.py AAAA-MM-DD AAAA-MM-DD [CSV|XLS|PDF]
"""
from __future__ import annotations
import sys, re, datetime as dt
from prime.client import CAP, PrimeSession, campos_todos, action_do_form
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
URL = "/Prime/Relatorios/RelatorioPacientesAtendidos.aspx"
P = "ctl00$ctl00$DefaultContent$ChildDefaultContent$"

def main(a):
    ini, fim = a[0], a[1]; fmt = (a[2] if len(a) > 2 else "CSV").upper()
    br = lambda s: dt.date.fromisoformat(s).strftime("%d/%m/%Y")
    with PrimeSession() as s:
        s.entrar()
        r = s.get(URL); html = r.text
        d = campos_todos(html)
        d.update({P+"rdpDataInicio": ini, P+"rdpDataInicio$dateInput": br(ini),
                  P+"rdpDataFim": fim, P+"rdpDataFim$dateInput": br(fim),
                  "__EVENTTARGET": f"ctl00$ctl00$DefaultContent$BotoesExtrasContent$btnGerarRelatorio{fmt}",
                  "__EVENTARGUMENT": ""})
        t = dt.datetime.now()
        r = s.post(action_do_form(html, str(r.url)), d)
        ct = r.headers.get("content-type", ""); cd = r.headers.get("content-disposition", "")
        print(r.status_code, ct, cd, len(r.content), "bytes", f"{(dt.datetime.now()-t).total_seconds():.1f}s", r.url)
        ext = {"CSV": "csv", "XLS": "xls", "PDF": "pdf"}[fmt] if "html" not in ct else "html"
        out = CAP / f"atendidos_{ini}_{fim}.{ext}"; out.write_bytes(r.content)
        if "html" in ct:
            m = re.findall(r"window\.open\([^)]*\)|location\.href\s*=\s*['\"][^'\"]+|swal[^;]{0,200}|radalert\([^)]*\)", r.text)
            print("html ->", out.name, m[:5])
        elif ext == "csv":
            txt = r.content.decode("utf-8-sig", errors="replace").splitlines()
            print("linhas:", len(txt)); print("cabeçalho:", txt[0][:300] if txt else "")
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
