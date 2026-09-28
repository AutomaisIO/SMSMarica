"""Le o resultado de `sondar_nome_mae.py` e responde: resolver por CPF muda o que? SOMENTE LEITURA.

A carga do Prime foi montada resolvendo paciente por **CNS**, porque o relatorio *Pacientes
Atendidos* nao tem CPF em coluna nenhuma. Sobraram 2.547 candidatos a criacao. Mas a **busca** de
paciente do Prime traz CPF — e o hub tem CPF em 91% dos pacientes, contra 61% com CNS.

Esta analise mede quantos daqueles 2.547 deixam de ser "paciente novo" quando resolvidos por CPF.
Cada um que casa e uma ficha duplicada que NAO sera criada.

Tambem confere a chave de origem: quantos vieram com `pacienteId` do Prime (`urn:prime:paciente`)
e quantos sao apenas eco do CadWeb (GUID zerado — ver `sondar_nome_mae.py`).

Uso:  python analisar_busca_prime.py
"""

from __future__ import annotations

import collections
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]
                       / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")
LOTE = 1000


def dv_cpf_ok(cpf: str) -> bool:
    """CPF com digito verificador valido. [ADR-0041]: `00000000000` passa em 'tem 11 digitos' e
    fundiria duas pessoas — a regua e o DV, nao o comprimento."""
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


def main() -> int:
    arq = SAIDA / "prime_busca.ndjson"
    if not arq.exists():
        raise SystemExit(f"falta {arq} — rode sondar_nome_mae.py primeiro")
    linhas = [json.loads(l) for l in arq.open(encoding="utf-8") if l.strip()]
    print(f"buscas lidas: {len(linhas):,}")

    c = collections.Counter()
    cpfs: dict[str, dict] = {}
    for r in linhas:
        cpf = (r.get("cpf_prime") or "").strip()
        if r.get("so_no_cadweb"):
            c["so no CadWeb (sem chave do Prime)"] += 1
        if r.get("paciente_id_prime"):
            c["com pacienteId do Prime"] += 1
        if not cpf:
            c["SEM CPF na grade"] += 1
        elif not dv_cpf_ok(cpf):
            c["CPF com DV invalido"] += 1
        else:
            c["com CPF valido"] += 1
            cpfs.setdefault(cpf, r)
    print()
    for k, v in c.most_common():
        print(f"   {k:<36} {v:>6,}  ({v/len(linhas)*100:>5.1f}%)")

    if not cpfs:
        print("\nnenhum CPF valido — nada a cruzar")
        return 0

    print(f"\ncruzando {len(cpfs):,} CPF contra fhir.patient (em lotes, indice btree)...")
    achados: dict[str, str] = {}
    lista = sorted(cpfs)
    with conn() as conexao, conexao.cursor() as cur:
        for i in range(0, len(lista), LOTE):
            parte = lista[i:i + LOTE]
            cur.execute("""select cpf, id::text from fhir.patient
                           where not is_deleted and cpf = any(%s::text[])""", (parte,))
            for cpf, pid in cur.fetchall():
                achados[cpf] = pid

    print(f"\nDOS {len(cpfs):,} COM CPF VALIDO:")
    print(f"   JA EXISTEM no hub por CPF:  {len(achados):>6,} "
          f"({len(achados)/len(cpfs)*100:.1f}%)  <- NAO criar, sao duplicata")
    print(f"   realmente novos:            {len(cpfs)-len(achados):>6,}")

    # Quantos desses ja eram pegos pelo nome+nascimento e quantos SO o CPF pegou
    pid_por_cpf = set(achados.values())
    print(f"   pacientes distintos do hub envolvidos: {len(pid_por_cpf):,}")

    fora = [r for cpf, r in cpfs.items() if cpf not in achados]
    print(f"\nos {len(fora):,} realmente novos, com o que entrariam:")
    com_id = sum(1 for r in fora if r.get("paciente_id_prime"))
    com_mae = sum(1 for r in fora if (r.get("mae_prime") or "").strip())
    print(f"   com CPF valido:            {len(fora):>6,}  (100%)")
    print(f"   com pacienteId do Prime:   {com_id:>6,}")
    print(f"   com nome da mae:           {com_mae:>6,}")

    (SAIDA / "resolucao_por_cpf.json").write_text(
        json.dumps({"por_cpf": achados,
                    "novos": [r["cpf_prime"] for r in fora]}, ensure_ascii=False),
        encoding="utf-8")
    print(f"\nsalvo em {SAIDA / 'resolucao_por_cpf.json'} — NADA foi gravado no hub.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
