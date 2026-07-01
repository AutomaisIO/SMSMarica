"""Extrator completo de 'Agendados pela Regulação' (cons_marcados_reg).

Percorre TODAS as páginas (campo `pagina`, 0-based) para um período por data de
EXECUÇÃO (tp_periodo=exe por padrão) e grava um CSV em capturas/ (gitignored, tem
PII). Imprime só o agregado (total, unidades executantes, faixa de datas).

Uso:
    python extrair_marcados.py 01/07/2026 31/07/2026 [exe|aut|sol] [CNES]

Reaproveita a sessão salva (sessão única por operador).
"""

from __future__ import annotations

import csv
import os
import pathlib
import re
import sys
from collections import Counter

from dotenv import load_dotenv
from bs4 import BeautifulSoup

from sisreg import SisregClient, SisregLoginError

COLUNAS = [
    "codigo_solicitacao", "cns", "usuario", "endereco", "telefone", "procedimento",
    "profissional_executante", "unidade_executante", "data_execucao", "hora_execucao", "avisado",
]


def _tabela_dados(soup: BeautifulSoup):
    """Retorna a lista de linhas (cada uma lista de 11 células) da table_listagem."""
    for t in soup.find_all("table", class_="table_listagem"):
        linhas = []
        for r in t.find_all("tr"):
            cells = [c.get_text(" ", strip=True) for c in r.find_all(["td", "th"])]
            if len(cells) == 11:
                linhas.append(cells)
        # descarta a linha de cabeçalho (contém 'Código Solicitação')
        dados = [l for l in linhas if l and not l[0].lower().startswith("código") and "digo solicita" not in l[0].lower()]
        if dados:
            return dados
    return []


def _total_paginas(html: str) -> int:
    m = re.search(r"de\s+(\d+)\s*<A HREF", html)
    if m:
        return int(m.group(1))
    m = re.search(r"value='1'>\s*de\s+(\d+)", html)
    return int(m.group(1)) if m else 1


def _total_registros(html: str) -> int | None:
    m = re.search(r"PESQUISADAS\s*\((\d+)\)", html, re.IGNORECASE)
    return int(m.group(1)) if m else None


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("uso: python extrair_marcados.py DD/MM/AAAA DD/MM/AAAA [exe|aut|sol] [CNES]", file=sys.stderr)
        return 2
    dt_ini, dt_fim = argv[0], argv[1]
    tp = argv[2] if len(argv) > 2 else "exe"
    cnes = argv[3] if len(argv) > 3 else "3132358"

    load_dotenv()
    usuario = os.getenv("SISREG_USUARIO", "").strip()
    senha = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            print("sessão:", cli.conectar(usuario, senha))
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1

        def consulta(pagina: int):
            return cli.post("/cgi-bin/cons_marcados_reg", data={
                "etapa": "LISTAR_SOLICITACOES", "cns_paciente": "", "unidade": cnes,
                "cod_procedimento": "", "ds_procedimento": "", "tp_periodo": tp,
                "dt_inicial": dt_ini, "dt_final": dt_fim, "ordenacao": "",
                "pagina": str(pagina), "co_solicitacao": "",
            })

        r0 = consulta(0)
        total = _total_registros(r0.text)
        npag = _total_paginas(r0.text)
        print(f"total registros={total}  páginas={npag}  (tp={tp}, {dt_ini}..{dt_fim}, CNES {cnes})")

        todas: list[list[str]] = []
        vistos: set[str] = set()
        for p in range(npag):
            resp = r0 if p == 0 else consulta(p)
            linhas = _tabela_dados(BeautifulSoup(resp.text, "html.parser"))
            novas = 0
            for l in linhas:
                chave = l[0] or "|".join(l)
                if chave in vistos:
                    continue
                vistos.add(chave)
                todas.append(l)
                novas += 1
            print(f"  página {p + 1}/{npag}: {len(linhas)} linhas ({novas} novas)")

        out = cap / f"marcados_full_{cnes}_{tp}_{dt_ini.replace('/', '')}_{dt_fim.replace('/', '')}.csv"
        with out.open("w", newline="", encoding="utf-8") as f:
            w = csv.writer(f, delimiter=";")
            w.writerow(COLUNAS)
            w.writerows(todas)
        print(f"\nCSV salvo ({len(todas)} linhas): {out}")

        # agregado SEM PII
        uni = Counter(l[7] for l in todas if l[7])
        print(f"\nregistros coletados: {len(todas)}" + (f" (esperado {total})" if total else ""))
        print("unidades executantes distintas:", len(uni))
        for u, n in uni.most_common():
            print(f"  {n:4d}  {u}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
