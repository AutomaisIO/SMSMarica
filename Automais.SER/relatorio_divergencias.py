"""Relatorio das DIVERGENCIAS DE IDENTIDADE achadas na resolucao contra o SER.

Existe porque achado de qualidade de dado que nao vira relatorio nao vira correcao. Cada linha
aqui e um caso em que o SER, perguntado por um CPF, devolveu a ficha de OUTRA pessoa -- e a
importacao teria gravado o cadastro trocado sem ninguem perceber.

Fecha o veredito com a cascata mais barata primeiro:

  1. DIGITO VERIFICADOR -- aritmetica local, custo zero. CPF com DV invalido nao e de ninguem.
  2. RECEITA (Hub do Desenvolvedor, `nome_cpf/`) -- so quando os dois CPFs sao validos e portanto
     a aritmetica nao decide. 10 creditos por consulta, e o saldo e finito (10.500).

Saida em CSV para leitura humana. CONTEM PII: nome e CPF de cidadao. Vai para `capturas/`, que e
gitignored -- nunca para o repositorio.
"""
from __future__ import annotations

import argparse
import csv
import json
import pathlib
import sys
import time

import httpx

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = pathlib.Path(__file__).parent
JSONL = BASE / "capturas" / "identidades.jsonl"
SAIDA = BASE / "capturas" / "divergencias_identidade.csv"

HUB = "https://ws.hubdodesenvolvedor.com.br/v2/nome_cpf/"


def dv_ok(cpf: str) -> bool:
    """Digito verificador do CPF. O desempate mais barato que existe."""
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
    """Nome + nascimento na Receita. 10 creditos -- so para quem a aritmetica nao resolveu."""
    try:
        r = httpx.get(HUB, params={"cpf": cpf, "token": token}, timeout=40)
        d = r.json()
        if d.get("status") and (res := d.get("result")):
            return {"nome": res.get("nome", ""), "nascimento": res.get("data_de_nascimento", "")}
        return {"erro": str(d.get("message") or d.get("return") or "")[:60]}
    except Exception as e:  # rede/timeout: nao e negativa, e indisponibilidade
        return {"erro": f"falha: {str(e)[:50]}"}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--token", help="token do Hub; sem ele, so o veredito por DV")
    ap.add_argument("--consultar-receita", action="store_true",
                    help="gasta creditos APENAS nos casos que o DV nao resolveu")
    args = ap.parse_args(argv)

    if not JSONL.exists():
        print(f"!! falta {JSONL}", file=sys.stderr)
        return 2

    div = [json.loads(l) for l in JSONL.open(encoding="utf-8")
           if '"identidade_divergente"' in l]
    print(f"divergencias registradas: {len(div)}")
    if not div:
        return 0

    linhas, gastos = [], 0
    for r in div:
        pedido = r["chave"]
        veio = r.get("alerta", "").split("veio ")[-1].strip()
        a, b = dv_ok(pedido), dv_ok(veio)

        if a and not b:
            veredito, fonte = "CPF pedido e valido; o devolvido nao — manter o nosso", "DV"
        elif b and not a:
            veredito, fonte = "CPF devolvido e valido; o nosso nao — o NOSSO esta errado", "DV"
        elif not a and not b:
            veredito, fonte = "nenhum dos dois e valido", "DV"
        else:
            veredito, fonte = "os dois sao validos — indefinido sem a Receita", "—"

        rec_pedido = rec_veio = ""
        if fonte == "—" and args.consultar_receita and args.token:
            p = receita(pedido, args.token)
            time.sleep(1)
            v = receita(veio, args.token)
            time.sleep(1)
            gastos += 20
            rec_pedido = p.get("nome") or p.get("erro", "")
            rec_veio = v.get("nome") or v.get("erro", "")
            if rec_pedido and rec_veio and rec_pedido != rec_veio:
                veredito = "pessoas DIFERENTES na Receita — o SER devolveu outra ficha"
                fonte = "Receita"

        linhas.append({
            "cpf_consultado": pedido,
            "cpf_devolvido_pelo_ser": veio,
            "dv_do_consultado": "valido" if a else "INVALIDO",
            "dv_do_devolvido": "valido" if b else "INVALIDO",
            "nome_devolvido_pelo_ser": r.get("nome", ""),
            "nascimento_devolvido": r.get("nascimento", ""),
            "cns_devolvido": r.get("cns", ""),
            "nome_na_receita_do_consultado": rec_pedido,
            "nome_na_receita_do_devolvido": rec_veio,
            "veredito": veredito,
            "decidido_por": fonte,
        })

    with SAIDA.open("w", encoding="utf-8-sig", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=list(linhas[0]), delimiter=";")
        w.writeheader()
        w.writerows(linhas)

    print(f"\ncreditos gastos na Receita: {gastos}")
    print(f"relatorio: {SAIDA}")
    print("\n=== resumo por veredito ===")
    from collections import Counter
    for v, n in Counter(l["veredito"] for l in linhas).most_common():
        print(f"  {n:3}  {v}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
