"""Sonda do fluxo GERAR UMA REQUISIÇÃO NOVA (mamografia) no SISCAN.

    EXAME -> GERENCIAR EXAME -> [Novo Exame] -> Cartão SUS -> tipo -> unidade
    -> [Avançar] -> requisição -> tipo de mamografia -> Responsável -> (Salvar)

NÃO CLICA SALVAR. O cliente nasce `somente_leitura=True` e a trava barra
salvar / encerrar / confirmar / incluir / liberar / alterar; só "novo" é liberado,
porque [Novo Exame] apenas renderiza a tela.

    python probe_nova_requisicao.py --parar-em 2                  # abrir a tela
    python probe_nova_requisicao.py --cns 700000000000000         # + CADSUS pelo CNS
    python probe_nova_requisicao.py --cns ... --unidade 35        # até o Responsável

PRODUÇÃO FEDERAL, pacientes reais. Toda escrita exige OK explícito do Bernardo,
por ação. Capturas em capturas/ (gitignored — contêm PII).
"""

from __future__ import annotations

import argparse
import sys

from siscan import exame, requisicao as req
from siscan.client import SiscanClient, mensagens
from siscan.inspecao import relatorio, titulos


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cns", help="Cartão SUS do paciente")
    ap.add_argument("--tipo", default="01", help="tipo de exame (01 = Mamografia)")
    ap.add_argument("--unidade", help="índice da Unidade Requisitante (ver passo 4)")
    ap.add_argument("--mamografia", default="02", help="01 Diagnóstica | 02 Rastreamento")
    ap.add_argument("--responsavel", help="índice do Responsável (ver passo 6)")
    ap.add_argument("--parar-em", type=int, default=9)
    args = ap.parse_args()

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        doc = exame.abrir(c)
        print("PASSO 1  GERENCIAR EXAME:", titulos(doc)[:1])
        if args.parar_em < 2:
            return 0

        doc = req.novo_exame(c, doc)
        print("PASSO 2  [Novo Exame] ->", (c.action(doc, "frm") or "?"))
        relatorio(doc, "PASSO 2  etapa 1 — identificação do paciente", so_forms=("frm",))
        if args.parar_em < 3 or not args.cns:
            return 0

        doc = req.digitar_cartao_sus(c, doc, args.cns)
        relatorio(doc, "PASSO 3  CNS digitado — o que o CADSUS montou", so_forms=("frm",))
        if args.parar_em < 4:
            return 0

        antes = len(req.unidades(doc))
        doc = req.marcar_tipo_exame(c, doc, args.tipo)
        us = req.unidades(doc)
        print(f"\nPASSO 4  tipo de exame {args.tipo} ({req.TIPO_EXAME.get(args.tipo)}): "
              f"Unidade Requisitante passou de {antes} para {len(us)} opções")
        for v, t in us:
            print(f"   {v:>3} | {t}")
        if args.parar_em < 5:
            return 0

        unidade = args.unidade or next((v for v, _ in us if v != "0"), "0")
        doc = req.avancar(c, doc, unidade, args.tipo)
        print(f"\nPASSO 5  [Avançar] com unidade={unidade} ({dict(us).get(unidade)})")
        relatorio(doc, "PASSO 5  etapa 2 — SOLICITAR REQUISIÇÃO", so_forms=("frm",))
        for m in dict.fromkeys(m for m in mensagens(doc) if len(m) < 200):
            print("   !", m)
        if args.parar_em < 6:
            return 0

        print(f"\nPASSO 6  Responsável ANTES do tipo de mamografia: "
              f"{len(req.responsaveis(doc))} opções")
        doc = req.marcar_tipo_mamografia(c, doc, args.mamografia)
        rs = req.responsaveis(doc)
        print(f"PASSO 6  tipo de mamografia {args.mamografia} "
              f"({req.TIPO_MAMOGRAFIA.get(args.mamografia)}): {len(rs)} responsáveis")
        for v, t in rs:
            print(f"   {v:>3} | {t}")
        if args.parar_em < 7 or not args.responsavel:
            return 0

        doc = req.escolher_responsavel(c, doc, args.responsavel)
        conselho = doc.find(attrs={"name": "frm:conselho"})
        print(f"\nPASSO 7  Responsável {args.responsavel} -> Conselho derivado: "
              f"{conselho.get('value')!r}"
              f"{' (DISABLED)' if conselho.has_attr('disabled') else ''}")
        print("\nPARADO AQUI DE PROPÓSITO: o próximo clique seria [Salvar] "
              "(frm:btSalvar), que CRIA a requisição em produção.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
