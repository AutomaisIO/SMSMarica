"""Rastreador do mapa de telas/endpoints do Prime. SOMENTE LEITURA (só GET).

Mesmo método do `Automais.klinikos/probe_mapa.py`: parte da home (`AtencaoBasica/Default.aspx`,
cujo menu já lista ~90 telas), segue todo link `.aspx` dentro de `/Prime/` e registra, por
página: título, tamanho, redirecionamento, campos/botões do form (o "contrato" do postback),
alvos `.asmx/.ashx/.svc` e WebMethods `.aspx/Metodo`, `url:` de `$.ajax`, `PageMethods.X`,
`__doPostBack` estáticos e scripts próprios (baixados e varridos). Para cada `.asmx`, baixa o
proxy `/js` e extrai os NOMES dos métodos.

Saídas (em capturas/, gitignored — HTML de tela pode ter nome de paciente):
- capturas/mapa/<pagina>.html e capturas/mapa.json
Resumo SEM PII em docs/mapa-endpoints.md (gerar_mapa_md.py).

Trava: nunca visita caminho com verbo de escrita/ação nem Logout/AlterarSenha/sessão; no Prime
também pula carga, sincronização, importação, exportação, migração, fechamento e unificação
(telas que podem disparar processo no GET/Page_Load). Nunca faz POST.

Uso:  python probe_mapa.py [--max 400]
"""

from __future__ import annotations

import json
import re
import sys
import time
from collections import Counter, deque
from urllib.parse import urljoin, urlsplit

from prime.client import APP, BASE, CAP, URL_HOME, PrimeSession, eh_sessao_expirada, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

NAO_VISITAR = re.compile(
    r"(logout|sair|alterarsenha|trocarsenha|sessaoexpirada|excluir|estorno|cancel|baixa|reprocess|"
    r"salvar|gravar|remover|deletar|apagar|inserir|incluir|confirmar|transferir|efetivar|"
    r"finalizar|autorizar|liberar|enviar|imprimir|carga|sincroniza|importa|exporta|migrar|"
    r"fechamento|unificar|unificacao(?!paciente)|gerar)", re.I)

VARIANTES_POR_CAMINHO = 4
VENDOR_JS = re.compile(r"(App_Themes|Skins|Telerik|WebResource\.axd|ScriptResource\.axd|jquery|"
                       r"bootstrap|select2|sweetalert|ext-all|cdn\.)", re.I)

RE_HREF = re.compile(r"""(?:href|src|action|NavigateUrl|window\.open\(|location\.href\s*=)\s*=?\s*["']([^"'#]+\.aspx[^"']*)["']""", re.I)
RE_ASPX_ABS = re.compile(APP + r"/[\w\-/\.]+\.aspx(?:\?[\w\-=&%\.\+]*)?", re.I)
RE_ENDPOINT = re.compile(r"""["'(]([^"'()\s]+\.(?:asmx|ashx|svc)(?:/[\w]+)?[^"'()\s]*)""", re.I)
RE_WEBMETHOD = re.compile(r"""["']([^"'\s]+\.aspx/[A-Za-z_]\w*)["']""")
RE_AJAX_URL = re.compile(r"""url\s*:\s*["']([^"']+)["']""")
RE_PAGEMETHODS = re.compile(r"PageMethods\.(\w+)\s*\(")
RE_DOPOSTBACK = re.compile(r"__doPostBack\((?:\\?['\"]|&#39;)([^'\"\\&]+)")

# Controles do master page (menu, cabeçalho) — fora do "contrato" da tela.
MASTER = re.compile(r"(linkButtonPainel|LoginStatus|ddlUnidadeMaster|HeaderLinks|Menu)", re.I)


def normalizar(url: str, base: str) -> str | None:
    u = urljoin(base, url.replace("&amp;", "&").strip())
    p = urlsplit(u)
    if p.netloc and p.netloc not in BASE:
        return None
    if not p.path.lower().startswith(APP.lower() + "/") or not p.path.lower().endswith(".aspx"):
        return None
    if NAO_VISITAR.search(p.path):
        return None
    return f"{p.path}?{p.query}" if p.query else p.path


def nome_arquivo(url: str) -> str:
    return re.sub(r"[^A-Za-z0-9]+", "_", url.split(APP + "/")[-1])[:100]


def descrever(html: str, url: str) -> dict:
    d = sopa(html)
    f = d.find("form")
    campos, botoes = [], []
    if f:
        for i in f.find_all(["input", "select", "textarea"]):
            n = i.get("name")
            if not n or n.startswith("__") or n.endswith("_ClientState") or MASTER.search(n):
                continue
            t = i.name if i.name != "input" else (i.get("type") or "text").lower()
            if t in ("submit", "image", "button"):
                botoes.append(n)
            elif t != "hidden":
                campos.append({"nome": n, "tipo": t, "opcoes": len(i.find_all("option")) if i.name == "select" else None})
    inline = " ".join(sc.get_text() for sc in d.find_all("script") if not sc.get("src"))
    scripts = [urljoin(BASE + url, sc["src"]) for sc in d.find_all("script") if sc.get("src") and not VENDOR_JS.search(sc["src"])]
    brutos = set(RE_HREF.findall(html)) | set(RE_ASPX_ABS.findall(html))
    return {
        "url": url,
        "titulo": (d.title.get_text(strip=True) if d.title else ""),
        "h1": " | ".join(x.get_text(" ", strip=True)[:80] for x in d.find_all(["h1", "h2", "h3", "legend"])[:3]),
        "bytes": len(html),
        "campos": campos,
        "botoes": botoes,
        "endpoints": sorted(set(RE_ENDPOINT.findall(html)) | set(RE_WEBMETHOD.findall(html))),
        "ajax_urls": sorted(set(RE_AJAX_URL.findall(inline))),
        "pagemethods": sorted(set(RE_PAGEMETHODS.findall(inline))),
        "postbacks": sorted(set(x for x in RE_DOPOSTBACK.findall(html) if not MASTER.search(x)))[:40],
        "scripts_proprios": scripts,
        "links": sorted(set(x for x in (normalizar(a, BASE + url) for a in brutos) if x)),
    }


def metodos_asmx(s: PrimeSession, asmx: str) -> list[str]:
    caminho = re.sub(r"(\.asmx)/\w+$", r"\1", asmx.split("?")[0])
    r = s.get(caminho + "/js")
    if r.status_code != 200:
        return [f"(js -> {r.status_code})"]
    js = r.text
    (CAP / "mapa" / (nome_arquivo(caminho) + ".js")).write_text(js, encoding="utf-8")
    return sorted(set(re.findall(r"^\s*(\w+)\s*:\s*function\s*\(", js, re.M)) | set(re.findall(r"prototype\.(\w+)\s*=\s*function", js)))


def main(args: list[str]) -> int:
    maximo = int(args[args.index("--max") + 1]) if "--max" in args else 400
    (CAP / "mapa").mkdir(exist_ok=True)
    s = PrimeSession()
    s.entrar()
    fila: deque[str] = deque([URL_HOME])
    vistos: set[str] = set()
    enfileirados: set[str] = set(fila)
    variantes: Counter[str] = Counter({URL_HOME.lower(): 1})
    paginas: dict[str, dict] = {}
    asmx_vistos: dict[str, list[str]] = {}
    scripts_vistos: dict[str, list[str]] = {}
    t0 = time.time()

    while fila and len(paginas) < maximo:
        url = fila.popleft()
        if url in vistos:
            continue
        vistos.add(url)
        try:
            r = s.get(url)
        except Exception as e:  # noqa: BLE001
            paginas[url] = {"url": url, "erro": str(e)[:200]}
            print(f"[{len(paginas):3d}] ERRO {url} {str(e)[:80]}")
            continue
        if eh_sessao_expirada(r):
            print("!! sessão expirada em", url, "— relogando")
            s.c.cookies.clear()
            s.entrar()
            r = s.get(url)
        html = r.text
        final = str(r.url).replace(BASE, "")
        (CAP / "mapa" / (nome_arquivo(url) + ".html")).write_text(html, encoding="utf-8")
        info = descrever(html, str(r.url).replace(BASE, ""))
        info["url"] = url
        info["status"] = r.status_code
        info["url_final"] = final if final != url else None
        paginas[url] = info
        print(f"[{len(paginas):3d}] {r.status_code} {url[:90]:90} {info['titulo'][:30]:30} "
              f"campos={len(info['campos'])} btn={len(info['botoes'])} ep={len(info['endpoints'])}")

        for l in info["links"]:
            caminho = l.split("?")[0].lower()
            if l in vistos or l in enfileirados or variantes[caminho] >= VARIANTES_POR_CAMINHO:
                continue
            variantes[caminho] += 1
            enfileirados.add(l)
            fila.append(l)
        for ep in info["endpoints"]:
            if ".asmx" in ep.lower():
                chave = re.sub(r"(\.asmx).*$", r"\1", urljoin(BASE + final, ep)).replace(BASE, "")
                if chave not in asmx_vistos:
                    asmx_vistos[chave] = metodos_asmx(s, chave)
                    print(f"      asmx {chave}: {asmx_vistos[chave]}")
        for sc in info["scripts_proprios"]:
            if sc not in scripts_vistos:
                try:
                    js = s.get(sc).text
                except Exception:
                    continue
                achados = sorted(set(RE_ENDPOINT.findall(js)) | set(RE_WEBMETHOD.findall(js)) | set(RE_AJAX_URL.findall(js)))
                scripts_vistos[sc] = achados
                if achados:
                    print(f"      js {sc.replace(BASE, '')}: {achados[:8]}")
        time.sleep(0.5)  # gentileza com o servidor do fornecedor

    saida = {"base": BASE, "quando": time.strftime("%Y-%m-%d %H:%M"), "paginas": paginas,
             "asmx": asmx_vistos, "scripts": scripts_vistos, "fila_restante": list(fila)}
    (CAP / "mapa.json").write_text(json.dumps(saida, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n{len(paginas)} páginas, {len(asmx_vistos)} serviços .asmx, {len(scripts_vistos)} scripts próprios, "
          f"{len(fila)} na fila, {time.time() - t0:.0f}s -> capturas/mapa.json")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
