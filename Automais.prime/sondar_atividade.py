"""Mede se o Prime está em USO: nº de linhas do relatório Pacientes Atendidos (CSV, GET direto em
Relatorios/RelatorioPacientesAtendidosRPT.aspx) por unidade e por janela. SOMENTE LEITURA.
Só imprime CONTAGENS (sem PII); os CSVs ficam em capturas/atividade/.

Uso: python sondar_atividade.py DD/MM/AAAA DD/MM/AAAA [guid ...]   (sem guid = as 35 unidades)
"""
from __future__ import annotations
import sys, time
from bs4 import BeautifulSoup
from prime.client import CAP, PrimeSession
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def unidades() -> dict[str, str]:
    h = (CAP / "pos_login.html").read_text(encoding="utf-8")
    sel = BeautifulSoup(h, "html.parser").find("select", attrs={"name": "LoginView1$ddlUnidade"})
    return {o["value"]: o.get_text(strip=True) for o in sel.find_all("option") if o["value"] != "-1"}

def main(a: list[str]) -> int:
    ini, fim, alvo = a[0], a[1], a[2:]
    uns = unidades(); (CAP / "atividade").mkdir(exist_ok=True)
    total = 0
    with PrimeSession() as s:
        s.entrar()
        for g, nome in uns.items():
            if alvo and g not in alvo:
                continue
            r = s.get("/Prime/Relatorios/RelatorioPacientesAtendidosRPT.aspx", params={
                "dataInicio": ini, "dataFim": fim, "funcao": "", "prof": "", "idGrupoPrioritario": "",
                "extensao": "CSV", "unidades": g})
            if "csv" not in r.headers.get("content-type", ""):
                print(f"{'?':>6}  {nome}  ({r.status_code} {r.headers.get('content-type')})"); continue
            # Registro = CRLF; o campo de procedimentos tem LF solto dentro (medido 16/09/2026):
            # contar splitlines() infla ~3x.
            corpo = r.content.decode("utf-8-sig", errors="replace").split("
")[1:]
            n = sum(1 for linha in corpo if linha.strip())
            (CAP / "atividade" / f"{g}_{ini.replace('/', '')}_{fim.replace('/', '')}.csv").write_bytes(r.content)
            total += n
            print(f"{n:>6}  {nome}")
            time.sleep(0.3)
    print(f"{total:>6}  TOTAL {ini}–{fim}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
