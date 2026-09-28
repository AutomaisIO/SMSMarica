"""Onde a especializada registra a ORIGEM do paciente (SISREG, SER, espontâneo)? SOMENTE LEITURA.

Uma sessão só do Prime (não alternar com o Saúde Mental: dividem a sessão única da conta). Escolhe a
unidade, lê a Agenda da Recepção do dia (GET) e o relatório Pacientes Agendados (CSV), e imprime
SÓ distribuições de campos categóricos curtos + padrões de URL das ações (ids mascarados).
Nomes, CNS, datas e textos longos não são impressos. HTML/CSV brutos ficam em capturas/.

Uso: python sondar_origem_sisreg.py <guid_unidade> DD/MM/AAAA DD/MM/AAAA
"""
from __future__ import annotations
import collections, re, sys
from prime.client import CAP, URL_LOGIN, PrimeSession, sopa
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
PII = re.compile(r"\d{5,}|\d{2}/\d{2}/\d{4}|^[A-ZÀ-Üa-zà-ü' ]{12,}$")

def cat(nome, vals):
    c = collections.Counter(v for v in vals if v)
    if c and len(c) <= 20 and not any(PII.search(v) or len(v) > 50 for v in c):
        print(f"   {nome[:32]:32} {c.most_common(12)}")
    else:
        print(f"   {nome[:32]:32} preenchido={sum(1 for v in vals if v)} distintos={len(c)} (livre/PII)")

def main(a):
    unidade, ini, fim = a
    s = PrimeSession(); s.entrar()
    r = s.get(URL_LOGIN)
    if "LoginView1$ddlUnidade" in r.text:
        s.selecionar_unidade(r, unidade=unidade)
    else:  # já logado noutra unidade: força o gate
        print("  (sem gate no GET login.aspx; seguindo com a unidade da sessão)")
    # 1) Agenda da recepção (GET, renderiza a grade do dia)
    r = s.get("/Prime/Agendamento/AgendaRecepcao.aspx"); h = r.text
    (CAP / "agenda_recepcao_unidade.html").write_text(h, encoding="utf-8")
    d = sopa(h)
    print("AgendaRecepcao", r.status_code, str(r.url)[-50:], len(h))
    for t in d.find_all("table"):
        ths = [x.get_text(" ", strip=True) for x in t.find_all("th", recursive=True)]
        if ths[:3] == ["Atendimento", "Agenda", "#"] or ("Procedência" in ths and "Tipo de Agendamento" in ths and ths[0] == "Atendimento"):
            linhas = [[x.get_text(" ", strip=True) for x in tr.find_all("td", recursive=False)] for tr in t.find_all("tr")]
            linhas = [l for l in linhas if len(l) >= len(ths) - 2]
            print("  grade:", ths, "| linhas:", len(linhas))
            for i, n in enumerate(ths):
                if n in ("Tipo da Demanda", "Procedência", "Tipo de Agendamento", "Situação", "Motivo da Procura", "Função de Atendimento"):
                    cat(n, [l[i] if i < len(l) else "" for l in linhas])
            break
    acoes = collections.Counter(re.sub(r"[0-9a-f]{8}-[0-9a-f\-]{27}|\d+", "<id>", m)
                                for m in re.findall(r"""['"]((?:\.\./)*[\w/]+\.aspx\?[\w=&<>%\-]*)""", h))
    print("  URLs de ação na agenda:", sorted(k for k in acoes if not re.search(r"(?i)relatorio|sessao|alterarsenha", k)))
    # 2) Pacientes Agendados (CSV)
    r = s.get("/Prime/Relatorios/RelatorioPacientesAgendadosRPT.aspx", params={
        "equipe": "", "prof_codigo": "", "idfuncaoatendimento": "", "idespecialidade": "",
        "dtInicio": ini, "dtFim": fim, "extensao": "CSV", "unidades": unidade})
    ct = r.headers.get("content-type", ""); (CAP / "agendados_unidade.csv").write_bytes(r.content)
    print("PacientesAgendados", r.status_code, ct, len(r.content))
    if "csv" in ct:
        regs = [x for x in r.content.decode("utf-8-sig", errors="replace").split("\r\n") if x.strip()]
        cab = regs[0].split(";"); print("  cabeçalho:", cab, "| registros:", len(regs) - 1)
        rows = [x.rstrip(";").split(";") for x in regs[1:]]
        rows = [x for x in rows if len(x) == len(cab)]
        for i, n in enumerate(cab):
            cat(n, [x[i] for x in rows])
    else:
        print("  html:", " ".join(sopa(r.text).get_text(" ", strip=True).split())[:200])
    s.close()

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
