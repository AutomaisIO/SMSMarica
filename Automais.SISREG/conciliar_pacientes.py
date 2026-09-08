"""CONCILIACAO: liga os pacientes do SISREG ao hub FHIR, ou cria os que faltam.

## A regra que sustenta tudo

Um paciente do SISREG so e NOVO se NENHUMA de suas chaves conhecidas existir no hub:

    chaves = CNS do SISREG + CNS definitivo (SER) + CPF (SER)

Decidir por uma chave so foi o erro das duas primeiras "secas": "nao achei por este CNS" nao
significa "esta pessoa nao existe". Medido em 08/09/2026 -- 36% do hub nao tinha CNS nenhum, e
27% dos CPFs que o SER devolveu ja existiam la. Cruzar so por CNS criaria ~13 mil duplicatas.

## O que NAO faz, por decisao

- CONFLITO (chaves apontando para fichas DIFERENTES do hub): nao toca. E duplicata que ja existe
  do nosso lado -- escolher uma esconderia o prontuario partido.
- BAIXA CONFIANCA (divergencia de identidade retida): nao toca. Nome identico com nascimento
  diferente nao se resolve por deducao.
Os dois saem no relatorio final, para tratamento humano.

## Modo seco

Por padrao NAO escreve. `--gravar` liga a escrita, e so entao. O backup de fhir.patient e
pre-requisito -- o hub nao tem desfazer.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import pathlib
import sys
import time
import unicodedata
from collections import defaultdict

import psycopg2
import psycopg2.extras

sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

BASE = pathlib.Path(__file__).parent
IMPL = BASE / "capturas" / "implantacao"
SER = BASE.parent / "Automais.SER" / "capturas"

COL_CNS, COL_NOME, COL_TEL = 9, 10, 21
SYS_CNS = "https://fhir.saude.gov.br/sid/cns"
SYS_CPF = "https://fhir.saude.gov.br/sid/cpf"
FONTE = "https://smsmarica.saude.marica/source/sisreg/implantacao"


def dig(s):
    return "".join(c for c in (s or "") if c.isdigit())


def norm(n):
    s = unicodedata.normalize("NFKD", n or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def carregar():
    """Junta o que o SISREG trouxe com o que o SER resolveu nas duas fases."""
    pacientes = {}
    for arq in sorted(IMPL.glob("*/sisreg-unidade-*.txt")):
        with arq.open(encoding="utf-8", errors="replace") as fh:
            for i, l in enumerate(fh):
                if i == 0 or not l.strip():
                    continue
                c = l.split(";")
                if len(c) <= COL_TEL:
                    continue
                cns = c[COL_CNS].strip()
                if len(cns) == 15 and cns.isdigit():
                    pacientes.setdefault(cns, {"nome": c[COL_NOME].strip(),
                                               "telefone": dig(c[COL_TEL])})

    # fase 1: CPF de paciente do hub -> CNS que o SER conhece
    cns_fase1 = {}
    for l in (SER / "identidades.jsonl").open(encoding="utf-8"):
        d = json.loads(l)
        if d.get("estado") == "ok" and d.get("cns"):
            cns_fase1[d["cns"]] = d["chave"]

    # fase 2: CNS do SISREG -> cadastro completo
    cadastro, extras = {}, defaultdict(set)
    for l in (SER / "identidades_cns.jsonl").open(encoding="utf-8"):
        d = json.loads(l)
        if d.get("estado") != "ok":
            continue
        base = d["chave"]
        cadastro[base] = d
        if len(cpf := dig(d.get("cpf"))) == 11 and set(cpf) != {"0"}:
            extras[base].add(("cpf", cpf))
        for k in ("cns_definitivo", "cns"):
            if len(v := dig(d.get(k))) == 15 and v != base:
                extras[base].add(("cns", v))

    # divergencias ja classificadas: `mesma_pessoa` vira chave; `RETER` fica de fora
    retidos = set()
    arq = SER / "divergencias_classificadas.csv"
    if arq.exists():
        with arq.open(encoding="utf-8-sig") as fh:
            for r in csv.DictReader(fh, delimiter=";"):
                base = r["cns_sisreg"]
                if r["decisao"] != "mesma_pessoa":
                    retidos.add(base)
                    continue
                if len(r["cns_do_ser"]) == 15:
                    extras[base].add(("cns", r["cns_do_ser"]))
                if len(r["cpf_do_ser"]) == 11 and set(r["cpf_do_ser"]) != {"0"}:
                    extras[base].add(("cpf", r["cpf_do_ser"]))
    return pacientes, cns_fase1, cadastro, extras, retidos


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--gravar", action="store_true", help="sem isto, NAO escreve nada")
    ap.add_argument("--lote", type=int, default=500)
    args = ap.parse_args(argv)

    print("lendo SISREG + SER...")
    pacientes, cns_fase1, cadastro, extras, retidos = carregar()
    print(f"  pacientes do SISREG: {len(pacientes):,}")
    print(f"  com cadastro do SER: {len(cadastro):,}")
    print(f"  retidos (baixa confianca): {len(retidos):,}")

    cx = conexao()
    cx.autocommit = True
    cur = cx.cursor()

    print("\nindexando o hub...")
    cur.execute("select id, cns, cns_todos, cpf from fhir.patient where not is_deleted")
    por_cns, por_cpf = {}, {}
    for pid, cns, todos, cpf in cur.fetchall():
        for c in (todos or ([cns] if cns else [])):
            por_cns.setdefault(c, pid)
        if cpf:
            por_cpf.setdefault(cpf, pid)
    for cns, cpf in cns_fase1.items():
        if cns not in por_cns and (pid := por_cpf.get(cpf)):
            por_cns[cns] = pid
    print(f"  {len(por_cns):,} CNS · {len(por_cpf):,} CPF")

    vincular, criar, conflitos, pulados = [], [], [], []
    for cns_sisreg, dados in pacientes.items():
        if cns_sisreg in retidos:
            pulados.append((cns_sisreg, dados, "baixa confianca: divergencia de identidade retida"))
            continue
        chaves = {("cns", cns_sisreg)} | extras.get(cns_sisreg, set())
        achados = {}
        for tipo, valor in chaves:
            if pid := (por_cns if tipo == "cns" else por_cpf).get(valor):
                achados.setdefault(str(pid), (tipo, valor))

        if len(achados) > 1:
            conflitos.append((cns_sisreg, dados, achados))
        elif achados:
            pid, (tipo, valor) = next(iter(achados.items()))
            vincular.append((cns_sisreg, pid, tipo, valor))
        else:
            criar.append((cns_sisreg, dados))

    print(f"\n=== PLANO ===")
    print(f"  vincular : {len(vincular):7,}")
    print(f"  criar    : {len(criar):7,}")
    print(f"  CONFLITO (nao fazer)      : {len(conflitos):5,}")
    print(f"  BAIXA CONFIANCA (nao fazer): {len(pulados):5,}")

    if not args.gravar:
        print("\n[MODO SECO] nada foi escrito. Use --gravar para executar.")
        _relatorio(conflitos, pulados)
        return 0

    print("\n=== GRAVANDO ===")
    _gravar(cur, vincular, criar, cadastro, pacientes, args.lote)
    _relatorio(conflitos, pulados)
    return 0


def _gravar(cur, vincular, criar, cadastro, pacientes, lote):
    """Acrescenta identificador nos vinculados e cria os novos. Nunca sobrescreve dado existente."""
    # UM update por LOTE, com VALUES -- nao um por paciente. A primeira versao fazia uma ida ao
    # banco por ficha e rendeu 2/s contra o Postgres gerenciado: 17 horas para 114 mil. A latencia
    # de rede dominava; o trabalho em si e trivial. Agrupando, a mesma escrita cai para minutos.
    t0, feitos = time.monotonic(), 0
    for i in range(0, len(vincular), lote):
        fatia = vincular[i:i + lote]
        cur.execute("""
            UPDATE fhir.patient p
            SET content = jsonb_set(p.content, '{identifier}',
                    (p.content->'identifier') || jsonb_build_array(
                        jsonb_build_object('system', %s, 'value', v.cns, 'use', 'old'))),
                cns_todos = CASE WHEN v.cns = ANY(COALESCE(p.cns_todos, ARRAY[]::text[]))
                                 THEN p.cns_todos
                                 ELSE COALESCE(p.cns_todos, ARRAY[]::text[]) || v.cns END,
                version_id = p.version_id + 1,
                last_updated = now()
            FROM (VALUES %s) AS v(id, cns)
            WHERE p.id = v.id::uuid
              -- idempotente: quem ja tem o identificador nao e tocado de novo
              AND NOT (p.content->'identifier') @> jsonb_build_array(
                    jsonb_build_object('system', %s, 'value', v.cns))
        """ % ('%s', ",".join(["(%s,%s)"] * len(fatia)), '%s'),
            [SYS_CNS] + [x for c, pid, _, _ in fatia for x in (pid, c)] + [SYS_CNS])
        feitos += len(fatia)
        if feitos % 20000 < lote:
            print(f"  vinculados {feitos:,}/{len(vincular):,}  "
                  f"({feitos/max(time.monotonic()-t0,1):.0f}/s)")

    print(f"  vinculacao concluida: {feitos:,} em {(time.monotonic()-t0)/60:.1f} min")

    t0, criados = time.monotonic(), 0
    for i in range(0, len(criar), lote):
        linhas = []
        for cns_sisreg, dados in criar[i:i + lote]:
            c = cadastro.get(cns_sisreg, {})
            ident = [{"system": SYS_CNS, "value": cns_sisreg, "use": "official"}]
            todos = [cns_sisreg]
            if len(cpf := dig(c.get("cpf"))) == 11 and set(cpf) != {"0"}:
                ident.append({"system": SYS_CPF, "value": cpf})
            else:
                cpf = None
            nome = c.get("nome") or dados["nome"]
            nasc = _iso(c.get("nascimento"))

            doc = {"resourceType": "Patient", "identifier": ident,
                   "name": [{"use": "official", "text": nome}], "active": True,
                   "meta": {"source": FONTE}}
            if nasc:
                doc["birthDate"] = nasc
            if s := c.get("sexo"):
                doc["gender"] = "male" if s.lower().startswith("m") else "female"
            if tel := dados.get("telefone"):
                doc["telecom"] = [{"system": "phone", "value": tel}]
            if mae := c.get("nome_mae"):
                doc["contact"] = [{"relationship": [{"text": "mãe"}], "name": {"text": mae}}]
            # Sem CPF ou sem nascimento, a identidade e incompleta (ADR-0041): entra MARCADA.
            if not cpf or not nasc:
                doc["meta"]["tag"] = [{"system": "urn:smsmarica:qualidade",
                                       "code": "identidade-incompleta"}]
            linhas.append((json.dumps(doc, ensure_ascii=False), cns_sisreg, todos, cpf, nome, nasc,
                           dados.get("telefone")))

        psycopg2.extras.execute_batch(cur, """
            INSERT INTO fhir.patient (id, version_id, last_updated, meta_source, is_deleted,
                                      content, cns, cns_todos, cpf, nome, nascimento, telefone)
            VALUES (gen_random_uuid(), 1, now(), %s, false,
                    %s::jsonb, %s, %s, %s, %s, %s::date, %s)
        """, [(FONTE, *l) for l in linhas])
        criados += len(linhas)
        if criados % 5000 < lote:
            print(f"  criados {criados:,}/{len(criar):,}  "
                  f"({criados/max(time.monotonic()-t0,1):.0f}/s)")

    print(f"  criacao concluida: {criados:,} em {(time.monotonic()-t0)/60:.1f} min")


def _iso(d):
    if not d or len(d) != 10:
        return None
    dd, mm, aa = d.split("/")
    return f"{aa}-{mm}-{dd}"


def _relatorio(conflitos, pulados):
    destino = IMPL / "_inconformidades.csv"
    with destino.open("w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh, delimiter=";")
        w.writerow(["cns_sisreg", "nome", "telefone", "tipo", "detalhe"])
        for cns, d, ach in conflitos:
            w.writerow([cns, d["nome"], d["telefone"], "CONFLITO",
                        "chaves apontam para " + str(len(ach)) + " fichas: " + " | ".join(ach)])
        for cns, d, motivo in pulados:
            w.writerow([cns, d["nome"], d["telefone"], "BAIXA CONFIANCA", motivo])
    print(f"\ninconformidades: {destino}  ({len(conflitos) + len(pulados)} casos)")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
