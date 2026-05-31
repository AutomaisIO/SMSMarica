"""Extrai strings ASCII de arquivos PowerBuilder (.pbd/.exe) procurando SQL.

Estratégia:
1. Para cada PBD/EXE no caminho, lê bytes
2. Extrai sequências de bytes printáveis (ASCII 32-126) com tamanho >= MIN_LEN
3. Filtra strings que parecem SQL (começam com SELECT, INSERT, UPDATE, DELETE, MERGE — case insens)
4. Salva em capturas/sql_pbd_<modulo>.txt
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas"

_PATTERN_PRINTABLE = re.compile(rb"[ -~\t\r\n]{20,}")
_SQL_KEYWORDS = re.compile(
    r"^\s*(SELECT|INSERT\s+INTO|UPDATE|DELETE\s+FROM|MERGE\s+INTO|WITH\s+\w)",
    re.IGNORECASE,
)


def extrair_strings(arquivo: pathlib.Path, min_len: int = 30) -> list[str]:
    """Extrai sequências de texto >= min_len, tentando ASCII e UTF-16 LE.

    PowerBuilder armazena strings em UTF-16 LE (cada char ASCII seguido de \\x00).
    """
    try:
        with arquivo.open("rb") as f:
            blob = f.read()
    except Exception:
        return []

    out: list[str] = []

    # ASCII puro (inclui \n e \r pra capturar queries multilinha)
    for m in re.finditer(rb"[ -~\t\r\n]{%d,}" % min_len, blob):
        try:
            s = m.group(0).decode("latin-1", errors="replace")
            out.append(s)
        except Exception:
            pass

    # UTF-16 LE: sequência de pares (char-printable, \x00)
    pattern_utf16 = rb"(?:[ -~\t\r\n][\x00]){%d,}" % min_len
    for m in re.finditer(pattern_utf16, blob):
        try:
            s = m.group(0).decode("utf-16-le", errors="replace").rstrip("\x00")
            if s:
                out.append(s)
        except Exception:
            pass

    return out


def filtrar_sql(strings: list[str]) -> list[str]:
    return [s for s in strings if _SQL_KEYWORDS.match(s)]


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--modulo", default=None,
                   help="Nome do módulo em C:\\Salux\\Modulos (ex: Ambulatorio.SUS); se ausente, processa todos")
    p.add_argument("--min-len", type=int, default=30)
    p.add_argument("--so-sql", action="store_true", default=False,
                   help="Filtra só strings que parecem SQL")
    args = p.parse_args()

    base = pathlib.Path(r"C:\Salux\Modulos")
    if not base.exists():
        print(f"{base} não existe"); return 1

    modulos = [base / args.modulo] if args.modulo else [d for d in base.iterdir() if d.is_dir()]
    _SAIDA.mkdir(exist_ok=True)

    for mod in modulos:
        if not mod.is_dir():
            continue
        sufixo = "_sql" if args.so_sql else "_strings"
        arquivo_saida = _SAIDA / f"pbd_{mod.name}{sufixo}.txt"
        total_strings = 0
        total_sql = 0
        with arquivo_saida.open("w", encoding="utf-8") as out:
            for binario in mod.glob("*.pbd"):
                strings = extrair_strings(binario, args.min_len)
                if args.so_sql:
                    strings = filtrar_sql(strings)
                if not strings:
                    continue
                out.write(f"\n========== {binario.name} ({len(strings)} strings) ==========\n")
                for s in strings:
                    out.write(s.replace("\r", "").rstrip() + "\n---\n")
                total_strings += len(strings)
                total_sql += len(strings) if args.so_sql else 0
            for binario in mod.glob("*.exe"):
                strings = extrair_strings(binario, args.min_len)
                if args.so_sql:
                    strings = filtrar_sql(strings)
                if not strings:
                    continue
                out.write(f"\n========== {binario.name} ({len(strings)} strings) ==========\n")
                for s in strings:
                    out.write(s.replace("\r", "").rstrip() + "\n---\n")
                total_strings += len(strings)
                total_sql += len(strings) if args.so_sql else 0
        print(f"{mod.name:30s}  {total_strings:>6} strings  -> {arquivo_saida.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
