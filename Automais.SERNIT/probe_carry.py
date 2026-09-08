import re, sys
from sernit.client import URL_SOLIC, campos_todos, login_e_modulo, seguir_redirect_a4j, sopa, viewstate
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def sel_sit(doc):
    for s in doc.find_all("select"):
        v=[o.get("value") for o in s.find_all("option")]
        if "EM_FILA" in v and "AGENDADA" in v: return s.get("name")

def vs_qualquer(html):
    m=re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html); return m.group(1) if m else None

def linhas(html):
    g=sopa(html).find("table",id="form0:listagem"); return [tr for tr in (g.find_all("tr") if g else []) if tr.find("td")]

s=login_e_modulo("ambulatorial")
tela=s.get(URL_SOLIC).text; d=sopa(tela); f=d.find("form",id="form0")
act=f.get("action") or URL_SOLIC
sit=sel_sit(d)
btn=f.find("input",attrs={"value":re.compile("Pesquisar",re.I)}) or f.find("input",attrs={"type":"submit"})
form_limpo=campos_todos(d,"form0")   # snapshot do form LIMPO (como _htmlForm do backend)
vs_limpo=viewstate(tela)

# 1) busca estilo GRADE: EM_FILA com data (fatiamento), captura viewstate da resposta (=_ultimoViewState)
g1=dict(form_limpo); g1[sit]="EM_FILA"; g1["form0:dtInicialSolicitacaoInputDate"]="01/01/2015"; g1["form0:dtFinalSolicitacaoInputDate"]="27/08/2026"
g1[btn.get("name")]=btn.get("value") or "Pesquisar"; g1["AJAXREQUEST"]="form0"; g1["AJAX:EVENTS_COUNT"]="1"; g1["javax.faces.ViewState"]=vs_limpo or ""
r1=s.post(act,g1,ajax=True); h1=seguir_redirect_a4j(s,r1.text)
vs_grade=vs_qualquer(h1)   # viewstate herdado
print("grade EM_FILA linhas=",len(linhas(h1)),"vs_grade_capturado=",bool(vs_grade))

# 2) BACKEND-like: form LIMPO + viewstate HERDADO da grade (sem GET fresco)
b=dict(form_limpo); b["form0"]="form0"; b[sit]="PENDENTE"; b["form0:idSolicitacao"]="894"
b[btn.get("name")]=btn.get("value") or "Pesquisar"; b["AJAXREQUEST"]="form0"; b["AJAX:EVENTS_COUNT"]="1"; b["javax.faces.ViewState"]=vs_grade or ""
r2=s.post(act,b,ajax=True); h2=seguir_redirect_a4j(s,r2.text)
print("[CARRYOVER backend-like] id=894 com viewstate herdado -> linhas=",len(linhas(h2)))
s.close()
