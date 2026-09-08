"""Junta TODAS as divergencias de identidade e as solicitacoes que dependem delas.

Tres fontes, uma tela:

- `divergencias_classificadas.csv` (585) -- o SER, perguntado por um CNS, devolveu ficha com OUTRO
  CNS. A maioria e a mesma pessoa com mais de um numero; o que interessa aqui e ver, campo a campo,
  onde o cadastro difere -- porque e o mesmo cadastro que a operacao diaria consulta.
- `dossie_divergencias.json` (10) -- fase por CPF, ja conferida contra a Receita Federal.
- `_inconformidades.csv` (19) -- conflito de ficha no hub e baixa confianca; nenhum foi importado.

## Por que as solicitacoes entram no relatorio

Uma divergencia de identidade nao e um problema de cadastro: e um problema de PRONTUARIO. Quem
julga o caso precisa saber quanto historico esta pendurado naquela ficha e de quando ele e -- uma
divergencia com 40 pedidos desde 2019 nao se resolve como uma sem nenhum. Por isso cada linha traz
a contagem e o intervalo, e o detalhe lista pedido a pedido.

Para os casos que NAO entraram, a solicitacao nao esta no banco -- so no TXT. Uma varredura unica
dos arquivos recupera data e procedimento dos 92 codigos.

Sai em JSON; quem desenha a tela e o render_divergencias.py. Separado de proposito: a varredura dos
TXT leva minutos e nao pode ser refeita a cada ajuste de layout.
"""
from __future__ import annotations

import csv
import json
import os
import pathlib
import sys
from collections import defaultdict

import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

BASE = pathlib.Path(__file__).parent
CAP = BASE / "capturas"
IMPL = BASE.parent / "Automais.SISREG" / "capturas" / "implantacao"

C_CODIGO, C_PROC_TEXTO, C_DATA, C_CNS, C_NOME_PAC, C_DT_SOLIC = 0, 3, 6, 9, 10, 29


def dig(s):
    return "".join(c for c in (s or "") if c.isdigit())


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def ler(p):
    return list(csv.DictReader(p.open(encoding="utf-8-sig"), delimiter=";")) if p.exists() else []


def iso(br):
    """21/03/1960 -> 1960-03-21. Comparar data como texto so funciona no mesmo formato."""
    b = (br or "").strip().replace(".", "/")     # o TXT alterna `/` e `.` no mesmo arquivo
    if len(b) == 10 and b[2] == "/" and b[5] == "/":
        return f"{b[6:]}-{b[3:5]}-{b[0:2]}"
    return b


def norm_nome(s):
    return " ".join((s or "").upper().split())


# ---------------------------------------------------------------- coleta

casos: list[dict] = []

# 1. fase por CNS
for r in ler(CAP / "divergencias_classificadas.csv"):
    casos.append({
        "grupo": "cns",
        "decisao": r["decisao"],
        "motivo": r["motivo"],
        "chave": dig(r["cns_sisreg"]),
        "hub": {"nome": r["nome_sisreg"], "cns": dig(r["cns_sisreg"])},
        "ser": {"nome": r["nome_ser"], "cns": dig(r["cns_do_ser"]),
                "cpf": dig(r["cpf_do_ser"]), "nascimento": iso(r["nascimento_ser"])},
    })

# 2. fase por CPF (ja conferida na Receita)
for d in json.loads((CAP / "dossie_divergencias.json").read_text(encoding="utf-8")):
    h, s = d["hub"], d["ser_devolveu"]
    risco = (d.get("risco") or "").upper()
    casos.append({
        "grupo": "cpf",
        "decisao": "RETER" if risco in ("CRITICO", "CRÍTICO", "ALTO") else "conferido",
        "motivo": d.get("veredito", ""),
        "risco": risco,
        "chave": dig(h.get("cpf")),
        "receita": d.get("receita"),
        "hub": {"nome": h.get("nome"), "cpf": dig(h.get("cpf")), "nascimento": h.get("nascimento"),
                "cns": dig(h.get("cns")), "origem": h.get("origem")},
        "ser": {"nome": s.get("nome"), "cpf": dig(s.get("cpf")), "nascimento": iso(s.get("nascimento")),
                "cns": dig(s.get("cns")), "nome_mae": s.get("nome_mae")},
    })

# 3. inconformidades da carga
for r in ler(IMPL / "_inconformidades.csv"):
    casos.append({
        "grupo": "conflito" if r["tipo"] == "CONFLITO" else "baixa",
        "decisao": r["tipo"],
        "motivo": r["detalhe"],
        "chave": dig(r["cns_sisreg"]),
        "hub": {"nome": r["nome"], "cns": dig(r["cns_sisreg"])},
        "ser": {},
    })

print(f"casos: {len(casos)}")

# ---------------------------------------------------------------- hub + solicitacoes

cx = conexao()
cur = cx.cursor()

chaves = sorted({c["chave"] for c in casos if c["chave"]})
cur.execute("""
    select k, p.id, p.nome, p.cpf, p.cns, p.nascimento, p.cns_todos, p.meta_source, p.last_updated
    from unnest(%s::text[]) k
    join fhir.patient p on (not p.is_deleted)
      and (p.cns_todos && array[k] or p.cpf = k or p.cns = k)
""", (chaves,))
por_chave = defaultdict(list)
for k, pid, nome, cpf, cns, nasc, todos, src, upd in cur.fetchall():
    por_chave[k].append({"id": str(pid), "nome": nome, "cpf": cpf or "", "cns": cns or "",
                         "nascimento": str(nasc) if nasc else "", "cns_todos": todos or [],
                         "origem": src or "", "atualizado": str(upd)[:10]})
print(f"chaves com ficha no hub: {len(por_chave):,}/{len(chaves):,}")

pids = sorted({f["id"] for v in por_chave.values() for f in v})
cur.execute("""
    select s.paciente_id, s.codigo_solicitacao, s.data_solicitacao,
           (s.data_agendada at time zone 'America/Sao_Paulo')::date,
           s.procedimento_texto, u.nome, s.cid_codigo
    from smsmarica.solicitacao s
    left join smsmarica.unidade u on u.id = s.unidade_executante_id
    where s.excluido_em is null and s.paciente_id = any(%s::uuid[])
    order by s.data_agendada
""", (pids,))
sol_por_pid = defaultdict(list)
for pid, cod, dsol, dag, proc, uni, cid in cur.fetchall():
    sol_por_pid[str(pid)].append({"cod": cod or "", "solic": str(dsol or ""), "data": str(dag or ""),
                                  "proc": proc or "", "uni": uni or "", "cid": cid or "", "in": 1})
print(f"solicitacoes ligadas: {sum(len(v) for v in sol_por_pid.values()):,}")

# ---------------------------------------------------------------- as que NAO entraram
# Nao estao no banco: so no TXT. Uma varredura recupera data e procedimento dos 92 codigos.
faltantes = {r["codigo_solicitacao"] for r in ler(IMPL / "_solicitacoes_nao_importadas.csv")}
nao_imp = defaultdict(list)
if faltantes:
    vistos, nome_txt = set(), {}
    for arq in sorted(IMPL.glob("*/sisreg-unidade-*.txt")):
        with arq.open(encoding="utf-8", errors="replace") as fh:
            fh.readline()
            for linha in fh:
                c = linha.rstrip("\n").split(";")
                if len(c) < 38:
                    continue
                cod = c[C_CODIGO].strip()
                if cod not in faltantes or cod in vistos:
                    continue                       # janelas de colheita se sobrepoem
                vistos.add(cod)
                nome_txt.setdefault(dig(c[C_CNS]), c[C_NOME_PAC].strip())
                nao_imp[dig(c[C_CNS])].append(
                    {"cod": cod, "solic": iso(c[C_DT_SOLIC]), "data": iso(c[C_DATA]),
                     "proc": c[C_PROC_TEXTO].strip(), "uni": "", "cid": "", "in": 0})
    print(f"nao importadas recuperadas do TXT: {len(vistos)}/{len(faltantes)}")

# ---------------------------------------------------------------- os que ninguem registrou
# Nem divergencia nem inconformidade: paciente que a conciliacao NAO chegou a avaliar. A lista de
# entrada dela foi montada antes de a colheita terminar, entao a ultima leva de TXT trouxe CNS que
# nunca passaram por triagem -- e sumiram em silencio, que e o pior jeito de um dado se perder.
conhecidas = {c["chave"] for c in casos}
orfaos = sorted(k for k in nao_imp if k and k not in conhecidas)
for k in orfaos:
    casos.append({
        "grupo": "orfao",
        "decisao": "SEM FICHA",
        "motivo": "paciente nunca avaliado pela conciliacao — a lista de entrada foi fechada antes "
                  "do fim da colheita",
        "chave": k,
        "hub": {"nome": nome_txt.get(k, ""), "cns": k},
        "ser": {},
    })
print(f"orfaos (sem ficha e sem registro): {len(orfaos)}")


# ---------------------------------------------------------------- montagem

GRUPOS = {"cns": "SER devolveu outro CNS", "cpf": "SER devolveu outro CPF",
          "conflito": "Ficha partida no hub", "baixa": "Baixa confiança",
          "orfao": "Sem ficha no hub"}
CAMPOS = ("nome", "nascimento", "cpf", "cns")


def diffs(caso, ficha):
    """Onde os dados divergem. Vazio de um dos lados NAO e divergencia -- e campo em branco."""
    out = {}
    ser = caso["ser"]
    base = dict(caso["hub"])
    if ficha:
        base = {k: ficha.get(k) or base.get(k) or "" for k in CAMPOS}
    for campo in CAMPOS:
        a, b = (base.get(campo) or ""), (ser.get(campo) or "")
        if campo == "nome":
            a, b = norm_nome(a), norm_nome(b)
        if campo == "cns" and ficha and b and b in (ficha.get("cns_todos") or []):
            continue                                # ja reconhecido como CNS da mesma pessoa
        if a and b and a != b:
            out[campo] = [a, b]
    return out


linhas = []
for i, caso in enumerate(casos):
    fichas = por_chave.get(caso["chave"], [])
    ficha = fichas[0] if fichas else None
    sols = list(sol_por_pid.get(ficha["id"], [])) if ficha else []
    sols += nao_imp.get(caso["chave"], [])
    sols.sort(key=lambda s: s["data"] or s["solic"] or "")
    datas = [s["data"] or s["solic"] for s in sols if (s["data"] or s["solic"])]
    linhas.append({
        "i": i, "grupo": caso["grupo"], "decisao": caso["decisao"], "motivo": caso["motivo"],
        "risco": caso.get("risco", ""), "chave": caso["chave"],
        "nome": (ficha or {}).get("nome") or caso["hub"].get("nome") or "",
        "hub": ficha or caso["hub"], "ser": caso["ser"], "receita": caso.get("receita"),
        "fichas": len(fichas), "diff": diffs(caso, ficha),
        "sols": sols, "qtd": len(sols),
        "de": datas[0] if datas else "", "ate": datas[-1] if datas else "",
        "fora": sum(1 for s in sols if not s["in"]),
    })

# Ordem util para caca a problema: mais campos divergentes primeiro, depois mais historico em risco.
linhas.sort(key=lambda l: (-len(l["diff"]), -l["qtd"], l["de"] or "9"))
print(f"linhas: {len(linhas)} · com solicitacao: {sum(1 for l in linhas if l['qtd'])}")

destino = CAP / "_divergencias_dados.json"
destino.write_text(json.dumps({"grupos": GRUPOS, "linhas": linhas}, ensure_ascii=False),
                   encoding="utf-8")
print(f"dados: {destino}")
