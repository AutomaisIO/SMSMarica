"""Sonda 3 — o ESUS sabe se o paciente COMPARECEU ao exame agendado? (01/10/2026)

A lista de agendados (`controller-paciente-agendado-fila-exames/buscar`) não traz efetivação. O front
tem duas telas que trazem:

- "Histórico de Atendimentos do Paciente" → `pacientes/controller-paciente/buscar-historico-geral-paciente`
  com corpo `{"arrFiltro": {pes_id, mod_id, periodoInicial, periodoFinal, rdg_regulacao, limiteInicio,
  limiteFim}}` (NÃO é `arrFormData`), e o detalhe da efetivação em
  `buscar-detalhes-historico-exame-efetivacao-paciente` `{"idExame": id}`;
- o modal de agendamento usa `exames2/controller-exame-paciente-exames-2/buscar-historico-de-exames-agendados`,
  que devolve `exe_id_exames_efetivacao`: 1 = EM ABERTO, 2 = EFETIVADA, 3 = NÃO EFETIVADA.

SOMENTE LEITURA (a trava do client recusa o resto). Custo: login (3) + 1 por paciente + 1 detalhe.
O que identifica paciente vai para `capturas/` (gitignored); o terminal só mostra contagens e nomes
de campo.

    python probe_historico_paciente.py --amostra 4
"""
from __future__ import annotations

import argparse
import collections
import datetime as dt
import json
import pathlib
import random

from esus.client import CAP, sessao

HIST = "pacientes/controller-paciente/buscar-historico-geral-paciente"
DET_EFET = "pacientes/controller-paciente/buscar-detalhes-historico-exame-efetivacao-paciente"
# O que a tela chama ao clicar numa linha de EXAME do histórico (searchDetailsHistoryExamPatient).
DET_EXAME = "pacientes/controller-paciente/buscar-detalhes-historico-exame-paciente"


def _data(s: str | None) -> dt.date | None:
    try:
        return dt.datetime.strptime((s or "")[:10], "%d/%m/%Y").date()
    except ValueError:
        return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--amostra", type=int, default=4, help="quantos pacientes (agendamento já passado)")
    ap.add_argument("--pes", help="JSON com [{pes_id, unidade, data}] — usa estes em vez da captura de 2019")
    a = ap.parse_args()

    agendados = json.loads((CAP / "agendados_exame_20260930.json").read_text(encoding="utf-8"))
    hoje = dt.date.today()
    passados = [l for l in agendados if (d := _data(l.get("data_hora_formatada") or l.get("data_agendada"))) and d < hoje - dt.timedelta(days=7)]
    random.seed(20261001)
    # Metade bem antiga, metade recente: o comparecimento pode depender da idade.
    passados.sort(key=lambda l: _data(l.get("data_hora_formatada") or l.get("data_agendada")))
    metade = max(1, a.amostra // 2)
    escolhidos = random.sample(passados[: len(passados) // 2], metade) + random.sample(passados[len(passados) // 2:], a.amostra - metade)
    if a.pes:
        escolhidos = [{"pes_id": x["pes_id"], "data_hora_formatada": f"{x['data']} ({x['unidade']})"}
                      for x in json.loads(pathlib.Path(a.pes).read_text(encoding="utf-8"))]

    bruto: dict[str, object] = {}
    with sessao() as s:
        campos = collections.Counter()
        status = collections.Counter()
        exemplo_id = None
        for l in escolhidos:
            pes_id = l["pes_id"]
            corpo = {"pes_id": pes_id, "mod_id": None, "periodoInicial": "01/01/2015",
                     "periodoFinal": hoje.strftime("%d/%m/%Y"), "rdg_regulacao": None,
                     "limiteInicio": 0, "limiteFim": 100}
            dados = s.legado(HIST, None, arrFiltro=corpo)
            linhas = dados.get("recordSet", []) if isinstance(dados, dict) else dados
            bruto[str(pes_id)] = {"agendamento_da_lista": l, "historico": dados}
            print(f"pes_id ...{str(pes_id)[-3:]} — agendado {l.get('data_hora_formatada')} — {len(linhas)} linha(s) no histórico")
            for h in linhas:
                campos.update(h.keys())
                for k, v in h.items():
                    if any(t in k.lower() for t in ("situa", "status", "efet", "modulo", "tipo", "compar")):
                        status[(k, str(v)[:40])] += 1
                if exemplo_id is None and str(h.get("id_modulo", "")) and h.get("id"):
                    exemplo_id = h.get("id")
        print("\nCAMPOS do histórico:", sorted(campos))
        print("\nVALORES de situação/efetivação/módulo:")
        for (k, v), n in sorted(status.items()):
            print(f"  {k} = {v!r}: {n}")
        if exemplo_id:
            ids = [int(x) for x in str(exemplo_id).split(",") if x.strip().isdigit()]
            if ids:
                det = s.legado(DET_EFET, None, idExame=ids[0])
                bruto["detalhe_efetivacao"] = det
                linha = det[0] if isinstance(det, list) and det else det
                print("\nDETALHE de efetivação — campos:", sorted(linha.keys()) if isinstance(linha, dict) else type(det))

        # Detalhe de cada linha de exame — como a tela faz (id pode vir "78090,78093").
        det_campos = collections.Counter()
        det_valores = collections.Counter()
        detalhes = {}
        for chave, v in list(bruto.items()):
            if not isinstance(v, dict) or "historico" not in v:
                continue
            h = v["historico"]
            for linha in (h.get("recordSet", []) if isinstance(h, dict) else h):
                for x in str(linha.get("id", "")).split(","):
                    if not x.strip().isdigit():
                        continue
                    d = s.legado(DET_EXAME, None, idExame=int(x))
                    detalhes[x] = d
                    for r in (d if isinstance(d, list) else [d]):
                        if not isinstance(r, dict):
                            continue
                        det_campos.update(r.keys())
                        for k, val in r.items():
                            if any(t in k.lower() for t in ("efet", "situa", "status", "compar", "falt", "realiz", "exe_id", "tlg_nome")):
                                det_valores[(k, str(val)[:40])] += 1
                        if r.get("tlg_nome") or r.get("efl_id_exames_efetivacao"):
                            print(f"   exame {x}: data_exame={r.get('data_exame')!r} tlg={r.get('tlg_nome')!r} "
                                  f"efetivacao={r.get('efl_id_exames_efetivacao')!r} data_efet={r.get('data_efetivacao')!r} "
                                  f"motivo_nao={str(r.get('motivo_nao_efetivacao'))[:30]!r} destino={str(r.get('unidadeDestino'))[:25]!r}")
        bruto["detalhes_exame"] = detalhes
        print("\nDETALHE do exame — campos:", sorted(det_campos))
        print("DETALHE do exame — valores de efetivação/situação:")
        for (k, v), n in sorted(det_valores.items()):
            print(f"  {k} = {v!r}: {n}")

    saida = CAP / f"historico_paciente_{hoje:%Y%m%d}.json"
    saida.write_text(json.dumps(bruto, ensure_ascii=False, indent=1, default=str), encoding="utf-8")
    print(f"\nbruto (com PII) em {saida}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
