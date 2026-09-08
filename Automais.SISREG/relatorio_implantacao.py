"""Relatorio final da implantacao: o que entrou, e o que NAO entrou e por que.

O que nao entrou e o que importa aqui. Sao os casos em que a conciliacao teria de ADIVINHAR --
e a decisao foi nao adivinhar. Cada linha precisa de olho humano.
"""
from __future__ import annotations

import csv
import json
import os
import pathlib
import sys
from collections import Counter

import psycopg2

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = pathlib.Path(__file__).parent
IMPL = BASE / "capturas" / "implantacao"
SER = BASE.parent / "Automais.SER" / "capturas"
SAIDA = IMPL / "RELATORIO_IMPLANTACAO.md"


def conexao():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    m = {k.strip().lower(): v.strip() for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=m["host"], port=m.get("port", 25060), dbname=m["database"],
                            user=m.get("username") or m.get("user id"), password=m["password"],
                            sslmode="require")


def ler(p):
    return list(csv.DictReader(p.open(encoding="utf-8-sig"), delimiter=";")) if p.exists() else []


def n(v):
    """Numero em pt-BR. O relatorio e lido pela secretaria, nao por um console americano."""
    return f"{v:,}".replace(",", ".")


def cel(s):
    """O `|` dentro do texto parte a coluna da tabela e o markdown some com o resto da linha."""
    return " ".join((s or "").replace("|", "·").split())


cx = conexao()
cur = cx.cursor()

cur.execute("""select count(*),
       count(*) filter (where meta_source like '%sisreg/implantacao%'),
       count(*) filter (where array_length(cns_todos,1) > 1)
from fhir.patient where not is_deleted""")
pac_total, pac_novos, pac_multi = cur.fetchone()

cur.execute("""select count(*),
       count(*) filter (where cid_codigo is not null),
       count(*) filter (where data_agendada is not null),
       min(data_solicitacao), max((data_agendada at time zone 'America/Sao_Paulo')::date)
from smsmarica.solicitacao where excluido_em is null""")
sol_total, sol_cid, sol_agend, sol_min, sol_max = cur.fetchone()

inconf = ler(IMPL / "_inconformidades.csv")
nao_imp = ler(IMPL / "_solicitacoes_nao_importadas.csv")
diverg = ler(SER / "divergencias_classificadas.csv")
retidos = [d for d in diverg if d["decisao"] != "mesma_pessoa"]

md = [
    "# Relatório da implantação — histórico do SISREG",
    "",
    "**08/09/2026** · Maricá",
    "",
    "> Contém identificadores de cidadão nos anexos. Este documento em si traz apenas os casos "
    "que precisam de decisão humana.",
    "",
    "## O que entrou",
    "",
    "| | |",
    "|---|---|",
    f"| Solicitações no banco | **{n(sol_total)}** |",
    f"| ↳ com data agendada | {n(sol_agend)} |",
    f"| ↳ com CID | {n(sol_cid)} |",
    f"| Período coberto | {sol_min} a {sol_max} |",
    f"| Pacientes no hub | **{n(pac_total)}** |",
    f"| ↳ criados nesta carga | {n(pac_novos)} |",
    f"| ↳ com mais de um CNS | {n(pac_multi)} |",
    "",
    "Antes desta carga havia 23.836 solicitações — 2% do histórico. O restante era invisível.",
    "",
    "---",
    "",
    "# INCONFORMIDADES",
    "",
    "Nenhum destes foi importado. Em todos, concluir exigiria **deduzir** — e a decisão foi não "
    "deduzir, porque identidade trocada é o defeito mais caro deste sistema e o mais difícil de "
    "descobrir depois.",
    "",
]

conflitos = [i for i in inconf if i["tipo"] == "CONFLITO"]
baixa = [i for i in inconf if i["tipo"] == "BAIXA CONFIANCA"]

md += [
    "## 1. Conflito — o paciente tem DUAS fichas no nosso hub",
    "",
    f"**{len(conflitos)} casos.**",
    "",
    "As chaves conhecidas da mesma pessoa (CNS do SISREG, CNS definitivo, CPF) apontam para "
    "**fichas diferentes** do hub. Isto **não é problema do SISREG** — é prontuário partido do "
    "nosso lado, que a carga apenas revelou.",
    "",
    "Escolher uma das fichas esconderia a duplicata; fundir sem conferência arriscaria juntar "
    "pessoas distintas. Precisa de decisão caso a caso.",
    "",
]
if conflitos:
    md += ["| Paciente | CNS do SISREG | Fichas encontradas |", "|---|---|---|"]
    for c in conflitos:
        md.append(f"| {cel(c['nome'])} | `{c['cns_sisreg']}` | {cel(c['detalhe'])} |")
    md.append("")

md += [
    "## 2. Baixa confiança — identidade divergente",
    "",
    f"**{len(baixa)} casos.**",
    "",
    "O SER, perguntado por um identificador, devolveu ficha com **outro** identificador, e os "
    "sinais não fecham: em vários deles o **nome é idêntico mas o nascimento difere**.",
    "",
    "É exatamente o padrão que uma regra por nome aprovaria e que estaria errada. Sem nascimento "
    "coincidente, não há como afirmar que é a mesma pessoa.",
    "",
]
if retidos:
    md += ["| Nome no SISREG | Nome no SER | Motivo |", "|---|---|---|"]
    for r in retidos:
        md.append(f"| {cel(r['nome_sisreg'])} | {cel(r['nome_ser'])} | {cel(r['motivo'])} |")
    md.append("")

md += [
    "## 3. Solicitações não importadas",
    "",
    f"**{len(nao_imp)} solicitações** — todas pertencentes aos pacientes dos itens 1 e 2. "
    "Sem paciente no hub, a solicitação não teria para onde apontar.",
    "",
    "Entram sozinhas assim que a identidade daqueles pacientes for resolvida: basta rodar "
    "`importar_solicitacoes.py --gravar` de novo, que é idempotente.",
    "",
    "---",
    "",
    "## Divergências de identidade no SER (achado à parte)",
    "",
    f"Ao resolver identidade no SER, **{len(diverg)} casos** vieram com identificador diferente do "
    f"perguntado. Analisados: **{len(diverg) - len(retidos)}** são a mesma pessoa com mais de um "
    f"CNS — situação normal, agora suportada pelo hub. Os **{len(retidos)}** restantes estão no "
    "item 2.",
    "",
    "Além destes, na fase por CPF houve 64 divergências, das quais duas foram confirmadas pela "
    "Receita Federal como **pessoas diferentes** — o SER devolveu a ficha de outra pessoa para um "
    "CPF válido. Detalhe em `Automais.SER/documentacao/divergencias-identidade-ser.md`.",
    "",
    "**Isso vale para a operação diária, não só para a carga:** a varredura consulta a mesma fonte "
    "sem essa conferência hoje. A pendência está registrada naquele documento.",
    "",
    "## Anexos (contêm dado pessoal, fora do git)",
    "",
    "- `_inconformidades.csv` — conflitos e baixa confiança",
    "- `_solicitacoes_nao_importadas.csv` — as 92 solicitações",
    "- `Automais.SER/capturas/dossie_divergencias.md` — os casos com os três lados",
]

SAIDA.write_text("\n".join(md), encoding="utf-8")
print(f"relatorio: {SAIDA}")
print()
print(f"  solicitacoes : {n(sol_total)}")
print(f"  pacientes    : {n(pac_total)} ({n(pac_novos)} novos)")
print(f"  CONFLITOS    : {len(conflitos)}")
print(f"  BAIXA CONFIANCA: {len(baixa)}")
print(f"  solicitacoes nao importadas: {len(nao_imp)}")
