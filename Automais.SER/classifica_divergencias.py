"""Separa, entre as divergencias de CNS, a MESMA pessoa da pessoa TROCADA.

Medido em 07/09/2026 sobre as 585 da fase 2: praticamente todas sao a mesma pessoa com mais de um
CNS -- gente que acumulou registros ao longo dos anos. Reter todas seria jogar fora 585 vinculos
validos; aceitar todas seria repetir o caso JULIA/ANDREA da fase 1, em que o SER devolveu a ficha
da mae para o CPF da filha.

## O que decide

1. NASCIMENTO, quando ha com o que comparar (a chave devolvida ja existe no hub). E o unico sinal
   que separou os casos reais: JULIA 2007 contra ANDREA 1987. Medido: 398 iguais contra 4
   diferentes em 402 comparaveis.

2. NOME TOLERANTE, quando nao ha nascimento. Comparacao exata nao serve -- os dados reais trazem
   `SOZUA` por `SOUZA`, `VALDELI` por `VALDELIR`, nome de casada acrescentado e campo truncado.
   Usa similaridade de sequencia mais sobreposicao de tokens.

3. O que nao fechar por nenhum dos dois fica RETIDO. Preferir reter a adivinhar: identidade
   trocada e o defeito mais caro deste projeto.
"""
from __future__ import annotations

import csv
import difflib
import json
import os
import pathlib
import sys
import unicodedata
from collections import Counter

import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = pathlib.Path(__file__).parent
SISREG = BASE.parent / "Automais.SISREG" / "capturas" / "implantacao"
ENTRADA = BASE / "capturas" / "identidades_cns.jsonl"
SAIDA = BASE / "capturas" / "divergencias_classificadas.csv"

COL_CNS, COL_NOME = 9, 10
LIMITE_SIMILARIDADE = 0.82


def norm(n: str) -> str:
    s = unicodedata.normalize("NFKD", n or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def dig(s: str) -> str:
    return "".join(c for c in (s or "") if c.isdigit())


def parecidos(a: str, b: str) -> tuple[bool, float]:
    """Tolerante de proposito: nome real tem digitacao errada, nome de casada e truncamento."""
    if not a or not b:
        return False, 0.0
    if a == b:
        return True, 1.0
    seq = difflib.SequenceMatcher(None, a, b).ratio()
    ta, tb = set(a.split()), set(b.split())
    sobrep = len(ta & tb) / max(1, min(len(ta), len(tb)))
    # Um nome pode ser prefixo do outro (campo truncado, ou sobrenome de casada acrescentado).
    prefixo = a.startswith(b[:18]) or b.startswith(a[:18])
    return (seq >= LIMITE_SIMILARIDADE or sobrep >= 0.75 or prefixo), max(seq, sobrep)


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


nomes_sisreg: dict[str, str] = {}
for arq in sorted(SISREG.glob("*/sisreg-unidade-*.txt")):
    with arq.open(encoding="utf-8", errors="replace") as fh:
        for i, l in enumerate(fh):
            if i and l.strip():
                c = l.split(";")
                if len(c) > COL_NOME and len(c[COL_CNS].strip()) == 15:
                    nomes_sisreg.setdefault(c[COL_CNS].strip(), c[COL_NOME].strip())

div = [json.loads(l) for l in ENTRADA.open(encoding="utf-8") if '"identidade_divergente"' in l]
print(f"divergencias a classificar: {len(div)}")

with conexao() as cx, cx.cursor() as cur:
    chaves_cpf = [dig(x.get("cpf")) for x in div if len(dig(x.get("cpf"))) == 11]
    cur.execute("select cpf, nome, nascimento from fhir.patient "
                "where cpf = any(%s) and not is_deleted", (chaves_cpf,))
    hub = {r[0]: (norm(r[1]), r[2]) for r in cur.fetchall()}

linhas, contagem = [], Counter()
for x in div:
    cns_sisreg = x["alerta"].split("CNS ")[1].split(",")[0].strip()
    cns_ser = dig(x.get("cns"))
    cpf_ser = dig(x.get("cpf"))
    nome_sis, nome_ser = norm(nomes_sisreg.get(cns_sisreg)), norm(x.get("nome"))
    nasc_ser = x.get("nascimento", "")

    decisao = motivo = ""
    if cpf_ser in hub and hub[cpf_ser][1] and nasc_ser:
        nasc_hub = hub[cpf_ser][1]
        if f"{nasc_hub.day:02d}/{nasc_hub.month:02d}/{nasc_hub.year}" == nasc_ser:
            decisao, motivo = "mesma_pessoa", "nascimento do hub confere com o do SER"
        else:
            decisao, motivo = "RETER", f"nascimento diverge: hub {nasc_hub} x SER {nasc_ser}"
    else:
        ok, score = parecidos(nome_sis, nome_ser)
        if ok:
            decisao, motivo = "mesma_pessoa", f"nomes compativeis (similaridade {score:.2f})"
        else:
            decisao, motivo = "RETER", f"sem nascimento comparavel e nomes distantes ({score:.2f})"

    contagem[decisao] += 1
    linhas.append({
        "cns_sisreg": cns_sisreg, "cns_do_ser": cns_ser, "cpf_do_ser": cpf_ser,
        "nome_sisreg": nomes_sisreg.get(cns_sisreg, ""), "nome_ser": x.get("nome", ""),
        "nascimento_ser": nasc_ser, "decisao": decisao, "motivo": motivo,
    })

with SAIDA.open("w", encoding="utf-8-sig", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=list(linhas[0]), delimiter=";")
    w.writeheader()
    w.writerows(linhas)

print()
for k, v in contagem.most_common():
    print(f"  {v:4}  {k}")
print(f"\nclassificacao: {SAIDA}")
print("\n=== os RETIDOS (nao entram em conciliacao nenhuma) ===")
for l in [x for x in linhas if x["decisao"] == "RETER"][:10]:
    print(f"  {l['nome_sisreg'][:26]:28} | {l['nome_ser'][:26]:28} | {l['motivo'][:44]}")
