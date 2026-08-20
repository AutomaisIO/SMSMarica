"""O caso relatado: CONSULTA em Mastologia, termo "I64" / "Acidente" — e o campo VAZIO.

Duas perguntas:

  1. Mastologia aceita I64 (AVC)? A medição de 20/08 diz que os recursos oncológicos usam a lista
     restrita (136 CID, só neoplasia) no ramo "Não" — mas no ramo "Sim" TODOS os recursos usam a
     lista ampla. Se o operador está no ramo "Sim", I64 aparece; no "Não", não. Esta sonda mede os
     dois lados do mesmo recurso.
  2. Campo vazio lista tudo? O operador relata que apagando o texto o SER lista "todos". Se
     `inputvalue` vazio devolve as 500 primeiras, a nossa tela pode fazer o mesmo.

SOMENTE LEITURA: só o fetch de sugestões.

Uso:  python probe_cid_mastologia.py
"""

from __future__ import annotations

import pathlib
import sys

import httpx

RAIZ = pathlib.Path(__file__).parent
sys.path.insert(0, str(RAIZ))

from probe_editar_solicitacao import BASE, UA, Ser, viewstate  # noqa: E402
from probe_criar_solicitacao import (  # noqa: E402
    CAMPO_CID, CAMPO_RECURSO, CAMPO_SISREG, CAMPO_TIPO,
    Criar, combo, linhas_de_sugestao, suggestion_box,
)

TERMOS = ["I64", "Acidente", "acidente vascular", "I64 Acidente Vascular", "C50", "mama", ""]


def sugerir(t: Criar, box_id: str, termo: str) -> list[list[str]]:
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
    return [c for c in linhas_de_sugestao(html, box_id) if len(c) >= 3 and c[1].strip()]


def main() -> None:
    with httpx.Client(base_url=BASE, timeout=90, follow_redirects=False,
                      headers={"User-Agent": UA}) as c:
        ser = Ser(c)
        ser.login()
        t = Criar(ser)

        for ramo in ("false", "true"):
            t.abrir()
            t.trocar(CAMPO_SISREG, ramo)
            t.trocar(CAMPO_TIPO, "CONSULTA")
            recursos = t.ler(lambda h: combo(h, CAMPO_RECURSO)) or []
            box_id = t.ler(lambda h: suggestion_box(h, CAMPO_CID))[0]

            alvos = [(v, r) for v, r in recursos if "mastologia" in r.lower()]
            print(f"\n=== ambulatório estadual = {'SIM' if ramo == 'true' else 'NÃO'} "
                  f"— {len(alvos)} recurso(s) de mastologia")

            for valor, rotulo in alvos:
                t.trocar(CAMPO_RECURSO, valor)
                print(f"\n  {valor} {rotulo}")
                for termo in TERMOS:
                    linhas = sugerir(t, box_id, termo)
                    amostra = ", ".join(l[1] for l in linhas[:6])
                    print(f"      {termo!r:26} -> {len(linhas):4} : {amostra}")
                    if termo == "I64" and linhas:
                        print(f"          texto do campo: {linhas[0][0]!r}")


if __name__ == "__main__":
    main()
