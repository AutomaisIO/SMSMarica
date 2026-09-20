"""Consulta de Marcações Canceladas — quem cancelou no SISREG, por período.

É a fonte para CONCILIAR a nossa base: cancelamento feito pela unidade executante ou pela
solicitante não passa por nós, e hoje o SMSMais só descobre por acaso. Numa amostra de 12 fichas
da fila de Cancelamento (20/09/2026), **4 já estavam canceladas no SISREG** e o sistema não sabia.

    POST /cgi-bin/cons_marcacao_cancelada
      etapa       = LISTAR_MARCACOES
      tp_periodo  = C   (período de CANCELAMENTO; S = solicitação, M = marcação)
      dt_inicial / dt_final   dd/mm/aaaa — o SISREG recusa intervalo maior que 31 dias
      co_cnes_ups = CNES da executante, ou vazio para TODAS
      pagina      = 0-based

Uso:
    python consultar_canceladas.py                 # cancelamentos de hoje, rede toda
    python consultar_canceladas.py 18/09 20/09     # um intervalo
    python consultar_canceladas.py --conciliar     # mostra o que bate com a nossa base

Só leitura. A saída tem dado de paciente: mandar para fora do repositório.
"""
from __future__ import annotations

import argparse
import datetime as dt
import html as _html
import os
import pathlib
import re
import sys

from dotenv import load_dotenv

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg.client import SisregClient  # noqa: E402
import cancelar_solicitacao as C  # noqa: E402

CAMINHO = "/cgi-bin/cons_marcacao_cancelada"


def buscar(cli: SisregClient, inicio: str, fim: str, cnes: str = "", pagina: int = 0) -> str:
    r = cli.post(CAMINHO, data={
        "etapa": "LISTAR_MARCACOES",
        "tp_periodo": "C",          # pelo período do CANCELAMENTO — é o que interessa conciliar
        "dt_inicial": inicio,
        "dt_final": fim,
        "co_cnes_ups": cnes,
        "pagina": str(pagina),
    })
    r.raise_for_status()
    if not C.sessao_viva(r.text):
        raise C.SessaoExpirada()
    return r.text


def linhas(html: str) -> list[dict]:
    """
    Uma linha por cancelamento. O HTML é tabela pura; cada <tr> de dados começa com o código da
    solicitação. Guarda as células como vieram — interpretar de menos é melhor que inventar.
    """
    fora = []
    for tr in re.findall(r"<tr[^>]*>(.*?)</tr>", html, re.S | re.I):
        celulas = [
            re.sub(r"\s+", " ", _html.unescape(re.sub(r"<[^>]+>", " ", td))).strip()
            for td in re.findall(r"<td[^>]*>(.*?)</td>", tr, re.S | re.I)
        ]
        if len(celulas) >= 4 and re.fullmatch(r"\d{6,}", celulas[0]):
            fora.append({"codigo": celulas[0], "celulas": celulas})
    return fora


MAX_PAGINAS = 80   # 20 linhas por página; teto de sanidade, não expectativa


def todas_as_paginas(pegar, inicio_html: str) -> list[dict]:
    """
    Junta todas as páginas.

    O rodapé NÃO diz quantas são — só oferece `buscaSolicitacoes(1)`, o que me fez acreditar,
    na primeira versão, que 20 linhas eram o resultado inteiro (e 20 é exatamente o tamanho de
    uma página: número redondo é sempre suspeito). A forma honesta é pedir a próxima página até
    ela não trazer nada novo.
    """
    vistos: dict[str, dict] = {}
    html = inicio_html
    for pag in range(MAX_PAGINAS):
        if pag:
            html = pegar(pag)
        novos = [l for l in linhas(html) if l["codigo"] not in vistos]
        if not novos:
            break
        for l in novos:
            vistos[l["codigo"]] = l
    return list(vistos.values())


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inicio", nargs="?", help="dd/mm ou dd/mm/aaaa (padrão: hoje)")
    ap.add_argument("fim", nargs="?", help="dd/mm ou dd/mm/aaaa (padrão: igual ao início)")
    ap.add_argument("--cnes", default="", help="CNES da unidade executante (padrão: todas)")
    ap.add_argument("--conciliar", action="store_true",
                    help="cruza com a nossa base e mostra o que está desencontrado")
    args = ap.parse_args()

    hoje = dt.date.today()

    def data(v: str | None) -> str:
        if not v:
            return hoje.strftime("%d/%m/%Y")
        return v if len(v) == 10 else f"{v}/{hoje.year}"

    inicio, fim = data(args.inicio), data(args.fim or args.inicio)

    load_dotenv(pathlib.Path(__file__).parent / ".env")
    usuario, senha = os.getenv("SISREG_USUARIO", "").strip(), os.getenv("SISREG_SENHA", "")
    if not usuario or not senha:
        print("SISREG_USUARIO/SISREG_SENHA ausentes no .env.")
        return 1

    print(f"cancelamentos de {inicio} a {fim}" + (f" · CNES {args.cnes}" if args.cnes else " · todas as unidades"))

    with SisregClient() as cli:
        cli.conectar(usuario, senha)

        def pegar(pag):
            try:
                return buscar(cli, inicio, fim, args.cnes, pag)
            except C.SessaoExpirada:
                cli.login(usuario, senha)
                cli.salvar_sessao("capturas/.sessao.json")
                return buscar(cli, inicio, fim, args.cnes, pag)

        todas = todas_as_paginas(pegar, pegar(0))

        print(f"{len(todas)} cancelamento(s).\n")
        for l in todas:
            print("  " + " | ".join(l["celulas"][:7]))

        if args.conciliar and todas:
            cur = C.conectar_smsmais().cursor()
            codigos = [l["codigo"] for l in todas]
            cur.execute("""
                select codigo_solicitacao, status, status_confirmacao
                  from smsmarica.solicitacao
                 where codigo_solicitacao = any(%s) and excluido_em is null
            """, (codigos,))
            nossos = {c: (st, sc) for c, st, sc in cur.fetchall()}
            desencontrados = [c for c in codigos if c in nossos and nossos[c][0] != 4]
            print(f"\nna nossa base: {len(nossos)} de {len(codigos)}")
            print(f"cancelados no SISREG mas AINDA DE PÉ aqui: {len(desencontrados)}")
            for c in desencontrados:
                st, sc = nossos[c]
                print(f"   {c}  status={st} status_confirmacao={sc}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
