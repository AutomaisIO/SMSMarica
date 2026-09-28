"""Gera docs/sondagem-relatorios.md a partir das saídas do sondar_relatorios.py. Sem rede, sem PII.

Lê todos os `capturas/sondagem_relatorios*.json` (cada rodada grava um) e monta uma tabela
por relatório: alvo, título, campos da tela, URL do `rptviewXls` gerado (o layout `parN`),
formato/tamanho da resposta, nº de linhas e colunas do XLS (só o cabeçalho).

Uso:  python gerar_sondagem_md.py
"""

from __future__ import annotations

import glob
import json
import pathlib
import re
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
RAIZ = pathlib.Path(__file__).resolve().parent
CAP = RAIZ / "capturas"
DOCS = RAIZ / "docs"


def main() -> int:
    from sondar_relatorios import cabecalho_xls
    itens: dict[str, dict] = {}
    for arq in sorted(glob.glob(str(CAP / "sondagem_relatorios*.json"))):
        for it in json.loads(pathlib.Path(arq).read_text(encoding="utf-8")):
            chave = it.get("alvo") or it.get("parrel") or "?"
            itens[chave] = it  # a rodada mais recente vence
            # Recalcula cabeçalho/linhas a partir do XLS salvo (regra de ruído do Crystal mais nova).
            nome = re.sub(r"[^A-Za-z0-9]+", "_", chave)[:60]
            for cand in (CAP / f"sond_{nome}.xls", CAP / f"sond_{chave}.xls"):
                if cand.exists():
                    try:
                        it["colunas"], it["linhas"] = cabecalho_xls(cand.read_bytes())
                    except Exception as e:  # noqa: BLE001
                        it["erro"] = f"xls ilegível: {e}"[:80]
                    break
    L = ["# Klinikos — sondagem de relatórios (parâmetros, `parN`, colunas do XLS)\n",
         f"> Gerado por `gerar_sondagem_md.py` em {time.strftime('%Y-%m-%d %H:%M')} a partir de "
         f"`capturas/sondagem_relatorios*.json` (rodadas de `sondar_relatorios.py`, 1 dia = 15/09/2026, "
         f"instância do Conde, `unid_codigo` 0005). {len(itens)} relatórios. Só cabeçalho de coluna — "
         f"as células ficam nos XLS de `capturas/` (gitignored).\n",
         "Como ler: **rptviewXls** é a URL que a tela gera no `window.open` — é o endpoint de verdade, um GET "
         "com cookie; o conector chama isso direto, sem a tela. `par3`/`par1`=`0005` é a unidade. "
         "Resposta \"Nenhum registro\" com 1 dia não invalida o relatório — só diz que naquele dia não houve.\n",
         "| alvo | título | campos da tela | rptviewXls gerado | resposta | linhas | colunas |",
         "|---|---|---|---|---|---|---|"]
    for chave, it in sorted(itens.items(), key=lambda kv: (re.sub(r"\D", "", kv[0]).zfill(5), kv[0])):
        campos = ", ".join(f"{c['nome']}{'(' + str(c['n_opcoes']) + ')' if c.get('n_opcoes') is not None else ''}"
                           for c in it.get("campos", []) if "dateInput" not in c["nome"] and "$dateInput" not in c["nome"])
        rpt = it.get("rptview", "")
        resp = it.get("erro") or ("XLS" if it.get("colunas") is not None else (it.get("resposta") or ""))
        resp = re.sub(r"\s+", " ", str(resp))[:90]
        cols = ", ".join(str(c)[:22] for c in (it.get("colunas") or [])[:10])
        L.append(f"| `{chave}` | {it.get('titulo', '')} | {campos[:120]} | `{rpt[:160]}` | {resp} | "
                 f"{it.get('linhas', '')} | {cols} |")
    (DOCS / "sondagem-relatorios.md").write_text("\n".join(L) + "\n", encoding="utf-8")
    print(f"-> docs/sondagem-relatorios.md ({len(itens)} relatórios)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
