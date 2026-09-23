# -*- coding: utf-8 -*-
"""Aplica o backfill no banco de PRODUÇÃO a partir do relatório do `backfill_siscan.py`.

O que grava, por par aprovado:

  smsmarica.exame_imagem  siscan_protocolo, siscan_numero_exame, siscan_requisicao_em
  smsmarica.anamnese      conteudo_json (só as LACUNAS preenchidas)

`siscan_requisicao_por` fica **NULL de propósito**: ninguém daqui criou essa
requisição. Protocolo preenchido + autor nulo é a assinatura do backfill, e o
carimbo `_origemSiscan` no próprio JSON diz de onde veio e quando.

Consequência que não é detalhe: gravar o protocolo **congela a anamnese**
(AnamnesesService recusa alteração de anamnese já enviada ao SISCAN). Para as
que já têm requisição isso é o comportamento certo — mas é por isso que um par
errado custa caro, e por isso o `--confirmar` é explícito e o desfazer é gerado
ANTES de escrever.

    python aplicar_backfill.py --relatorio <relatorio.json> --desfazer <desfazer.json>
    python aplicar_backfill.py ... --confirmar     # <- só aqui escreve

Sem `--confirmar` ele apenas mostra o plano. Nada é escrito.
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import difflib
import json
import os
import unicodedata

import psycopg2
from psycopg2.extras import Json


def asc(s: str) -> str:
    s = unicodedata.normalize("NFD", s or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def conectar():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    mm = {k.strip().lower(): v.strip()
          for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=mm["host"], port=mm.get("port", 25060), dbname=mm["database"],
                            user=mm.get("username") or mm.get("user id"),
                            password=mm["password"], sslmode="require")


def triagem(linha: dict, limiar_nome: float) -> str | None:
    """Motivo para NÃO aplicar, ou None se pode."""
    if linha.get("erro"):
        return "erro na leitura"
    if not linha.get("protocolo") or not linha.get("numero_exame"):
        return "sem protocolo ou sem Nº do exame"
    if not linha.get("cns_confere"):
        return "CNS da requisição não bate com o da paciente"
    if linha.get("prontuario_no_siscan"):
        # Prontuário preenchido = alguém pôs um número ali. Se for o nosso
        # accession, a requisição saiu daqui e o carimbo é trivial; se for outro,
        # ninguém sabe o que é. Nos dois casos, olho humano.
        if linha["prontuario_no_siscan"].strip() != linha["accession"]:
            return "prontuário preenchido no SISCAN com outro número"
    r = difflib.SequenceMatcher(None, asc(linha.get("paciente_nome")),
                                asc(linha.get("paciente_siscan"))).ratio()
    if r < limiar_nome:
        return "nome da paciente diverge (%.2f)" % r
    return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--relatorio", required=True)
    ap.add_argument("--desfazer", required=True)
    ap.add_argument("--confirmar", action="store_true")
    ap.add_argument("--limiar-nome", type=float, default=0.80)
    args = ap.parse_args()

    linhas = json.load(open(args.relatorio, encoding="utf-8"))
    hoje = dt.date.today().isoformat()

    aplicar, segurar = [], collections.Counter()
    for l in linhas:
        motivo = triagem(l, args.limiar_nome)
        if motivo:
            segurar[motivo] += 1
        else:
            aplicar.append(l)

    print("relatório: %d linha(s)" % len(linhas))
    print("  aplicáveis: %d" % len(aplicar))
    for k, v in segurar.most_common():
        print("  segurado — %-46s %4d" % (k, v))

    campos = collections.Counter(c.split(" ")[0] for l in aplicar for c in l.get("preenche") or [])
    print("\nlacunas que seriam preenchidas na anamnese:")
    for k, v in campos.most_common():
        print("  %-34s %4d" % (k, v))
    com_div = [l for l in aplicar if l.get("divergencias")]
    print("\ncom divergência entre a nossa anamnese e a requisição: %d "
          "(o carimbo vai; a divergência fica no relatório)" % len(com_div))

    if not args.confirmar:
        print("\n--confirmar ausente: NADA foi escrito.")
        return 0

    conn = conectar()
    cur = conn.cursor()

    # O desfazer nasce ANTES da escrita, do estado que está lá agora.
    cur.execute("""
      select e.id, e.siscan_protocolo, e.siscan_numero_exame, e.siscan_requisicao_em,
             a.id, a.conteudo_json
      from smsmarica.exame_imagem e join smsmarica.anamnese a on a.exame_imagem_id = e.id
      where e.id::text = any(%s) and a.excluido_em is null""",
                ([l["exame_id"] for l in aplicar],))
    antes = {str(r[0]): {"protocolo": r[1], "numero": r[2],
                         "requisicao_em": r[3].isoformat() if r[3] else None,
                         "anamnese_id": str(r[4]), "conteudo": r[5]} for r in cur.fetchall()}
    with open(args.desfazer, "w", encoding="utf-8") as fh:
        json.dump(antes, fh, ensure_ascii=False, indent=1)
    print("\nestado anterior salvo em %s" % args.desfazer)

    escritos = pulados = 0
    for l in aplicar:
        conteudo = dict(l["conteudo_novo"])
        conteudo["_origemSiscan"] = {
            "backfill": hoje,
            "protocolo": l["protocolo"],
            "numeroExame": l["numero_exame"],
            "observacao": "Requisição criada à mão no SISCAN e pareada por CNS + data.",
        }

        # `siscan_protocolo is null` na cláusula: se alguém gerou a requisição
        # pela tela enquanto este script rodava, o dele vale e o nosso não entra.
        cur.execute("""
          update smsmarica.exame_imagem
             set siscan_protocolo = %s, siscan_numero_exame = %s,
                 siscan_requisicao_em = now(), atualizado_em = now()
           where id::text = %s and siscan_protocolo is null""",
                    (l["protocolo"], l["numero_exame"], l["exame_id"]))
        if cur.rowcount == 0:
            pulados += 1
            continue

        cur.execute("""
          update smsmarica.anamnese
             set conteudo_json = %s, atualizado_em = now()
           where id::text = %s and excluido_em is null""",
                    (Json(conteudo), l["anamnese_id"]))
        escritos += 1

    conn.commit()
    print("gravados: %d · pulados (já tinham protocolo): %d" % (escritos, pulados))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
