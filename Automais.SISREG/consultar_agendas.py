"""Consulta a Agenda de Profissional (cons_agendas) — SOMENTE LEITURA.

etapa=ListaConsulta lista os agendamentos de uma unidade (ups=CNES) num
intervalo de datas dd/mm/aaaa. NÃO usa etapas de escrita (Confirma/Falta).

Uso:
    python consultar_agendas.py 01/07/2026 07/07/2026 [CNES]

Salva o HTML em capturas/ e imprime SÓ a estrutura (cabeçalhos + nº de linhas),
sem despejar PII de paciente no terminal.
"""

from __future__ import annotations

import os
import pathlib
import sys

from dotenv import load_dotenv
from bs4 import BeautifulSoup

from sisreg import SisregClient, SisregLoginError


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("uso: python consultar_agendas.py DD/MM/AAAA DD/MM/AAAA [CNES]", file=sys.stderr)
        return 2
    dt_ini, dt_fim = argv[0], argv[1]
    cnes = argv[2] if len(argv) > 2 else None

    load_dotenv()
    usuario = os.getenv("SISREG_USUARIO", "").strip()
    senha = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            estado = cli.conectar(usuario, senha)
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"sessão: {estado}")

        # descobrir a UPS default se não informada (1a opção não-vazia do select)
        if not cnes:
            tela = cli.get("/cgi-bin/cons_agendas").text
            soup = BeautifulSoup(tela, "html.parser")
            sel = soup.find("select", {"name": "ups"})
            opts = [o.get("value") for o in sel.find_all("option") if o.get("value")] if sel else []
            cnes = opts[0] if opts else ""
            print(f"UPS disponíveis no perfil: {opts}  -> usando {cnes!r}")

        data = {
            "co_solicitacao": "",
            "cns_paciente": "",
            "dataInicial": dt_ini,
            "dataFinal": dt_fim,
            "ups": cnes,
            "cpf": "",
            "pa": "",
            "cmbTipoOperacao": "",
            "chkboxExibirProcedimentos": "on",
            "cmbOrdenacao": "1",
            "cmbMaxResults": "50",
            "etapa": "ListaConsulta",
            "pagina": "0",
            "linhas": "0",
        }
        r = cli.post("/cgi-bin/cons_agendas", data=data)
        print(f"POST cons_agendas ListaConsulta status={r.status_code} len={len(r.text)}")
        out = cap / f"agendas_{cnes}_{dt_ini.replace('/', '')}_{dt_fim.replace('/', '')}.html"
        out.write_text(r.text, encoding="utf-8")
        print(f"salvo: {out}")

        soup = BeautifulSoup(r.text, "html.parser")
        # mensagem eventual
        msg = soup.find(id="mensagem")
        if msg and msg.get_text(strip=True):
            print(f"mensagem: {msg.get_text(' ', strip=True)!r}")

        # tenta achar a tabela de resultados (a maior por nº de linhas)
        tables = soup.find_all("table")
        if not tables:
            print("nenhuma tabela encontrada.")
            return 0
        alvo = max(tables, key=lambda t: len(t.find_all("tr")))
        rows = alvo.find_all("tr")
        print(f"\ntabela de resultados: {len(rows)} linhas (inclui cabeçalho)")
        if rows:
            headers = [c.get_text(" ", strip=True) for c in rows[0].find_all(["th", "td"])]
            print(f"colunas ({len(headers)}): {headers}")
            # amostra ANONIMIZADA: nº de células por linha, sem conteúdo
            print(f"linhas de dados: {max(0, len(rows) - 1)} (conteúdo omitido — PII)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
