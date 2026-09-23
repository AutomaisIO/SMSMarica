"""Relatório legível de uma resposta do SISCAN — para sondas de recon.

Só leitura: nada aqui submete formulário. Imprime o que a tela É (h1, mensagens,
campos, gatilhos A4J, botões), para que a sonda decida o próximo passo sem eu
ter de abrir o HTML de 100 KB no editor.
"""

from __future__ import annotations

import re

from bs4 import BeautifulSoup

RE_A4J = re.compile(r"A4J\.AJAX\.Submit\(\s*'([^']+)'\s*,\s*event\s*,\s*\{(.*?)\}\s*\)", re.S)
RE_PARAMS = re.compile(r"'parameters':\s*\{(.*?)\}", re.S)
RE_JSFCLJS = re.compile(r"jsfcljs\(document\.getElementById\('([^']+)'\),\s*\{'([^']+)':'([^']+)'\}")


def titulos(doc: BeautifulSoup) -> list[str]:
    return [h.get_text(" ", strip=True) for h in doc.find_all(["h1", "h2"])
            if h.get_text(strip=True)]


def _rotulo_de(el) -> str:
    """Rótulo visível de um campo: <label for>, <label> envolvente, ou o td anterior."""
    doc = el.find_parent(["html", "body"]) or el
    if el.get("id"):
        lb = doc.find("label", attrs={"for": el.get("id")})
        if lb:
            return lb.get_text(" ", strip=True)
    lb = el.find_parent("label")
    if lb:
        return lb.get_text(" ", strip=True)
    td = el.find_parent("td")
    if td:
        ant = td.find_previous_sibling("td")
        if ant:
            t = ant.get_text(" ", strip=True)
            if t:
                return t
    return ""


def campos_visiveis(form) -> list[dict]:
    out: list[dict] = []
    for el in form.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome:
            continue
        tipo = (el.get("type") or el.name).lower()
        if tipo == "hidden":
            continue
        item = {
            "name": nome,
            "tipo": tipo,
            "rotulo": _rotulo_de(el)[:70],
            "disabled": el.has_attr("disabled"),
            "valor": (el.get("value") or "")[:40] if el.name == "input" else "",
        }
        if el.name == "select":
            opts = [(o.get("value"), o.get_text(" ", strip=True)) for o in el.find_all("option")]
            item["opcoes"] = opts[:12]
            item["n_opcoes"] = len(opts)
            sel = el.find("option", selected=True)
            item["valor"] = sel.get("value") if sel else ""
        if el.name == "textarea":
            item["valor"] = (el.text or "")[:40]
        if tipo in ("checkbox", "radio"):
            item["marcado"] = el.has_attr("checked")
        for ev in ("onclick", "onblur", "onchange"):
            if el.get(ev) and "A4J" in el[ev]:
                item["a4j"] = _a4j_de(el[ev])
        out.append(item)
    return out


def _a4j_de(js: str) -> dict[str, str]:
    m = RE_PARAMS.search(js or "")
    if not m:
        return {}
    return dict(re.findall(r"'([^']+)'\s*:\s*'([^']*)'", m.group(1)))


def gatilhos(doc: BeautifulSoup) -> list[dict]:
    """Links/botões que disparam servidor: A4J (RichFaces) ou jsfcljs (Mojarra)."""
    out: list[dict] = []
    for a in doc.find_all(["a", "input", "button"]):
        js = " ".join(filter(None, [a.get("onclick"), a.get("onkeypress")]))
        rotulo = (a.get("value") or a.get("title") or a.get_text(" ", strip=True) or "").strip()
        form = a.find_parent("form")
        base = {"rotulo": rotulo[:60], "id": a.get("id"), "form": form.get("id") if form else None}
        if "A4J.AJAX.Submit" in js:
            out.append({**base, "tipo": "a4j", "parametros": _a4j_de(js)})
        elif "jsfcljs" in js:
            m = RE_JSFCLJS.search(js)
            if m:
                out.append({**base, "tipo": "jsfcljs", "form": m.group(1),
                            "parametros": {m.group(2): m.group(3)}})
        elif (a.get("type") or "").lower() in ("submit", "button") and a.get("name"):
            out.append({**base, "tipo": "submit", "parametros": {a["name"]: a.get("value", "")}})
    return out


def relatorio(doc: BeautifulSoup, cabecalho: str = "", so_forms: tuple[str, ...] = ()) -> None:
    print("\n" + "=" * 78)
    print(cabecalho)
    print("=" * 78)
    print("H1/H2 :", titulos(doc) or "(nenhum)")
    from .client import mensagens
    msgs = [m for m in mensagens(doc) if len(m) < 300]
    if msgs:
        print("MENSAGENS:")
        for m in dict.fromkeys(msgs):
            print("   !", m[:160])
    for form in doc.find_all("form"):
        fid = form.get("id")
        if so_forms and fid not in so_forms:
            continue
        cs = campos_visiveis(form)
        print(f"\n-- form {fid!r} action={form.get('action')}  ({len(cs)} campos visíveis)")
        for c in cs:
            marca = "x" if c.get("marcado") else " "
            dis = " [DISABLED]" if c["disabled"] else ""
            extra = ""
            if "opcoes" in c:
                extra = f"  opcoes={c['n_opcoes']}: " + ", ".join(
                    f"{v}={t[:28]}" for v, t in c["opcoes"])
            if c.get("a4j"):
                extra += f"  A4J={c['a4j']}"
            print(f"   [{marca}] {c['tipo']:<9} {c['name']:<46} {c['rotulo']!r:<40}"
                  f" val={c['valor']!r}{dis}{extra}")
    gs = gatilhos(doc)
    if gs:
        print(f"\n-- gatilhos ({len(gs)}):")
        for g in gs:
            print(f"   {g['tipo']:<8} {str(g['rotulo'])!r:<42} form={g['form']!r} "
                  f"params={g['parametros']}")
