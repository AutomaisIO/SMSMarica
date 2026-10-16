"""Mapa completo do que muda na requisição entre DIAGNÓSTICA e RASTREAMENTO.

Pergunta (16/10/2026): o `mapear_requisicao_nova.py` só mede o PRIMEIRO nível — "Diagnóstica abre
6 checkboxes" — e a operação diz que a diferença é bem maior. Aqui se desce a árvore: cada
indicação da diagnóstica é marcada, e tudo o que ela abrir com A4J é marcado de novo (até
`--profundidade` níveis), comparando a tela INTEIRA a cada passo — não só o bloco do tipo.

Ordem: estado base → Rastreamento → Diagnóstica → indicações (cumulativo). Rastreamento antes, para
a comparação Diagnóstica × Rastreamento não carregar o estado das indicações.

Nunca clica Salvar — a trava de `SiscanClient` barra "salvar"/"encerrar"/"confirmar"/"incluir".
Não imprime dado da paciente: só nomes de campo, rótulos e códigos de opção. O inventário completo
vai para capturas/mapa-tipo-mamografia.json (gitignored).

    python mapear_tipo_mamografia.py                 # usa o 1º caso de casos.py
    python mapear_tipo_mamografia.py --cns <CNS> --cnes <CNES da unidade>

PRODUÇÃO FEDERAL. Só leitura.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import OrderedDict

from siscan import exame, requisicao as req
from siscan.client import CAPTURAS, BloqueioEscrita, SiscanClient, aplicar_a4j, mensagens
from siscan.inspecao import _a4j_de, _rotulo_de

RE_OCULTO = re.compile(r"display\s*:\s*none", re.I)
SO_CONTAGEM = {"frm:responsavelColeta"}   # nomes de profissionais: só a contagem
ATRIBUTOS_QUE_MUDAM = ("oculto", "disabled", "n_opcoes", "rotulo", "legendas")


def txt(e) -> str:
    return " ".join(e.get_text(" ", strip=True).split())


def _texto_util(t) -> bool:
    return bool(t and t.strip()) and t.parent.name not in ("script", "style")


def legendas(el) -> list[str]:
    out = []
    for fs in reversed(el.find_parents("fieldset")):
        lg = fs.find("legend", recursive=False) or fs.find("legend")
        if lg:
            out.append(txt(lg))
    return out


def rotulo(doc, el) -> str:
    tipo = (el.get("type") or el.name).lower()
    if el.get("id"):
        lb = doc.find("label", attrs={"for": el["id"]})
        if lb and txt(lb):
            return txt(lb)[:90]
    lb = el.find_parent("label")
    if lb and txt(lb):
        return txt(lb)[:90]
    if tipo in ("radio", "checkbox"):
        s = el.find_next(string=_texto_util)
        return " ".join(s.split())[:90] if s else ""
    r = _rotulo_de(el)
    if r:
        return r[:90]
    s = el.find_previous(string=_texto_util)
    return " ".join(s.split())[:90] if s else ""


def oculto(el) -> bool:
    return any(RE_OCULTO.search(e.get("style") or "") for e in [el, *el.find_parents()]
               if hasattr(e, "get"))


def inventario(doc) -> "OrderedDict[str, dict]":
    """Todo controle PRESENTE no form (radio/checkbox contam desmarcados — ver mapear_requisicao_nova)."""
    f = doc.find("form", id="frm")
    inv: OrderedDict[str, dict] = OrderedDict()
    if f is None:
        return inv
    for el in f.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or nome == "javax.faces.ViewState":
            continue
        tipo = (el.get("type") or el.name).lower()
        valor = el.get("value") or ("on" if tipo == "checkbox" else "")
        chave = f"{nome}={valor}" if tipo in ("radio", "checkbox") else nome
        js = " ".join(filter(None, (el.get(ev) for ev in ("onclick", "onchange", "onblur"))))
        tem_a4j = bool(_a4j_de(js))
        item = {"name": nome, "tipo": tipo,
                "valor": valor if tipo in ("radio", "checkbox") else "",
                "rotulo": rotulo(doc, el), "legendas": legendas(el),
                "a4j": tem_a4j, "js": "" if tem_a4j else js[:140],
                "disabled": el.has_attr("disabled"), "oculto": oculto(el)}
        if el.name == "select":
            ops = [txt(o) for o in el.find_all("option")]
            item["n_opcoes"] = len(ops)
            item["opcoes"] = [] if nome in SO_CONTAGEM else ops[:40]
        inv[chave] = item
    return inv


def fmt(it: dict) -> str:
    partes = [f"{it['tipo']:<8} {it['name']}" + (f"={it['valor']}" if it["valor"] else ""),
              f"— {it['rotulo']!r}"]
    if it["a4j"]:
        partes.append("[A4J]")
    elif it["js"]:
        partes.append(f"[js: {it['js'][:70]}]")
    if it["oculto"]:
        partes.append("[OCULTO]")
    if it["disabled"]:
        partes.append("[DISABLED]")
    if "n_opcoes" in it:
        partes.append(f"opções={it['n_opcoes']}"
                      + (f": {' | '.join(it['opcoes'][:25])}" if it["opcoes"] else ""))
    return " ".join(partes)


def caminho(it: dict) -> str:
    return " › ".join(it["legendas"]) or "(fora de fieldset)"


def diff(antes, depois):
    abriu = [k for k in depois if k not in antes]
    fechou = [k for k in antes if k not in depois]
    mudou = [k for k in depois if k in antes
             and any(antes[k].get(a) != depois[k].get(a) for a in ATRIBUTOS_QUE_MUDAM)]
    return abriu, fechou, mudou


def imprimir_diff(titulo: str, antes, depois, ids=(), msgs=()) -> list[str]:
    abriu, fechou, mudou = diff(antes, depois)
    print(f"\n### {titulo}\n    abriu {len(abriu)} · fechou {len(fechou)} · mudou {len(mudou)}"
          + (f" · reRender={list(ids)}" if ids else ""))
    for m in dict.fromkeys(m for m in msgs if len(m) < 250):
        print(f"    ! {m}")
    for rot, ks, fonte in (("ABRIU", abriu, depois), ("FECHOU", fechou, antes)):
        grupos: OrderedDict[str, list[str]] = OrderedDict()
        for k in ks:
            grupos.setdefault(caminho(fonte[k]), []).append(k)
        for cam, itens in grupos.items():
            print(f"    {rot} em [{cam}]")
            for k in itens:
                print(f"        {fmt(fonte[k])}")
    for k in mudou:
        dif = {a: (antes[k].get(a), depois[k].get(a)) for a in ATRIBUTOS_QUE_MUDAM
               if antes[k].get(a) != depois[k].get(a)}
        print(f"    MUDOU {k}: {dif}")
    return abriu


class Varredura:
    def __init__(self, c: SiscanClient, limite: int, profundidade: int):
        self.c = c
        self.estado: dict[str, str] = {}
        self.chamadas = 0
        self.limite = limite
        self.profundidade = profundidade
        self.mapa: dict[str, dict] = {}

    def marcar(self, doc, nome: str, valor: str, cap: str):
        el = next((i for i in doc.find_all(["input", "select"], attrs={"name": nome})
                   if i.name == "select" or (i.get("value") or "on") == valor), None)
        if el is None:
            return None, [], []
        js = " ".join(filter(None, (el.get(ev) for ev in ("onclick", "onchange", "onblur"))))
        params = _a4j_de(js)
        if not params:
            return "SEM-A4J", [], []
        self.estado[nome] = valor
        self.chamadas += 1
        # Todo valor já empurrado vai de novo no POST: o parcial do A4J não devolve o radio
        # marcado (FLUXO §5) e um checkbox ausente do POST é decodificado como desmarcado.
        r = self.c.post_a4j(doc, "frm", dict(self.estado), params, cap)
        parcial = self.c.sopa(r)
        ids: list[str] = []
        for m in parcial.find_all("meta", attrs={"name": "Ajax-Update-Ids"}):
            ids += [i.strip() for i in (m.get("content") or "").split(",") if i.strip()]
        return aplicar_a4j(doc, parcial), ids, mensagens(parcial)

    def explorar(self, doc, inv, chaves_novas, prof: int, prefixo: str):
        grupos: OrderedDict[str, list[dict]] = OrderedDict()
        for k in chaves_novas:
            it = inv[k]
            if it["tipo"] == "select" and it["a4j"]:
                print(f"    (select com A4J não explorado: {fmt(it)})")
            if not it["a4j"] or it["disabled"] or it["tipo"] not in ("radio", "checkbox"):
                continue
            grupos.setdefault(it["name"], []).append(it)
        for nome, itens in grupos.items():
            radio = itens[0]["tipo"] == "radio"
            inv_grupo = inv
            for it in itens:
                if self.chamadas >= self.limite:
                    print(f"\n!!! limite de {self.limite} chamadas A4J atingido — parei aqui")
                    return doc, inv
                titulo = f"{prefixo} › {it['rotulo'] or it['name']} ({it['name'].replace('frm:', '')}={it['valor']})"
                try:
                    novo, ids, msgs = self.marcar(doc, nome, it["valor"], f"tm-{nome.replace('frm:', '')}-{it['valor']}")
                except BloqueioEscrita as e:
                    print(f"\n### {titulo}\n    BLOQUEADO pela trava de escrita: {e}")
                    continue
                if novo is None or novo == "SEM-A4J":
                    continue
                inv_novo = inventario(novo)
                abriu = imprimir_diff(titulo, inv_grupo if radio else inv, inv_novo, ids, msgs)
                self.mapa[titulo] = {k: inv_novo[k] for k in abriu}
                doc = novo
                if abriu and prof < self.profundidade:
                    doc, inv_novo = self.explorar(doc, inv_novo, abriu, prof + 1, titulo)
                inv = inv_novo
        return doc, inv


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cns")
    ap.add_argument("--cnes", help="CNES da unidade requisitante")
    ap.add_argument("--profundidade", type=int, default=3)
    ap.add_argument("--limite", type=int, default=120, help="teto de chamadas A4J")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    cns, cnes = args.cns, args.cnes
    if not cns:
        import casos
        caso = next(iter(casos.CASOS.values()))
        cns, cnes = caso["cns"], cnes or caso["cnes_unidade"]

    with SiscanClient(somente_leitura=True, capturar=True) as c:
        c.login()
        doc = exame.abrir(c)
        doc = req.novo_exame(c, doc)
        doc = req.digitar_cartao_sus(c, doc, cns)
        doc = req.marcar_tipo_exame(c, doc, "01")
        base = req.avancar(c, doc, req.unidade_por_cnes(doc, cnes), "01")
        inv_base = inventario(base)
        print(f"etapa 2 carregada: {len(inv_base)} controles no estado base (sem tipo de mamografia)")
        for m in dict.fromkeys(m for m in mensagens(base) if len(m) < 250):
            print(f"    ! {m}")

        v = Varredura(c, args.limite, args.profundidade)

        doc_r, ids, msgs = v.marcar(base, "frm:j_id242", "02", "tm-rastreamento")
        inv_r = inventario(doc_r)
        imprimir_diff("BASE → RASTREAMENTO (02)", inv_base, inv_r, ids, msgs)
        print(f"    Responsável: {next((i['n_opcoes'] for i in inv_r.values() if i['name'] == 'frm:responsavelColeta'), '?')} opções")
        _, inv_r = v.explorar(doc_r, inv_r, [k for k in inv_r if k not in inv_base], 1, "RASTREAMENTO")

        # A população 02 (risco elevado/história familiar) nasce DISABLED. Quem a liga? Suspeita: o
        # A4J de "Apresenta risco elevado" (j_id91) — que, medido SEM tipo marcado, "não abria nada".
        print("\n### População do rastreamento × APRESENTA RISCO ELEVADO (j_id91)")
        for risco, nome_risco in (("01", "Sim"), ("02", "Não"), ("03", "Não sabe"), ("01", "Sim de novo")):
            doc_r, ids, msgs = v.marcar(doc_r, "frm:j_id91", risco, f"tm-risco-{risco}")
            pops = [(i.get("value"), "DISABLED" if i.has_attr("disabled") else "habilitada")
                    for i in doc_r.find_all("input", attrs={"name": "frm:tipoMamografiaRastreamento"})]
            print(f"    risco elevado = {nome_risco:<12} → população {pops}  reRender={ids[:4]}")
        inv_r = inventario(doc_r)

        doc_d, ids, msgs = v.marcar(doc_r, "frm:j_id242", "01", "tm-diagnostica")
        inv_d = inventario(doc_d)
        imprimir_diff("RASTREAMENTO → DIAGNÓSTICA (01) — a troca de tipo", inv_r, inv_d, ids, msgs)
        print(f"    Responsável: {next((i['n_opcoes'] for i in inv_d.values() if i['name'] == 'frm:responsavelColeta'), '?')} opções")
        imprimir_diff("BASE → DIAGNÓSTICA (01)", inv_base, inv_d)

        novos_d = [k for k in inv_d if k not in inv_r]
        _, inv_fim = v.explorar(doc_d, inv_d, novos_d, 1, "DIAGNÓSTICA")

        imprimir_diff("DIAGNÓSTICA (só o tipo) → DIAGNÓSTICA com TODAS as indicações abertas", inv_d, inv_fim)
        print(f"\n{v.chamadas} chamadas A4J. Nada foi salvo.")

        CAPTURAS.mkdir(exist_ok=True)
        (CAPTURAS / "mapa-tipo-mamografia.json").write_text(json.dumps({
            "base": inv_base, "rastreamento": inv_r, "diagnostica": inv_d,
            "diagnostica_tudo_aberto": inv_fim, "passos": v.mapa,
        }, ensure_ascii=False, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
