"""Varre, opção a opção, os ramos condicionais da tela de CRIAÇÃO da requisição.

Sucessor do antigo `mapear_formulario.py` (perdido) para a tela que nasce do
[Novo Exame]. A pergunta que responde: marcar X **abre** ou **fecha** quais campos?

Método: a partir de um estado base, liga UM valor por vez via A4J, aplica o
parcial e compara o conjunto de campos submissíveis com o do estado base. Nunca
clica Salvar — a trava de `SiscanClient` barra "salvar"/"encerrar"/"confirmar".

    python mapear_requisicao_nova.py --cns <CNS> --unidade 35

PRODUÇÃO FEDERAL. Só leitura. Capturas em capturas/ (gitignored).
"""

from __future__ import annotations

import argparse
import sys

from siscan import exame, requisicao as req
from siscan.client import SiscanClient, aplicar_a4j
from siscan.inspecao import _a4j_de

# (rótulo humano, name do controle) — os j_idNN são auto-gerados; ficam aqui só
# porque a tela não dá outro jeito de endereçá-los. Conferir antes de confiar.
PERGUNTAS = [
    ("Apresenta risco elevado", "frm:j_id91", ["01", "02", "03"]),
    ("Mamas já examinadas", "frm:mamasExaminadas", ["01", "02", "03"]),
    ("Fez mamografia alguma vez", "frm:j_id105", ["01", "02", "03"]),
    ("Fez radioterapia", "frm:j_id121", ["01", "02", "03"]),
    ("Fez cirurgia de mama", "frm:j_id151", ["S", "N"]),
    ("Tipo de mamografia", "frm:j_id242", ["01", "02"]),
    ("Achado no exame clínico", "frm:localizacaoNodulo", ["01", "02", "04"]),
]


def campos_do_form(c: SiscanClient, doc) -> set[str]:
    """Campos PRESENTES no form — não os submissíveis.

    ARMADILHA que quase publicou um mapa errado (22/09/2026): `client.campos()`
    imita o navegador e **ignora radio/checkbox não marcado**. Um ramo que abre um
    grupo de radios (localização da radioterapia, população-alvo do rastreamento)
    aparecia como "nada abre nem fecha". Aqui a chave é `name=value` para
    radio/checkbox, e o grupo inteiro conta mesmo desmarcado.
    """
    f = doc.find("form", id="frm")
    out: set[str] = set()
    if f is None:
        return out
    for el in f.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome:
            continue
        tipo = (el.get("type") or el.name).lower()
        out.add(f"{nome}={el.get('value')}" if tipo in ("radio", "checkbox") else nome)
    return out


def marcar(c: SiscanClient, doc, name: str, valor: str, nome: str):
    el = next((i for i in doc.find_all("input", {"name": name})
               if i.get("value") == valor), None)
    if el is None:
        return None
    params = _a4j_de(el.get("onclick") or el.get("onchange") or "")
    if not params:
        return "SEM-A4J"       # controle que só mexe no cliente
    return aplicar_a4j(doc, c.sopa(c.post_a4j(doc, "frm", {name: valor}, params, nome)))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cns", required=True)
    ap.add_argument("--unidade", default="35")
    ap.add_argument("--tipo", default="01")
    args = ap.parse_args()

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        doc = exame.abrir(c)
        doc = req.novo_exame(c, doc)
        doc = req.digitar_cartao_sus(c, doc, args.cns)
        doc = req.marcar_tipo_exame(c, doc, args.tipo)
        base = req.avancar(c, doc, args.unidade, args.tipo)
        campos_base = campos_do_form(c, base)
        print(f"etapa 2 carregada: {len(campos_base)} campos submissíveis no estado base\n")

        for rotulo, name, valores in PERGUNTAS:
            print(f"== {rotulo}  ({name})")
            for v in valores:
                depois = marcar(c, base, name, v, f"map-{name.split(':')[-1]}-{v}")
                if depois is None:
                    print(f"   {v:<3} -> opção ausente")
                    continue
                if depois == "SEM-A4J":
                    print(f"   {v:<3} -> não chama o servidor (só JS no cliente)")
                    continue
                agora = campos_do_form(c, depois)
                abriu = sorted(agora - campos_base)
                fechou = sorted(campos_base - agora)
                if not abriu and not fechou:
                    print(f"   {v:<3} -> nada abre nem fecha (mas dispara A4J)")
                else:
                    print(f"   {v:<3} -> abre {len(abriu)}: {abriu[:14]}"
                          + (" ..." if len(abriu) > 14 else ""))
                    if fechou:
                        print(f"       fecha {len(fechou)}: {fechou[:14]}")

        # 3º nível: radioterapia -> localização -> ano por lado. Uma sondagem de
        # um nível só para na localização e perde os anos (erro cometido em ago/2026).
        print("\n== 3º nível: radioterapia Sim -> localização (frm:j_id128)")
        nivel2 = marcar(c, base, "frm:j_id121", "01", "map-radio-sim")
        if isinstance(nivel2, str) or nivel2 is None:
            print("   não consegui abrir a localização")
            return 0
        campos2 = campos_do_form(c, nivel2)
        for v in ("01", "02", "03"):
            nivel3 = marcar(c, nivel2, "frm:j_id128", v, f"map-localiz-{v}")
            if nivel3 is None:
                print(f"   {v:<3} -> opção ausente")
                continue
            if nivel3 == "SEM-A4J":
                print(f"   {v:<3} -> não chama o servidor")
                continue
            abriu = sorted(campos_do_form(c, nivel3) - campos2)
            print(f"   {v:<3} -> abre {len(abriu)}: {abriu}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
