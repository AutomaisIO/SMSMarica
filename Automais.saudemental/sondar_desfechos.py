"""Perfil (SEM PII) dos relatórios de desfecho do Prime Saúde Mental no CAPS da sessão: roda cada
relatório em XLS e imprime cabeçalho de colunas + distribuição dos campos categóricos (valores com
até 40 caracteres e poucas ocorrências distintas). Nomes, datas e documentos não são impressos.

Uso: python sondar_desfechos.py DD/MM/AAAA DD/MM/AAAA [guid_unidade]
"""
from __future__ import annotations
import collections, re, sys, contextlib, io
import xlrd
from saudemental.client import CAP, URL_LOGIN, SaudeMentalSession
import probe_relatorio
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RELS = [("imbDestinosPacientes", "rdpDestinosUsuariosInicio", "rdpDestinosUsuariosFim"),
        ("imbAbandono", "rdpMotivoSaidaInicio", "rdpMotivoSaidaFim"),
        ("imbObito", "rdpObitoInicio", "rdpObitoFim"),
        ("imbInternacao", "", ""),
        ("imbPermanenciaLeito", "rdpInicioLeito", "rdpFimLeito"),
        ("imbAtendimentosCancelados", "rdpAtendimentosCanceladosInicio", "rdpAtendimentosCanceladosFim"),
        ("imbSolicitacaoExames", "", ""),
        ("imbProcedimentosPaciente", "rdpInicioProducaoPaciente", "rdpFimProducaoPaciente")]

PII = re.compile(r"\d{5,}|\d{2}/\d{2}/\d{4}|^[A-ZÀ-Ü' ]{6,}$")

def perfil(f):
    sh = xlrd.open_workbook(f, logfile=io.StringIO()).sheet_by_index(0)
    linhas = [[str(sh.cell_value(r, c)).strip() for c in range(sh.ncols)] for r in range(sh.nrows)]
    # cabeçalho = linha com mais células curtas sem dígitos, depois das 8 primeiras
    cand = [(sum(1 for v in l if v and len(v) < 40 and not re.search(r"\d", v)), i) for i, l in enumerate(linhas)]
    cab_i = max((c for c in cand if c[1] > 6), default=(0, -1))[1]
    if cab_i < 0:
        return print("   (vazio)")
    cab = {c: v for c, v in enumerate(linhas[cab_i]) if v}
    print("   cabeçalho:", list(cab.values()))
    det = [l for l in linhas[cab_i + 1:] if sum(1 for v in l if v) >= max(2, len(cab) // 2)]
    print("   linhas de detalhe:", len(det))
    for c, nome in cab.items():
        vals = [l[c] for l in det if c < len(l)]
        cnt = collections.Counter(v for v in vals if v)
        if cnt and len(cnt) <= 15 and not any(PII.search(v) for v in cnt):
            print(f"     {nome[:30]:30} {cnt.most_common(10)}")
        else:
            print(f"     {nome[:30]:30} preenchido={sum(1 for v in vals if v)} distintos={len(cnt)} (livre/PII)")

def main(a):
    ini, fim = a[0], a[1]
    if len(a) > 2:
        s = SaudeMentalSession(); s.entrar(); s.selecionar_unidade(s.get(URL_LOGIN), unidade=a[2]); s.close()
    for imb, ci, cf in RELS:
        print("==", imb)
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            probe_relatorio.main([imb, ci, cf, ini, fim, "XLS"])
        info = [l for l in buf.getvalue().splitlines() if "export" in l]
        print("  ", info[0].strip() if info else "(sem export)")
        f = CAP / f"rel_{imb}_{ini.replace('/','')}_{fim.replace('/','')}.xls"
        if info and "vnd.ms-excel" in info[0] and f.exists():
            perfil(f)

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
