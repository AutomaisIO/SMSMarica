"""Coleta em SÉRIE (vários meses) dos dados oficiais do SISREG para o relatório de indicadores.

SOMENTE LEITURA. Nasceu em 30/09/2026 para o relatório jan/2025–ago/2026 pedido pelo cliente
(docs/regulacao/). O que cada tela é, e por que cada parâmetro, está em docs/APRENDIZADOS.md
§"Telas para INDICADORES DE REGULAÇÃO" — este script só encadeia as consultas da `sonda_indicadores.py`.

⚠️  Usa o PROGRAMADOR-BERNARDO (mesmo operador do robô de produção): cada login derruba a sessão dos
motores, e o robô ao relogar derruba a nossa. O script aguenta isso: detecta sessão derrubada,
reloga e repete a consulta (com teto de relogins). Só rodar com OK do Bernardo.

Etapas (na ordem de valor ÷ custo), cada uma retomável — o progresso é salvo a cada requisição:
  ppi        cotas PPI por competência (1 req/mês, <1 s)
  canceladas total de marcações canceladas por mês, pela data do cancelamento (1 req/mês)
  desfechos  devolvidas (4) / negadas (6) / canceladas antes de agendar (3) por unidade solicitante;
             primeiro testa se a janela de um ANO por unidade é aceita (senão, cai para meses)
  faltas     lista oficial de faltas em janelas de ~1 semana (`imprimir_lista=1`, ~40 s cada)
  motivos    lê INTEIRA a lista de canceladas de um mês (20 por página) para os motivos
  amostra    motivos por AMOSTRA: 6 páginas espalhadas por mês (~120 cancelamentos/mês) — o padrão

Ritmo: no máximo ~300 requisições/hora (o orçamento anti-robô é do OPERADOR e o robô de produção
também gasta dele). Para em CAPTCHA na hora.

Guarda SÓ: código da solicitação, datas, procedimento, unidade e justificativa. Nada de nome,
endereço, telefone, CNS ou operador. Saída em capturas/indicadores/serie/ (gitignored).

Uso:
  python coletar_serie_indicadores.py --de 2025-01 --ate 2026-08 [--etapas ppi,canceladas,...]
         [--motivos-mes 2026-08] [--max-req 600]
"""
from __future__ import annotations

import argparse
import calendar
import datetime as dt
import json
import pathlib
import re
import sys
import time

from bs4 import BeautifulSoup

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
from sisreg import SisregClient  # noqa: E402
from sonda_fila_gerenciador import MARCAS_CAPTCHA, MARCAS_SESSAO_MORTA, consulta, credencial, texto  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SAIDA = BASE / "capturas" / "indicadores" / "serie"
INTERVALO_MIN = 12.0      # segundos entre requisições → ≤ 300/h
MAX_RELOGINS = 20


class Captcha(Exception):
    pass


def meses(de: str, ate: str) -> list[tuple[int, int]]:
    a, m = map(int, de.split("-")); a2, m2 = map(int, ate.split("-"))
    out = []
    while (a, m) <= (a2, m2):
        out.append((a, m)); m += 1
        if m == 13:
            a, m = a + 1, 1
    return out


def fmt(d: dt.date) -> str:
    return d.strftime("%d/%m/%Y")


def semanas(a: int, m: int) -> list[tuple[dt.date, dt.date]]:
    ult = calendar.monthrange(a, m)[1]
    cortes = [(1, 8), (9, 16), (17, 23), (24, ult)]
    return [(dt.date(a, m, i), dt.date(a, m, f)) for i, f in cortes]


def linhas(html: str, minimo: int) -> list[list[str]]:
    d = BeautifulSoup(html, "html.parser")
    out = []
    for tr in d.find_all("tr"):
        c = [" ".join(x.get_text(" ", strip=True).split()) for x in tr.find_all("td")]
        if len(c) >= minimo and re.fullmatch(r"\d{9,10}", c[0] or ""):
            out.append(c)
    return out


class Coletor:
    def __init__(self, max_req: int):
        self.env = credencial()
        self.cli = SisregClient(base_url=self.env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br", timeout=170)
        self.gastas = 0
        self.relogins = 0
        self.max_req = max_req
        self.ultimo = 0.0
        SAIDA.mkdir(parents=True, exist_ok=True)
        self.prog_path = SAIDA / "progresso.json"
        self.prog = json.loads(self.prog_path.read_text(encoding="utf-8")) if self.prog_path.exists() else {}

    def salvar(self) -> None:
        self.prog_path.write_text(json.dumps(self.prog, ensure_ascii=False), encoding="utf-8")

    def login(self) -> None:
        self.cli.login(self.env["SISREG_USUARIO"], self.env["SISREG_SENHA"]); self.gastas += 2

    def pedir(self, metodo: str, caminho: str, campos: dict) -> str:
        """Uma consulta, com ritmo, relogin se o robô de produção derrubou a sessão, e CAPTCHA = parar."""
        for tentativa in range(3):
            if self.gastas >= self.max_req:
                raise SystemExit(f"teto de {self.max_req} requisições atingido — rode de novo para continuar")
            espera = INTERVALO_MIN - (time.time() - self.ultimo)
            if espera > 0:
                time.sleep(espera)
            self.ultimo = time.time()
            try:
                r = self.cli.get(caminho, params=campos) if metodo == "GET" else self.cli.post(caminho, data=campos)
                html = texto(r)
            except Exception as e:  # corte de conexão (~65 s) ou rede
                self.gastas += 1
                print(f"    ! {type(e).__name__} em {caminho} (tentativa {tentativa + 1})")
                time.sleep(20)
                continue
            self.gastas += 1
            low = html.lower()
            if any(m in low for m in MARCAS_CAPTCHA):
                raise Captcha()
            if any(m in low for m in MARCAS_SESSAO_MORTA) or 'name="senha_256"' in low:
                self.relogins += 1
                if self.relogins > MAX_RELOGINS:
                    raise SystemExit("relogins demais — o robô de produção está disputando a sessão; tente mais tarde")
                print(f"    ~ sessão derrubada (relogin {self.relogins})")
                self.login()
                continue
            return html
        raise RuntimeError(f"falhou 3 vezes: {caminho}")

    # ------------------------------------------------------------------ etapas
    def ppi(self, lista_meses):
        for a, m in lista_meses:
            k = f"ppi:{a}-{m:02d}"
            if k in self.prog:
                continue
            html = self.pedir("POST", "/cgi-bin/cons_ppi_cotas", {
                "tipo": "2", "exec": "330270", "solic": "330270", "mes": str(m), "ano": str(a), "ETAPA": "EXIBIR_PPI"})
            itens = []
            for tr in BeautifulSoup(html, "html.parser").find_all("tr"):
                c = [x.get_text(" ", strip=True) for x in tr.find_all("td")]
                if len(c) >= 7 and c[3].isdigit() and c[4].isdigit():
                    itens.append({"co_unificado": c[0], "co_interno": c[1], "procedimento": c[2],
                                  "total": int(c[3]), "usada": int(c[4]), "saldo": int(c[5]) if c[5].lstrip("-").isdigit() else None,
                                  "tipo": c[6]})
            self.prog[k] = itens; self.salvar()
            print(f"  PPI {a}-{m:02d}: {len(itens)} procedimentos, total={sum(i['total'] for i in itens)}, usada={sum(i['usada'] for i in itens)}")

    def canceladas(self, lista_meses):
        for a, m in lista_meses:
            k = f"canc:{a}-{m:02d}"
            if k in self.prog:
                continue
            ini, fim = dt.date(a, m, 1), dt.date(a, m, calendar.monthrange(a, m)[1])
            html = self.pedir("POST", "/cgi-bin/cons_marcacao_cancelada", {
                "etapa": "LISTAR_MARCACOES", "tp_periodo": "C", "dt_inicial": fmt(ini), "dt_final": fmt(fim),
                "co_cnes_ups": "", "pagina": "0"})
            n = re.findall(r"PESQUISADAS?\s*\((\d+)\)", html, re.I)
            vazio = "nenhum" in html.lower()
            total = int(n[0]) if n else (0 if vazio else None)
            self.prog[k] = total; self.salvar()
            print(f"  canceladas {a}-{m:02d}: {total}")

    def motivos(self, a: int, m: int):
        k = f"motivos:{a}-{m:02d}"
        estado = self.prog.get(k) or {"paginas": None, "lidas": [], "linhas": []}
        ini, fim = dt.date(a, m, 1), dt.date(a, m, calendar.monthrange(a, m)[1])
        pagina = 0
        while True:
            if pagina in estado["lidas"]:
                pagina += 1
                if estado["paginas"] is not None and pagina >= estado["paginas"]:
                    break
                continue
            html = self.pedir("POST", "/cgi-bin/cons_marcacao_cancelada", {
                "etapa": "LISTAR_MARCACOES", "tp_periodo": "C", "dt_inicial": fmt(ini), "dt_final": fmt(fim),
                "co_cnes_ups": "", "pagina": str(pagina)})
            if estado["paginas"] is None:
                p = re.findall(r"exibirPagina\(\s*[^,]+,\s*(\d+)\s*\)", html)
                estado["paginas"] = int(p[0]) if p else 1
                estado["declarado"] = int((re.findall(r"PESQUISADAS?\s*\((\d+)\)", html, re.I) or ["0"])[0])
            for c in linhas(html, 9):
                estado["linhas"].append({"codigo": c[0], "data_marcacao": c[1], "procedimento": c[3],
                                         "justificativa": c[6], "cancelado_em": c[8]})
            estado["lidas"].append(pagina)
            self.prog[k] = estado; self.salvar()
            if pagina % 10 == 0:
                print(f"  motivos {a}-{m:02d}: página {pagina + 1}/{estado['paginas']} ({len(estado['linhas'])} linhas)")
            pagina += 1
            if pagina >= estado["paginas"]:
                break
        print(f"  motivos {a}-{m:02d}: {len(estado['linhas'])} lidas × {estado.get('declarado')} declaradas")

    def amostra_motivos(self, lista_meses, paginas_por_mes: int = 6):
        """Motivos por AMOSTRA: lê `paginas_por_mes` páginas espalhadas pelo mês (a 1ª diz quantas há).

        Ler todos os cancelamentos de 20 meses custaria ~2.400 requisições; a distribuição por
        categoria de motivo não precisa disso. As páginas saem da listagem ordenada da própria tela,
        espalhadas do início ao fim, para não concentrar a amostra em poucos dias.
        """
        for a, m in lista_meses:
            k = f"amostra:{a}-{m:02d}"
            estado = self.prog.get(k) or {"paginas": None, "lidas": [], "linhas": []}
            ini, fim = dt.date(a, m, 1), dt.date(a, m, calendar.monthrange(a, m)[1])

            def ler(pagina: int) -> str:
                return self.pedir("POST", "/cgi-bin/cons_marcacao_cancelada", {
                    "etapa": "LISTAR_MARCACOES", "tp_periodo": "C", "dt_inicial": fmt(ini), "dt_final": fmt(fim),
                    "co_cnes_ups": "", "pagina": str(pagina)})

            if estado["paginas"] is None:
                html = ler(0)
                p = re.findall(r"exibirPagina\(\s*[^,]+,\s*(\d+)\s*\)", html)
                estado["paginas"] = int(p[0]) if p else 1
                estado["declarado"] = int((re.findall(r"PESQUISADAS?\s*\((\d+)\)", html, re.I) or ["0"])[0])
                estado["linhas"] += [{"justificativa": c[6], "cancelado_em": c[8]} for c in linhas(html, 9)]
                estado["lidas"].append(0)
                self.prog[k] = estado; self.salvar()
            total = estado["paginas"]
            alvo = sorted({round(i * (total - 1) / max(1, paginas_por_mes - 1)) for i in range(paginas_por_mes)}) if total > 1 else [0]
            for pg in alvo:
                if pg in estado["lidas"]:
                    continue
                estado["linhas"] += [{"justificativa": c[6], "cancelado_em": c[8]} for c in linhas(ler(pg), 9)]
                estado["lidas"].append(pg)
                self.prog[k] = estado; self.salvar()
            print(f"  amostra motivos {a}-{m:02d}: {len(estado['linhas'])} linhas de {estado.get('declarado')} ({len(estado['lidas'])}/{total} páginas)")

    def unidades(self) -> list[tuple[str, str]]:
        if "unidades" not in self.prog:
            html = self.pedir("GET", "/cgi-bin/cons_negados_reg", {})
            sel = BeautifulSoup(html, "html.parser").find("select", attrs={"name": "unidade_adm"})
            self.prog["unidades"] = [(o.get("value"), o.get_text(strip=True)) for o in sel.find_all("option") if o.get("value")]
            self.salvar()
        return self.prog["unidades"]

    def desfechos(self, de: str, ate: str):
        a1, m1 = map(int, de.split("-")); a2, m2 = map(int, ate.split("-"))
        inicio, fim = dt.date(a1, m1, 1), dt.date(a2, m2, calendar.monthrange(a2, m2)[1])
        unids = self.unidades()
        # teste único: a janela longa (o período todo) é aceita pelo servidor para UMA unidade?
        if "desfecho:janela_longa_ok" not in self.prog:
            teste = consulta(4, inicio, fim, 0, tipo_periodo="S") | {"cnes_solicitante": unids[0][0]}
            html = self.pedir("GET", "/cgi-bin/gerenciador_solicitacao", teste)
            n = re.findall(r"RETORNADAS\s*\((\d+)\)", html)
            ok = bool(n) and len(linhas(html, 12)) == int(n[0])
            self.prog["desfecho:janela_longa_ok"] = ok; self.salvar()
            print(f"  teste janela longa (unidade {unids[0][1][:30]}): declarado={n[:1]} ok={ok}")
        janelas = [(inicio, fim)] if self.prog["desfecho:janela_longa_ok"] else [
            (dt.date(a, m, 1), dt.date(a, m, calendar.monthrange(a, m)[1])) for a, m in meses(de, ate)]
        for cnes, nome in unids:
            for sit in (4, 6, 3):
                for ji, jf in janelas:
                    k = f"desf:{sit}:{cnes}:{ji}:{jf}"
                    if k in self.prog:
                        continue
                    html = self.pedir("GET", "/cgi-bin/gerenciador_solicitacao",
                                      consulta(sit, ji, jf, 0, tipo_periodo="S") | {"cnes_solicitante": cnes})
                    n = re.findall(r"RETORNADAS\s*\((\d+)\)", html)
                    rows = [{"codigo": c[0], "data_solicitacao": c[1], "procedimento": c[7], "situacao": c[11]}
                            for c in linhas(html, 12)]
                    vazio = "nenhum" in html.lower() or (n and n[0] == "0")
                    self.prog[k] = {"declarado": int(n[0]) if n else (0 if vazio else None), "linhas": rows, "unidade": nome}
                    self.salvar()
                    if rows:
                        print(f"  desfecho sit {sit} {nome[:28]:28} {ji}..{jf}: {len(rows)} (declarado {n[:1]})")

    def faltas(self, lista_meses):
        hoje = dt.date.today()
        for a, m in lista_meses:
            for ini, fim in semanas(a, m):
                if fim >= hoje:
                    fim = hoje - dt.timedelta(days=1)
                if ini > fim:
                    continue
                k = f"faltas:{ini}:{fim}"
                if k in self.prog:
                    continue
                t0 = time.time()
                html = self.pedir("GET", "/cgi-bin/rel_amb_faltas_sol.pl", {
                    "co_solicitacao": "", "ETAPA": "", "ordem": "1", "offset": "0", "cnes_solicitante": "",
                    "cnes_executante": "", "cns": "", "co_proc": "", "no_proc": "", "data1": fmt(ini),
                    "data2": fmt(fim), "imprimir_lista": "1"})
                rows = [{"codigo": c[0], "unidade_solicitante": c[1], "data_execucao": c[5], "hora": c[6],
                         "procedimento": c[7]} for c in linhas(html, 8)]
                vazio = "nenhum" in html.lower() or "nao foram encontrad" in html.lower()
                if not rows and not vazio:
                    print(f"  ! faltas {ini}..{fim}: resposta sem linhas e sem 'nenhum' — NÃO gravado, repetir depois")
                    continue
                self.prog[k] = rows; self.salvar()
                print(f"  faltas {ini}..{fim}: {len(rows)} ({time.time() - t0:.0f}s, gastas {self.gastas})")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--de", required=True); ap.add_argument("--ate", required=True)
    ap.add_argument("--etapas", default="ppi,canceladas,desfechos,faltas,amostra")
    ap.add_argument("--motivos-mes", default=None)
    ap.add_argument("--max-req", type=int, default=600)
    args = ap.parse_args(argv)
    lista = meses(args.de, args.ate)
    c = Coletor(args.max_req)
    c.login()
    print(f"[login ok] {len(lista)} meses, etapas={args.etapas}")
    try:
        for etapa in args.etapas.split(","):
            print(f"== {etapa}")
            if etapa == "ppi":
                c.ppi(lista)
            elif etapa == "canceladas":
                c.canceladas(lista)
            elif etapa == "desfechos":
                c.desfechos(args.de, args.ate)
            elif etapa == "faltas":
                c.faltas(lista)
            elif etapa == "motivos":
                a, m = map(int, (args.motivos_mes or args.ate).split("-"))
                c.motivos(a, m)
            elif etapa == "amostra":
                c.amostra_motivos(lista)
    except Captcha:
        print("!! CAPTCHA — PARANDO. Um humano precisa abrir o SISREG no navegador com esse operador.")
        return 4
    finally:
        try:
            c.cli.logout(); c.gastas += 1
        except Exception:
            pass
        c.salvar()
        print(f"[fim] requisições gastas: {c.gastas}, relogins: {c.relogins} — progresso em {c.prog_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
