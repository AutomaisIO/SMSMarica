"""Teste do caminho PONTUAL do botao "Importar" (durante o horario comercial,
com o expo_solicitacoes bloqueado 8h-15h): puxar a agenda de UM dia via
cons_agendas (paginado, SEM bloqueio de horario).

Alvo: CMI (2930242) x RENATO ROQUE (08675605765) x 0229000 (GRUPO - USG GESTANTES).
SOMENTE LEITURA (etapa=ListaConsulta). Reaproveita a sessao PROGRAMADOR ja salva.

Uso:
    python teste_consagendas_dia.py                 # dia = hoje (26/08/2026)
    python teste_consagendas_dia.py 26/08/2026      # um dia
    python teste_consagendas_dia.py 26/08/2026 28/08/2026
"""
from __future__ import annotations

import pathlib
import sys
import time

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except AttributeError:
    pass

BASE = pathlib.Path(__file__).parent
sys.path.insert(0, str(BASE))
from sisreg import SisregClient  # noqa: E402
from extrair_agenda_unidade import parse_registros, total_paginas  # noqa: E402

CAP = BASE / "capturas"
SESSAO = CAP / ".sessao_programador.json"
ENV_SISCAN = BASE.parent / "Automais.SISCAN" / ".env"

CNES_CMI = "2930242"
CPF_RENATO = "08675605765"
NOME_RENATO = "RENATO ROQUE DE ANDRADE"
PA = "0229000"
PA_NOME = "GRUPO - ULTRASSONOGRAFIA GESTANTES"

# Marcadores ESPECIFICOS da parede de captcha (nao o api.js benigno).
MARCAS_CAPTCHA = ("recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85", 'class="g-recaptcha"')
MARCAS_SESSAO_MORTA = ("sisreg_erro", "erro ao carregar a sessao", "logon em outra", "foi finalizada")


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        if chave.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[chave.strip()] = valor.strip()
    return d


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


def consultar(cli, dt_ini, dt_fim, pagina=0) -> str:
    dados = {
        "co_solicitacao": "", "cns_paciente": "",
        "dataInicial": dt_ini, "dataFinal": dt_fim,
        "ups": CNES_CMI, "cpf": CPF_RENATO, "pa": PA,
        "cmbTipoOperacao": "Consulta",
        "chkboxExibirProcedimentos": "on", "chkboxExibirTelefones": "on",
        "cmbOrdenacao": "1", "cmbMaxResults": "50",
        "etapa": "ListaConsulta", "pagina": str(pagina), "linhas": "0",
    }
    time.sleep(0.4)
    html = texto(cli.post("/cgi-bin/cons_agendas", data=dados))
    baixo = html.lower()
    if any(m in baixo for m in MARCAS_CAPTCHA):
        raise SystemExit("!! CAPTCHA real na cons_agendas — parar; humano resolve no navegador.")
    return html


def main(argv: list[str]) -> int:
    if len(argv) >= 2:
        dt_ini, dt_fim = argv[0], argv[1]
    elif len(argv) == 1:
        dt_ini = dt_fim = argv[0]
    else:
        dt_ini = dt_fim = "26/08/2026"  # hoje

    env = ler_env(ENV_SISCAN)
    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
            cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
            cli.salvar_sessao(SESSAO)
            print("login: novo")
        else:
            print("login: sessao reaproveitada")

        print(f"consulta cons_agendas: CMI={CNES_CMI} prof={NOME_RENATO} pa={PA} periodo={dt_ini}..{dt_fim}")
        html = consultar(cli, dt_ini, dt_fim, 0)
        baixo = html.lower()
        if any(m in baixo for m in MARCAS_SESSAO_MORTA):
            print("sessao morta — relogando e repetindo…")
            cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"]); cli.salvar_sessao(SESSAO)
            html = consultar(cli, dt_ini, dt_fim, 0)

        npag = total_paginas(html)
        prof = (CPF_RENATO, NOME_RENATO)
        pa = (PA, PA_NOME)
        registros = list(parse_registros(html, prof, pa))
        reqs = 1
        for pagina in range(1, npag):
            html = consultar(cli, dt_ini, dt_fim, pagina)
            registros += parse_registros(html, prof, pa)
            reqs += 1

        (CAP / f"_consagendas_dia_{dt_ini.replace('/','')}.html").write_text(html, encoding="utf-8")
        print(f"paginas={npag} requisicoes_consulta={reqs} agendamentos={len(registros)}\n")

        cols = ["co_solicitacao", "data", "hora", "situacao", "paciente", "cns",
                "telefones", "unidade_solicitante", "cnes_solicitante", "cid10",
                "vaga_consumida", "procedimentos"]
        for i, r in enumerate(registros, 1):
            print(f"--- agendamento {i} ---")
            for c in cols:
                print(f"   {c:<20} {r.get(c,'')}")
        if not registros:
            print("(0 agendamentos nesse dia p/ esse prof+procedimento)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
