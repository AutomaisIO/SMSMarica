"""Mapa de endpoints a partir de um HAR exportado do DevTools (aba Network → ⤓ Export HAR).

Lê um ou mais `.har` e imprime, por endpoint (método + caminho sem query), quantas vezes foi
chamado, o tipo da resposta, o tamanho e um exemplo de parâmetros (nomes, não valores — o HAR
tem dado de paciente). Separa o que é página (`.aspx` GET/POST), postback WebForms
(`__EVENTTARGET`), WebMethod (`.aspx/Metodo` JSON), handler (`.ashx`), serviço (`.asmx`,
`.svc`) e Telerik (`.axd`). Estáticos (css/js/png/fonte) ficam de fora.

Uso:  python analisar_har.py capturas/klinikos.har [outro.har ...] [--csv saida.csv]

O HAR fica em capturas/ (gitignored): contém cookies e dado de paciente.
"""

from __future__ import annotations

import csv
import json
import pathlib
import re
import sys
from collections import defaultdict
from urllib.parse import parse_qs, urlsplit

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ESTATICO = re.compile(r"\.(css|js|png|jpg|jpeg|gif|svg|ico|woff2?|ttf|eot|map)(\?|$)", re.I)


def classificar(url: str, metodo: str, mime: str, post_nomes: list[str]) -> str:
    p = urlsplit(url).path.lower()
    if ".axd" in p:
        return "telerik/axd"
    if p.endswith(".ashx"):
        return "handler .ashx"
    if p.endswith(".asmx") or "/" in p.split(".asmx")[-1] and ".asmx" in p:
        return "webservice .asmx"
    if p.endswith(".svc") or ".svc/" in p:
        return "wcf .svc"
    if re.search(r"\.aspx/[a-z_]+$", p):
        return "webmethod .aspx/Metodo"
    if p.endswith(".aspx"):
        if metodo == "POST" and "__EVENTTARGET" in post_nomes:
            return "postback WebForms"
        if metodo == "POST":
            return "POST .aspx"
        return "página .aspx"
    if "json" in (mime or ""):
        return "json"
    return "outro"


def main(args: list[str]) -> int:
    csv_out = None
    if "--csv" in args:
        i = args.index("--csv")
        csv_out = args[i + 1]
        args = args[:i] + args[i + 2:]
    if not args:
        print(__doc__)
        return 2

    agreg: dict[tuple[str, str], dict] = defaultdict(lambda: {
        "n": 0, "mimes": set(), "bytes": 0, "status": set(), "query": set(), "post": set(),
        "targets": set(), "classe": "", "exemplo": ""})

    for arq in args:
        har = json.loads(pathlib.Path(arq).read_text(encoding="utf-8"))
        for e in har["log"]["entries"]:
            req, res = e["request"], e["response"]
            url, metodo = req["url"], req["method"].upper()
            if ESTATICO.search(url):
                continue
            partes = urlsplit(url)
            chave = (metodo, partes.path)
            a = agreg[chave]
            a["n"] += 1
            a["status"].add(res.get("status"))
            mime = (res.get("content") or {}).get("mimeType") or ""
            a["mimes"].add(mime.split(";")[0])
            a["bytes"] += (res.get("content") or {}).get("size") or 0
            a["query"].update(parse_qs(partes.query).keys())
            post_nomes: list[str] = []
            pd = req.get("postData") or {}
            if pd.get("params"):
                post_nomes = [p["name"] for p in pd["params"]]
            elif pd.get("text"):
                t = pd["text"]
                if t.lstrip().startswith("{"):
                    try:
                        post_nomes = list(json.loads(t).keys())
                    except Exception:
                        post_nomes = ["<json>"]
                else:
                    post_nomes = list(parse_qs(t).keys())
                    for p in pd.get("params") or []:
                        post_nomes.append(p["name"])
                if "__EVENTTARGET" in post_nomes:
                    tgt = parse_qs(t).get("__EVENTTARGET", [""])[0]
                    if tgt:
                        a["targets"].add(tgt)
            a["post"].update(n for n in post_nomes if not n.startswith("__VIEWSTATE"))
            a["classe"] = classificar(url, metodo, mime, post_nomes)
            if not a["exemplo"]:
                a["exemplo"] = url[:160]

    linhas = sorted(agreg.items(), key=lambda kv: (kv[1]["classe"], kv[0][1]))
    print(f"{len(linhas)} endpoints distintos (sem estáticos)\n")
    classe_atual = None
    for (metodo, caminho), a in linhas:
        if a["classe"] != classe_atual:
            classe_atual = a["classe"]
            print(f"== {classe_atual} ==")
        st = ",".join(str(s) for s in sorted(a["status"], key=str))
        print(f"  {metodo:4} {caminho}  x{a['n']}  [{st}] {'/'.join(sorted(a['mimes']))} {a['bytes']}B")
        if a["query"]:
            print(f"        query: {', '.join(sorted(a['query']))}")
        if a["post"]:
            post = sorted(a["post"])
            print(f"        post ({len(post)}): {', '.join(post[:25])}{' …' if len(post) > 25 else ''}")
        if a["targets"]:
            print(f"        __EVENTTARGET: {', '.join(sorted(a['targets']))}")

    if csv_out:
        with open(csv_out, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["classe", "metodo", "caminho", "chamadas", "status", "mime", "bytes",
                        "query", "post", "eventtargets"])
            for (metodo, caminho), a in linhas:
                w.writerow([a["classe"], metodo, caminho, a["n"],
                            ",".join(str(s) for s in sorted(a["status"], key=str)),
                            "/".join(sorted(a["mimes"])), a["bytes"],
                            " ".join(sorted(a["query"])), " ".join(sorted(a["post"])),
                            " ".join(sorted(a["targets"]))])
        print(f"\n-> {csv_out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
