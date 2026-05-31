"""Guard read-only: rejeita qualquer SQL que não seja SELECT/WITH/EXPLAIN.

Aplica-se a TODO comando enviado ao Oracle PRODUCAO do Salux. Live HIS.
"""
from __future__ import annotations

import re

_PREFIXO_PERMITIDO = re.compile(
    r"""^\s*
        (?:--[^\n]*\n\s*|/\*[\s\S]*?\*/\s*)*   # comentários de linha/bloco no início
        (SELECT|WITH|EXPLAIN)\b
    """,
    re.IGNORECASE | re.VERBOSE,
)

_BLOQUEADOS = re.compile(
    r"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|REVOKE|"
    r"COMMIT|ROLLBACK|SAVEPOINT|LOCK|CALL|EXECUTE|BEGIN|DECLARE)\b",
    re.IGNORECASE,
)


class SqlNaoSeguro(Exception):
    pass


def garantir_leitura(sql: str) -> None:
    if not _PREFIXO_PERMITIDO.match(sql):
        raise SqlNaoSeguro(
            "SQL bloqueado: deve começar com SELECT, WITH ou EXPLAIN.\n"
            f"Recebido: {sql[:120]!r}"
        )
    if _BLOQUEADOS.search(sql):
        # Permite palavras-chave dentro de string literal? Sim, mas raro em SELECT —
        # nesse caso vai bloquear. Aceitar falso positivo é OK: peca pela segurança.
        raise SqlNaoSeguro(
            "SQL contém token de escrita (INSERT/UPDATE/DELETE/MERGE/DDL/DCL/etc). Bloqueado."
        )
