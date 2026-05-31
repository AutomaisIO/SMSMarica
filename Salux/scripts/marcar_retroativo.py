"""Marca um timestamp retroativo (N minutos atrás) — útil pra capturar telas
que o usuário abriu antes de você marcar.
"""
from __future__ import annotations

import pathlib
import sys

from conexao import executar_select

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_ARQUIVO = _RAIZ / "capturas" / "marcador.txt"


def main() -> int:
    minutos = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    rotulo = sys.argv[2] if len(sys.argv) > 2 else f"retro_{minutos}m"
    sql = (
        "SELECT TO_CHAR(SYSTIMESTAMP - INTERVAL '"
        + str(minutos)
        + "' MINUTE, 'YYYY-MM-DD\"T\"HH24:MI:SS.FF6 TZH:TZM') FROM dual"
    )
    linhas, _ = executar_select(sql)
    ts = linhas[0][0]
    _ARQUIVO.parent.mkdir(exist_ok=True)
    _ARQUIVO.write_text(f"{ts}\n{rotulo}\n", encoding="utf-8")
    print(f"marcador retroativo ({minutos} min) '{rotulo}' = {ts}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
