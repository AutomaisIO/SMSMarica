"""Marca um timestamp do banco em capturas/marcador.txt.

Idéia: você narra "vou abrir tela X", eu rodo `python marcar.py inicio`.
Depois você executa no Salux. Quando termina, rodo `python capturar.py <rotulo>`
que pega tudo de v$sql entre o marcador e agora.
"""
from __future__ import annotations

import pathlib
import sys

from conexao import executar_select

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_PASTA = _RAIZ / "capturas"
_ARQUIVO = _PASTA / "marcador.txt"


def main() -> int:
    _PASTA.mkdir(exist_ok=True)
    linhas, _ = executar_select(
        "SELECT TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD\"T\"HH24:MI:SS.FF6 TZH:TZM') FROM dual"
    )
    if not linhas:
        print("sem retorno do banco — erro")
        return 1
    ts = linhas[0][0]
    rotulo = sys.argv[1] if len(sys.argv) > 1 else "marcador"
    _ARQUIVO.write_text(f"{ts}\n{rotulo}\n", encoding="utf-8")
    print(f"marcador '{rotulo}' = {ts}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
