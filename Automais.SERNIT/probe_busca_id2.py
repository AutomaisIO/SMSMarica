import re, sys
from sernit.client import URL_SOLIC, login_e_modulo, seguir_redirect_a4j, sopa, viewstate
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def select_situacao(doc):
    for sel in doc.find_all("select"):
        vals=[o.get("value") for o in sel.find_all("option")]
        if "EM_FILA" in vals and "AGENDADA" in vals: return sel.get("name")
    return None

def campos_backend(doc, form_id="form0"):
    # Replica CamposDoForm(comoNavegador=false): inputs (skip submit/button/image/reset; radio/checkbox so checked),
    # selects (selected ou primeira), SEM textareas.
    f=doc.find("form", id=form_id); d={}
    for i in f.find_all("input"):
        n,t=i.get("name"),(i.get("type") or "text").lower()
        if not n or t in {"submit","button","image","reset"}: continue
        if t in {"checkbox","radio"} and not i.has_attr("checked"): continue
        d[n]=i.get("value") or ""
    for s in f.find_all("select"):
        if s.get("name"):
            o=s.find("option",selected=True) or s.find("option"); d[s["name"]]=(o.get("value") if o else "") or ""
    return d  # SEM textareas

def buscar(s, situacao, idsol, builder, rot):
    tela=s.get(URL_SOLIC).text; d=sopa(tela); f=d.find("form",id="form0")
    act=f.get("action") or URL_SOLIC
    sit=select_situacao(d)
    btn=f.find("input",attrs={"value":re.compile("Pesquisar",re.I)}) or f.find("input",attrs={"type":"submit"})
    dados=builder(d,"form0")
    dados["form0"]="form0"
    dados[sit]=situacao
    dados["form0:idSolicitacao"]=idsol
    dados[btn.get("name")]=btn.get("value") or "Pesquisar"
    dados["AJAXREQUEST"]="form0"; dados["AJAX:EVENTS_COUNT"]="1"
    dados["javax.faces.ViewState"]=viewstate(tela) or ""
    r=s.post(act,dados,ajax=True); html=seguir_redirect_a4j(s,r.text)
    grade=sopa(html).find("table",id="form0:listagem")
    corpo=[tr for tr in (grade.find_all("tr") if grade else []) if tr.find("td")]
    print(f"[{rot}] situacao={situacao} id={idsol} campos={len(dados)} -> linhas={len(corpo)}")

s=login_e_modulo("ambulatorial")
buscar(s,"PENDENTE","894",campos_backend,"BACKEND sem-textarea")
s.close()
