"""O combo "É AMBULATÓRIO ESTADUAL?" (form0:comboSisReg) muda a lista de recursos?

Ele é o PRIMEIRO campo da aba Editar e tem onchange A4J próprio (form0:j_id46) que re-renderiza
`form0:renderizaSisReg`. Nossa cópia do catálogo leu os 203 recursos SEM nunca tocar nele — ou
seja, no default "Selecione...".

Se a resposta for "muda", o catálogo espelhado é UM RAMO SÓ e está incompleto: faltariam os
recursos do outro ramo, e o operador não teria como pedi-los.

SOMENTE LEITURA. Trocar combo apenas re-renderiza a view.
"""

from __future__ import annotations

import pathlib
import re
import sys

import httpx
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
sys.path.insert(0, str(RAIZ))

from probe_campos_dinamicos import BASE, UA, Editar, catalogo, login_e_modulo  # noqa: E402
from probe_radio_dinamico import evento_do_combo  # noqa: E402

SISREG = "form0:comboSisReg"
TIPO = "form0:comboTipoRecurso"


def recursos(ed: Editar, sisreg: str | None, tipo: str) -> list[tuple[str, str]]:
    ed.abrir()
    if sisreg is not None:
        ed.mudar(SISREG, sisreg, evento_do_combo(ed.full, SISREG))
    return catalogo(ed.mudar(TIPO, tipo, evento_do_combo(ed.full, TIPO)))


with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                  follow_redirects=True, verify=False) as c:
    login_e_modulo(c)
    ed = Editar(c)

    for tipo in ("CONSULTA", "EXAME"):
        print(f"\n{'='*70}\n{tipo}\n{'='*70}")
        conj = {}
        for rotulo, valor in (("sem tocar", None), ("Sim", "true"), ("Não", "false")):
            r = recursos(ed, valor, tipo)
            conj[rotulo] = {v for v, _ in r}
            print(f"  {rotulo:10} -> {len(r)} recursos")

        base = conj["sem tocar"]
        for rotulo in ("Sim", "Não"):
            so_la = conj[rotulo] - base
            so_ca = base - conj[rotulo]
            veredito = "IDÊNTICO ao default" if not so_la and not so_ca else "DIFERE"
            print(f"  {rotulo:10} {veredito}"
                  f"{'' if veredito.startswith('IDÊNT') else f' (+{len(so_la)} / -{len(so_ca)})'}")
