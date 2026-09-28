"""O Prime guarda o código do SISREG em algum lugar? Teste objetivo. SOMENTE LEITURA.

Para agendamentos do Prime que CASARAM com uma solicitação SISREG do hub (mesmo CNS, mesma unidade
executante, mesma data; base smsmarica.solicitacao + fhir.patient, só SELECT), procura o
`codigo_solicitacao` e a `chave_confirmacao` EXATOS nas telas de leitura do agendamento no Prime
(DetalhesLogAgenda) e nos CSVs já baixados. Imprime só achou/não achou + rótulos da tela.

Uso: python sondar_codigo_sisreg.py <csv_agendados> <unidade_id_smsmarica> [max]
"""
from __future__ import annotations
import collections, datetime as dt, re, sys, time, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn
from prime.client import CAP, PrimeSession, sopa
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def main(a):
    arq, unid = a[0], a[1]; maximo = int(a[2]) if len(a) > 2 else 8
    raw = open(arq, "rb").read().decode("utf-8-sig")
    regs = [x.rstrip(";").split(";") for x in raw.split("\r\n") if x.strip()]
    cab = regs[0]; R = [dict(zip(cab, x)) for x in regs[1:] if len(x) == len(cab)]
    cnss = sorted({r["cns_numero"].strip() for r in R})
    with conn() as c, c.cursor() as cur:
        cur.execute("""select p.cns, p.cns_todos, s.codigo_solicitacao, s.chave_confirmacao,
                              (s.data_agendada at time zone 'America/Sao_Paulo')::date
                       from fhir.patient p join smsmarica.solicitacao s on s.paciente_id = p.id
                       where not p.is_deleted and s.excluido_em is null and s.unidade_executante_id = %s
                         and (p.cns = any(%s) or p.cns_todos && %s::text[])""", (unid, cnss, cnss))
        sol = cur.fetchall()
    idx = collections.defaultdict(list)
    for cns, todos, cod, chave, data in sol:
        for x in {cns, *(todos or [])}:
            idx[(x, data)].append((cod, chave))
    pares = []
    for r in R:
        data = dt.datetime.strptime(r["dataagenda"][:10], "%d/%m/%Y").date()
        for cod, chave in idx.get((r["cns_numero"].strip(), data), []):
            if cod: pares.append((r["agendaid"], cod, chave))
    print("pares agendamento×solicitação:", len(pares))
    todos_csv = " ".join(p.read_text(encoding="utf-8-sig", errors="replace") for p in CAP.glob("*.csv"))
    print("códigos SISREG presentes em algum CSV baixado do Prime:",
          sum(1 for _, cod, _ in pares if cod in todos_csv), "| chaves:", sum(1 for *_, ch in pares if ch and ch in todos_csv))
    s = PrimeSession(); s.entrar()
    rotulos = collections.Counter(); ach_cod = ach_chave = vistos = 0
    for agendaid, cod, chave in pares[:maximo]:
        r = s.get("/Prime/Agendamento/DetalhesLogAgenda.aspx", params={"AgendaId": agendaid})
        h = r.text; vistos += 1
        (CAP / f"logagenda_{agendaid[:8]}.html").write_text(h, encoding="utf-8")
        dig = re.sub(r"\D", "", sopa(h).get_text(" "))
        ach_cod += (cod in h) or (re.sub(r"\D", "", cod) in dig)
        ach_chave += bool(chave) and ((chave in h) or (re.sub(r"\D", "", chave) in dig))
        d = sopa(h)
        for el in d.find_all(["th", "label", "legend"]):
            t = el.get_text(" ", strip=True)
            if t and len(t) < 40: rotulos[t] += 1
        print(f"  agenda {agendaid[:8]}…: {r.status_code} {len(h)} bytes | código SISREG na tela: {cod in h or re.sub(chr(92)+'D','',cod) in dig} | chave: {bool(chave) and (chave in h)}")
        time.sleep(0.5)
    print(f"telas vistas: {vistos} | com código: {ach_cod} | com chave: {ach_chave}")
    print("rótulos da tela de log:", [k for k, _ in rotulos.most_common(40)])
    s.close()

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
