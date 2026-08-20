"""Quantas listas de CID DISTINTAS existem no catálogo inteiro?

`probe_cid_catalogo.py` mediu 7 recursos e achou só DUAS listas: a ampla (14.226 CID — o CID-10
inteiro, idêntica entre recursos de especialidades diferentes e entre CONSULTA e EXAME) e uma
restrita, do oncológico. Se isso valer para o catálogo todo, espelhar a relação na nossa base é
barato: varre-se uma vez por LISTA, não por recurso.

Esta sonda mede a assinatura de TODOS os recursos de um ramo e agrupa. A assinatura são três
buscas espalhadas pelo CID-10; dois recursos com a mesma tripla quase certamente compartilham a
lista (o `--conferir` refaz a varredura completa de um representante de cada grupo para provar).

SOMENTE LEITURA: só o fetch de sugestões. O `onselect` não é acionado.

Uso:  python probe_cid_grupos.py [--ramo nao|sim] [--conferir]
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

# Três buscas bem espalhadas: endócrino/geral, neoplasia e o capítulo Z.
SONDAGEM = ["diab", "malig", "Z9"]

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


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ramo", choices=["nao", "sim"], default="nao")
    ap.add_argument("--conferir", action="store_true",
                    help="varre por inteiro um representante de cada grupo")
    args = ap.parse_args()

    with httpx.Client(base_url=BASE, timeout=90, follow_redirects=False,
                      headers={"User-Agent": UA}) as c:
        ser = Ser(c)
        ser.login()
        t = Criar(ser)
        t.abrir()
        t.trocar(CAMPO_SISREG, "true" if args.ramo == "sim" else "false")

        grupos: dict[tuple[int, ...], list[tuple[str, str, str]]] = {}
        inicio = time.monotonic()

        for tipo in ("CONSULTA", "EXAME"):
            t.trocar(CAMPO_TIPO, tipo)
            recursos = t.ler(lambda h: combo(h, CAMPO_RECURSO)) or []
            box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]
            print(f"\n=== {tipo} no ramo {args.ramo.upper()}: {len(recursos)} recursos ===",
                  flush=True)

            for i, (valor, rotulo) in enumerate(recursos, 1):
                t.trocar(CAMPO_RECURSO, valor)
                a = tuple(len(sugerir(t, box_id, termo)) for termo in SONDAGEM)
                grupos.setdefault(a, []).append((tipo, valor, rotulo))
                if i % 25 == 0:
                    print(f"    {i}/{len(recursos)}  ({len(grupos)} grupo(s) até aqui, "
                          f"{time.monotonic() - inicio:.0f}s)", flush=True)

        print(f"\n=== {len(grupos)} lista(s) distinta(s) em "
              f"{sum(len(v) for v in grupos.values())} recursos "
              f"({time.monotonic() - inicio:.0f}s) ===")
        for a, quem in sorted(grupos.items(), key=lambda kv: -len(kv[1])):
            print(f"\n  assinatura {a} — {len(quem)} recurso(s)")
            for tipo, valor, rotulo in quem[:12]:
                print(f"      {tipo[:3]} {valor} {rotulo[:66]}")
            if len(quem) > 12:
                print(f"      ... (+{len(quem) - 12})")

        if args.conferir:
            print("\n=== varredura completa de um representante por grupo ===")
            listas = {}
            for a, quem in grupos.items():
                tipo, valor, rotulo = quem[0]
                t.trocar(CAMPO_TIPO, tipo)
                box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]
                t.trocar(CAMPO_RECURSO, valor)
                todos: set[str] = set()
                for prefixo in PREFIXOS:
                    todos.update(sugerir(t, box_id, prefixo))
                listas[a] = todos
                print(f"    {a} ({valor} {rotulo[:40]}): {len(todos)} CID distintos", flush=True)

            chaves = list(listas)
            for i in range(len(chaves)):
                for j in range(i + 1, len(chaves)):
                    a, b = listas[chaves[i]], listas[chaves[j]]
                    print(f"    {chaves[i]} x {chaves[j]}: interseção {len(a & b)}, "
                          f"só no 1º {len(a - b)}, só no 2º {len(b - a)}")


if __name__ == "__main__":
    main()
