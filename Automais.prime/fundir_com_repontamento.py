"""Funde paciente repontando AS DUAS METADES: `smsmarica.*` por SQL + `fhir.*` pelo `$merge`.

**Nao grava nada sem `--gravar`.**

### O problema que este script existe para resolver
O `$merge` do hub alcanca so `fhir.*` (6 tabelas, 820 linhas no total dos 1.195 grupos). As outras
**23 colunas** com id de paciente vivem em `smsmarica.*` — solicitacao, laudo, tratamento,
conversa, whatsapp_mensagem… — e somam **8.112 linhas, 91% do volume**. Nenhuma delas tem FK.

Fundir so uma metade e **pior que nao fundir**. Exemplo real medido em 25/09/2026: um par de fichas
da mesma pessoa (CNS 704609119899728) em que o absorvido carrega **111 solicitacoes do SISREG** e
zero recursos FHIR. Chamar so o `$merge` ali deixaria as 111 apontando para uma lapide: some do
historico de quem ficou, e o banco nao reclama porque nao ha integridade referencial para reclamar.
Antes da fusao o historico esta partido em duas fichas, mas VISIVEL nas duas; depois da fusao pela
metade, metade fica invisivel.

### A ordem, que e a decisao central
**Reponta `smsmarica.*` PRIMEIRO, chama o `$merge` DEPOIS.** Nao e capricho:

- merge primeiro + repontamento falhando = dado invisivel numa lapide (o caso ruim);
- repontamento primeiro + merge falhando = os registros ja estao na ficha que vai sobreviver e a
  duplicata continua ali. Feio, mas **nada some**, e reexecutar conserta.

Entre duas falhas possiveis, escolhe-se a que nao esconde dado.

### Conferencia
Depois de cada par, conta quantas linhas ainda apontam para o absorvido nas **29** colunas
conhecidas. Tem de dar zero — e se nao der, o script PARA. Sem FK, essa contagem e o unico jeito
de perceber que faltou coluna.

### Pre-requisito
So entra par com veredito da Receita (`conferir_cpf_receita.py`). O caso dos gemeos
(`WANDERLEY DA SILVA` x `LAVINIA DOS SANTOS PIMENTEL`, mesmo CPF, mesma mae, mesma data de
nascimento) passou por quatro criterios heuristicos e so a Receita o barrou.

Uso:
  python fundir_com_repontamento.py                          # seco
  python fundir_com_repontamento.py --gravar --api http://127.0.0.1:15081
  python fundir_com_repontamento.py --limite 5 --gravar ...  # lote pequeno primeiro
"""

from __future__ import annotations

import collections
import csv
import difflib
import json
import pathlib
import sys
import unicodedata

import httpx

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]
                       / "Aprendizados e Scratchpads" / "ferramentas"))
from prime.referencias_paciente import REFERENCIAS  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")
# As de fhir.* saem da lista de repontamento por SQL: quem cuida delas e o `$merge`, que alem da
# coluna reescreve o `subject.reference` do documento (repontar so a coluna deixa coluna e
# documento discordando — ver APRENDIZADOS §35).
COLUNAS_SMSMARICA = [(s, t, c) for (s, t, c) in REFERENCIAS if s == "smsmarica"]


def norm(s: str | None) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                   if unicodedata.category(c) != "Mn").strip()


def prox(a: str, b: str) -> float:
    x, y = norm(a), norm(b)
    if not x or not y:
        return 0.0
    if x.startswith(y) or y.startswith(x):
        return 1.0
    return difflib.SequenceMatcher(None, x, y).ratio()


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
    arq = SAIDA / "conferencia_receita.json"
    if not arq.exists():
        return {}
    return {r["cpf"]: r for r in json.loads(arq.read_text(encoding="utf-8")) if r.get("receita")}


def pares(argv: list[str]) -> list[dict]:
    """Pares elegiveis: CPF com DV valido, mae nao divergente, par simples, nomes parecidos e
    veredito da Receita favoravel. O teto de linhas do absorvido e configuravel (`--max-linhas`)."""
    teto = int(argv[argv.index("--max-linhas") + 1]) if "--max-linhas" in argv else 10
    receita = veredito_receita()
    g: dict[tuple, list[dict]] = collections.defaultdict(list)
    with (SAIDA / "duplicatas.csv").open(encoding="utf-8-sig") as f:
        for l in csv.DictReader(f):
            g[(l["tier"], l["chave"])].append(l)

    saida, sem_receita = [], 0
    for (tier, chave), ms in g.items():
        if tier != "T1-CPF" or not dv_cpf_ok(chave) or len(ms) != 2:
            continue
        if any(m["maes_divergem"] == "True" for m in ms):
            continue
        absorv = [m for m in ms if m["sobrevivente"] != "True"]
        if len(absorv) != 1 or int(absorv[0]["referencias"]) > teto:
            continue
        fica = next(m for m in ms if m["sobrevivente"] == "True")
        if prox(fica["nome"], absorv[0]["nome"]) < 0.85:
            continue  # nomes diferentes: gemeo ou CPF trocado — so com pessoa olhando
        r = receita.get(chave)
        if not r:
            sem_receita += 1
            continue
        if "NAO FUNDIR" in (r.get("veredito") or ""):
            continue
        if max(prox(r["receita"], m["nome"]) for m in ms) < 0.85:
            continue
        saida.append({"cpf": chave, "fica": fica, "vai": absorv[0],
                      "nome_oficial": r["receita"], "situacao": r.get("situacao")})
    if sem_receita:
        print(f"({sem_receita} pares elegiveis SEM conferencia da Receita — ficaram de fora; "
              f"rode conferir_cpf_receita.py para libera-los)")
    return saida


def repontar(cur, de: str, para: str) -> dict[str, int]:
    """UPDATE em cada coluna de smsmarica.* que guarda id de paciente. O chamador controla a
    transacao — aqui nao se faz commit."""
    movidas: dict[str, int] = {}
    for sch, tab, col in COLUNAS_SMSMARICA:
        try:
            cur.execute(f'UPDATE {sch}."{tab}" SET {col} = %s WHERE {col}::text = %s', (para, de))
        except Exception as e:  # noqa: BLE001 — tabela pode nao existir nesta instancia
            raise RuntimeError(f"falha repontando {sch}.{tab}.{col}: {e}") from e
        if cur.rowcount:
            movidas[f"{tab}.{col}"] = cur.rowcount
    return movidas


def ainda_aponta(cur, pid: str) -> dict[str, int]:
    """Linhas que AINDA apontam para o absorvido, nas 29 colunas. Tem de vir vazio."""
    sobrou: dict[str, int] = {}
    for sch, tab, col in REFERENCIAS:
        try:
            cur.execute(f'SELECT count(*) FROM {sch}."{tab}" WHERE {col}::text = %s', (pid,))
        except Exception:  # noqa: BLE001
            continue
        n = cur.fetchone()[0]
        if n:
            sobrou[f"{sch}.{tab}.{col}"] = n
    return sobrou


def corrigir_nome(http: httpx.Client, pid: str, oficial: str) -> str | None:
    """Poe no sobrevivente o nome oficial da Receita. Devolve o nome anterior, ou None se nada
    mudou. O `$merge` nao faz isso — sem este passo a fusao cristaliza a grafia errada."""
    lido = http.get(f"/fhir/Patient/{pid}")
    if lido.status_code != 200:
        return None
    doc = lido.json()
    nomes = doc.get("name") or []
    alvo = next((n for n in nomes if n.get("use") == "official"), None)
    if alvo is None:
        alvo = {"use": "official"}
        nomes.append(alvo)
        doc["name"] = nomes
    anterior = alvo.get("text")
    if prox(anterior or "", oficial) >= 0.999:
        return None
    alvo["text"] = oficial
    versao = (doc.get("meta") or {}).get("versionId")
    posto = http.put(f"/fhir/Patient/{pid}", json=doc,
                     headers={"Content-Type": "application/fhir+json",
                              **({"If-Match": f'W/"{versao}"'} if versao else {})})
    return anterior if posto.status_code == 200 else None


def main(argv: list[str]) -> int:
    api = argv[argv.index("--api") + 1] if "--api" in argv else "http://127.0.0.1:15081"
    gravar = "--gravar" in argv
    limite = int(argv[argv.index("--limite") + 1]) if "--limite" in argv else None

    lista = pares(argv)
    if gravar and lista:
        # Descarta os ja fundidos ANTES de aplicar o --limite. Sem isso, "--limite 50" podia
        # gastar o lote inteiro pulando pares que rodadas anteriores resolveram — foi o que
        # aconteceu: um lote de 50 fundiu 0 porque os 50 primeiros do CSV ja estavam feitos.
        from db import conn as _conn  # noqa: PLC0415
        with _conn() as _c, _c.cursor() as _cur:
            _cur.execute("""select id::text from fhir.patient
                            where content->'link'->0->>'type' = 'replaced-by'""")
            fundidos = {r[0] for r in _cur.fetchall()}
        antes = len(lista)
        lista = [x for x in lista if x["vai"]["id"] not in fundidos]
        if antes != len(lista):
            print(f"({antes - len(lista)} pares do CSV ja estavam fundidos — descartados)")
    if limite:
        lista = lista[:limite]
    print(f"pares elegiveis: {len(lista)}\n")
    for p in lista:
        print(f"   CPF {p['cpf']}  {p['fica']['nome'][:30]:<32} <- {p['vai']['nome'][:30]:<32} "
              f"({p['vai']['referencias']} linhas a mover)")

    if not gravar:
        print(f"\n--seco: nada foi gravado. Destino seria {api}")
        return 0

    from db import conn  # noqa: PLC0415

    ok = falhas = ja = 0
    with httpx.Client(base_url=api, timeout=120.0) as http, conn() as c:
        c.autocommit = False
        for p in lista:
            fica, vai = p["fica"]["id"], p["vai"]["id"]
            try:
                # Par ja fundido? O `duplicatas.csv` e um retrato: se foi gerado antes de um lote
                # anterior, ainda lista pares que ja se resolveram. Sem esta checagem o script
                # reponta (0 linhas, inofensivo) e depois colhe 400 do $merge — aconteceu com 5
                # pares num lote de 50. Pular e mais honesto que "falhar com seguranca".
                with c.cursor() as cur:
                    cur.execute("""select content->'link'->0->>'type' from fhir.patient
                                   where id::text = %s""", (vai,))
                    linha = cur.fetchone()
                if linha and linha[0] == "replaced-by":
                    ja += 1
                    continue
                # 1. smsmarica.* PRIMEIRO, numa transacao propria. Se o merge falhar depois, os
                #    registros ja estao na ficha que sobrevive — nada fica invisivel.
                with c.cursor() as cur:
                    movidas = repontar(cur, vai, fica)
                c.commit()

                # 2. fhir.* + identifiers + link, pelo endpoint do hub
                r = http.post(f"/fhir/Patient/{fica}/$merge", params={"source": vai})
                if r.status_code not in (200, 201):
                    falhas += 1
                    print(f"   FALHA no $merge {vai[:8]}: {r.status_code} {r.text[:140]}")
                    print(f"      (o repontamento de smsmarica.* JA foi aplicado: {movidas or 'nada'} "
                          f"— reexecutar este par conserta)")
                    continue

                anterior = corrigir_nome(http, fica, p["nome_oficial"])

                # 3. conferencia: nada pode continuar apontando para o absorvido
                with c.cursor() as cur:
                    sobrou = ainda_aponta(cur, vai)
                if sobrou:
                    falhas += 1
                    print(f"   PARANDO — {vai[:8]} ainda tem linhas apontando para ele: {sobrou}")
                    break

                ok += 1
                extra = f"  nome: {anterior!r} -> {p['nome_oficial']!r}" if anterior else ""
                print(f"   ok  {vai[:8]} -> {fica[:8]}  movidas={movidas or '{}'}{extra}")
            except Exception as e:  # noqa: BLE001
                c.rollback()
                falhas += 1
                print(f"   ERRO {vai[:8]}: {type(e).__name__}: {str(e)[:140]}")
                break

    print(f"\nfundidos: {ok}   falhas: {falhas}"
          + (f"   ja fundidos antes (pulados): {ja}" if ja else ""))
    return 1 if falhas else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
