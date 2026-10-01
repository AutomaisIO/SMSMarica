"""Lista oficial de faltas do SISREG ANTES de 2025, por unidade executante — o passado da ficha do paciente.

SOMENTE LEITURA no SISREG. Nasceu em 01/10/2026 (pedido do Bernardo: "backfill do passado de quem
veio"). A ficha do paciente separa Compareceu / Faltou / Em aberto; o "faltou" sai da lista oficial
(`rel_amb_faltas_sol.pl`), e ela só estava carregada de 02/01/2025 em diante. Antes disso, ~360 mil
agendamentos passados aparecem sem resposta.

Por que POR UNIDADE EXECUTANTE (e não a rede inteira, como `coletar_serie_indicadores.py`):
medido em 01/10/2026 à tarde, a lista da rede inteira de 2 dias de dez/2024 passa do corte de ~65 s
do SISREG (conexão derrubada), e até 2 dias de ago/2026 levam 50 s. Com `cnes_executante`, uma
semana do Péricles saiu em 8 s e o MÊS inteiro em 29 s. Antes de 2025 eram só 13 executantes — as
que têm agendamento nosso nessa época (tabela abaixo, medida no banco em 01/10/2026).

Janela = um mês por unidade. Se o SISREG cortar a conexão, a janela se parte ao meio (até 3 vezes)
em vez de repetir a mesma consulta pesada.

Usa o PROGRAMADOR-BERNARDO, como o coletor da série — derruba a sessão do robô de produção enquanto
roda. Só com OK do Bernardo. Ritmo ≤ 300 req/h; CAPTCHA = parar.

Guarda só código da solicitação, data/hora de execução, procedimento e unidade solicitante. Saída em
capturas/indicadores/faltas_historico/ (gitignored). Quem grava em produção é
`carregar_faltas_historico_prod.py`.

Uso:
  python coletar_faltas_historico.py --de 2024-07 --ate 2024-12 [--executantes 2266741,3132358] [--max-req 60]
"""
from __future__ import annotations

import argparse
import calendar
import datetime as dt
import json
import pathlib
import sys

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
import coletar_serie_indicadores as serie  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SAIDA = BASE / "capturas" / "indicadores" / "faltas_historico"
MAX_DIVISOES = 3

# Executantes com agendamento nosso antes de 2025: CNES -> (nome, primeiro mês, último mês).
# Medido em produção em 01/10/2026 (solicitacao Agendada, sem chegada na recepção, data < 2025).
EXECUTANTES: dict[str, tuple[str, str, str]] = {
    "2266741": ("AMBULATORIO PERICLES SIQUEIRA FERREIRA", "2019-10", "2024-12"),
    "3132358": ("CDT - DR ALBERTO LUIS M. BORGES", "2019-10", "2024-12"),
    "9895124": ("ERNESTO CHE GUEVARA SMSM", "2021-06", "2024-12"),
    "2930242": ("CENTRO MATERNO INFANTIL", "2022-06", "2024-12"),
    "9834540": ("CENTRO DE RADIOLOGIA MARICA", "2020-10", "2024-12"),
    "5833841": ("RADIOCENTER", "2019-11", "2024-12"),
    "2266733": ("HOSPITAL MUNICIPAL CONDE MODESTO LEAL", "2019-12", "2024-12"),
    "5874211": ("CEO", "2020-07", "2024-12"),
    "2285134": ("DIMAGEM", "2024-09", "2024-12"),
    "2266881": ("UNIDADE DE SAUDE DA FAMILIA CENTRAL", "2024-01", "2024-12"),
    "4256387": ("CENTRO DE REABILITACAO AMBULATORIAL E DOMICILIAR", "2024-04", "2024-12"),
    "2266784": ("UNIDADE DE SAUDE DA FAMILIA JARDIM ATLANTICO", "2024-02", "2024-12"),
    "9082379": ("UNIDADE DE SAUDE DA FAMILIA CORDERINHO", "2024-01", "2024-12"),
}


def ativa(cnes: str, a: int, m: int) -> bool:
    _, de, ate = EXECUTANTES[cnes]
    return de <= f"{a}-{m:02d}" <= ate


class ColetorHistorico(serie.Coletor):
    def __init__(self, max_req: int):
        super().__init__(max_req)
        SAIDA.mkdir(parents=True, exist_ok=True)
        self.prog_path = SAIDA / "progresso.json"
        self.prog = json.loads(self.prog_path.read_text(encoding="utf-8")) if self.prog_path.exists() else {}

    def janela(self, cnes: str, ini: dt.date, fim: dt.date, divisoes: int = 0) -> None:
        k = f"{cnes}:{ini}:{fim}"
        if k in self.prog:
            return
        try:
            html = self.pedir("GET", "/cgi-bin/rel_amb_faltas_sol.pl", {
                "co_solicitacao": "", "ETAPA": "", "ordem": "1", "offset": "0", "cnes_solicitante": "",
                "cnes_executante": cnes, "cns": "", "co_proc": "", "no_proc": "", "data1": serie.fmt(ini),
                "data2": serie.fmt(fim), "imprimir_lista": "1"}, repetir_em_erro=False)
        except serie.ConexaoCortada:
            if divisoes >= MAX_DIVISOES or ini == fim:
                print(f"  ! {cnes} {ini}..{fim}: cortada e não dá mais para partir — fica para depois")
                return
            meio = ini + (fim - ini) // 2
            print(f"  ~ {cnes} {ini}..{fim}: cortada; partindo em {ini}..{meio} e {meio + dt.timedelta(days=1)}..{fim}")
            metades = [(ini, meio), (meio + dt.timedelta(days=1), fim)]
            for a, b in metades:
                self.janela(cnes, a, b, divisoes + 1)
            # As duas metades lidas = a janela inteira lida: marca, para a próxima rodada não pagar de
            # novo a consulta que já se sabe que corta. Quem carrega usa só as metades (têm as linhas).
            if all(f"{cnes}:{a}:{b}" in self.prog for a, b in metades):
                self.prog[k] = {"partida_em": [f"{a}:{b}" for a, b in metades]}
                self.salvar()
            return

        rows = [{"codigo": c[0], "unidade_solicitante": c[1], "data_execucao": c[5], "hora": c[6],
                 "procedimento": c[7]} for c in serie.linhas(html, 8)]
        low = html.lower()
        # Com `cnes_executante` preenchido, "nenhuma falta" NÃO vem escrito: o SISREG devolve só o
        # formulário de pesquisa (13.557 bytes). Provado em 01/10/2026 com controle de resposta
        # conhecida — DIMAGEM em jan/2024, antes de ter agenda — idêntica byte a byte à do Conde. E
        # bate com o banco: Conde e USFs não apontavam nada em 2024 (0 confirmados), e o Centro de
        # Radiologia só passou a apontar falta em nov/2024 (out: 142 pendentes, lista vazia; nov: 552
        # de 554 pendentes na lista). O atributo vem SEM aspas (`name=cnes_executante`).
        so_formulario = "consulta de absenteismo" in low and "cnes_executante" in low
        vazio = "nenhum" in low or "nao foram encontrad" in low or so_formulario
        if not rows and not vazio:
            # Guarda a resposta para entender o que veio (não é lista nem "nenhum"). Página de erro ou
            # de filtro inválido não tem dado de paciente, mas fica em capturas/ (gitignored) mesmo assim.
            estranhas = SAIDA / "respostas_estranhas"
            estranhas.mkdir(parents=True, exist_ok=True)
            (estranhas / f"{cnes}_{ini}_{fim}.html").write_text(html, encoding="utf-8")
            print(f"  ! {cnes} {ini}..{fim}: resposta sem linhas e sem 'nenhum' ({len(html)} bytes) — NÃO gravado, guardada em respostas_estranhas/")
            return

        # Conferência de layout: a data de execução tem de cair dentro da janela pedida. Se a tela
        # mudou de colunas, isto pega antes de gravar lixo como falta.
        for r in rows:
            try:
                d = dt.datetime.strptime(r["data_execucao"], "%d/%m/%Y").date()
            except ValueError:
                raise SystemExit(f"layout inesperado em {cnes} {ini}..{fim}: data_execucao={r['data_execucao']!r}")
            if not ini <= d <= fim:
                raise SystemExit(f"data fora da janela em {cnes} {ini}..{fim}: {d}")

        self.prog[k] = {"lido_em": dt.datetime.now(dt.timezone.utc).isoformat(), "linhas": rows}
        self.salvar()
        print(f"  {EXECUTANTES[cnes][0][:34]:34} {ini}..{fim}: {len(rows):5} faltas (gastas {self.gastas})")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--de", required=True)
    ap.add_argument("--ate", required=True)
    ap.add_argument("--executantes", default=None, help="CNES separados por vírgula (padrão: todos os ativos no mês)")
    ap.add_argument("--max-req", type=int, default=60)
    args = ap.parse_args(argv)

    if args.ate > "2024-12":
        print("Este coletor é do passado SEM lista carregada (até 2024-12). De 2025 em diante quem lê é a plataforma.")
        return 2
    escolhidos = args.executantes.split(",") if args.executantes else list(EXECUTANTES)
    desconhecidos = [c for c in escolhidos if c not in EXECUTANTES]
    if desconhecidos:
        print(f"CNES fora da tabela: {desconhecidos}")
        return 2

    meses = serie.meses(args.de, args.ate)
    arquivo = SAIDA / "progresso.json"
    feitas = json.loads(arquivo.read_text(encoding="utf-8")) if arquivo.exists() else {}
    pendentes = [(c, a, m) for a, m in reversed(meses) for c in escolhidos if ativa(c, a, m)
                 and f"{c}:{dt.date(a, m, 1)}:{dt.date(a, m, calendar.monthrange(a, m)[1])}" not in feitas]
    print(f"{len(pendentes)} janela(s) unidade×mês a ler (do mais recente para o mais antigo)")
    if not pendentes:
        return 0

    col = ColetorHistorico(args.max_req)
    col.login()
    try:
        for cnes, a, m in pendentes:
            col.janela(cnes, dt.date(a, m, 1), dt.date(a, m, calendar.monthrange(a, m)[1]))
    except serie.Captcha:
        print("!! CAPTCHA — PARANDO. Um humano precisa abrir o SISREG no navegador com esse operador.")
        return 4
    except SystemExit as e:
        print(f"[parada] {e}")
    finally:
        try:
            col.cli.logout()
            col.gastas += 1
        except Exception:
            pass
        col.salvar()
        print(f"[fim] requisições gastas: {col.gastas}, relogins: {col.relogins} — progresso em {col.prog_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
