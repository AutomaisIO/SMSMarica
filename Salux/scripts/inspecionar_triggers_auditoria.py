"""Mostra source code dos triggers AUDITORIA_* / RAST_* / *_RAST_TIU
para entender onde escrevem o audit log.
"""
from __future__ import annotations

import pathlib

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas" / "investig_triggers_audit.txt"


_TRIGGERS = [
    "AUDITORIA_PACIENTE_TU",
    "AUDITORIA_FIA_TU",
    "AUDITORIA_BAA_TU",
    "AUDITORIA_FIA_TI",
    "AUDITORIA_BAA_TI",
    "PACIENTE_RAST_TIU",
    "RAST_ASSIST_BAA_TIUD",
]


def main() -> int:
    _SAIDA.write_text("", encoding="utf-8")
    for trig in _TRIGGERS:
        try:
            linhas, _ = executar_select(
                "SELECT line || ' | ' || REPLACE(text, CHR(10), ' ') "
                "FROM dba_source WHERE owner='INFOSAUDE' AND name=:n AND type='TRIGGER' "
                "ORDER BY line",
                {"n": trig}, timeout=60,
            )
            with _SAIDA.open("a", encoding="utf-8") as f:
                f.write(f"\n# {trig}\n" + "-" * 80 + "\n")
                if not linhas:
                    f.write("(não encontrado)\n")
                for l in linhas:
                    f.write(l[0] + "\n")
            print(f"OK {trig}: {len(linhas)} linhas")
        except Exception as e:
            print(f"FAIL {trig}: {e}")
            with _SAIDA.open("a", encoding="utf-8") as f:
                f.write(f"\n# {trig}: FALHOU {e}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
