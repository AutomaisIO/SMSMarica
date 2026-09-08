"""Sonda 9: varre os campos dinâmicos da nova solicitação do SERNIT, por Recurso.

O formulário de pedido tem um bloco fixo e um bloco DINÂMICO (`form0:campoDinamicoBox`) que muda
conforme o Recurso. Esta sonda troca Tipo → Recurso e anota os campos dinâmicos de cada um,
agrupando os recursos por "assinatura de formulário" (quais campos aparecem). Espelha a
`probe_campos_dinamicos.py` do SER-RJ, adaptada ao SERNIT (combo A4J em `form0`, box
`campoDinamicoBox`). SOMENTE LEITURA.

Uso:  python probe_campos_dinamicos.py [CONSULTA|EXAME|AMBOS] [limite]
"""

from __future__ import annotations

import json
import re
import sys
import time

from sernit.client import (CAP, URL_SOLIC, campos_todos, hidden_do_form,
                           login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SAIDA = CAP / "campos_dinamicos_sernit.json"


def evento_do_combo(html, nomeCombo):
    """Extrai o similarityGroupingId (id do evento onchange) do combo — volátil, lido da página."""
    d = sopa(html)
    sel = d.find("select", attrs={"name": nomeCombo})
    oc = (sel.get("onchange") if sel else "") or ""
    m = re.search(r"similarityGroupingId'\s*:\s*'([^']+)'", oc)
    return m.group(1) if m else None


class Nova:
    def __init__(self, c):
        self.c = c
        self.full = ""
        self.vs = None
        self.act = URL_SOLIC

    def abrir(self):
        tela = self.c.get(URL_SOLIC).text
        d = sopa(tela)
        act = d.find("form", id="form0").get("action") or URL_SOLIC
        dados = campos_todos(d, "form0") | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(tela) or "",
        }
        html = seguir_redirect_a4j(self.c, self.c.post(act, dados).text)
        self._absorver(html)
        return html

    def mudar(self, campo, valor):
        evento = evento_do_combo(self.full, campo)
        dados = hidden_do_form(self.full, "form0")
        dados[campo] = valor
        dados |= {
            # A4J manda _viewRoot (não 'form0') quando o init do componente não passa containerId —
            # medido: com 'form0' o comboRecurso volta disabled+vazio; com _viewRoot, popula.
            "AJAXREQUEST": "_viewRoot",
            "ajaxSingle": campo,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs or "",
        }
        if evento:
            dados[evento] = evento
        html = seguir_redirect_a4j(self.c, self.c.post(self.act, dados, ajax=True).text)
        self._absorver(html)
        return html

    def _absorver(self, html):
        if '<form id="form0"' in html:
            self.full = html
            d = sopa(html)
            self.act = d.find("form", id="form0").get("action") or self.act
        vs = viewstate(html)
        if vs:
            self.vs = vs


def catalogo(html):
    d = sopa(html)
    sel = d.find("select", attrs={"name": "form0:comboRecurso"})
    if not sel:
        return []
    return [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
            for o in sel.find_all("option")
            if (o.get("value") or "").strip()
            and "noSelection" not in (o.get("value") or "")]


def campos_dinamicos(html):
    """{name: {rotulo, tipo, obrigatorio, opcoes}} dentro de form0:campoDinamicoBox."""
    d = sopa(html)
    box = d.find(id="form0:campoDinamicoBox")
    if box is None:
        return {}
    out = {}
    for el in box.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome:
            continue
        t = el.name if el.name != "input" else (el.get("type") or "text").lower()
        if t in ("hidden", "submit", "button", "image", "reset"):
            continue
        # rótulo: label mais próximo (mesmo td/div ou anterior)
        rot = ""
        pai = el.find_parent(["td", "div", "tr"])
        if pai:
            lab = pai.find("label")
            if not lab:
                td = el.find_parent("td")
                prev = td.find_previous_sibling("td") if td else None
                lab = prev.find("label") if prev else prev
            rot = " ".join((lab.get_text(" ", strip=True) if lab else "").split())
        info = {"rotulo": rot.replace(" *", "").strip(" :*"), "tipo": t,
                "obrigatorio": "*" in rot}
        if el.name == "select":
            info["opcoes"] = [" ".join(o.get_text(" ", strip=True).split())[:50]
                              for o in el.find_all("option")][:30]
        out[nome] = info
    return out


def assinatura(dins):
    return tuple(sorted((i["rotulo"], i["tipo"]) for i in dins.values()))


def main() -> int:
    alvo = (sys.argv[1] if len(sys.argv) > 1 else "AMBOS").upper()
    limite = int(sys.argv[2]) if len(sys.argv) > 2 else 0

    s = login_e_modulo("ambulatorial")
    nova = Nova(s)
    resultado = {}
    if SAIDA.exists():
        resultado = json.loads(SAIDA.read_text(encoding="utf-8"))

    tipos = ["CONSULTA", "EXAME"] if alvo == "AMBOS" else [alvo]

    for tipo in tipos:
        nova.abrir()
        html = nova.mudar("form0:comboTipoRecurso", tipo)
        cat = catalogo(html)
        base = campos_dinamicos(html)
        print(f"\n=== {tipo}: {len(cat)} recursos | campos base: {[i['rotulo'] for i in base.values()]}")
        if not cat:
            print("  (comboRecurso não populou — ver capturas/nova.html)")
            continue

        feitos = resultado.setdefault(tipo, {})
        alvo_lista = cat[:limite] if limite else cat
        t0 = time.time()
        for n, (valor, nome) in enumerate(alvo_lista, 1):
            if valor in feitos:
                continue
            try:
                h = nova.mudar("form0:comboRecurso", valor)
                dins = campos_dinamicos(h)
            except Exception as e:  # noqa: BLE001
                print(f"  !! {valor} {nome[:40]}: {e}")
                continue
            feitos[valor] = {"nome": nome, "campos": dins}
            if n % 10 == 0 or n == len(alvo_lista):
                SAIDA.write_text(json.dumps(resultado, ensure_ascii=False, indent=1), encoding="utf-8")
                dec = time.time() - t0
                print(f"  {n}/{len(alvo_lista)} ({dec:.0f}s)  último: {nome[:44]} -> {len(dins)} campos dinâmicos")

        SAIDA.write_text(json.dumps(resultado, ensure_ascii=False, indent=1), encoding="utf-8")

    print("\n" + "=" * 78)
    print("FORMULÁRIOS DISTINTOS (recursos agrupados por campos dinâmicos)")
    print("=" * 78)
    for tipo, itens in resultado.items():
        grupos = {}
        for valor, info in itens.items():
            grupos.setdefault(assinatura(info["campos"]), []).append(info["nome"])
        print(f"\n### {tipo}: {len(itens)} recursos em {len(grupos)} formulários distintos")
        for chave, nomes in sorted(grupos.items(), key=lambda kv: -len(kv[1])):
            print(f"\n  [{len(nomes)} recursos] campos: {[f'{r} ({t})' for r, t in chave]}")
            for nm in nomes[:3]:
                print(f"      ex.: {nm[:66]}")
            if len(nomes) > 3:
                print(f"      ... e mais {len(nomes)-3}")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
