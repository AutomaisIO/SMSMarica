"""Funde os pares de paciente do NIVEL MAIS SEGURO, pelo `$merge` da API FHIR.

**Nao grava nada sem `--gravar`.** Sem a flag, imprime exatamente o que faria.

### Por que so o nivel 1 passa por aqui
A fusao tem duas metades: os recursos de `fhir.*` (que o `$merge` resolve) e as ~23 colunas de
`smsmarica.*` que guardam id de paciente (laudo, solicitacao, conversa…), que **nao tem FK nenhuma**
e ficariam apontando para o absorvido sem nada avisar. Fundir so uma metade e pior que nao fundir:
o dado nao some, fica invisivel numa ficha inativa.

O nivel 1 e o unico onde esse problema **nao existe**, porque o absorvido tem ZERO linha apontando
para ele — em `fhir.*` e em `smsmarica.*`. Nao ha o que repontar, entao a chamada de API sozinha ja
e a fusao inteira, numa transacao so. Os demais niveis exigem o script que faz as duas metades
juntas, e nao entram aqui de proposito.

### O criterio (medido em 24/09/2026, dos 1.195 grupos)
- mesmo **CPF com DV valido** — a evidencia mais forte que existe aqui, nao CNS (99,3% dos CNS do
  hub sao provisorios, e a mesma pessoa pode ter varios);
- **nome da mae nao diverge** — onde diverge (95 grupos) podem ser pessoas diferentes com um
  documento digitado errado. **Atencao: isto sozinho NAO basta** (ver o criterio de nome abaixo);
- **par simples** (exatamente 2 fichas) — grupo de 3+ tem ordem de fusao a decidir;
- **absorvido com ZERO dado clinico**;
- **os dois nomes sao o mesmo nome escrito torto** (similaridade >= 0,85). Sem este criterio o
  modo seco ia fundir `WANDERLEY DA SILVA` com `LAVINIA DOS SANTOS PIMENTEL` — mesmo CPF, mesma
  data de nascimento, **mesma mae**, nascidos em 2023: gemeos com o CPF trocado. Ver
  `nomes_sao_a_mesma_pessoa`.

Sobram **5 pares** (eram 6 antes do criterio de nome entrar). E pouco de proposito: serve para
validar o mecanismo ponta a ponta com risco proximo de zero antes de encostar nos 443 do nivel 2.

### Conferencia
Depois de cada fusao, conta quantas linhas ainda apontam para o absorvido nas 29 colunas
conhecidas. Tem de dar **zero**. Se nao der, para e avisa — e o unico jeito de perceber, ja que
o banco nao tem FK para reclamar.

Uso:
  python fundir_seguros.py                      # seco: mostra o que faria
  python fundir_seguros.py --gravar             # executa (ESCRITA EM PRODUCAO)
  python fundir_seguros.py --gravar --api http://localhost:5081
"""

from __future__ import annotations

import collections
import csv
import difflib
import pathlib
import sys
import unicodedata

import httpx

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
# O helper de banco (`db.py`) vive nas ferramentas do monorepo e so e importado no modo --gravar,
# para a conferencia pos-fusao. O caminho entra aqui: quando desacoplei REFERENCIAS deste import,
# tirei o sys.path junto e o `from db import conn` la embaixo passou a estourar — no meio da
# execucao, ja com o tunel aberto. Import tardio precisa do caminho preparado cedo.
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]
                       / "Aprendizados e Scratchpads" / "ferramentas"))
from prime.referencias_paciente import REFERENCIAS  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")


def norm(s: str | None) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                   if unicodedata.category(c) != "Mn").strip()


def prox_nome(a: str, b: str) -> float:
    x, y = norm(a), norm(b)
    return difflib.SequenceMatcher(None, x, y).ratio() if x and y else 0.0


def nomes_sao_a_mesma_pessoa(a: str, b: str) -> bool:
    """Os dois nomes sao variacao de grafia do MESMO nome, nao nomes diferentes.

    Isto entrou depois de o modo seco pegar um par que teria fundido duas criancas:
    mesmo CPF, mesma data de nascimento (23/02/2023), **mesma mae** — e nomes
    `WANDERLEY DA SILVA` x `LAVINIA DOS SANTOS PIMENTEL`. Sao gemeos, com o CPF de um
    na ficha do outro.

    A licao: para quem nasceu no mesmo dia, **mae igual e evidencia de GEMEOS, nao de
    duplicata**. O filtro de "mae concorda" nao so falha nesse caso como aponta para o
    lado errado. O que distingue duplicata de gemeo e o NOME: duplicata tem o mesmo nome
    escrito torto (`ANTONIO`/`ANTTONIO`, `PEREIRA`/`PERREIRA`); gemeo tem outro nome.
    """
    x, y = norm(a), norm(b)
    if not x or not y:
        return False
    return difflib.SequenceMatcher(None, x, y).ratio() >= 0.85


def dv_cpf_ok(cpf: str | None) -> bool:
    c = "".join(ch for ch in (cpf or "") if ch.isdigit())
    if len(c) != 11 or c == c[0] * 11:
        return False
    for corte in (9, 10):
        soma = sum(int(c[i]) * (corte + 1 - i) for i in range(corte))
        d = (soma * 10) % 11
        if d == 10:
            d = 0
        if d != int(c[corte]):
            return False
    return True


def veredito_receita() -> dict[str, dict]:
    """CPF -> o que a Receita disse (`conferir_cpf_receita.py`). Vazio se ainda nao foi conferido.

    Vira criterio ELIMINATORIO: so entra na fusao o CPF cujo nome oficial casa com alguma ficha.
    Foi assim que o par `WANDERLEY DA SILVA` x `LAVINIA DOS SANTOS PIMENTEL` saiu — a Receita
    respondeu `LAVINIA DOS SANTOS SILVA PIMENTEL`, entao so uma delas e dona daquele CPF e a outra
    esta com documento de terceiro.
    """
    arq = SAIDA / "conferencia_receita.json"
    if not arq.exists():
        return {}
    import json
    return {r["cpf"]: r for r in json.loads(arq.read_text(encoding="utf-8")) if r.get("receita")}


def pares_seguros() -> list[dict]:
    g: dict[tuple, list[dict]] = collections.defaultdict(list)
    with (SAIDA / "duplicatas.csv").open(encoding="utf-8-sig") as f:
        for l in csv.DictReader(f):
            g[(l["tier"], l["chave"])].append(l)

    receita = veredito_receita()
    seguros = []
    for (tier, chave), ms in g.items():
        if tier != "T1-CPF" or not dv_cpf_ok(ms[0]["cpf"]):
            continue
        if any(m["maes_divergem"] == "True" for m in ms):
            continue
        if len(ms) != 2:
            continue
        absorvidos = [m for m in ms if m["sobrevivente"] != "True"]
        if len(absorvidos) != 1 or int(absorvidos[0]["referencias"]) != 0:
            continue
        fica = next(m for m in ms if m["sobrevivente"] == "True")
        if not nomes_sao_a_mesma_pessoa(fica["nome"], absorvidos[0]["nome"]):
            continue  # nomes diferentes: gemeo ou CPF trocado — so com pessoa olhando

        # A Receita manda sobre a heuristica. Sem conferencia, o par NAO entra: o par de gemeos
        # passou em quatro criterios meus e so a Receita o barrou.
        r = receita.get(chave)
        if not r:
            continue
        if "NAO FUNDIR" in (r.get("veredito") or ""):
            continue
        if prox_nome(r["receita"], fica["nome"]) < 0.85 and            prox_nome(r["receita"], absorvidos[0]["nome"]) < 0.85:
            continue  # nenhuma das fichas e a pessoa que a Receita conhece

        seguros.append({"cpf": chave, "fica": fica, "vai": absorvidos[0],
                        # O sobrevivente continua sendo quem tem o dado clinico (menos linha a
                        # repontar). O nome oficial NAO troca o sobrevivente — ele corrige o nome
                        # dele, que e outra decisao. Em 2 dos 11 a grafia certa estava na ficha
                        # absorvida, e trocar o sobrevivente por isso so aumentaria o repontamento.
                        "nome_oficial": r["receita"],
                        "situacao": r.get("situacao")})
    return seguros


def ainda_aponta(cur, patient_id: str) -> dict[str, int]:
    """Quantas linhas ainda apontam para o absorvido, por coluna. Tem de vir vazio."""
    sobrou: dict[str, int] = {}
    for sch, tab, col in REFERENCIAS:
        try:
            cur.execute(f'select count(*) from {sch}."{tab}" where {col}::text = %s', (patient_id,))
            n = cur.fetchone()[0]
        except Exception:  # noqa: BLE001 — tabela pode nao existir nesta instancia
            continue
        if n:
            sobrou[f"{sch}.{tab}.{col}"] = n
    return sobrou


def main(argv: list[str]) -> int:
    api = argv[argv.index("--api") + 1] if "--api" in argv else "http://localhost:5081"
    gravar = "--gravar" in argv

    pares = pares_seguros()
    print(f"pares no nivel mais seguro: {len(pares)}\n")
    for p in pares:
        print(f"   CPF {p['cpf']}")
        print(f"      FICA  {p['fica']['id']}  {p['fica']['nome'][:34]:<34} "
              f"fonte={p['fica']['fonte'][:24]}  refs={p['fica']['referencias']}")
        print(f"      VAI   {p['vai']['id']}  {p['vai']['nome'][:34]:<34} "
              f"fonte={p['vai']['fonte'][:24]}  refs={p['vai']['referencias']}")
        print(f"      RECEITA: {p['nome_oficial'][:44]:<44} situacao={p.get('situacao') or '-'}")
        if prox_nome(p["nome_oficial"], p["fica"]["nome"]) < 0.999:
            print("               ^ o sobrevivente devera ter o NOME corrigido para este")

    if not gravar:
        print(f"\n--seco: nada foi gravado. Destino seria {api}")
        print("Para executar:  python fundir_seguros.py --gravar")
        return 0

    from db import conn  # noqa: PLC0415 — so quando ha conferencia a fazer

    ok = falhas = 0
    with httpx.Client(base_url=api, timeout=120.0) as http, conn() as c, c.cursor() as cur:
        for p in pares:
            fica, vai = p["fica"]["id"], p["vai"]["id"]
            r = http.post(f"/fhir/Patient/{fica}/$merge", params={"source": vai})
            if r.status_code not in (200, 201):
                falhas += 1
                print(f"   FALHA {vai} -> {fica}: {r.status_code} {r.text[:160]}")
                continue
            # Corrigir o nome do sobrevivente para o oficial da Receita, na mesma passada.
            # O `$merge` NAO faz isso — ele cuida de identifiers, link e do clinico. Sem este
            # passo a fusao cristaliza a grafia errada: em 4 dos 7 pares o sobrevivente e
            # justamente a ficha com o nome torto (`WLDIR WETSCHY`, `ROGERIO GUIMARAES MACHAD`).
            oficial = p["nome_oficial"]
            if prox_nome(oficial, p["fica"]["nome"]) < 0.999:
                lido = http.get(f"/fhir/Patient/{fica}")
                if lido.status_code == 200:
                    doc = lido.json()
                    nomes = doc.get("name") or []
                    alvo = next((n for n in nomes if n.get("use") == "official"), None)
                    if alvo is None:
                        alvo = {"use": "official"}
                        nomes.append(alvo)
                        doc["name"] = nomes
                    anterior = alvo.get("text")
                    alvo["text"] = oficial
                    versao = (doc.get("meta") or {}).get("versionId")
                    posto = http.put(
                        f"/fhir/Patient/{fica}", json=doc,
                        headers={"Content-Type": "application/fhir+json",
                                 **({"If-Match": f'W/"{versao}"'} if versao else {})})
                    if posto.status_code == 200:
                        print(f"   nome corrigido: {anterior!r} -> {oficial!r}")
                    else:
                        print(f"   AVISO: fusao ok mas o nome NAO foi corrigido "
                              f"({posto.status_code}) — corrigir a mao em {fica}")
                else:
                    print(f"   AVISO: nao consegui reler Patient/{fica} para corrigir o nome")

            sobrou = ainda_aponta(cur, vai)
            if sobrou:
                falhas += 1
                print(f"   ATENCAO {vai}: ainda ha linhas apontando para o absorvido -> {sobrou}")
                print("   PARANDO: repontamento incompleto deixa dado invisivel sem erro nenhum.")
                break
            ok += 1
            print(f"   ok  {vai} -> {fica}")

    print(f"\nfundidos: {ok}   falhas: {falhas}")
    return 1 if falhas else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
