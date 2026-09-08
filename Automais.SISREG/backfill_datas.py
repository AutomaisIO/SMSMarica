"""Backfill de data_agendada: consulta o cons_agendas por (ups + co_solicitacao) e extrai a
Data/Hora do agendamento. SOMENTE LEITURA no SISREG.

Entrada: capturas/_backfill_codigos.txt  (linhas "codigo;cnes")
Saída:   capturas/_backfill_datas.csv     (codigo;data_local;hora;utc_iso  |  codigo;;;NAO_ENCONTRADO)

O DB grava data_agendada em UTC (Brasília = UTC-3), então o utc_iso já é local + 3h.
"""
from __future__ import annotations
import datetime as dt
import pathlib, sys, time
try: sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except AttributeError: pass
BASE = pathlib.Path(__file__).parent; CAP = BASE / "capturas"
sys.path.insert(0, str(BASE))
from sisreg import SisregClient  # noqa: E402
from extrair_agenda_unidade import parse_registros  # noqa: E402

SESSAO = CAP / ".sessao_programador.json"
ENV = BASE.parent / "Automais.SISCAN" / ".env"
MARCAS_CAPTCHA = ("recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85", 'class="g-recaptcha"')
MARCAS_MORTA = ("sisreg_erro", "erro ao carregar a sessao", "logon em outra", "foi finalizada")

def ler_env():
    d={}
    for l in ENV.read_text(encoding="utf-8").splitlines():
        l=l.strip()
        if l.startswith("#") or "=" not in l: continue
        k,v=l.split("=",1)
        if k.strip() in ("SISREG_USUARIO","SISREG_SENHA","SISREG_BASE_URL"): d[k.strip()]=v.strip()
    return d

def texto(r):
    try: return r.content.decode("utf-8")
    except UnicodeDecodeError: return r.content.decode("iso-8859-1","replace")

def consulta(cli, cod, ups):
    time.sleep(0.4)
    d={"co_solicitacao":cod,"cns_paciente":"","dataInicial":"","dataFinal":"","ups":ups,"cpf":"","pa":"",
       "cmbTipoOperacao":"Consulta","chkboxExibirProcedimentos":"on","chkboxExibirTelefones":"on",
       "cmbOrdenacao":"1","cmbMaxResults":"50","etapa":"ListaConsulta","pagina":"0","linhas":"0"}
    return texto(cli.post("/cgi-bin/cons_agendas",data=d))

def utc_iso(data_local, hora):
    # "14/08/2026" + "08:45" (Brasilia, UTC-3) -> "2026-08-14 11:45:00+00"
    try:
        d=dt.datetime.strptime(data_local,"%d/%m/%Y")
        hh,mm=(hora or "00:00").split(":")[:2]
        loc=d.replace(hour=int(hh),minute=int(mm))
        return (loc+dt.timedelta(hours=3)).strftime("%Y-%m-%d %H:%M:%S+00")
    except Exception:
        return ""

def main():
    pares=[l.strip().split(";") for l in (CAP/"_backfill_codigos.txt").read_text().splitlines() if l.strip()]
    env=ler_env(); saida=[]
    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
            cli.login(env["SISREG_USUARIO"],env["SISREG_SENHA"]); cli.salvar_sessao(SESSAO); print("login novo")
        else: print("sessao reaproveitada")
        for i,(cod,ups) in enumerate(pares,1):
            html=consulta(cli,cod,ups); baixo=html.lower()
            if any(k in baixo for k in MARCAS_CAPTCHA):
                print(f"!! CAPTCHA em {cod} — parando (parcial preservado)"); break
            if any(k in baixo for k in MARCAS_MORTA):
                print("   sessao morta — relogando"); cli.login(env["SISREG_USUARIO"],env["SISREG_SENHA"]); cli.salvar_sessao(SESSAO); html=consulta(cli,cod,ups)
            regs=parse_registros(html,("",""),("",""))
            reg=next((r for r in regs if r.get("co_solicitacao")==cod), regs[0] if regs else None)
            if reg and reg.get("data"):
                iso=utc_iso(reg["data"],reg.get("hora",""))
                saida.append((cod,reg["data"],reg.get("hora",""),iso))
                print(f"[{i:>2}/{len(pares)}] {cod} -> {reg['data']} {reg.get('hora','')}  {reg.get('paciente','')[:22]}  [utc {iso}]")
            else:
                saida.append((cod,"","","NAO_ENCONTRADO"))
                print(f"[{i:>2}/{len(pares)}] {cod} -> NAO ENCONTRADO (ups={ups})")
    out=CAP/"_backfill_datas.csv"
    out.write_text("\n".join(";".join(map(str,r)) for r in saida),encoding="utf-8")
    ok=sum(1 for r in saida if r[3] not in ("NAO_ENCONTRADO",) and r[3])
    print(f"\nsalvo: {out}  ({ok} com data / {len(saida)})")
    return 0

if __name__=="__main__":
    raise SystemExit(main())
