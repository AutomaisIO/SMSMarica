"""Sonda da Agenda — ETAPA 2: pesquisa de paciente e carga da grade. Ainda SOMENTE LEITURA.

Faz os dois POSTs que NÃO alteram nada:
  1. `imbConsulta` — pesquisa o paciente por CPF/CNS (é a lupa da tela);
  2. `RadAjaxManager1` com `REFRESH` — manda a agenda se desenhar, que é de onde saem os
     `hiAgendaId` dos slots.

Serve para responder, antes de qualquer escrita: quem é o paciente por trás de um CPF, qual o
`pacienteId` dele, e quais slots existem na agenda.

Uso:  python probe_agenda_busca.py <CPF ou CNS> [GUID-da-unidade]
"""

from __future__ import annotations

import os
import re
import sys

from prime.client import (APP, CAP, LIBERADOS, PrimeSession, action_do_form, campos_todos, sopa)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL_AGENDA = f"{APP}/Agendamento/AgendaRecepcao.aspx"
GUID = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", re.I)


def curto(nome: str) -> str:
    return nome.rsplit("$", 1)[-1]


def nome_longo(html: str, sufixo: str) -> str | None:
    """Nome completo do controle WebForms a partir do sufixo (ctl00$...$txtPaciente)."""
    for m in re.finditer(r'name="([^"]+)"', html):
        if curto(m.group(1)) == sufixo:
            return m.group(1)
    return None


def descreve_grade(html: str) -> list[str]:
    d = sopa(html)
    grade = d.find(id=re.compile(r"gridCompromissos$"))
    if not grade:
        return []
    ids = []
    for tr in grade.find_all("tr"):
        for td in tr.find_all("td"):
            t = td.get_text(strip=True)
            if GUID.fullmatch(t):
                # a linha inteira, para saber de quem é o slot
                texto = " | ".join(x.get_text(strip=True) for x in tr.find_all("td"))
                ids.append((t, texto[:150]))
                break
    return ids


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    busca = re.sub(r"\D", "", sys.argv[1])
    if len(sys.argv) > 2:
        os.environ["PRIME_UNIDADE"] = sys.argv[2]
    por = "CPF" if len(busca) == 11 else "CNS"

    with PrimeSession() as s:
        s.entrar()
        html = s.get(URL_AGENDA).text

        # ---------------------------------------------------- 1. pesquisar o paciente
        originais = campos_todos(html)
        dados = dict(originais)
        n_txt = nome_longo(html, "txtPaciente")
        n_rbl = nome_longo(html, "rblPesquisarPor")
        if not n_txt or not n_rbl:
            raise SystemExit("campos de busca não encontrados na tela")
        dados[n_txt] = busca
        dados[n_rbl] = por
        dados["__EVENTTARGET"] = ""
        dados["__EVENTARGUMENT"] = ""
        # A lupa é um ImageButton: o navegador posta as coordenadas do clique.
        n_lupa = nome_longo(html, "imbConsulta") or "imbConsulta"
        dados[f"{n_lupa}.x"] = "10"
        dados[f"{n_lupa}.y"] = "10"

        # A trava do laboratório barra por NOME, e nesta tela 21 dos 169 campos do form têm nome
        # "perigoso" (`hidCancelarAcolhimento`, `hiAgendaId`, `rcboMotivoCancelamentoAcolhimento`…)
        # embora cheguem todos vazios ou em "Selecione" — o navegador os manda em qualquer POST,
        # inclusive no da lupa. Liberamos SÓ os que devolvemos IDÊNTICOS ao que a tela entregou.
        # Tudo que eu alterar ou acrescentar (um botão `rbSalvar…`, por exemplo) segue barrado.
        LIBERADOS.update(k for k, v in dados.items() if k in originais and originais[k] == v)

        print(f"pesquisando por {por} = {busca[:3]}...{busca[-2:]}")
        r = s.post(action_do_form(html, URL_AGENDA), dados)
        h2 = r.text
        (CAP / "agenda_busca.html").write_text(h2, encoding="utf-8")
        print(f"  status={r.status_code}  {len(h2):,} chars")

        # O resultado da busca é uma lista; o id do paciente aparece no SELECIONAR_PACIENTE
        # que a linha dispara, e/ou no hidden hidPacienteId.
        achados = sorted(set(re.findall(r"SELECIONAR_PACIENTE\|([0-9a-f-]{36})", h2)))
        print(f"  pacientes na lista: {len(achados)}")
        for g in achados[:5]:
            # nome mais próximo do guid, para conferência humana
            pos = h2.find(g)
            trecho = re.sub(r"<[^>]+>", " ", h2[max(0, pos - 500):pos + 200])
            trecho = re.sub(r"\s+", " ", trecho).strip()
            print(f"    {g}")
            print(f"       ...{trecho[-220:]}")

        i = sopa(h2).find("input", attrs={"name": re.compile(r"hidPacienteId$")})
        print("  hidPacienteId na tela:", (i.get("value") if i else None) or "(vazio)")

        # ---------------------------------------------------- 2. carregar a agenda
        dados2 = campos_todos(h2)
        n_mgr = nome_longo(h2, "RadAjaxManager1")
        if n_mgr:
            dados2["__EVENTTARGET"] = n_mgr
            dados2["__EVENTARGUMENT"] = "REFRESH"
            r2 = s.post(action_do_form(h2, URL_AGENDA), dados2, ajax=True)
            h3 = r2.text
            (CAP / "agenda_refresh.html").write_text(h3, encoding="utf-8")
            slots = descreve_grade(h3)
            print(f"\nREFRESH: status={r2.status_code}  {len(h3):,} chars  slots com GUID: {len(slots)}")
            for g, linha in slots[:10]:
                print(f"   {g}  {linha}")
        else:
            print("\nRadAjaxManager1 não encontrado — REFRESH não enviado")

        print("\nNenhum POST de escrita foi feito. Liberados na trava:", LIBERADOS or "(nenhum)")


if __name__ == "__main__":
    main()
