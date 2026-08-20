"""Dá para ESPELHAR a relação de CID na nossa base? Quantas listas distintas existem?

`probe_cid.py` provou que a lista de CID é do RECURSO. Falta o que decide se dá para copiá-la
como se copia o catálogo de recursos e campos:

  1. **Quantas listas distintas existem?** Se 200 recursos produzem poucas listas — como os 203
     recursos produzem 21 formulários — a cópia é barata: varre-se uma vez por lista.
  2. **Qual o tamanho de uma lista?** Varre os 260 prefixos `letra+dígito` (A0…Z9), que é como se
     enumera o CID inteiro sem esbarrar no teto de 500 por busca.

SOMENTE LEITURA: só o fetch de sugestões. O `onselect` não é acionado.

Uso:  python probe_cid_catalogo.py [--completa N]   (N = quantos recursos varrer por inteiro)
"""

from __future__ import annotations

import argparse
import pathlib
import string
import sys
import time

import httpx

RAIZ = pathlib.Path(__file__).parent
sys.path.insert(0, str(RAIZ))

from probe_editar_solicitacao import BASE, UA, Ser, viewstate  # noqa: E402
from probe_criar_solicitacao import (  # noqa: E402
    CAMPO_CID, CAMPO_RECURSO, CAMPO_SISREG, CAMPO_TIPO,
    Criar, combo, linhas_de_sugestao, suggestion_box,
)

# Termos de sondagem: baratos e bem espalhados pelo CID-10. Dois recursos com a mesma contagem
# nos cinco muito provavelmente compartilham a mesma lista.
SONDAGEM = ["diab", "malig", "M54", "Z9", "A0"]

PREFIXOS = [f"{letra}{digito}" for letra in string.ascii_uppercase for digito in "0123456789"]


def sugerir(t: Criar, box_id: str, termo: str) -> list[str]:
    html = t.ser.postar(t.act, t._hidden_atual() | {
        "AJAXREQUEST": "_viewRoot",
        "inputvalue": termo,
        box_id: box_id,
        "ajaxSingle": box_id,
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": t.vs,
    })
    if (vs := viewstate(html)):
        t.vs = vs
    return [c[1].strip() for c in linhas_de_sugestao(html, box_id)
            if len(c) >= 3 and c[1].strip()]


def assinatura(t: Criar, box_id: str) -> tuple[int, ...]:
    return tuple(len(sugerir(t, box_id, termo)) for termo in SONDAGEM)


def varrer_tudo(t: Criar, box_id: str, rotulo: str) -> set[str]:
    """A lista inteira daquele recurso, por prefixo de dois caracteres."""
    todos: set[str] = set()
    cheios = []
    inicio = time.monotonic()
    for i, prefixo in enumerate(PREFIXOS, 1):
        achados = sugerir(t, box_id, prefixo)
        todos.update(achados)
        if len(achados) >= 500:
            cheios.append(prefixo)
        if i % 40 == 0:
            print(f"      {rotulo}: {i}/{len(PREFIXOS)} prefixos, {len(todos)} CID, "
                  f"{time.monotonic() - inicio:.0f}s", flush=True)
    print(f"    {rotulo}: {len(todos)} CID distintos em {time.monotonic() - inicio:.0f}s"
          + (f"  (NO TETO em {cheios} — precisa de 3 caracteres ali)" if cheios else ""))
    return todos


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--completa", type=int, default=2,
                    help="quantos recursos varrer por inteiro (0 = nenhum)")
    args = ap.parse_args()

    with httpx.Client(base_url=BASE, timeout=90, follow_redirects=False,
                      headers={"User-Agent": UA}) as c:
        ser = Ser(c)
        ser.login()
        t = Criar(ser)
        t.abrir()
        t.trocar(CAMPO_SISREG, "false")
        t.trocar(CAMPO_TIPO, "CONSULTA")
        consultas = t.ler(lambda h: combo(h, CAMPO_RECURSO)) or []
        box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]

        # Uma amostra espalhada pela lista + o oncológico, que já sabemos ser diferente.
        indices = [0, 15, 35, 60, 90, 115]
        alvos = [consultas[i] for i in indices if i < len(consultas)]
        alvos += [r for r in consultas if r[0] == "1063"]

        print(f"\n=== assinatura de {len(alvos)} recursos de CONSULTA "
              f"({', '.join(SONDAGEM)}) ===")
        grupos: dict[tuple[int, ...], list[str]] = {}
        for valor, rotulo in alvos:
            t.trocar(CAMPO_RECURSO, valor)
            a = assinatura(t, box_id)
            grupos.setdefault(a, []).append(f"{valor} {rotulo[:44]}")
            print(f"    {valor} {rotulo[:52]:52} -> {a}")

        print(f"\n    {len(grupos)} assinatura(s) distinta(s) em {len(alvos)} recursos:")
        for a, quem in grupos.items():
            print(f"      {a}: {len(quem)} recurso(s) — ex.: {quem[0]}")

        # EXAME tem a mesma lista?
        t.trocar(CAMPO_TIPO, "EXAME")
        exames = t.ler(lambda h: combo(h, CAMPO_RECURSO)) or []
        box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]
        print(f"\n=== EXAME (amostra de {min(3, len(exames))}) ===")
        for valor, rotulo in exames[:3]:
            t.trocar(CAMPO_RECURSO, valor)
            print(f"    {valor} {rotulo[:52]:52} -> {assinatura(t, box_id)}")

        # Varredura completa: quanto custa e quanto dá.
        if args.completa > 0:
            t.trocar(CAMPO_TIPO, "CONSULTA")
            box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]
            print(f"\n=== varredura completa ({len(PREFIXOS)} prefixos por recurso) ===")
            listas = {}
            for valor, rotulo in alvos[:args.completa]:
                t.trocar(CAMPO_RECURSO, valor)
                listas[valor] = varrer_tudo(t, box_id, f"{valor} {rotulo[:32]}")

            valores = list(listas.values())
            if len(valores) >= 2:
                a, b = valores[0], valores[1]
                print(f"\n    interseção: {len(a & b)}   só no 1º: {len(a - b)}   "
                      f"só no 2º: {len(b - a)}")


if __name__ == "__main__":
    main()
