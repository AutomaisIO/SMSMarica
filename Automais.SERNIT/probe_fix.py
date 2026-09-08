import re, sys
from sernit.client import URL_SOLIC, campos_todos, login_e_modulo, seguir_redirect_a4j, sopa, viewstate
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
def sel_sit(doc):
    for s in doc.find_all("select"):
        v=[o.get("value") for o in s.find_all("option")]
        if "EM_FILA" in v and "AGENDADA" in v: return s.get("name")
def linhas(html):
    g=sopa(html).find("table",id="form0:listagem"); return [tr for tr in (g.find_all("tr") if g else []) if tr.find("td")]
s=login_e_modulo("ambulatorial")
tela=s.get(URL_SOLIC).text; d=sopa(tela); f=d.find("form",id="form0"); act=f.get("action") or URL_SOLIC
sit=sel_sit(d); btn=f.find("input",attrs={"value":re.compile("Pesquisar",re.I)}) or f.find("input",attrs={"type":"submit"})
g=campos_todos(d,"form0"); g[sit]="AGENDADA"; g["form0:dtInicialSolicitacaoInputDate"]="01/01/2015"; g["form0:dtFinalSolicitacaoInputDate"]="31/12/2020"
g[btn.get("name")]=btn.get("value") or "Pesquisar"; g["AJAXREQUEST"]="form0"; g["AJAX:EVENTS_COUNT"]="1"; g["javax.faces.ViewState"]=viewstate(tela) or ""
r=s.post(act,g,ajax=True); h=seguir_redirect_a4j(s,r.text); dd=sopa(h)
campos_poluido=campos_todos(dd,"form0")   # datas preenchidas
# FIX: busca por id LIMPANDO as datas (empty)
b=dict(campos_poluido); b["form0"]="form0"; b[sit]="PENDENTE"; b["form0:idSolicitacao"]="894"
b["form0:dtInicialSolicitacaoInputDate"]=""; b["form0:dtFinalSolicitacaoInputDate"]=""
b[btn.get("name")]=btn.get("value") or "Pesquisar"; b["AJAXREQUEST"]="form0"; b["AJAX:EVENTS_COUNT"]="1"; b["javax.faces.ViewState"]=viewstate(h) or ""
r2=s.post(act,b,ajax=True); h2=seguir_redirect_a4j(s,r2.text)
print(f"[FIX: id=894 com datas LIMPAS] linhas={len(linhas(h2))}  (esperado 1)")
s.close()
