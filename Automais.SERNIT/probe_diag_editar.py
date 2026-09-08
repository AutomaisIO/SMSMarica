"""Diagnóstico: por que o BACKEND falha ao abrir a aba Editar ("combo de Tipo ausente")
enquanto abrir_aba_nova (lab) funciona.

Compara, no mesmo login/sessão:
  A) POST "lab"     = campos_todos (inclui textareas; select = selected|primeira opção)
  B) POST "backend" = CamposDoForm(comoNavegador=False): SEM textareas; select = selected (ou vazio)

Imprime, para cada um: se veio <meta Location>, o destino (com/sem cid), e se a página de
destino contém form0:comboTipoRecurso. SOMENTE LEITURA (nunca aciona Gravar).
"""
from __future__ import annotations
import re, sys
from sernit.client import (URL_SOLIC, campos_todos, login_e_modulo,
                           seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def campos_backend(doc, form_id="form0") -> dict:
    """Replica SernitHtmlParser.CamposDoForm(comoNavegador=false): inputs (skip submit/button/
    image/reset; checkbox/radio só se checked), selects (selected; sem selected -> vazio),
    e NÃO inclui textareas."""
    f = doc.find("form", id=form_id)
    d: dict[str, str] = {}
    if not f:
        return d
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset"}:
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        d[n] = i.get("value") or ""
    for s in f.find_all("select"):
        if s.get("name"):
            o = s.find("option", selected=True)   # backend: só o selected; sem selected -> vazio
            d[s["name"]] = (o.get("value") if o else "") or ""
    # textareas: NÃO (comoNavegador=false)
    return d


def abrir(s, builder, rotulo):
    tela = s.get(URL_SOLIC).text
    d = sopa(tela)
    f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    dados = builder(d, "form0") | {
        "form0": "form0",
        "form0:editar_server_submit": "form0:editar_server_submit",
        "javax.faces.ViewState": viewstate(tela) or "",
    }
    r = s.post(act, dados)
    body = r.text
    m = re.search(r'<meta name="Location" content="([^"]+)"', body)
    destino = m.group(1).replace("&amp;", "&") if m else None
    final = seguir_redirect_a4j(s, body)
    tem_combo = "form0:comboTipoRecurso" in final
    tem_textarea = len(d.find("form", id="form0").find_all("textarea")) if f else 0
    print(f"[{rotulo}] campos={len(dados)} textareas_no_form={tem_textarea} "
          f"meta_location={'SIM' if destino else 'NÃO'} cid={'cid=' in (destino or '')} "
          f"resp_bytes={len(body)} final_bytes={len(final)} comboTipoRecurso={'SIM' if tem_combo else 'NÃO'}")
    if destino:
        print("   destino:", destino[:160])
    if not tem_combo:
        print("   trecho final:", " ".join(final[:300].split()))
    return final


def main() -> int:
    s = login_e_modulo("ambulatorial")
    print("=== A) POST estilo LAB (campos_todos) ===")
    abrir(s, campos_todos, "lab")
    print("=== B) POST estilo BACKEND (sem textareas, select selected|vazio) ===")
    abrir(s, campos_backend, "backend")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
