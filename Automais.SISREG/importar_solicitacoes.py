"""Importa o HISTORICO de solicitacoes do SISREG (994 mil) para smsmarica.solicitacao.

Roda DEPOIS da conciliacao de pacientes: cada solicitacao aponta para um paciente, e criar
solicitacao orfa seria pior que nao importar.

## O que pula, e por que

- Paciente que a conciliacao NAO criou (conflito ou baixa confianca): sem paciente no hub, a
  solicitacao nao tem para onde apontar. Vai para o relatorio.
- Unidade executante sem CNES conhecido: a FK e obrigatoria.
- `codigo_solicitacao` que ja existe: indice unico parcial ja garante idempotencia, mas filtrar
  antes evita 19 mil conflitos inuteis no banco.

## Fidelidade ao que o dado diz

`status = Agendada` porque toda linha do `expo_solicitacoes` TEM data de atendimento -- e o
arquivo de agendamentos, nao de pedidos em aberto. Marcar como `Solicitada` diria que o pedido
ainda espera vaga, que e falso e contamina exatamente as telas de fila que acabamos de construir.

A linha crua vai para `raw_sisreg`: e dela que saiu, em 05/09/2026, o profissional executante de
21 mil solicitacoes que ninguem tinha guardado. Dado bruto preservado paga por si.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import pathlib
import sys
import time
import uuid
from datetime import datetime

import psycopg2
import psycopg2.extras

sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

BASE = pathlib.Path(__file__).parent
IMPL = BASE / "capturas" / "implantacao"

# Colunas do TXT (38 campos) — mesmos indices do AgendaTxtParser.
C_CODIGO, C_PROC_SISREG, C_SIGTAP, C_PROC_TEXTO = 0, 1, 2, 3
C_CPF_EXEC, C_NOME_EXEC, C_DATA, C_HORA = 4, 5, 6, 7
C_CNS, C_NOME_PAC = 9, 10
C_CNES_SOLIC, C_NOME_SOLIC = 26, 27
C_DT_SOLIC, C_DT_REG, C_CID, C_CPF_MED, C_NOME_MED = 29, 31, 35, 36, 37

CATEGORIA_CONSULTA, CATEGORIA_EXAME = 1, 2
STATUS_AGENDADA, PRIORIDADE_ELETIVA, CONFIRMACAO_PENDENTE = 2, 1, 1


def dig(s):
    return "".join(c for c in (s or "") if c.isdigit())


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def data_br(s):
    s = (s or "").strip().replace(".", "/")
    for f in ("%d/%m/%Y", "%d/%m/%y"):
        try:
            return datetime.strptime(s, f).date()
        except ValueError:
            pass
    return None


def instante(d, h):
    dia = data_br(d)
    if not dia:
        return None
    hh, mm = (h or "00:00").strip()[:5].split(":") if ":" in (h or "") else ("00", "00")
    try:
        # Brasilia fixo (UTC-3): o banco guarda timestamptz e a regra do repositorio e esta.
        return datetime(dia.year, dia.month, dia.day, int(hh), int(mm)).isoformat() + "-03:00"
    except ValueError:
        return None


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--gravar", action="store_true")
    ap.add_argument("--lote", type=int, default=2000)
    args = ap.parse_args(argv)

    cx = conexao()
    cx.autocommit = True
    cur = cx.cursor()

    print("indexando...")
    cur.execute("select id, cns, cns_todos from fhir.patient where not is_deleted")
    paciente_por_cns = {}
    for pid, cns, todos in cur.fetchall():
        for c in (todos or ([cns] if cns else [])):
            paciente_por_cns.setdefault(c, pid)
    print(f"  pacientes por CNS: {len(paciente_por_cns):,}")

    cur.execute("select id, cnes from smsmarica.unidade where cnes is not null and cnes <> ''")
    unidade_por_cnes = {c: i for i, c in cur.fetchall()}
    print(f"  unidades por CNES: {len(unidade_por_cnes):,}")

    cur.execute("select codigo_solicitacao from smsmarica.solicitacao where codigo_solicitacao is not null")
    ja_existem = {r[0] for r in cur.fetchall()}
    print(f"  solicitacoes ja no banco: {len(ja_existem):,}")

    lidas = vistos = 0
    prontas, sem_paciente, sem_unidade = [], [], []
    t0 = time.monotonic()

    for arq in sorted(IMPL.glob("*/sisreg-unidade-*.txt")):
        with arq.open(encoding="utf-8", errors="replace") as fh:
            cab = fh.readline().split(";")
            cnes_exec = cab[0].strip() if cab else ""
            unidade_exec = unidade_por_cnes.get(cnes_exec)
            for linha in fh:
                if not linha.strip():
                    continue
                c = linha.rstrip("\n").split(";")
                if len(c) < 38:
                    continue
                lidas += 1
                codigo = c[C_CODIGO].strip()
                if not codigo.isdigit() or codigo in ja_existem:
                    continue
                ja_existem.add(codigo)          # dedup entre arquivos (janelas se sobrepoem)
                vistos += 1

                if not unidade_exec:
                    sem_unidade.append((codigo, cnes_exec))
                    continue
                pid = paciente_por_cns.get(c[C_CNS].strip())
                if not pid:
                    sem_paciente.append((codigo, c[C_CNS].strip(), c[C_NOME_PAC].strip()))
                    continue

                proc = c[C_PROC_TEXTO].strip()
                prontas.append((
                    str(uuid.uuid4()), pid,
                    CATEGORIA_EXAME if "CONSULTA" not in proc.upper() else CATEGORIA_CONSULTA,
                    unidade_exec, unidade_por_cnes.get(c[C_CNES_SOLIC].strip()),
                    c[C_NOME_MED].strip()[:200] or "NAO INFORMADO", "", "", "CRM",
                    STATUS_AGENDADA, PRIORIDADE_ELETIVA, CONFIRMACAO_PENDENTE,
                    codigo, (dig(c[C_SIGTAP]) or None), proc[:300] or None,
                    c[C_PROC_SISREG].strip() or None,
                    data_br(c[C_DT_SOLIC]), data_br(c[C_DT_REG]),
                    instante(c[C_DATA], c[C_HORA]),
                    (dig(c[C_CPF_EXEC]) if len(dig(c[C_CPF_EXEC])) == 11 else None),
                    c[C_NOME_EXEC].strip()[:200] or None,
                    (dig(c[C_CPF_MED]) if len(dig(c[C_CPF_MED])) == 11 else None),
                    # CID: e o que torna a analise clinica. Sem ele da para dizer quantas
                    # ultrassonografias a rede pede, nao POR QUE. A coluna foi criada para isto.
                    (c[C_CID].strip()[:10] or None),
                    linha.rstrip("\n"),
                ))

    print(f"\nlidas: {lidas:,} · novas: {vistos:,}")
    print(f"  prontas para inserir : {len(prontas):,}")
    print(f"  SEM paciente no hub  : {len(sem_paciente):,}")
    print(f"  SEM unidade executante: {len(sem_unidade):,}")

    if not args.gravar:
        print("\n[MODO SECO] nada foi escrito.")
        _relatorio(sem_paciente, sem_unidade)
        return 0

    print("\n=== GRAVANDO ===")
    sql = """
        INSERT INTO smsmarica.solicitacao
          (id, paciente_id, categoria, unidade_executante_id, unidade_solicitante_id,
           solicitante_nome, solicitante_num_conselho, solicitante_uf_conselho, solicitante_conselho,
           status, prioridade, status_confirmacao,
           codigo_solicitacao, procedimento_sigtap_codigo, procedimento_texto,
           procedimento_codigo_sisreg, data_solicitacao, data_regulacao, data_agendada,
           profissional_executante_cpf, profissional_executante_nome, solicitante_cpf, cid_codigo,
           raw_sisreg, criado_em)
        VALUES %s
        ON CONFLICT DO NOTHING
    """
    t0, feitos = time.monotonic(), 0
    for i in range(0, len(prontas), args.lote):
        fatia = prontas[i:i + args.lote]
        psycopg2.extras.execute_values(
            cur, sql, fatia,
            template="(" + ",".join(["%s"] * 24) + ",now())",
            page_size=args.lote)
        feitos += len(fatia)
        if feitos % 50000 < args.lote:
            print(f"  {feitos:,}/{len(prontas):,}  ({feitos/max(time.monotonic()-t0,1):.0f}/s)")

    print(f"  concluido: {feitos:,} em {(time.monotonic()-t0)/60:.1f} min")
    _relatorio(sem_paciente, sem_unidade)
    return 0


def _relatorio(sem_paciente, sem_unidade):
    destino = IMPL / "_solicitacoes_nao_importadas.csv"
    with destino.open("w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh, delimiter=";")
        w.writerow(["codigo_solicitacao", "motivo", "detalhe"])
        for cod, cns, nome in sem_paciente:
            w.writerow([cod, "paciente nao esta no hub", f"CNS {cns} · {nome}"])
        for cod, cnes in sem_unidade:
            w.writerow([cod, "unidade executante desconhecida", f"CNES {cnes}"])
    print(f"nao importadas: {destino}  ({len(sem_paciente) + len(sem_unidade)} casos)")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
