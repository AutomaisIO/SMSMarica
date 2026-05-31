"""Grava uma marca narrativa em capturas/historico.jsonl.

Uso:
    python scripts/marca.py "abri lista de pacientes do setor BA"
    python scripts/marca.py "cliquei em Osvaldo Pereira"
    python scripts/marca.py "abriu histórico — vejo BA + emergência + FIA"

Cada marca tem timestamp do banco e timestamp local — pra correlacionar com
as queries que o monitor grava entre marcas.
"""
from __future__ import annotations

import json
import pathlib
import sys
from datetime import datetime, timezone

from conexao import executar_select

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_HIST = _RAIZ / "capturas" / "historico.jsonl"


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/marca.py \"<texto narrativo>\"")
        return 2
    texto = " ".join(sys.argv[1:])
    linhas, _ = executar_select(
        "SELECT TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD\"T\"HH24:MI:SS.FF6 TZH:TZM') FROM dual"
    )
    ts_banco = linhas[0][0] if linhas else "?"
    entrada = {
        "tipo": "marca",
        "ts_local": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "ts_banco": ts_banco,
        "texto": texto,
    }
    _HIST.parent.mkdir(exist_ok=True)
    with _HIST.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entrada, ensure_ascii=False) + "\n")
    print(f"marca: {texto}")
    print(f"ts_banco: {ts_banco}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
