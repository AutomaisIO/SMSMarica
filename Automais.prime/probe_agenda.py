"""Sonda da Agenda da recepção — ETAPA 1: SOMENTE LEITURA.

Entra, abre `Agendamento/AgendaRecepcao.aspx` e descreve o que a tela entrega sem postar nada
de escrita: a unidade da sessão, o `__EVENTVALIDATION`, os slots da grade (`hiAgendaId`) e as
opções do form de busca de paciente.

É a etapa que responde, sem tocar em dado nenhum: um GET limpo já traz os ids de agenda que o
`Acolher|<guid>` usa? Qual unidade a sessão assume? Como se pesquisa um paciente?

Uso:  python probe_agenda.py [GUID-da-unidade]
"""

from __future__ import annotations

import re
import sys

from prime.client import APP, CAP, PrimeSession, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL_AGENDA = f"{APP}/Agendamento/AgendaRecepcao.aspx"
GUID = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", re.I)


def curto(nome: str) -> str:
    return nome.rsplit("$", 1)[-1]


def main() -> None:
    if len(sys.argv) > 1:
        import os
        os.environ["PRIME_UNIDADE"] = sys.argv[1]

    with PrimeSession() as s:
        s.entrar()
        r = s.get(URL_AGENDA)
        html = r.text
        (CAP / "agenda_recepcao.html").write_text(html, encoding="utf-8")
        print(f"\nGET {r.url}  status={r.status_code}  {len(html):,} chars")

        d = sopa(html)
        titulo = d.find("title")
        print("titulo:", titulo.get_text(strip=True) if titulo else "(sem)")

        # --- estado do servidor que um POST precisaria repetir
        for nome in ("__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION", "__PREVIOUSPAGE"):
            i = d.find("input", attrs={"name": nome})
            v = (i.get("value") or "") if i else ""
            print(f"  {nome:22} {len(v):6,} bytes   {v[:12]}")

        # --- contexto da sessão
        for nome in ("hidUnidadeId", "hidSER2IntegracaoLigada", "hidCadecoIntegracaoLigada",
                     "hidUnidadeEhPoliclinica", "hidPesquisaPadraoSimilaridadeFonetica"):
            achados = [i for i in d.find_all("input") if curto(i.get("name") or "") == nome]
            for i in achados[:1]:
                print(f"  {nome:38} {i.get('value') or '(vazio)'}")

        # --- a grade: é dela que saem os hiAgendaId do Acolher
        grade = d.find(id=re.compile(r"gridCompromissos$"))
        if grade:
            linhas = grade.find_all("tr")
            ids = []
            for tr in linhas:
                for td in tr.find_all("td"):
                    t = td.get_text(strip=True)
                    if GUID.fullmatch(t):
                        ids.append(t)
                        break
            print(f"\ngrade gridCompromissos: {len(linhas)} linhas, {len(ids)} com GUID de agenda")
            for g in ids[:8]:
                print("   slot", g)
            if len(ids) > 8:
                print(f"   ... mais {len(ids) - 8}")
        else:
            print("\ngrade gridCompromissos NAO encontrada neste GET (a agenda vem vazia sem filtro?)")

        # --- como se pesquisa um paciente
        for nome in ("rblPesquisarPor", "rblTurnoAgendaProfissional", "rblTipoVisualizacaoAgenda"):
            itens = [i for i in d.find_all("input", attrs={"type": "radio"})
                     if curto(i.get("name") or "").startswith(nome)]
            if not itens:
                continue
            print(f"\n{nome}:")
            for i in itens:
                lab = i.find_next("label")
                marcado = " <= marcado" if i.has_attr("checked") else ""
                print(f"   {i.get('value')!r:34} {lab.get_text(strip=True) if lab else ''}{marcado}")

        # --- profissionais disponíveis (combo do Telerik guarda no ClientState/hidden)
        for nome in ("rcboProfissional", "rcboUsuario"):
            i = d.find("input", attrs={"name": re.compile(re.escape(nome) + "$")})
            if i:
                print(f"\n{nome}: {i.get('value') or '(vazio)'}")

        print("\ncapturas/agenda_recepcao.html gravado. Nenhum POST de escrita foi feito.")


if __name__ == "__main__":
    main()
