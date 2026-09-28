"""Rastreador do mapa de endpoints do Klinikos. SOMENTE LEITURA (só GET).

Parte das homes dos 10 módulos, segue todo link `.aspx` dentro de `/KlinikosNet/` (menu
RadMenu, favoritos, links de conteúdo, URLs serializadas no `__VIEWSTATE`) e, em cada página,
registra:

- título, módulo, tamanho, redirecionamento (sessão expirada / gate de local);
- campos do form (nomes, sem valores) e botões (submit/image) — o "contrato" do postback;
- alvos `.asmx` / `.ashx` / `.svc` / `.aspx/Metodo` (WebMethod) referenciados no HTML/JS;
- `url:` de `$.ajax` inline, chamadas `PageMethods.X`, `__doPostBack('alvo')` estáticos;
- scripts externos próprios (fora de App_Themes/Skins/Telerik) — baixados e varridos também.

Para cada `.asmx` achado, baixa o proxy `/js` (ASP.NET AJAX) e extrai os NOMES dos métodos —
é o mais perto de um "swagger" que um WebForms de 2011 oferece.

Saídas (em capturas/, gitignored porque o HTML das telas pode ter nome de paciente):
- capturas/mapa/<pagina>.html — cada página visitada
- capturas/mapa.json — o mapa completo (páginas, campos, endpoints)
E um resumo SEM PII em docs/mapa-endpoints.md (gerado por gerar_mapa_md.py).

Trava: nunca segue URL cujo caminho tenha verbo de escrita ou que seja Logout/GravaCookie/
AlterarSenha; nunca faz POST. GET de tela WebForms só renderiza o form.

Uso:  python probe_mapa.py [--max 400] [--so-modulo UPA]
"""

from __future__ import annotations

import json
import pathlib
import re
import sys
import time
from collections import deque
from urllib.parse import urljoin, urlsplit, parse_qs

from klinikos.client import BASE, CAP, KlinikosSession, eh_sessao_expirada, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

MODULOS = {"Acesso": "013", "Administracao": "001", "Ambulatorio": "002", "Cadastro": "004",
           "CentroCirurgico": "005", "Internacao": "009", "Laboratorio": "010",
           "Radiologia": "016", "UPA": "014", "eProntuario": "012"}

# Caminhos que NÃO se visita nem por GET: saída, gate, senha e qualquer verbo de escrita no nome.
NAO_VISITAR = re.compile(
    r"(logout|gravacookie|alterarsenha|excluir|estorno|cancel|baixa|reprocess|salvar|gravar|"
    r"remover|deletar|apagar|inserir|incluir|confirmar|transferir|efetivar|finalizar|"
    r"autorizar|liberar|enviar|imprimir|rptview|relatorio\.aspx)", re.I)

VARIANTES_POR_CAMINHO = 4

VENDOR_JS = re.compile(r"(App_Themes|Skins|Telerik|WebResource\.axd|ScriptResource\.axd|jquery|bootstrap|cdn\.)", re.I)

RE_ASPX = re.compile(r"/KlinikosNet/[\w\-/\.]+\.aspx(?:\?[\w\-=&%\.\+]*)?", re.I)
RE_ENDPOINT = re.compile(r"""["'(]([^"'()\s]+\.(?:asmx|ashx|svc)(?:/[\w]+)?[^"'()\s]*)""", re.I)
RE_WEBMETHOD = re.compile(r"""["']([^"'\s]+\.aspx/[A-Za-z_]\w*)["']""")
RE_AJAX_URL = re.compile(r"""url\s*:\s*["']([^"']+)["']""")
RE_PAGEMETHODS = re.compile(r"PageMethods\.(\w+)\s*\(")
RE_DOPOSTBACK = re.compile(r"__doPostBack\(\\?['\"]([^'\"\\]+)\\?['\"]")


def normalizar(url: str, base: str) -> str | None:
    u = urljoin(base, url.replace("&amp;", "&"))
    u = u.replace("/KlinikosNet/" + urlsplit(base).path.split("/")[2] + "/~/", "/KlinikosNet/")  # links "~/" quebrados
    u = re.sub(r"/KlinikosNet/[\w\-]+/~/", "/KlinikosNet/", u)
    p = urlsplit(u)
    if not p.path.lower().startswith("/klinikosnet/") or not p.path.lower().endswith(".aspx"):
        return None
    if NAO_VISITAR.search(p.path):
        return None
    return f"{p.path}?{p.query}" if p.query else p.path


def nome_arquivo(url: str) -> str:
    return re.sub(r"[^A-Za-z0-9]+", "_", url.split("/KlinikosNet/")[-1])[:100]


def descrever(html: str, url: str) -> dict:
    d = sopa(html)
    f = d.find("form", id="aspnetForm") or d.find("form")
    campos, botoes = [], []
    if f:
        for i in f.find_all(["input", "select", "textarea"]):
            n = i.get("name")
            if not n or n.startswith("__") or n.endswith("_TSM") or n.endswith("_TSSM") or n.endswith("_ClientState"):
                continue
            t = i.name if i.name != "input" else (i.get("type") or "text").lower()
            if t in ("submit", "image", "button"):
                botoes.append(n)
            elif t != "hidden":
                campos.append({"nome": n, "tipo": t, "opcoes": len(i.find_all("option")) if i.name == "select" else None})
    # só os controles da tela (fora do master: HeaderLinks/RadMenu/StatusServico)
    proprio = lambda n: "HeaderLinks1" not in n and "RadMenu" not in n and "StatusServico" not in n
    campos = [c for c in campos if proprio(c["nome"])]
    botoes = [b for b in botoes if proprio(b)]

    inline = " ".join(sc.get_text() for sc in d.find_all("script") if not sc.get("src"))
    scripts = [urljoin(BASE + url, sc["src"]) for sc in d.find_all("script") if sc.get("src") and not VENDOR_JS.search(sc["src"])]
    endpoints = sorted(set(RE_ENDPOINT.findall(html)) | set(RE_WEBMETHOD.findall(html)))
    return {
        "url": url,
        "titulo": (d.title.get_text(strip=True) if d.title else ""),
        "h1": " | ".join(x.get_text(" ", strip=True)[:80] for x in d.find_all(["h1", "h2", "h3"])[:3]),
        "bytes": len(html),
        "campos": campos,
        "botoes": botoes,
        "endpoints": endpoints,
        "ajax_urls": sorted(set(RE_AJAX_URL.findall(inline))),
        "pagemethods": sorted(set(RE_PAGEMETHODS.findall(inline))),
        "postbacks": sorted(set(RE_DOPOSTBACK.findall(html)))[:40],
        "scripts_proprios": scripts,
        "links": sorted(set(x for x in (normalizar(a, BASE + url) for a in RE_ASPX.findall(html)) if x)),
    }


def urls_do_viewstate(html: str, base_url: str) -> list[str]:
    import base64
    m = re.search(r'id="__VIEWSTATE"[^>]*value="([^"]*)"', html)
    if not m:
        return []
    vs = m.group(1)
    try:
        blob = base64.b64decode(vs + "=" * (-len(vs) % 4)).decode("utf-8", errors="replace")
    except Exception:
        return []
    return sorted(set(x for x in (normalizar(a, base_url) for a in RE_ASPX.findall(blob)) if x))


def metodos_asmx(s: KlinikosSession, asmx: str) -> list[str]:
    """Proxy JS do ASP.NET AJAX (`Servico.asmx/js`) lista os métodos expostos."""
    caminho = asmx.split("?")[0]
    caminho = re.sub(r"(\.asmx)/\w+$", r"\1", caminho)
    r = s.get(caminho + "/js")
    if r.status_code != 200:
        return [f"(js -> {r.status_code})"]
    js = r.text
    (CAP / "mapa" / (nome_arquivo(caminho) + ".js")).write_text(js, encoding="utf-8")
    # padrão: Ns.Servico.prototype={ Metodo:function(a,b,succeeded,failed,userContext){...
    return sorted(set(re.findall(r"^\s*(\w+)\s*:\s*function\s*\(", js, re.M)) | set(re.findall(r"prototype\.(\w+)\s*=\s*function", js)))


def main(args: list[str]) -> int:
    maximo = int(args[args.index("--max") + 1]) if "--max" in args else 400
    so = args[args.index("--so-modulo") + 1] if "--so-modulo" in args else None
    (CAP / "mapa").mkdir(exist_ok=True)

    s = KlinikosSession()
    s.entrar()
    fila: deque[str] = deque()
    for nome, mod in MODULOS.items():
        if so and nome.lower() != so.lower():
            continue
        fila.append(f"/KlinikosNet/{nome}/Default.aspx?mod={mod}")
    vistos: set[str] = set()
    enfileirados: set[str] = set(fila)
    caminhos_conhecidos: set[str] = {u.split("?")[0].lower() for u in fila}
    from collections import Counter
    variantes: Counter[str] = Counter({u.split("?")[0].lower(): 1 for u in fila})
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
            continue
        html = r.text
        if eh_sessao_expirada(r):
            print("!! sessão expirada em", url, "— relogando")
            s.entrar()
            r = s.get(url)
            html = r.text
        final = str(r.url).replace(BASE, "")
        (CAP / "mapa" / (nome_arquivo(url) + ".html")).write_text(html, encoding="utf-8")
        info = descrever(html, url)
        info["status"] = r.status_code
        info["url_final"] = final if final != url else None
        info["links_viewstate"] = urls_do_viewstate(html, BASE + url)
        paginas[url] = info
        print(f"[{len(paginas):3d}] {r.status_code} {url[:90]:90} {info['titulo'][:30]:30} campos={len(info['campos'])} btn={len(info['botoes'])} ep={len(info['endpoints'])}")

        # Dedup: no máximo VARIANTES_POR_CAMINHO query strings por caminho, e URL do ViewState
        # só entra se o CAMINHO ainda não é conhecido (o LOS "vaza" bytes na query: `mod=014dd`).
        for origem, lista in (("html", info["links"]), ("viewstate", info["links_viewstate"])):
            for l in lista:
                caminho = l.split("?")[0].lower()
                if l in vistos or l in enfileirados:
                    continue
                if "%26" in l or "rcbID" in l:
                    continue
                if origem == "viewstate" and caminho in caminhos_conhecidos:
                    continue
                if variantes[caminho] >= VARIANTES_POR_CAMINHO:
                    continue
                variantes[caminho] += 1
                caminhos_conhecidos.add(caminho)
                enfileirados.add(l)
                fila.append(l)
        for ep in info["endpoints"]:
            if ".asmx" in ep.lower():
                chave = re.sub(r"(\.asmx).*$", r"\1", urljoin(BASE + url, ep)).replace(BASE, "")
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

    saida = {"base": BASE, "quando": time.strftime("%Y-%m-%d %H:%M"), "paginas": paginas,
             "asmx": asmx_vistos, "scripts": scripts_vistos, "fila_restante": list(fila)}
    (CAP / "mapa.json").write_text(json.dumps(saida, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n{len(paginas)} páginas, {len(asmx_vistos)} serviços .asmx, {len(scripts_vistos)} scripts próprios, "
          f"{len(fila)} na fila, {time.time() - t0:.0f}s -> capturas/mapa.json")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
