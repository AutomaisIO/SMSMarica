import re, sys
from sernit.client import URL_SOLIC, campos_todos, login_e_modulo, seguir_redirect_a4j, sopa, viewstate
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals = [o.get("value") for o in sel.find_all("option")]
        if "EM_FILA" in vals and "AGENDADA" in vals:
            return sel.get("name")
    return None

def buscar(s, situacao, idsol):
    tela = s.get(URL_SOLIC).text
    d = sopa(tela); f = d.find("form", id="form0")
    act = f.get("action") or URL_SOLIC
    sit = select_situacao(d)
    btn = f.find("input", attrs={"value": re.compile("Pesquisar", re.I)}) or f.find("input", attrs={"type":"submit"})
    dados = campos_todos(d, "form0")
    dados[sit] = situacao
    if f.find(attrs={"name":"form0:idSolicitacao"}) is not None:
        dados["form0:idSolicitacao"] = idsol
        campo_id = "achou form0:idSolicitacao"
    else:
        campo_id = "NAO achou form0:idSolicitacao"
    dados[btn.get("name")] = btn.get("value") or "Pesquisar"
    dados["AJAXREQUEST"]="form0"; dados["AJAX:EVENTS_COUNT"]="1"
    dados["javax.faces.ViewState"] = viewstate(tela) or ""
    r = s.post(act, dados, ajax=True)
    html = seguir_redirect_a4j(s, r.text)
    grade = sopa(html).find("table", id="form0:listagem")
    corpo = [tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
    msg = sopa(html).find(id=re.compile("msgErro|messages"))
    print(f"[situacao={situacao} id={idsol}] {campo_id} -> linhas={len(corpo)}  msg={(msg.get_text(' ',strip=True)[:80] if msg else '')}")

s = login_e_modulo("ambulatorial")
buscar(s, "ALTA", "495")
buscar(s, "PENDENTE", "894")
buscar(s, "EM_FILA", "")   # controle: EM_FILA sem id deve trazer muitas
s.close()
