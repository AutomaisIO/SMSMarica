"""Detalha o impacto do combo "É AMBULATÓRIO ESTADUAL?" no catálogo.

Responde duas perguntas que decidem a modelagem:
  1. QUAIS recursos só existem em cada ramo (o "Sim" abre 31 consultas que não copiamos).
  2. Os campos dinâmicos de um MESMO recurso mudam conforme o ramo? Se mudarem, o catálogo
     precisa guardar campos por (recurso × ramo), não só por recurso.

SOMENTE LEITURA.
"""

from __future__ import annotations

import json
import pathlib
import sys

import httpx
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
sys.path.insert(0, str(RAIZ))

from probe_campos_dinamicos import (  # noqa: E402
    BASE, CAP, UA, Editar, campos_dinamicos, catalogo, login_e_modulo,
)
from probe_radio_dinamico import evento_do_combo  # noqa: E402

SISREG = "form0:comboSisReg"
TIPO = "form0:comboTipoRecurso"
RECURSO = "form0:comboRecurso"


def lista(ed: Editar, sisreg: str | None, tipo: str):
    ed.abrir()
    if sisreg is not None:
        ed.mudar(SISREG, sisreg, evento_do_combo(ed.full, SISREG))
    return dict(catalogo(ed.mudar(TIPO, tipo, evento_do_combo(ed.full, TIPO))))


def campos(ed: Editar, sisreg: str | None, tipo: str, recurso: str):
    ed.abrir()
    if sisreg is not None:
        ed.mudar(SISREG, sisreg, evento_do_combo(ed.full, SISREG))
    ed.mudar(TIPO, tipo, evento_do_combo(ed.full, TIPO))
    return campos_dinamicos(ed.mudar(RECURSO, recurso, evento_do_combo(ed.full, RECURSO)))


def assinatura(d):
    return sorted((n, i["rotulo"], i["tipo"], i["obrigatorio"]) for n, i in d.items())


with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                  follow_redirects=True, verify=False) as c:
    login_e_modulo(c)
    ed = Editar(c)
    saida = {}

    for tipo in ("CONSULTA", "EXAME"):
        nao = lista(ed, "false", tipo)
        sim = lista(ed, "true", tipo)
        so_sim = {v: r for v, r in sim.items() if v not in nao}
        so_nao = {v: r for v, r in nao.items() if v not in sim}
        saida[tipo] = {"so_sim": so_sim, "so_nao": so_nao,
                       "ambos": sorted(set(sim) & set(nao))}

        print(f"\n{'='*72}\n{tipo}: Não={len(nao)}  Sim={len(sim)}  "
              f"ambos={len(set(sim) & set(nao))}\n{'='*72}")
        print(f"-- SÓ com 'Sim' ({len(so_sim)}):")
        for v, r in list(so_sim.items())[:40]:
            print(f"   {v:>5}  {r}")
        print(f"-- SÓ com 'Não' ({len(so_nao)}):")
        for v, r in list(so_nao.items())[:40]:
            print(f"   {v:>5}  {r}")

    # Um recurso presente nos DOIS ramos: os campos mudam?
    comum = saida["CONSULTA"]["ambos"]
    if comum:
        alvo = comum[0]
        a = campos(ed, "false", "CONSULTA", alvo)
        b = campos(ed, "true", "CONSULTA", alvo)
        igual = assinatura(a) == assinatura(b)
        print(f"\n{'='*72}\ncampos do recurso {alvo} nos dois ramos: "
              f"{'IDÊNTICOS' if igual else 'DIFEREM'}  (Não={len(a)} campos, Sim={len(b)})")
        if not igual:
            print("  Não:", [i["rotulo"] for i in a.values()])
            print("  Sim:", [i["rotulo"] for i in b.values()])
        saida["campos_iguais_nos_dois_ramos"] = igual

    (CAP / "sisreg_ramos.json").write_text(
        json.dumps(saida, ensure_ascii=False, indent=1), encoding="utf-8")
    print("\nsalvo em capturas/sisreg_ramos.json")
