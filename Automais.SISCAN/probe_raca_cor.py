"""Sonda: o que a tela do SISCAN faz quando o CADSUS traz a paciente SEM Raça/Cor?

Em 30/09/2026 o Avançar recusou por Raça/Cor. As capturas só tinham pacientes COM o dado
(campo `frm:racaCor` = input DISABLED), e daí eu concluí, sem medir, que ele nunca é editável.
O Bernardo viu na tela que, na recusa, dá para escolher. Esta sonda mede:

  1. depois do CNS: como vem o bloco de Raça/Cor (tag, disabled, opções);
  2. depois do Avançar: se recusou, com que mensagem, e como o bloco ficou;
  3. se existe combo de Raça/Cor/Etnia em algum momento, e com quais códigos.

    python probe_raca_cor.py --cns <CNS da paciente> [--cnes 6886973]
    python probe_raca_cor.py --cns <CNS> --raca 5      # só o A4J do combo: o que abre p/ Indígena

SOMENTE LEITURA. Para no Avançar — que só navega para a etapa 2 — e NÃO clica Salvar. Imprime
só ESTRUTURA (tag, name, disabled, rótulos das opções), nunca valor de campo da paciente. As
páginas inteiras vão para capturas/ (gitignored — têm PII).

Com `--raca`, dispara SÓ o A4J do combo (estado da tela, em memória) e para: NUNCA avança com
uma raça escolhida pela sonda — se o SISCAN gravar o dado da paciente nesse passo, seria
informação falsa sobre uma pessoa real numa base federal.

MEDIDO em 30/09/2026 (paciente sem Raça/Cor no CADSUS): logo depois do CNS vem
`<select name="frm:cmbRacaCor">` EDITÁVEL, com A4J no onchange (ajaxSingle), opções
0 Selecione… · 1 BRANCA · 2 PRETA · 3 PARDA · 4 AMARELA · 5 INDIGENA. Sem escolher, o Avançar
recusa: "O campo Raça Cor deve ser informado."
"""

from __future__ import annotations

import argparse
import re
import sys

from siscan import exame, requisicao as req
from siscan.client import SiscanClient, aplicar_a4j, mensagens
from siscan.inspecao import titulos

PADRAO = re.compile(r"raca|racacor|cor\b|etnia", re.I)


def bloco_raca(doc, rotulo: str) -> None:
    print(f"\n=== {rotulo}")
    print("   títulos:", titulos(doc)[:2])
    achou = False
    for el in doc.find_all(["input", "select", "textarea"]):
        chave = f"{el.get('name') or ''} {el.get('id') or ''}"
        if not PADRAO.search(chave):
            continue
        achou = True
        estado = []
        if el.has_attr("disabled"):
            estado.append("DISABLED")
        if el.has_attr("readonly"):
            estado.append("READONLY")
        tem_valor = bool((el.get("value") or "").strip()) if el.name == "input" else None
        print(f"   <{el.name} type={el.get('type')!r} name={el.get('name')!r} id={el.get('id')!r}"
              f"> {' '.join(estado) or 'EDITÁVEL'}"
              + (f" · tem valor? {tem_valor}" if tem_valor is not None else ""))
        for ev in ("onchange", "onclick", "onblur"):
            if el.get(ev):
                print(f"      {ev}: {el.get(ev)[:220]}")
        if el.name == "select":
            ops = [(o.get("value"), o.get_text(" ", strip=True), o.has_attr("selected"))
                   for o in el.find_all("option")]
            print(f"      {len(ops)} opções:")
            for v, t, sel in ops:
                print(f"        {v!r:>6} | {t}{'  (selected)' if sel else ''}")
    if not achou:
        print("   (nenhum campo de raça/cor/etnia na tela)")
    for sid in ("frm:pnlEtnia", "frm:divEtnia", "frm:lblRacaCor"):
        el = doc.find(id=sid)
        if el is not None:
            filhos = [f"{x.name}:{x.get('name') or x.get('id')}" for x in el.find_all(["input", "select"])]
            print(f"   #{sid}: {filhos or 'vazio'}")
    ms = list(dict.fromkeys(m for m in mensagens(doc) if len(m) < 300))
    for m in ms:
        print("   !", m)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cns", required=True, help="Cartão SUS da paciente")
    ap.add_argument("--cnes", default="6886973", help="unidade requisitante (padrão: SMS Maricá)")
    ap.add_argument("--raca", help="dispara SÓ o A4J do combo com este código e para (sem Avançar)")
    args = ap.parse_args()

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        doc = exame.abrir(c)
        doc = req.novo_exame(c, doc)
        doc = req.digitar_cartao_sus(c, doc, args.cns, nome="raca-1-cns")
        bloco_raca(doc, "1. depois do CNS (CADSUS montou a paciente)")

        if args.raca:
            combo = doc.find("select", {"name": "frm:cmbRacaCor"})
            if combo is None:
                print("\nsem combo de Raça/Cor — nada a disparar")
                return 0
            r = c.post_a4j(doc, "frm", {"frm:cmbRacaCor": args.raca},
                           req._a4j_do_elemento(combo), "raca-2-combo")
            parcial = c.sopa(r)
            ids = parcial.find("meta", {"name": "Ajax-Update-Ids"})
            print(f"\nA4J do combo = {args.raca}: regiões re-renderizadas = "
                  f"{ids.get('content') if ids else '(nenhuma declarada)'}")
            doc = aplicar_a4j(doc, parcial)
            bloco_raca(doc, f"2. depois de escolher {args.raca} no combo (sem Avançar)")
            print("\nPARADO AQUI DE PROPÓSITO: nada foi avançado nem salvo.")
            return 0

        doc = req.marcar_tipo_exame(c, doc, "01", nome="raca-2-tipo")
        unidade = req.unidade_por_cnes(doc, args.cnes)
        doc = req.avancar(c, doc, unidade, "01", nome="raca-3-avancar")
        recusou = not any("SOLICITAR" in t.upper() for t in titulos(doc))
        bloco_raca(doc, f"2. depois do Avançar — {'RECUSOU' if recusou else 'abriu a etapa 2'}")

        print("\nPARADO AQUI DE PROPÓSITO: nada foi salvo no SISCAN. Páginas em capturas/raca-*.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
