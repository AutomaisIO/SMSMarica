"""Consulta de Marcações Canceladas — quem cancelou no SISREG, por período.

É a fonte para CONCILIAR a nossa base: cancelamento feito pela unidade executante ou pela
solicitante não passa por nós, e hoje o SMSMais só descobre por acaso. Numa amostra de 12 fichas
da fila de Cancelamento (20/09/2026), **4 já estavam canceladas no SISREG** e o sistema não sabia.

    POST /cgi-bin/cons_marcacao_cancelada
      etapa       = LISTAR_MARCACOES
      tp_periodo  = C   (período de CANCELAMENTO; S = solicitação, M = marcação)
      dt_inicial / dt_final   dd/mm/aaaa — o SISREG recusa intervalo maior que 31 dias
      co_cnes_ups = CNES da executante, ou vazio para TODAS
      pagina      = 0-based; a tela DECLARA o total em `MARCAÇÕES PESQUISADAS (N)` e o número de
                    páginas em `exibirPagina(_, P)` — pagine 0..P-1 e confira N no fim

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


TETO_PAGINAS = 500   # sanidade; o número real vem do rodapé


def total_anunciado(html: str) -> tuple:
    """(linhas, páginas) que a PRÓPRIA tela declara: `MARCAÇÕES PESQUISADAS (N)` e `de P`."""
    texto = _html.unescape(re.sub(r"<[^>]+>", " ", html))
    n = re.search(r"PESQUISADAS?\s*\((\d+)\)", texto, re.I)
    p = re.findall(r"exibirPagina\(\s*[^,]+,\s*(\d+)\s*\)", html)
    return (int(n.group(1)) if n else None, max(int(x) for x in p) if p else None)


def todas_as_paginas(pegar, inicio_html: str) -> tuple:
    """
    Junta todas as páginas. Devolve (linhas, aviso) — aviso vazio quando a leitura fechou.

    ARMADILHA QUE JÁ CUSTOU UMA CONCLUSÃO ERRADA (20/09/2026): a primeira versão parava na
    primeira página sem novidade e tinha teto de 80 páginas. Numa janela de 31 dias a tela
    anunciava **1.868 linhas em 94 páginas** e o coletor devolveu 1.599 — 269 a menos — calado.
    E o número redondo (80 × 20 = 1.600) não levantou suspeita nenhuma.

    Agora o total vem da própria tela e a leitura só se dá por completa quando o que se juntou
    bate com o que ela declarou. Faltando linha, devolve aviso — nunca silêncio: aqui "faltou"
    quer dizer "um cancelamento se perdeu", que é justamente o que não pode acontecer.
    """
    vistos = {}
    brutas = 0
    html = inicio_html
    linhas_decl, paginas_decl = total_anunciado(html)
    limite = min(paginas_decl or TETO_PAGINAS, TETO_PAGINAS)
    for pag in range(limite):
        if pag:
            html = pegar(pag)
        lidas = linhas(html)
        brutas += len(lidas)
        novos = [l for l in lidas if l["codigo"] not in vistos]
        for l in novos:
            vistos[l["codigo"]] = l
        # Sem total declarado, a única parada honesta é a página vazia.
        if not novos and paginas_decl is None:
            break
    aviso = ""
    if linhas_decl is not None and brutas != linhas_decl:
        # Faltou LINHA: o parser não leu tudo, ou a paginação cortou.
        aviso = (f"INCOMPLETO: a tela declarou {linhas_decl} linha(s) em {paginas_decl} página(s); "
                 f"o parser leu {brutas}. NÃO use para conciliar.")
    elif len(vistos) != brutas:
        # Todas as linhas foram lidas, mas há código repetido — a dedupe por código está errada,
        # ou a mesma marcação foi cancelada duas vezes. Não é motivo para recusar a conciliação,
        # mas precisa aparecer: silenciar repetição é como silenciar linha faltando.
        aviso = (f"ATENÇÃO: {brutas} linha(s) lidas e apenas {len(vistos)} código(s) distinto(s) — "
                 f"{brutas - len(vistos)} repetido(s). Conciliação segue, mas investigue a chave.")
    return list(vistos.values()), aviso


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

        todas, aviso = todas_as_paginas(pegar, pegar(0))
        if aviso:
            print("\n*** " + aviso + "\n")

        print(f"{len(todas)} cancelamento(s).\n")
        for l in todas:
            print("  " + " | ".join(l["celulas"][:7]))

        if args.conciliar and aviso.startswith("INCOMPLETO"):
            print("conciliação recusada: a leitura veio incompleta (ver aviso acima).")
            return 1
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
