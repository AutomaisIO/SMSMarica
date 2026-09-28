"""Identifica pacientes duplicados no hub e PROPOE o sobrevivente de cada grupo. SOMENTE LEITURA.

Nao funde nada. Produz a lista com a evidencia de cada grupo para revisao humana — fusao de
paciente e irreversivel na pratica (junta prontuario de duas pessoas) e nao se faz no escuro.

**Por que isto e perigoso e precisa de cuidado especial:** `fhir.patient` NAO tem nenhuma FK
apontando para ela — sao **33 colunas** em `fhir.*` e `smsmarica.*` que guardam id de paciente
sem integridade referencial. Uma fusao que esqueca uma coluna deixa dado orfao e **o banco nao
reclama**. Entre essas colunas estao `smsmarica.laudo`, `solicitacao`, `tratamento` e `conversa`.

### Camadas de evidencia (da mais forte para a mais fraca)
- **T1 — mesmo CPF (DV valido)**: chave nacional com digito verificador. E a evidencia mais forte
  que existe aqui. Medido em 24/09/2026: 510 grupos, 1.032 pacientes.
- **T2 — mesmo CNS**: forte, mas 99,3% dos CNS do hub sao PROVISORIOS (serie 7/8/9), que a mesma
  pessoa pode ter varios. Confirmar com nome da mae antes de fundir. 686 grupos, 1.447 pacientes.
- **T3 — mesmo nome+nascimento com nome de mae parecido**: a camada invisivel — mesma pessoa com
  chaves DIFERENTES, que por definicao nao aparece em T1 nem T2. Foi assim que 105 "homonimos" do
  Prime se revelaram duplicata nossa (pares Santa Rita + UPA24h). **Sugere, nunca decide.**

### Escolha do sobrevivente
Pontua identidade (CPF vale mais que CNS, que vale mais que mae/endereco) e desempata por volume
de dado clinico apontando para o registro. **O sobrevivente PROPOSTO nao e decisao** — vai na
planilha para uma pessoa confirmar.

Uso:
  python identificar_duplicatas.py              # T1 + T2
  python identificar_duplicatas.py --t3         # inclui a camada por nome+nascimento+mae (lenta)
"""

from __future__ import annotations

import collections
import csv
import difflib
import json
import pathlib
import sys
import unicodedata

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]
                       / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")

from prime.referencias_paciente import REFERENCIAS  # noqa: E402  (fonte unica da lista)


def norm(s: str | None) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                   if unicodedata.category(c) != "Mn").strip()


def dv_cpf_ok(cpf: str) -> bool:
    if len(cpf) != 11 or cpf == cpf[0] * 11:
        return False
    for corte in (9, 10):
        soma = sum(int(cpf[i]) * (corte + 1 - i) for i in range(corte))
        d = (soma * 10) % 11
        if d == 10:
            d = 0
        if d != int(cpf[corte]):
            return False
    return True


def maes_divergem(maes: list[str]) -> bool:
    """As maes sao de PESSOAS DIFERENTES — nao apenas escritas diferente.

    Comparar por igualdade exata errava nos DOIS sentidos, medido em 24/09/2026:
    - **falso positivo**: `LAURA PRAGANA WETSCHY` x `LAURA PRAGANA WESTCHKY` e a mesma mae com o
      mesmo typo de sobrenome do paciente — barrava par legitimo. Eram **52 dos 95** grupos
      marcados como divergentes;
    - **falso negativo**: gemeos tem a MESMA mae, entao "mae bate" aprovava o par mais perigoso.
      Quem separa gemeo de duplicata e o NOME do paciente, nao o da mae.
    """
    presentes = [m for m in maes if m]
    if len(presentes) < 2:
        return False  # sem duas maes nao ha divergencia AFIRMAVEL
    base = presentes[0]
    for outra in presentes[1:]:
        if base.startswith(outra) or outra.startswith(base):
            continue
        if difflib.SequenceMatcher(None, base, outra).ratio() < 0.85:
            return True
    return False


def mae_de(content: dict) -> str:
    for ct in (content.get("contact") or []):
        for r in (ct.get("relationship") or []):
            if "MAE" in norm(r.get("text")) or "MTH" in {c.get("code") for c in (r.get("coding") or [])}:
                return norm((ct.get("name") or {}).get("text"))
    return ""


def pontuar(p: dict, refs: int) -> int:
    """Quanto este registro merece SOBREVIVER. Identidade pesa mais que volume: um registro com
    CPF e a ancora nacional; um sem CPF mas com muitos atendimentos e so um registro movimentado."""
    s = 0
    if p["cpf"] and dv_cpf_ok(p["cpf"]):
        s += 100
    if p["cns"]:
        s += 40
    if p["mae"]:
        s += 15
    if (p["content"].get("address") or []):
        s += 10
    if (p["content"].get("telecom") or []):
        s += 10
    if p["tem_chave_origem"]:
        s += 20
    return s + min(refs, 50)  # volume desempata, mas nao decide


def grupos_por_chave(cur, campo: str) -> dict[str, list[dict]]:
    cur.execute(f"""
        select {campo}, id::text, cpf, cns, nome, nascimento::text, meta_source, content
        from fhir.patient
        where not is_deleted and coalesce({campo},'') <> ''
          and {campo} in (select {campo} from fhir.patient
                          where not is_deleted and coalesce({campo},'') <> ''
                          group by {campo} having count(*) > 1)
        order by {campo}""")
    g: dict[str, list[dict]] = collections.defaultdict(list)
    for chave, pid, cpf, cns, nome, nasc, source, content in cur.fetchall():
        ids = [i.get("system") for i in (content.get("identifier") or [])]
        g[chave].append({
            "id": pid, "cpf": cpf, "cns": cns, "nome": nome, "nascimento": nasc,
            "fonte": (source or "").split("/source/")[-1], "content": content,
            "mae": mae_de(content),
            "tem_chave_origem": any(s and "fhir.saude.gov.br" not in s for s in ids),
        })
    return g


def contar_referencias(cur, ids: list[str]) -> dict[str, int]:
    """Quantas linhas apontam para cada paciente, somando as 33 colunas."""
    total: dict[str, int] = collections.Counter()
    for sch, tab, col in REFERENCIAS:
        try:
            cur.execute(f'select {col}::text, count(*) from {sch}."{tab}" '
                        f'where {col}::text = any(%s::text[]) group by 1', (ids,))
        except Exception:  # noqa: BLE001 — tabela pode nao existir nesta instancia
            continue
        for pid, n in cur.fetchall():
            total[pid] += n
    return total


def main(argv: list[str]) -> int:
    SAIDA.mkdir(parents=True, exist_ok=True)
    linhas = []
    with conn() as c, c.cursor() as cur:
        print("T1: agrupando por CPF...")
        g_cpf = grupos_por_chave(cur, "cpf")
        print(f"   {len(g_cpf):,} grupos")
        print("T2: agrupando por CNS...")
        g_cns = grupos_por_chave(cur, "cns")
        print(f"   {len(g_cns):,} grupos")

        todos_ids = sorted({p["id"] for g in (g_cpf, g_cns) for m in g.values() for p in m})
        print(f"contando referencias de {len(todos_ids):,} pacientes em {len(REFERENCIAS)} colunas...")
        refs = contar_referencias(cur, todos_ids)

    vistos: set[str] = set()
    for tier, grupos in (("T1-CPF", g_cpf), ("T2-CNS", g_cns)):
        for chave, membros in grupos.items():
            assinatura = tuple(sorted(p["id"] for p in membros))
            if assinatura in vistos:
                continue  # o mesmo grupo pode cair em T1 e T2
            vistos.add(assinatura)
            for p in membros:
                p["_refs"] = refs.get(p["id"], 0)
                p["_pontos"] = pontuar(p, p["_refs"])
            vencedor = max(membros, key=lambda p: (p["_pontos"], p["_refs"]))
            divergem = maes_divergem([p["mae"] for p in membros])
            for p in membros:
                linhas.append({
                    "tier": tier, "chave": chave, "grupo": assinatura[0][:8],
                    "id": p["id"], "sobrevivente": p["id"] == vencedor["id"],
                    "nome": p["nome"], "nascimento": p["nascimento"],
                    "cpf": p["cpf"] or "", "cns": p["cns"] or "", "mae": p["mae"],
                    "fonte": p["fonte"], "referencias": p["_refs"], "pontos": p["_pontos"],
                    "maes_divergem": divergem,
                })

    destino = SAIDA / "duplicatas.csv"
    with destino.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(linhas[0].keys()))
        w.writeheader()
        w.writerows(linhas)

    grupos_n = len(vistos)
    divergem = len({l["grupo"] for l in linhas if l["maes_divergem"]})
    a_absorver = sum(1 for l in linhas if not l["sobrevivente"])
    refs_mover = sum(l["referencias"] for l in linhas if not l["sobrevivente"])
    print(f"\ngrupos: {grupos_n:,}   registros: {len(linhas):,}")
    print(f"   sobreviventes propostos:       {grupos_n:,}")
    print(f"   seriam absorvidos:             {a_absorver:,}")
    print(f"   linhas a repontar na fusao:    {refs_mover:,}")
    print(f"   grupos com MAE divergente:     {divergem:,}  <- revisar um a um, podem ser pessoas diferentes")
    print(f"\n{destino} — NADA foi alterado no hub.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
