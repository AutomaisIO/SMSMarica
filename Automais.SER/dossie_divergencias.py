"""Dossie COMPLETO das divergencias de identidade — para entender e reportar.

Reune, caso a caso, os tres lados da historia:

  1. O que o NOSSO HUB tem gravado (fhir.patient)
  2. O que o SER devolve quando perguntado pelo CPF do hub -- payload inteiro
  3. O que a RECEITA FEDERAL diz sobre os DOIS CPFs envolvidos

Os primeiros casos foram capturados antes de o resolvedor passar a guardar a evidencia, entao aqui
o SER e RECONSULTADO: sem o nome e o nascimento que ele devolve, nao da para demonstrar a terceiros
que a ficha e de outra pessoa -- e demonstrar e o proposito deste arquivo.

CONTEM DADO PESSOAL. Sai em `capturas/` (gitignored).
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import sys
import time

import httpx
import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
JSONL = BASE / "capturas" / "identidades.jsonl"
SAIDA_MD = BASE / "capturas" / "dossie_divergencias.md"
SAIDA_JSON = BASE / "capturas" / "dossie_divergencias.json"
HUB = "https://ws.hubdodesenvolvedor.com.br/v2/nome_cpf/"


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def dv_ok(cpf: str) -> bool:
    c = "".join(x for x in (cpf or "") if x.isdigit())
    if len(c) != 11 or c == c[0] * 11:
        return False
    for tam in (9, 10):
        s = sum(int(c[i]) * (tam + 1 - i) for i in range(tam))
        d = (s * 10) % 11
        if d == 10:
            d = 0
        if d != int(c[tam]):
            return False
    return True


def receita(cpf: str, token: str) -> dict:
    try:
        d = httpx.get(HUB, params={"cpf": cpf, "token": token}, timeout=40).json()
        if d.get("status") and (r := d.get("result")):
            return {"nome": r.get("nome", ""), "nascimento": r.get("data_de_nascimento", ""),
                    "atualizado": r.get("last_update", "")}
        return {"erro": str(d.get("message") or d.get("return") or "")[:60]}
    except Exception as e:
        return {"erro": f"falha de rede: {str(e)[:40]}"}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--token", required=True)
    args = ap.parse_args(argv)

    from resolver_identidades import SessaoSer

    div = [json.loads(l) for l in JSONL.open(encoding="utf-8") if '"identidade_divergente"' in l]
    print(f"divergencias a documentar: {len(div)}")

    print("abrindo sessao no SER para reconsultar...")
    s = SessaoSer(99)
    s.abrir()

    casos, gastos = [], 0
    with conexao() as cx, cx.cursor() as cur:
        for i, r in enumerate(div, 1):
            cpf_hub = r["chave"]
            cpf_ser = r.get("alerta", "").split("veio ")[-1].strip()
            print(f"  [{i}/{len(div)}] {cpf_hub}")

            cur.execute("""select id, nome, nascimento, cns, telefone, meta_source, last_updated
                           from fhir.patient where cpf=%s and not is_deleted limit 1""", (cpf_hub,))
            f = cur.fetchone() or ("", "", "", "", "", "", "")

            # Reconsulta: o payload do SER e a prova de que a ficha e de outra pessoa.
            ser = s.consultar(cpf_hub, reabrir=True) or {}
            time.sleep(0.5)

            rec_hub = receita(cpf_hub, args.token); time.sleep(1)
            rec_ser = receita(cpf_ser, args.token); time.sleep(1)
            gastos += 20

            mesma = (rec_hub.get("nome") and rec_ser.get("nome")
                     and rec_hub["nome"] == rec_ser["nome"])
            if dv_ok(cpf_hub) and not dv_ok(cpf_ser):
                veredito = ("O CPF que o SER devolveu tem dígito verificador INVÁLIDO — não "
                            "pertence a ninguém. O cadastro do nosso hub está correto.")
                risco = "baixo"
            elif not mesma and rec_hub.get("nome") and rec_ser.get("nome"):
                veredito = (f"PESSOAS DIFERENTES na Receita Federal. O CPF do hub é de "
                            f"{rec_hub['nome']}; o CPF que o SER devolveu é de {rec_ser['nome']}. "
                            f"O cadastro do SER para este CPF aponta para outra pessoa.")
                risco = "CRÍTICO"
            else:
                veredito = "Indefinido — precisa de conferência manual."
                risco = "alto"

            casos.append({
                "risco": risco,
                "hub": {"id": str(f[0]), "nome": f[1], "cpf": cpf_hub, "nascimento": str(f[2] or ""),
                        "cns": f[3] or "(vazio)", "telefone": f[4] or "", "origem": f[5] or "",
                        "atualizado_em": str(f[6] or "")},
                "ser_devolveu": {"cpf": ser.get("cpf", cpf_ser), "nome": ser.get("nome", ""),
                                 "nascimento": ser.get("nascimento", ""), "cns": ser.get("cns", ""),
                                 "nome_mae": ser.get("nome_mae", ""), "sexo": ser.get("sexo", ""),
                                 "municipio": ser.get("municipio", "")},
                "receita": {"cpf_do_hub": rec_hub, "cpf_do_ser": rec_ser},
                "dv": {"cpf_do_hub": dv_ok(cpf_hub), "cpf_do_ser": dv_ok(cpf_ser)},
                "veredito": veredito,
            })
    s.fechar()

    SAIDA_JSON.write_text(json.dumps(casos, indent=2, ensure_ascii=False), encoding="utf-8")

    ordem = {"CRÍTICO": 0, "alto": 1, "baixo": 2}
    casos.sort(key=lambda c: ordem[c["risco"]])
    md = [
        "# Dossiê — divergências de identidade no SER",
        "",
        f"**{len(casos)} casos** encontrados em {sum(1 for _ in JSONL.open(encoding='utf-8')):,} "
        "consultas de CPF ao SER, durante a implantação do histórico do SISREG (07/09/2026).",
        "",
        "> **CONTÉM DADO PESSOAL.** Não circular sem necessidade.",
        "",
        "Em todos os casos o SER, perguntado pelo CPF de um paciente, devolveu uma ficha cujo CPF "
        "é **diferente do perguntado**. Nenhum foi importado.",
        "",
    ]
    for i, c in enumerate(casos, 1):
        h, sr, rec = c["hub"], c["ser_devolveu"], c["receita"]
        md += [
            f"## Caso {i} — risco {c['risco']}", "",
            f"**Veredito:** {c['veredito']}", "",
            "| | No nosso hub | Devolvido pelo SER |",
            "|---|---|---|",
            f"| Nome | {h['nome']} | {sr['nome'] or '—'} |",
            f"| CPF | {h['cpf']} | {sr['cpf']} |",
            f"| DV do CPF | {'válido' if c['dv']['cpf_do_hub'] else 'INVÁLIDO'} | "
            f"{'válido' if c['dv']['cpf_do_ser'] else 'INVÁLIDO'} |",
            f"| Nascimento | {h['nascimento']} | {sr['nascimento'] or '—'} |",
            f"| CNS | {h['cns']} | {sr['cns'] or '—'} |",
            f"| Nome da mãe | — | {sr['nome_mae'] or '—'} |",
            "",
            "**Receita Federal:**", "",
            f"- CPF `{h['cpf']}` (o nosso) → {rec['cpf_do_hub'].get('nome') or rec['cpf_do_hub'].get('erro')}"
            f" · nasc {rec['cpf_do_hub'].get('nascimento', '—')}",
            f"- CPF `{sr['cpf']}` (o do SER) → {rec['cpf_do_ser'].get('nome') or rec['cpf_do_ser'].get('erro')}"
            f" · nasc {rec['cpf_do_ser'].get('nascimento', '—')}",
            "",
            f"*Registro no hub: `{h['id']}` · origem `{h['origem']}` · atualizado {h['atualizado_em']}*",
            "", "---", "",
        ]

    SAIDA_MD.write_text("\n".join(md), encoding="utf-8")
    print(f"\ncreditos gastos: {gastos}")
    print(f"dossie: {SAIDA_MD}")
    print(f"json  : {SAIDA_JSON}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
