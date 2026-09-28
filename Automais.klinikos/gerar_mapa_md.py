"""Gera docs/mapa-endpoints.md a partir de capturas/mapa.json (saída do probe_mapa.py). Sem rede.

O markdown é SEM PII: só URL, título, nº e nomes de campos/botões do form, endpoints
(`.asmx/.ashx/.svc`, WebMethods), alvos de `__doPostBack` e scripts próprios. As capturas HTML
(que podem ter nome de paciente em grade) ficam em capturas/ e não entram.

Uso:  python gerar_mapa_md.py
"""

from __future__ import annotations

import json
import pathlib
import re
import sys
from collections import defaultdict
from urllib.parse import urlsplit

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
RAIZ = pathlib.Path(__file__).resolve().parent
CAP = RAIZ / "capturas"
DOCS = RAIZ / "docs"


def curto(n: str) -> str:
    return n.replace("ctl00$ctl00$contentCenter$contentCenterChild$", "").replace("ctl00$ctl00$", "")


def main() -> int:
    mapa = json.loads((CAP / "mapa.json").read_text(encoding="utf-8"))
    paginas = mapa["paginas"]
    por_modulo: dict[str, list] = defaultdict(list)
    for url, p in paginas.items():
        partes = urlsplit(url).path.split("/")
        mod = partes[2] if len(partes) > 2 else "?"
        por_modulo[mod].append((url, p))

    linhas = []
    linhas.append("# Klinikos — mapa de endpoints (varredura GET, somente leitura)\n")
    linhas.append(f"> Gerado por `gerar_mapa_md.py` a partir de `capturas/mapa.json` — varredura de "
                  f"{mapa['quando']} contra `{mapa['base']}` (instância do Conde). "
                  f"{len(paginas)} páginas visitadas, {len(mapa['asmx'])} serviços `.asmx`, "
                  f"{len(mapa['scripts'])} scripts próprios. Nenhum dado de paciente aqui: só nomes de "
                  f"campos, botões e URLs. O que a varredura NÃO visita: Logout, GravaCookie, "
                  f"AlterarSenha e qualquer caminho com verbo de escrita (excluir/estorno/cancelar/"
                  f"baixa/salvar…), além de `rptview`/`Relatorio.aspx` (sondados à parte em "
                  f"`sondagem-relatorios.md`).\n")

    linhas.append("## 1. Serviços `.asmx` (JSON, cookie, sem ViewState)\n")
    linhas.append("| Serviço | Métodos (do proxy `/js`) |\n|---|---|")
    for svc, metodos in sorted(mapa["asmx"].items()):
        linhas.append(f"| `{svc}` | {', '.join(m for m in metodos if not m.startswith('_'))} |")
    linhas.append("")

    if mapa["scripts"]:
        linhas.append("## 2. Scripts próprios com endpoints\n")
        linhas.append("| Script | Endpoints referenciados |\n|---|---|")
        for sc, eps in sorted(mapa["scripts"].items()):
            if eps:
                linhas.append(f"| `{sc.replace(mapa['base'], '')}` | {', '.join(f'`{e}`' for e in eps[:8])} |")
        linhas.append("")

    linhas.append("## 3. Páginas por módulo\n")
    linhas.append("Colunas: **campos** = inputs/selects/textareas próprios da tela (fora do master); "
                  "**botões** = submit/image; **postbacks** = alvos estáticos de `__doPostBack`; "
                  "**endpoints** = `.asmx/.ashx/.svc/.aspx/Metodo` fora do master "
                  "(`ComboGrupoExameService.asmx/DeletarVinculo` está em todas e foi omitido).\n")
    master_ep = {"../WebServices/ComboGrupoExameService.asmx/DeletarVinculo",
                 "/KlinikosNet/WebServices/ComboGrupoExameService.asmx/DeletarVinculo"}
    for mod in sorted(por_modulo):
        itens = sorted(por_modulo[mod], key=lambda x: x[0])
        linhas.append(f"### {mod} ({len(itens)} páginas)\n")
        linhas.append("| Página | Status | Título/H | Campos | Botões | Postbacks | Endpoints |\n|---|---|---|---|---|---|---|")
        for url, p in itens:
            if "erro" in p and "campos" not in p:
                linhas.append(f"| `{url}` | erro | {p['erro'][:60]} | | | | |")
                continue
            campos = [curto(c["nome"]) for c in p.get("campos", [])]
            botoes = [curto(b) for b in p.get("botoes", [])]
            eps = [e for e in p.get("endpoints", []) if e not in master_ep and "ComboGrupoExameService" not in e]
            pbs = [curto(x) for x in p.get("postbacks", []) if "HeaderLinks" not in x and "RadMenu" not in x]
            titulo = (p.get("h1") or p.get("titulo") or "")[:40]
            status = str(p.get("status", "")) + (f" → `{p['url_final']}`" if p.get("url_final") else "")
            linhas.append("| `{}` | {} | {} | {} {} | {} | {} | {} |".format(
                url.replace("/KlinikosNet/", ""), status, titulo.replace("|", "/"),
                len(campos), ("(" + ", ".join(campos[:6]) + ("…" if len(campos) > 6 else "") + ")") if campos else "",
                ", ".join(botoes[:6]) + ("…" if len(botoes) > 6 else ""),
                ", ".join(pbs[:4]) + ("…" if len(pbs) > 4 else ""),
                ", ".join(f"`{e}`" for e in eps[:4])))
        linhas.append("")

    (DOCS / "mapa-endpoints.md").write_text("\n".join(linhas), encoding="utf-8")
    print(f"-> docs/mapa-endpoints.md ({len(paginas)} páginas, {sum(len(v) for v in por_modulo.values())} linhas)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
