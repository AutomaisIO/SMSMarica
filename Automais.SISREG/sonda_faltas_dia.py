"""Sonda: a lista de faltas de UM dia, paginada e inteira, para achar a linha que o coletor perde.

SOMENTE LEITURA no SISREG. Nasceu em 07/10/2026: o coletor de "faltas recentes" recusou o 14/09/2026
porque a lista inteira (`imprimir_lista=1`) trouxe 310 faltas e a paginada dizia 32 páginas (311–320).
A mesma assinatura explica faltas que a tela do SISREG mostra e a nossa base não tem (23/09: AGE/FALTA/EXEC
fora da lista gravada) — uma linha a menos cabe na folga de 10 da conferência e passa calada.

O que faz (até ~5 requisições, contando o login):
  1. a página 1 da consulta paginada → N páginas (o rodapé "Mostrando Página de N");
  2. a lista inteira → HTML salvo FORA do git (tem nome, endereço e telefone de paciente);
  3. analisa a lista como o parser do servidor (`IndicadoresSisregHtmlParser.Faltas`): linhas aceitas,
     linhas com cara de falta que ele descartaria (menos de 8 células, data ilegível) e códigos de
     solicitação que aparecem no HTML mas não numa linha aceita.

Usa o PROGRAMADOR-BERNARDO, como os outros coletores do laboratório — derruba a sessão do robô de
produção enquanto roda. Só com OK do Bernardo.

Uso:
  python sonda_faltas_dia.py 2026-09-14 --saida <pasta fora do repo> [--executante CNES] [--so-analisar]
"""
from __future__ import annotations

import argparse
import datetime as dt
import html as htmllib
import pathlib
import re
import sys

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
import coletar_serie_indicadores as serie  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TELA = "/cgi-bin/rel_amb_faltas_sol.pl"
TR = re.compile(r"<tr[^>]*>(.*?)</tr>", re.S | re.I)
TD = re.compile(r"<td[^>]*>(.*?)</td>", re.S | re.I)
TAG = re.compile(r"<[^>]+>")
CODIGO = re.compile(r"^\d{8,12}$")
MOSTRANDO = re.compile(r"Mostrando\s+P\S*gina\s+de\s+(\d+)", re.I)
DATA = re.compile(r"^\d{2}/\d{2}/\d{4}$")


def limpar(f: str) -> str:
    return re.sub(r"\s+", " ", htmllib.unescape(TAG.sub(" ", f))).strip()


def campos(dia: dt.date, executante: str, lista: bool) -> dict:
    br = dia.strftime("%d/%m/%Y")
    c = {"co_solicitacao": "", "ETAPA": "", "ordem": "1", "offset": "0", "cnes_solicitante": "",
         "cnes_executante": executante, "cns": "", "co_proc": "", "no_proc": "", "data1": br, "data2": br}
    if lista:
        c["imprimir_lista"] = "1"
    return c


def analisar(html: str) -> tuple[int | None, list[str]]:
    aceitas, descartadas = [], []
    for tr in TR.finditer(html):
        c = [limpar(td) for td in TD.findall(tr.group(1))]
        if not c or not CODIGO.match(c[0]):
            continue
        if len(c) >= 8 and DATA.match(c[5]):
            aceitas.append(c[0])
        else:
            descartadas.append((len(c), c[0], c[5] if len(c) > 5 else None))
    texto = limpar(html)
    m = MOSTRANDO.search(texto)
    print(f"  rodapé: {m.group(0) if m else '(sem rodapé)'}")
    print(f"  linhas aceitas pelo parser: {len(aceitas)} ({len(set(aceitas))} códigos distintos)")
    print(f"  linhas com código descartadas: {len(descartadas)}")
    for n, cod, d5 in descartadas:
        print(f"    - {cod}: {n} células, coluna 5 = {d5!r}")
    # Códigos de 9 dígitos soltos no HTML que não estão numa linha aceita (linha quebrada, tr sem fechar…).
    soltos = sorted(set(re.findall(r"(?<!\d)(\d{9,10})(?!\d)", html)) - set(aceitas))
    print(f"  números de 9–10 dígitos fora das linhas aceitas: {len(soltos)}")
    # Só o número: o HTML em volta tem nome, endereço e telefone de paciente.
    print(f"    {', '.join(soltos[:40])}")
    print(f"  <tr: {len(re.findall(r'<tr', html, re.I))}  </tr>: {len(re.findall(r'</tr>', html, re.I))}")
    return (int(m.group(1)) if m else None), aceitas


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("dia")
    ap.add_argument("--saida", required=True, help="pasta FORA do repositório (o HTML tem dado de paciente)")
    ap.add_argument("--executante", default="", help="CNES, ou vários separados por vírgula (uma sessão só)")
    ap.add_argument("--so-analisar", action="store_true", help="não chama o SISREG; analisa o HTML já salvo")
    ap.add_argument("--max-req", type=int, default=8)
    a = ap.parse_args()

    dia = dt.date.fromisoformat(a.dia)
    saida = pathlib.Path(a.saida)
    if BASE.parent in saida.resolve().parents:
        raise SystemExit("a saída tem que ficar fora do repositório")
    saida.mkdir(parents=True, exist_ok=True)
    col = None
    resumo = []
    for executante in (a.executante.split(",") if a.executante else [""]):
        sufixo = f"{dia:%Y%m%d}_{executante or 'rede'}"
        arq_pag, arq_lista = saida / f"faltas_{sufixo}_pagina1.html", saida / f"faltas_{sufixo}_lista.html"
        if not a.so_analisar and not arq_lista.exists():
            if col is None:
                col = serie.Coletor(a.max_req)
                col.login()
            print(f"[{executante or 'rede'}] paginada + lista inteira de {dia}")
            try:
                arq_pag.write_text(col.pedir("GET", TELA, campos(dia, executante, False), repetir_em_erro=False), encoding="utf-8")
                arq_lista.write_text(col.pedir("GET", TELA, campos(dia, executante, True), repetir_em_erro=False), encoding="utf-8")
            except serie.ConexaoCortada as e:
                print(f"    corte: {e}")
                resumo.append((executante, None, None, "corte"))
                continue
        print(f"== {executante or 'rede'} — paginada (página 1)")
        paginas, _ = analisar(arq_pag.read_text(encoding="utf-8"))
        print(f"== {executante or 'rede'} — lista inteira")
        _, aceitas = analisar(arq_lista.read_text(encoding="utf-8"))
        n = len(aceitas)
        if paginas is None:
            ok = "página única" if n <= 10 else "SEM RODAPÉ"
        else:
            ok = "ok" if (paginas - 1) * 10 < n <= paginas * 10 else f"DIFERE: {paginas} páginas = {(paginas - 1) * 10 + 1}–{paginas * 10}"
        resumo.append((executante, paginas, n, ok))
    if col is not None:
        print(f"requisições gastas: {col.gastas}")
    print("\n== resumo")
    for executante, paginas, n, ok in resumo:
        print(f"  {executante or 'rede':8} páginas={paginas} lista={n} {ok}")


if __name__ == "__main__":
    main()
