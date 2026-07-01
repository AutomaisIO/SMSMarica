"""Consulta 'Agendados pela Regulação' (cons_marcados_reg) — SOMENTE LEITURA.

etapa=LISTAR_SOLICITACOES lista marcações da unidade filtrando por período:
  tp_periodo = sol (data solicitação) | aut (autorização) | exe (execução)

Uso:
    python consultar_marcados_reg.py exe 01/07/2026 31/07/2026 [CNES]

Reaproveita a sessão salva (SISREG é sessão única). Mostra só a estrutura.
"""

from __future__ import annotations

import os
import pathlib
import sys

from dotenv import load_dotenv
from bs4 import BeautifulSoup

from sisreg import SisregClient, SisregLoginError


def main(argv: list[str]) -> int:
    if len(argv) < 3 or argv[0] not in {"sol", "aut", "exe"}:
        print("uso: python consultar_marcados_reg.py <sol|aut|exe> DD/MM/AAAA DD/MM/AAAA [CNES]", file=sys.stderr)
        return 2
    tp, dt_ini, dt_fim = argv[0], argv[1], argv[2]

    load_dotenv()
    usuario = os.getenv("SISREG_USUARIO", "").strip()
    senha = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()
    cnes = argv[3] if len(argv) > 3 else "3132358"

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            estado = cli.conectar(usuario, senha)
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"sessão: {estado}")

        data = {
            "etapa": "LISTAR_SOLICITACOES",
            "cns_paciente": "",
            "unidade": cnes,
            "cod_procedimento": "",
            "ds_procedimento": "",
            "tp_periodo": tp,
            "dt_inicial": dt_ini,
            "dt_final": dt_fim,
            "ordenacao": "",
            "pagina": "",
            "co_solicitacao": "",
        }
        r = cli.post("/cgi-bin/cons_marcados_reg", data=data)
        print(f"POST cons_marcados_reg tp={tp} status={r.status_code} len={len(r.text)}")
        out = cap / f"marcados_{cnes}_{tp}_{dt_ini.replace('/', '')}_{dt_fim.replace('/', '')}.html"
        out.write_text(r.text, encoding="utf-8")
        print(f"salvo: {out}")

        soup = BeautifulSoup(r.text, "html.parser")
        txt = soup.get_text(" ", strip=True)
        if "não retornou nenhum" in txt or "nao retornou nenhum" in txt.lower():
            print("→ sem resultados nesse filtro.")
        # tabela de dados = a com mais colunas
        tables = soup.find_all("table")
        alvo = max(tables, key=lambda t: max((len(r.find_all(['td','th'])) for r in t.find_all('tr')), default=0), default=None)
        if alvo:
            rows = alvo.find_all("tr")
            hdr = [c.get_text(" ", strip=True) for c in rows[0].find_all(["td", "th"])] if rows else []
            print(f"tabela alvo: {len(rows)} linhas; colunas={hdr}")
            print(f"linhas de dados: {max(0, len(rows) - 1)} (conteúdo omitido — PII)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
