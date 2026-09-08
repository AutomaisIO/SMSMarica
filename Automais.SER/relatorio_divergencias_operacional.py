"""Relatorio OPERACIONAL das divergencias — com PII, para quem vai corrigir.

O relatorio sem PII (documentacao/divergencias-identidade-ser.md) serve para circular o achado.
Este serve para CONSERTAR: traz o id do paciente no hub, o que esta gravado la, o que o SER
devolveu e o que a Receita diz, com a acao recomendada caso a caso.

CONTEM DADO PESSOAL. Sai em `capturas/`, que e gitignored, e nao deve ser colado em chat, ticket
publico ou e-mail sem o cuidado de sempre.
"""
from __future__ import annotations

import argparse
import csv
import json
import pathlib
import sys
import time

import httpx
import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = pathlib.Path(__file__).parent
JSONL = BASE / "capturas" / "identidades.jsonl"
CSV_SAIDA = BASE / "capturas" / "divergencias_para_corrigir.csv"
HTML_SAIDA = BASE / "capturas" / "divergencias_para_corrigir.html"

HUB = "https://ws.hubdodesenvolvedor.com.br/v2/nome_cpf/"


def conexao():
    import os
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
        soma = sum(int(c[i]) * (tam + 1 - i) for i in range(tam))
        d = (soma * 10) % 11
        if d == 10:
            d = 0
        if d != int(c[tam]):
            return False
    return True


def receita(cpf: str, token: str) -> dict:
    try:
        d = httpx.get(HUB, params={"cpf": cpf, "token": token}, timeout=40).json()
        if d.get("status") and (r := d.get("result")):
            return {"nome": r.get("nome", ""), "nascimento": r.get("data_de_nascimento", "")}
        return {"erro": str(d.get("message") or "")[:50]}
    except Exception as e:
        return {"erro": f"falha: {str(e)[:40]}"}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--token", required=True)
    args = ap.parse_args(argv)

    div = [json.loads(l) for l in JSONL.open(encoding="utf-8") if '"identidade_divergente"' in l]
    print(f"divergencias: {len(div)}")

    linhas = []
    with conexao() as cx, cx.cursor() as cur:
        for r in div:
            pedido = r["chave"]
            veio = r.get("alerta", "").split("veio ")[-1].strip()

            cur.execute("""select id, nome, nascimento, cns, telefone
                           from fhir.patient where cpf = %s and not is_deleted limit 1""", (pedido,))
            f = cur.fetchone()
            id_hub, nome_hub, nasc_hub, cns_hub, tel_hub = f if f else ("", "", "", "", "")

            a, b = dv_ok(pedido), dv_ok(veio)
            if a and not b:
                acao = ("MANTER o cadastro do hub. O CPF que o SER devolveu tem digito "
                        "verificador invalido — nao pertence a ninguem.")
                risco = "baixo"
            elif b and not a:
                acao = "CORRIGIR o CPF do hub: o nosso tem DV invalido e o do SER e valido."
                risco = "alto"
            else:
                rp = receita(pedido, args.token); time.sleep(1)
                rv = receita(veio, args.token); time.sleep(1)
                if rp.get("nome") and rv.get("nome") and rp["nome"] != rv["nome"]:
                    acao = (f"NAO conciliar. Sao pessoas diferentes na Receita: o CPF do hub e de "
                            f"{rp['nome']} e o CPF devolvido pelo SER e de {rv['nome']}. "
                            f"O cadastro do SER para este CPF esta trocado.")
                    risco = "CRITICO"
                else:
                    acao = "Indefinido — precisa de conferencia manual."
                    risco = "alto"

            linhas.append({
                "id_paciente_hub": str(id_hub),
                "nome_no_hub": nome_hub or "",
                "cpf_no_hub": pedido,
                "nascimento_no_hub": str(nasc_hub or ""),
                "cns_no_hub": cns_hub or "(vazio)",
                "telefone_no_hub": tel_hub or "",
                "cpf_devolvido_pelo_ser": veio,
                "nome_devolvido_pelo_ser": r.get("nome", ""),
                "nascimento_devolvido_pelo_ser": r.get("nascimento", ""),
                "cns_devolvido_pelo_ser": r.get("cns", ""),
                "risco": risco,
                "acao_recomendada": acao,
            })

    with CSV_SAIDA.open("w", encoding="utf-8-sig", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=list(linhas[0]), delimiter=";")
        w.writeheader()
        w.writerows(linhas)

    cor = {"CRITICO": "#fee2e2", "alto": "#ffedd5", "baixo": "#f0fdf4"}
    html = ["<!doctype html><meta charset='utf-8'><title>Divergencias de identidade</title>",
            "<style>body{font:14px system-ui;margin:24px;color:#111}"
            "table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:6px;"
            "text-align:left;vertical-align:top}th{background:#f5f5f5}"
            ".av{background:#fff7ed;border:1px solid #fdba74;padding:10px;border-radius:6px}</style>",
            "<h1>Divergências de identidade — SER</h1>",
            f"<p class='av'><b>Contém dado pessoal.</b> {len(linhas)} casos em que o SER, "
            "perguntado por um CPF, devolveu a ficha de outra pessoa. Nenhum foi conciliado.</p>",
            "<table><tr><th>Risco</th><th>No nosso hub</th><th>O que o SER devolveu</th>"
            "<th>Ação recomendada</th></tr>"]
    for l in sorted(linhas, key=lambda x: {"CRITICO": 0, "alto": 1, "baixo": 2}[x["risco"]]):
        html.append(
            f"<tr style='background:{cor[l['risco']]}'>"
            f"<td><b>{l['risco']}</b></td>"
            f"<td>{l['nome_no_hub']}<br><small>CPF {l['cpf_no_hub']} · nasc {l['nascimento_no_hub']}"
            f"<br>id {l['id_paciente_hub']}</small></td>"
            f"<td>{l['nome_devolvido_pelo_ser']}<br><small>CPF {l['cpf_devolvido_pelo_ser']} · "
            f"nasc {l['nascimento_devolvido_pelo_ser']}</small></td>"
            f"<td>{l['acao_recomendada']}</td></tr>")
    html.append("</table>")
    HTML_SAIDA.write_text("\n".join(html), encoding="utf-8")

    print(f"\nCSV : {CSV_SAIDA}")
    print(f"HTML: {HTML_SAIDA}")
    from collections import Counter
    print("\n=== por risco ===")
    for k, v in Counter(l["risco"] for l in linhas).most_common():
        print(f"  {v:3}  {k}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
