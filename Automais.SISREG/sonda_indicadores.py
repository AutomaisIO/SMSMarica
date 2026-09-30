"""Sondas SOMENTE LEITURA das telas do SISREG que alimentam os INDICADORES DE REGULAÇÃO.

Nasceu da demanda de 30/09/2026 (docs/regulacao/): série mensal de 12 meses de absenteísmo,
cotas (PPI), canceladas com motivo e devolvidas/negadas. O que cada tela respondeu, e o que é beco
sem saída, está em docs/APRENDIZADOS.md §"Telas para INDICADORES DE REGULAÇÃO".

⚠️  Usa o PROGRAMADOR-BERNARDO (mesmo operador do robô de produção): o login DERRUBA a sessão dos
motores, que relogam sozinhos. Só rodar com OK do Bernardo. Cada execução = 1 login + as consultas
pedidas + logout, com teto rígido de requisições e parada imediata em CAPTCHA/sessão derrubada.

⛔  Rede inteira no `gerenciador_solicitacao` (situação ≠ 1) e no `cons_negados_reg` estoura ~65 s e
o servidor corta a conexão — por isso `situacao` e `devolvidas` EXIGEM --unidade (CNES solicitante).

Uso (datas dd/mm/aaaa, janela ≤ 31 dias — regra das próprias telas):
  python sonda_indicadores.py faltas     --de 01/01/2026 --ate 07/01/2026 [--lista]
  python sonda_indicadores.py ppi        --mes 8 --ano 2026 [--tipo exec|solic]
  python sonda_indicadores.py canceladas --de 01/08/2026 --ate 31/08/2026 [--periodo C|M|S]
  python sonda_indicadores.py devolvidas --unidade 2266865 --de 01/08/2026 --ate 31/08/2026
  python sonda_indicadores.py situacao   --sit 4 --unidade 2266865 --de 01/08/2026 --ate 31/08/2026

Saída: só contagens no terminal; o HTML cru (tem PII) vai para capturas/indicadores/ (gitignored).
"""
from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import re
import sys
import time

from bs4 import BeautifulSoup

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
from sisreg import SisregClient  # noqa: E402
from sonda_fila_gerenciador import (MARCAS_CAPTCHA, MARCAS_SESSAO_MORTA, consulta,  # noqa: E402
                                     credencial, texto)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SAIDA = BASE / "capturas" / "indicadores"
TETO_REQUISICOES = 12


def data(s: str) -> dt.date:
    return dt.datetime.strptime(s, "%d/%m/%Y").date()


def pedido(args) -> tuple[str, str, dict]:
    """(método, caminho, campos) exatamente como o formulário da tela envia."""
    if args.cmd == "faltas":
        # "Consulta de Absenteismo por Unidade de Saude" — período de EXECUÇÃO, não futuro.
        # Sem --lista: 10 por página e o rodapé "Mostrando Página de N" (total ≈ N×10).
        # Com --lista (imprimir_lista=1): a lista inteira numa página (semana = ~800 linhas, ~40 s).
        return "GET", "/cgi-bin/rel_amb_faltas_sol.pl", {
            "co_solicitacao": "", "ETAPA": "", "ordem": "1", "offset": "0",
            "cnes_solicitante": args.solicitante or "", "cnes_executante": args.executante or "",
            "cns": "", "co_proc": "", "no_proc": "", "data1": args.de, "data2": args.ate,
            **({"imprimir_lista": "1"} if args.lista else {})}
    if args.cmd == "ppi":
        # "Consulta de PPI": por procedimento, PPI Total / Usada / Saldo (+ detalhe por unidade).
        return "POST", "/cgi-bin/cons_ppi_cotas", {
            "tipo": "2" if args.tipo == "exec" else "1", "exec": "330270", "solic": "330270",
            "mes": str(args.mes), "ano": str(args.ano), "ETAPA": "EXIBIR_PPI"}
    if args.cmd == "canceladas":
        # tp_periodo: C = data do cancelamento, M = data da marcação, S = data da solicitação.
        return "POST", "/cgi-bin/cons_marcacao_cancelada", {
            "etapa": "LISTAR_MARCACOES", "tp_periodo": args.periodo, "dt_inicial": args.de,
            "dt_final": args.ate, "co_cnes_ups": args.executante or "", "pagina": "0"}
    if args.cmd == "devolvidas":
        return "POST", "/cgi-bin/cons_negados_reg", {
            "etapa": "LISTAR_SOLICITACOES", "cns_paciente": "", "unidade_adm": args.unidade,
            "cod_procedimento": "", "ds_procedimento": "", "tp_periodo": "dev",
            "dt_inicial": args.de, "dt_final": args.ate, "ordenacao": "2", "pagina": "0",
            "co_solicitacao": ""}
    if args.cmd == "situacao":
        # 3 = Solicitação/Cancelada · 4 = Devolvida · 6 = Negada (listagem sem motivo; motivo só na ficha)
        return "GET", "/cgi-bin/gerenciador_solicitacao", consulta(
            args.sit, data(args.de), data(args.ate), 0, tipo_periodo="S") | {"cnes_solicitante": args.unidade}
    raise SystemExit(f"comando desconhecido: {args.cmd}")


def resumir(html: str) -> str:
    d = BeautifulSoup(html, "html.parser")
    codigos = [c[0] for c in ([x.get_text(" ", strip=True) for x in tr.find_all("td")] for tr in d.find_all("tr"))
               if c and re.fullmatch(r"\d{9,10}", c[0])]
    declarado = re.findall(r"(?:PESQUISADAS|RETORNADAS)\s*\((\d+)\)", html, re.I)
    paginas = re.findall(r"P[aá]gina de (\d+)|exibirPagina\(\s*\d+\s*,\s*(\d+)\s*\)", html)
    ppi = []
    for tr in d.find_all("tr"):
        c = [x.get_text(" ", strip=True) for x in tr.find_all("td")]
        if len(c) >= 7 and c[3].isdigit() and c[4].isdigit():
            ppi.append((int(c[3]), int(c[4])))
    partes = [f"códigos na página={len(codigos)}"]
    if declarado:
        partes.append(f"total declarado={declarado[0]}")
    if paginas:
        partes.append(f"páginas={next(x for x in paginas[0] if x)}")
    if ppi:
        partes.append(f"PPI: {len(ppi)} procedimentos, total={sum(a for a, _ in ppi)}, usada={sum(b for _, b in ppi)}")
    return " | ".join(partes)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    f = sub.add_parser("faltas"); f.add_argument("--de", required=True); f.add_argument("--ate", required=True)
    f.add_argument("--lista", action="store_true"); f.add_argument("--solicitante"); f.add_argument("--executante")
    p = sub.add_parser("ppi"); p.add_argument("--mes", type=int, required=True); p.add_argument("--ano", type=int, required=True)
    p.add_argument("--tipo", choices=["exec", "solic"], default="exec")
    c = sub.add_parser("canceladas"); c.add_argument("--de", required=True); c.add_argument("--ate", required=True)
    c.add_argument("--periodo", choices=["C", "M", "S"], default="C"); c.add_argument("--executante")
    v = sub.add_parser("devolvidas"); v.add_argument("--unidade", required=True)
    v.add_argument("--de", required=True); v.add_argument("--ate", required=True)
    s = sub.add_parser("situacao"); s.add_argument("--sit", type=int, choices=[3, 4, 6], required=True)
    s.add_argument("--unidade", required=True); s.add_argument("--de", required=True); s.add_argument("--ate", required=True)
    args = ap.parse_args(argv)

    env = credencial()
    if not env:
        print("!! credencial SISREG não encontrada", file=sys.stderr)
        return 2
    metodo, caminho, campos = pedido(args)
    SAIDA.mkdir(parents=True, exist_ok=True)
    gastas = 0
    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br", timeout=170) as cli:
        cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"]); gastas += 2
        try:
            t0 = time.time()
            try:
                r = cli.get(caminho, params=campos) if metodo == "GET" else cli.post(caminho, data=campos)
                html = texto(r); gastas += 1
            except Exception as e:  # o SISREG corta a conexão aos ~65 s em consulta pesada
                gastas += 1
                print(f"FALHOU {type(e).__name__} após {time.time() - t0:.0f}s — reduza a janela ou filtre por unidade")
                return 3
            low = html.lower()
            if any(m in low for m in MARCAS_CAPTCHA):
                print("!! CAPTCHA — parando."); return 4
            if any(m in low for m in MARCAS_SESSAO_MORTA):
                print("!! sessão derrubada (o robô de produção relogou) — rode de novo mais tarde."); return 5
            nome = f"{args.cmd}_{time.strftime('%Y%m%d_%H%M%S')}.html"
            (SAIDA / nome).write_text(html, encoding="utf-8")
            print(f"{args.cmd}: {time.time() - t0:.1f}s, {len(html)} bytes | {resumir(html)} -> capturas/indicadores/{nome}")
        finally:
            cli.logout(); gastas += 1
            print(f"[logout] requisições gastas: {gastas} (teto {TETO_REQUISICOES})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
