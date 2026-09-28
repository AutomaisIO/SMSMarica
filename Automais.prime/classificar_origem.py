"""Classifica agendamentos do Prime pela origem regulada, cruzando com o SISREG do hub. SOMENTE LEITURA.

Entrada: CSV do *Pacientes Agendados* (fatiado por profissional). Banco: fhir.patient (CNS) ×
smsmarica.solicitacao (SELECT). Regras em ordem (APRENDIZADOS §10.1):
 1 mesma data, mesma unidade executante;
 2 mesma data, outra unidade executante;
 3 mesma unidade, até ±3 dias;
 4 retorno: solicitação no passado (até 180 dias) na mesma unidade;
 5 função não regulada (Consulta de Enfermagem);
 6 solicitação antiga (>180d) ou só futura/em outra unidade;
 7 nada no SISREG / CNS fora do hub.
Imprime só contagens (geral, por função e passado × futuro).

Uso: python classificar_origem.py <csv> <unidade_id_hub> DD/MM/AAAA(hoje)
"""
from __future__ import annotations
import collections, datetime as dt, pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
NAO_REGULADAS = {"Consulta de Enfermagem"}

def main(a):
    arq, unid = a[0], a[1]; hoje = dt.datetime.strptime(a[2], "%d/%m/%Y").date()
    raw = open(arq, "rb").read().decode("utf-8-sig")
    regs = [x.rstrip(";").split(";") for x in raw.split("\r\n") if x.strip()]
    cab = regs[0]; R = [dict(zip(cab, x)) for x in regs[1:] if len(x) == len(cab)]
    cnss = sorted({r["cns_numero"].strip() for r in R if r["cns_numero"].strip()})
    with conn() as c, c.cursor() as cur:
        cur.execute("""select p.cns, p.cns_todos, s.unidade_executante_id::text,
                              (s.data_agendada at time zone 'America/Sao_Paulo')::date
                       from fhir.patient p join smsmarica.solicitacao s on s.paciente_id = p.id
                       where not p.is_deleted and s.excluido_em is null and s.data_agendada is not null
                         and (p.cns = any(%s) or p.cns_todos && %s::text[])""", (cnss, cnss))
        rows = cur.fetchall()
        cur.execute("select count(*) from fhir.patient where not is_deleted and (cns = any(%s) or cns_todos && %s::text[])", (cnss, cnss))
    hist = collections.defaultdict(list); no_hub = set(cnss)
    for cns, todos, un, dag in rows:
        for x in {cns, *(todos or [])}:
            if x in no_hub or x in hist: hist[x].append((un, dag))
    achados_hub = set()
    with conn() as c, c.cursor() as cur:
        cur.execute("select cns, cns_todos from fhir.patient where not is_deleted and (cns = any(%s) or cns_todos && %s::text[])", (cnss, cnss))
        for cns, todos in cur.fetchall():
            achados_hub.update(x for x in {cns, *(todos or [])} if x in set(cnss))
    tot = collections.Counter(); por_fun = collections.defaultdict(collections.Counter); tempo = collections.defaultdict(collections.Counter)
    for r in R:
        cns = r["cns_numero"].strip(); dia = dt.datetime.strptime(r["dataagenda"][:10], "%d/%m/%Y").date()
        fun = r["funcaoatendimento"]; h = hist.get(cns, [])
        mesma = [x for x in h if x[0] == unid]
        if any(x[1] == dia for x in mesma): k = "1 mesma data, mesma unidade"
        elif any(x[1] == dia for x in h): k = "2 mesma data, outra unidade"
        elif any(abs((x[1] - dia).days) <= 3 for x in mesma): k = "3 mesma unidade, ±3 dias"
        elif fun in NAO_REGULADAS: k = "5 função não regulada"
        elif any(0 < (dia - x[1]).days <= 180 for x in mesma): k = "4 retorno (≤180d, mesma unidade)"
        elif h: k = "6 SISREG antigo/futuro/outra unidade"
        elif cns in achados_hub: k = "7 no hub, sem SISREG"
        else: k = "7 CNS fora do hub"
        tot[k] += 1; por_fun[fun][k[0]] += 1; tempo["futuro" if dia > hoje else "passado/hoje"][k[0]] += 1
    print(f"agendamentos: {len(R)} | pacientes (CNS): {len(cnss)} | no hub: {len(achados_hub)}")
    for k in sorted(tot): print(f"  {tot[k]:5} ({100*tot[k]/len(R):4.1f}%)  {k}")
    print("\npassado×futuro:", {k: dict(sorted(v.items())) for k, v in tempo.items()})
    print("\npor função (1=mesma data 2=outra unid 3=±3d 4=retorno 5=não regulada 6=antigo/outro 7=sem SISREG):")
    for fun, cnt in sorted(por_fun.items(), key=lambda x: -sum(x[1].values())):
        n = sum(cnt.values()); print(f"  {n:4}  {fun[:40]:40} {dict(sorted(cnt.items()))}")

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
