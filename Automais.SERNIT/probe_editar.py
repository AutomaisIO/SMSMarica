"""Sonda 6: aba EDITAR — mapa + teste reversível de telefone.

Dois modos:
  (default / --map)      SOMENTE LEITURA. Abre a aba Editar de uma solicitação e mapeia:
                         os 3 telefones (por rótulo), os campos `disabled` (que o navegador
                         não envia), o botão *Gravar* e o conjunto de campos do form0.
  --testar-telefone      ESCRITA REVERSÍVEL AUTORIZADA (25/08/2026): altera UM telefone,
                         relê do zero para provar que gravou (comparando só os dígitos, porque
                         o SER aplica máscara), e RESTAURA o valor original, conferindo de novo.
                         Aplica a TRAVA INVERTIDA do SER-RJ §10: recusa se qualquer campo além
                         do telefone-alvo divergir do que a tela renderizou.

Uso:
  python probe_editar.py [ID_SOLICITACAO]                 # mapa (ensaio)
  python probe_editar.py [ID_SOLICITACAO] --testar-telefone
"""

from __future__ import annotations

import re
import sys

from sernit.client import (URL_SOLIC, campos_todos, hidden_do_form,
                           login_e_modulo, seguir_redirect_a4j, sopa, viewstate)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

args = [a for a in sys.argv[1:]]
TESTAR = "--testar-telefone" in args
ALVO = next((a for a in args if not a.startswith("--")), None)


def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = {o.get("value") for o in sel.find_all("option")}
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None


def so_digitos(s: str) -> str:
    return re.sub(r"\D", "", s or "")


def achar_gravar(doc):
    """Controle *Gravar* do form0 — por valor/texto/título, nunca por id (volátil)."""
    f = doc.find("form", id="form0")
    if not f:
        return None
    for el in f.find_all(["input", "a", "button"]):
        rot = (el.get("value") or el.get("title") or
               (el.get_text(" ", strip=True) if el.name != "input" else "")) or ""
        if re.fullmatch(r"\s*gravar\s*", rot, re.I):
            return el.get("id")
    return None


def campos_editar(doc):
    """Campos submissíveis do form0 COMO O NAVEGADOR: pula os disabled (o SER não os recebe)."""
    f = doc.find("form", id="form0")
    d, disabled = {}, []
    if not f:
        return d, disabled
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset"}:
            continue
        if i.has_attr("disabled"):
            disabled.append(n)
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        d[n] = i.get("value") or ""
    for s in f.find_all("select"):
        if s.get("name") and not s.has_attr("disabled"):
            o = s.find("option", selected=True) or s.find("option")
            d[s["name"]] = (o.get("value") if o else "") or ""
    for t in f.find_all("textarea"):
        if t.get("name") and not t.has_attr("disabled"):
            d[t["name"]] = t.get_text() or ""
    return d, disabled


def telefones(doc):
    """{rótulo: name} dos campos de telefone — resolvido pelo RÓTULO, nunca pelo j_id."""
    out = {}
    f = doc.find("form", id="form0")
    if not f:
        return out
    for lab in f.find_all(string=re.compile("Telefone", re.I)):
        td = lab.find_parent("td")
        if not td:
            continue
        sib = td.find_next_sibling("td")
        inp = sib.find("input") if sib else None
        if inp and inp.get("name"):
            out[" ".join(lab.split())] = inp.get("name")
    return out


def tela_pesquisa(s):
    """GET da tela de pesquisa, resiliente ao estado estranho pós-escrita (reacende o módulo)."""
    for _ in range(4):
        h = s.get(URL_SOLIC).text
        d = sopa(h)
        if d.find("input", attrs={"value": re.compile("Pesquisar", re.I)}) and d.find("form", id="form0"):
            return h, d
        s.ativar_modulo("ambulatorial")
    raise SystemExit("não consegui a tela de pesquisa com o botão Pesquisar")


def abrir_editar(s, alvo):
    tela, d = tela_pesquisa(s)
    sit = select_situacao(d)
    btn = d.find("input", attrs={"value": re.compile("Pesquisar", re.I)})
    dados = campos_todos(d, "form0")
    if alvo:
        dados["form0:idSolicitacao"] = alvo
    else:
        dados[sit] = "EM_FILA"
    dados[btn.get("name")] = btn.get("value")
    dados |= {"AJAXREQUEST": "form0", "AJAX:EVENTS_COUNT": "1",
              "javax.faces.ViewState": viewstate(tela) or ""}
    html = seguir_redirect_a4j(s, s.post(URL_SOLIC, dados, ajax=True).text)
    dd = sopa(html)
    grade = dd.find("table", id="form0:listagem")
    corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
    if not corpo:
        raise SystemExit("busca sem resultado — nada para editar")
    linha = corpo[0]
    id_txt = " ".join(linha.find("td").get_text(" ", strip=True).split())
    lid = next((a.get("id") for a in linha.find_all("a")
                if "editar" in " ".join(a.get_text(" ", strip=True).split()).lower()), None)
    if not lid:
        raise SystemExit("item 'Editar' não achado na 1ª linha (situação sem Editar?)")
    ph = hidden_do_form(html, "form0")
    ph |= {"AJAXREQUEST": "_viewRoot", lid: lid, "ajaxSingle": lid,
           "AJAX:EVENTS_COUNT": "1", "javax.faces.ViewState": viewstate(html) or ""}
    ed = seguir_redirect_a4j(s, s.post(URL_SOLIC, ph, ajax=True).text)
    return id_txt, ed


def main() -> int:
    s = login_e_modulo("ambulatorial")
    id_txt, ed = abrir_editar(s, ALVO)
    from sernit.client import CAP
    (CAP / "editar.html").write_text(ed, encoding="utf-8")
    d = sopa(ed)
    tels = telefones(d)
    campos, disabled = campos_editar(d)
    gravar_id = achar_gravar(d)

    print(f"solicitação: {id_txt}  -> capturas/editar.html ({len(ed)} bytes)")
    print(f"form0 presente: {bool(d.find('form', id='form0'))}")
    print("\n### TELEFONES (rótulo → name) — resolvidos por rótulo")
    for rot, name in tels.items():
        val = d.find("input", attrs={"name": name})
        v = so_digitos(val.get("value") if val else "")
        print(f"  {rot:26} name={name!r:22} dígitos={('*'*len(v)) if v else '(vazio)'} ({len(v)})")
    print(f"\n### CAMPOS DISABLED (não vão no POST): {disabled}")
    print(f"### Gravar: {gravar_id or '(NÃO ACHADO)'}")
    print(f"### total de campos submissíveis (comoNavegador): {len(campos)}")

    if not TESTAR:
        print("\n(modo ENSAIO — nenhuma escrita. Rode com --testar-telefone para o round-trip.)")
        s.close()
        return 0

    # ------------------------------------------------------------------ ESCRITA REVERSÍVEL
    if not tels or not gravar_id:
        raise SystemExit("sem telefone ou sem botão Gravar — não dá para testar com segurança")

    # escolhe um alvo: prefere um campo VAZIO (mínimo impacto); senão, muda 1 dígito e restaura
    alvo_rot, alvo_name = None, None
    for rot, name in tels.items():
        inp = d.find("input", attrs={"name": name})
        if inp and not so_digitos(inp.get("value") or ""):
            alvo_rot, alvo_name = rot, name
            break
    vazio = alvo_name is not None
    if not vazio:  # nenhum vazio: usa o 1º e altera só o último dígito
        alvo_rot, alvo_name = next(iter(tels.items()))
    original = d.find("input", attrs={"name": alvo_name}).get("value") or ""
    orig_dig = so_digitos(original)
    if vazio:
        novo = "(21) 90000-0000"          # número de teste em campo que estava vazio
    else:
        d2 = orig_dig[:-1] + ("1" if orig_dig[-1] != "1" else "2")
        novo = f"({d2[:2]}) {d2[2:7]}-{d2[7:11]}" if len(d2) == 11 else d2
    print(f"\n### TESTE de escrita no campo {alvo_rot!r} (estava {'VAZIO' if vazio else 'preenchido'})")
    print(f"    original(dígitos)={orig_dig!r}  ->  novo(dígitos)={so_digitos(novo)!r}")

    def gravar_telefone(valor):
        # relê a aba Editar do zero para pegar ViewState/cid frescos e o estado renderizado
        _id, ed2 = abrir_editar(s, id_txt)
        dd = sopa(ed2)
        base, _dis = campos_editar(dd)          # comoNavegador: sem disabled
        # TRAVA INVERTIDA: o POST parte do renderizado; só o telefone-alvo muda
        post = dict(base)
        post[alvo_name] = valor
        gid = achar_gravar(dd)
        form0 = dd.find("form", id="form0")
        act_ed = form0.get("action") if form0 else None       # §3.3: postar no ACTION da tela Editar
        if not gid or not act_ed:
            raise SystemExit("Gravar/action não localizados na releitura — abortando escrita")
        # confere que nada além do alvo diverge do renderizado
        divergentes = [k for k, v in post.items() if k != alvo_name and base.get(k) != v]
        if divergentes:
            raise SystemExit(f"TRAVA INVERTIDA: divergência além do telefone: {divergentes}")
        post |= {"AJAXREQUEST": "form0", gid: gid, "AJAX:EVENTS_COUNT": "1",
                 "javax.faces.ViewState": viewstate(ed2) or ""}
        r = s.post_escrita(act_ed, post, operacao=f"Editar telefone {alvo_rot} da solic {id_txt}", ajax=True)
        resp = seguir_redirect_a4j(s, r.text)
        from sernit.client import CAP as _CAP
        (_CAP / "editar_gravar_resp.html").write_text(resp, encoding="utf-8")
        # o que o SER respondeu? (mensagem de sucesso/erro/modal)
        dm = sopa(resp)
        msgs = [" ".join(m.get_text(" ", strip=True).split())
                for m in dm.find_all(id=re.compile("msg|messages", re.I)) if m.get_text(strip=True)]
        modal = "modal" in resp.lower() and ("anexo" in resp.lower() or "confirm" in resp.lower())
        print(f"    resposta do Gravar: msgs={msgs[:2] if msgs else '(nenhuma)'}  modal_suspeito={modal}")
        return resp

    def retrato_telefones():
        _id, edx = abrir_editar(s, id_txt)
        dx = sopa(edx)
        return {rot: so_digitos((dx.find("input", attrs={"name": nm}) or {}).get("value") or "")
                for rot, nm in telefones(dx).items()}

    # retrato ANTES (todos os telefones) — para provar que só o alvo mexeu
    antes = retrato_telefones()
    print(f"    retrato antes: { {k: len(v) for k, v in antes.items()} } (nº de dígitos)")

    # 1) grava o novo, relê e confere
    gravar_telefone(novo)
    depois_grava = retrato_telefones()
    ok_grava = (depois_grava.get(alvo_rot) == so_digitos(novo))
    outros_mexidos = {k: (antes[k], depois_grava.get(k)) for k in antes
                      if k != alvo_rot and antes[k] != depois_grava.get(k)}
    print(f"    após gravar: alvo={'OK gravou' if ok_grava else 'FALHOU'}  "
          f"outros_telefones_alterados={outros_mexidos or 'nenhum'}")

    # 2) restaura o original, relê e confere TODOS os telefones voltaram ao retrato inicial
    gravar_telefone(original)
    depois = retrato_telefones()
    ok_restaura = (depois == antes)
    print(f"    após restaurar: {'OK — todos os telefones idênticos ao início' if ok_restaura else '!! DIVERGÊNCIA'}")
    if not ok_restaura:
        print("!!! ATENÇÃO: restaurar à mão. antes=", antes, " depois=", depois)

    # 3) confirma que a situação não mudou (o save não deve promover o pedido)
    _id2, edz = abrir_editar(s, id_txt)  # só EM_FILA/PENDENTE oferecem Editar; abrir já é o sinal
    print(f"    situação ainda editável (EM_FILA/PENDENTE)? {'sim' if '<form id=\"form0\"' in edz else 'VERIFICAR'}")

    s.close()
    return 0 if (ok_grava and ok_restaura and not outros_mexidos) else 1


if __name__ == "__main__":
    raise SystemExit(main())
