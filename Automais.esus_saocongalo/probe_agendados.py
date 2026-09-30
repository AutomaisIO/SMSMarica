"""Sonda 2: Pacientes de Maricá AGENDADOS pela fila no e-SUS SG (exame ou consulta).

É a tela que responde "quando e onde o paciente de Maricá vai ser atendido em São Gonçalo".
O período (`--de`/`--ate`) filtra a DATA DO AGENDAMENTO e é obrigatório de fato: sem ele a
busca devolve 0. Imprime SÓ AGREGADOS (por unidade de destino, procedimento, dia, situação
da notificação ao paciente); a lista nominal vai para `--saida` (tem PII — `capturas/` é
gitignored). SOMENTE LEITURA.

Uso:  python probe_agendados.py                                 # exame, hoje-30d .. hoje+60d
      python probe_agendados.py --consulta --de 01/09/2026 --ate 31/10/2026
      python probe_agendados.py --saida C:/fora/da/arvore/agendados.json
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import json
import pathlib
import sys

from esus.client import CAP, paginar, sessao
from esus.telas import CAMINHO, form_agendados

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    hoje = dt.date.today()
    ap = argparse.ArgumentParser()
    ap.add_argument("--consulta", action="store_true", help="agendados de consulta (padrão: exame)")
    ap.add_argument("--de", default=(hoje - dt.timedelta(days=30)).strftime("%d/%m/%Y"))
    ap.add_argument("--ate", default=(hoje + dt.timedelta(days=60)).strftime("%d/%m/%Y"))
    ap.add_argument("--saida", type=pathlib.Path, help="JSON com as linhas (tem PII)")
    a = ap.parse_args()

    tipo = "consulta" if a.consulta else "exame"
    with sessao() as s:
        # Uma linha = um AGENDAMENTO: o pedido (fil_id) pode ter várias sessões.
        linhas, total = paginar(s, CAMINHO[f"agendados_{tipo}"],
                                form_agendados(s.unidade, exame=not a.consulta, de=a.de, ate=a.ate),
                                chave=("fil_id", "eap_id", "data_hora_formatada"))

    pedidos = len({ln.get("fil_id") for ln in linhas})
    print(f"agendados de {tipo} — solicitante {s.unidade}, agendamento de {a.de} a {a.ate}: "
          f"{total} agendamentos (únicos = declarado) em {pedidos} pedidos")
    if not linhas:
        return 0
    print("colunas:", ", ".join(sorted(linhas[0])))

    for rot, chave in (("unidade de destino", "unidadeAgendamento"), ("setor", "set_nome"),
                       ("local", "lca_nome"), ("procedimento", "stp_novo_nome_procedimento"),
                       ("prioridade", "pfi_nome"), ("agendado por", "usuarioAgendamento"),
                       ("comprovante impresso", "comprovanteImpresso"),
                       ("notificação: tipo", "not_tipo"), ("notificação: entrega", "not_delivered_status"),
                       ("notificação: resposta", "not_resposta"), ("TFD", "agendado_tfd")):
        if chave not in linhas[0]:
            continue
        c = collections.Counter((ln.get(chave) or "(vazio)") for ln in linhas)
        print(f"\npor {rot} ({len(c)} valores):")
        for v, n in c.most_common(12):
            print(f"  {n:5d}  {v}")

    dias = collections.Counter((ln.get("data_agendada") or ln.get("eha_data_exame") or "?")[:10] for ln in linhas)
    def ordem(d: str) -> str:  # "dd/mm/aaaa" -> "aaaa-mm-dd" só para ordenar
        return f"{d[6:10]}-{d[3:5]}-{d[0:2]}" if len(d) == 10 and d[2] == "/" else d
    print(f"\npor dia do atendimento ({len(dias)} dias):")
    for d, n in sorted(dias.items(), key=lambda kv: ordem(kv[0])):
        print(f"  {d}  {n}")

    n = len(linhas)
    cpf = sum(1 for ln in linhas if (ln.get("cpf_numero") or "").strip())
    tel = sum(1 for ln in linhas if (ln.get("telefone") or "").strip())
    print(f"\ncobertura: CPF {cpf}/{n}  telefone {tel}/{n}")

    saida = a.saida or CAP / f"agendados_{tipo}_{hoje:%Y%m%d}.json"
    saida.parent.mkdir(parents=True, exist_ok=True)
    saida.write_text(json.dumps(linhas, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nlinhas (COM PII) -> {saida}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
