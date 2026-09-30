"""Sonda 1: Fila de Regulação (exame ou consulta) dos pacientes de Maricá no e-SUS SG.

Pagina a fila inteira da unidade do usuário (a "MUNICÍPIO DE MARICÁ"), confere que o lido
bate com o total declarado e imprime SÓ AGREGADOS (procedimento, prioridade, pendência,
antiguidade, cobertura de CPF/CNS/telefone). A lista nominal vai para `--saida` — que tem
nome, CPF, CNS e telefone, então fica em `capturas/` (gitignored) ou fora da árvore.
SOMENTE LEITURA.

Uso:  python probe_fila.py                   # fila de exame (padrão)
      python probe_fila.py --consulta
      python probe_fila.py --saida C:/fora/da/arvore/fila.json
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import json
import pathlib
import sys

from esus.client import CAP, paginar, sessao
from esus.telas import CAMINHO, form_fila

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def _data(s: str | None) -> dt.date | None:
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%Y-%m-%d %H:%M:%S"):
        try:
            return dt.datetime.strptime((s or "")[:19], fmt).date()
        except ValueError:
            continue
    return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--consulta", action="store_true", help="fila de consulta (padrão: exame)")
    ap.add_argument("--saida", type=pathlib.Path, help="JSON com as linhas (tem PII)")
    a = ap.parse_args()

    tipo = "consulta" if a.consulta else "exame"
    with sessao() as s:
        linhas, total = paginar(s, CAMINHO[f"fila_{tipo}"], form_fila(s.unidade, exame=not a.consulta))

    print(f"fila de {tipo} — unidade {s.unidade}: {total} pacientes (lido = declarado)")
    if not linhas:
        return 0
    print("colunas:", ", ".join(sorted(linhas[0])))

    proc_chave = "fle_nome_procedimento" if not a.consulta else next(
        (k for k in ("ocp_nome", "fle_nome_procedimento", "nome_ocupacao") if k in linhas[0]), None)
    for rot, chave in (("procedimento", proc_chave), ("prioridade", "pfi_nome"),
                       ("pendência", "pendencia"), ("município", "mun_nome"),
                       ("unidade da fila", "unidade_fila")):
        if not chave or chave not in linhas[0]:
            continue
        c = collections.Counter((ln.get(chave) or "(vazio)") for ln in linhas)
        print(f"\npor {rot} ({len(c)} valores):")
        for v, n in c.most_common(15):
            print(f"  {n:5d}  {v}")

    hoje = dt.date.today()
    entradas = [d for d in (_data(ln.get("fil_data")) for ln in linhas) if d]
    if entradas:
        dias = sorted((hoje - d).days for d in entradas)
        print(f"\nentrada na fila: mais antiga {min(entradas)}, mais nova {max(entradas)}; "
              f"mediana de espera {dias[len(dias) // 2]} dias")
        faixas = collections.Counter(
            "≤30d" if x <= 30 else "31-90d" if x <= 90 else "91-180d" if x <= 180 else "181-365d" if x <= 365 else ">1 ano"
            for x in dias)
        print("  " + "  ".join(f"{k}: {faixas[k]}" for k in ("≤30d", "31-90d", "91-180d", "181-365d", ">1 ano")))

    def cobre(*ks: str) -> int:
        return sum(1 for ln in linhas if any((ln.get(k) or "").strip() for k in ks))
    n = len(linhas)
    print(f"\ncobertura: CPF {cobre('pep_cpf_numero', 'pae_cpf')}/{n}  "
          f"CNS {cobre('pep_cartaosus', 'pae_cartao_sus')}/{n}  telefone {cobre('telefone', 'nop_celular')}/{n}")

    saida = a.saida or CAP / f"fila_{tipo}_{hoje:%Y%m%d}.json"
    saida.parent.mkdir(parents=True, exist_ok=True)
    saida.write_text(json.dumps(linhas, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nlinhas (COM PII) -> {saida}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
