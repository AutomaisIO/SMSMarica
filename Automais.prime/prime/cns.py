"""Validacao de CNS (Cartao Nacional de Saude) pelo digito verificador.

Por que isto existe: "tem CNS" nao e o mesmo que "tem CNS confiavel". A auditoria do
[ADR-0041] ja mostrou o mesmo com CPF - `00000000000` passa como "11 digitos" e **funde duas
pessoas** num Patient so. CNS tem o mesmo buraco: um campo obrigatorio preenchido a esmo passa
no teste de formato e falha no digito.

Duas familias, com algoritmos DIFERENTES:
- **Definitivo** (comeca com 1 ou 2): os 11 primeiros digitos sao o PIS/PASEP; os 4 ultimos sao
  o complemento `000`/`001` + DV calculado por peso decrescente de 15 a 5.
- **Provisorio** (comeca com 7, 8 ou 9): nao ha PIS por tras; a validacao e so a soma ponderada
  dos 15 digitos ser multipla de 11.

**O provisorio e valido mas NAO e identidade nacional estavel** - e emitido na hora do
atendimento e a mesma pessoa pode ter varios. Quem quiser "100% de certeza" para ancorar paciente
deve exigir `definitivo()`, nao so `valido()`.
"""

from __future__ import annotations

import re


def _limpar(cns: str | None) -> str:
    return re.sub(r"\D", "", cns or "")


def valido(cns: str | None) -> bool:
    """DV confere (aceita definitivo E provisorio)."""
    n = _limpar(cns)
    if len(n) != 15 or n == n[0] * 15:
        return False
    if n[0] in "12":
        pis = n[:11]
        soma = sum(int(pis[i]) * (15 - i) for i in range(11))
        resto = soma % 11
        dv = 11 - resto
        if dv == 11:
            dv = 0
        if dv == 10:
            soma += 2
            dv = 11 - (soma % 11)
            return n == pis + "001" + str(dv)
        return n == pis + "000" + str(dv)
    if n[0] in "789":
        return sum(int(n[i]) * (15 - i) for i in range(15)) % 11 == 0
    return False


def definitivo(cns: str | None) -> bool:
    """CNS definitivo (serie 1/2) com DV valido - o unico que serve de chave nacional estavel."""
    n = _limpar(cns)
    return len(n) == 15 and n[0] in "12" and valido(n)


def classificar(cns: str | None) -> str:
    """'definitivo' | 'provisorio' | 'invalido' | 'ausente' - para contar e para marcar no hub."""
    n = _limpar(cns)
    if not n:
        return "ausente"
    if not valido(n):
        return "invalido"
    return "definitivo" if n[0] in "12" else "provisorio"
