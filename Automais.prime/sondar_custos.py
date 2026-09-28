"""Custo (tempo/tamanho) do caminho "marcar chegada/atendimento nas Solicitações". SOMENTE LEITURA.

Uma sessão só do Prime. Para UMA unidade e UM dia mede:
 1. lista de profissionais (ComboProfissionalPorEsquipeService.GetData — catálogo, não paciente);
 2. Pacientes Agendados fatiado por profissional (contorna o teto de 100 linhas);
 3. log de cada agendamento (DetalhesLogAgenda): chegada ("Acolhimento realizado") e início
    ("Atendimento Iniciado") com data e hora;
 4. Pacientes Atendidos, Atendimentos em Aberto e Pacientes Faltantes do dia (CSV).
Imprime só tempos, tamanhos e contagens. Brutos em capturas/custos/.

Uso: python sondar_custos.py <guid_unidade> DD/MM/AAAA [max_logs]
"""
from __future__ import annotations
import collections, re, sys, time, statistics
from prime.client import CAP, PrimeSession, sopa
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
OUT = CAP / "custos"; OUT.mkdir(exist_ok=True)
T = collections.defaultdict(list)

def cron(nome, fn):
    t = time.perf_counter(); r = fn(); T[nome].append((time.perf_counter() - t, len(r.content))); return r

def csv_regs(r):
    if "csv" not in r.headers.get("content-type", ""): return None, []
    regs = [x.rstrip(";").split(";") for x in r.content.decode("utf-8-sig", errors="replace").split("\r\n") if x.strip()]
    return regs[0], regs[1:]

def main(a):
    g, dia = a[0], a[1]; max_logs = int(a[2]) if len(a) > 2 else 30
    s = PrimeSession(); s.entrar()
    # 1) profissionais
    # Medido 16/09: ComboProfissionalPorEsquipeService/PorUnidade.GetData devolvem só 10 (equipe) e
    # cobrem 0–34 agendamentos; GetDataForRelProducao devolve os 64 da unidade e cobre 174, sem teto.
    r = cron("profissionais(asmx)", lambda: s.post("/Prime/Services/ComboProfissionalPorUnidadeService.asmx/GetDataForRelProducao",
             json={"context": {"Text": "", "NumberOfItems": 0, "UnidadeId": g}}, ajax=True))
    try:
        itens = r.json()["d"]["Items"]; profs = [(i["Value"], i["Text"]) for i in itens if i.get("Value") not in (None, "", " ")]
    except Exception:
        print("profissionais: falhou", r.status_code, r.text[:200]); profs = []
    print(f"profissionais na unidade: {len(profs)}")
    # 2) agendados: sem filtro (teto) e por profissional
    base = {"equipe": "", "idfuncaoatendimento": "", "idespecialidade": "", "dtInicio": dia, "dtFim": dia, "extensao": "CSV", "unidades": g}
    cab, rows = csv_regs(cron("agendados(sem filtro)", lambda: s.get("/Prime/Relatorios/RelatorioPacientesAgendadosRPT.aspx", params={**base, "prof_codigo": ""})))
    print(f"agendados sem filtro: {len(rows)}")
    agendas = {}
    for pid, _ in profs:
        cab, rows = csv_regs(cron("agendados(por prof)", lambda: s.get("/Prime/Relatorios/RelatorioPacientesAgendadosRPT.aspx", params={**base, "prof_codigo": pid})))
        if cab:
            for x in rows:
                if len(x) == len(cab): agendas[dict(zip(cab, x))["agendaid"]] = dict(zip(cab, x))
        if len(rows) >= 100: print("  !! profissional com 100 linhas (teto de novo)")
        time.sleep(0.2)
    print(f"agendados somando por profissional: {len(agendas)} (distintos)")
    # 3) logs
    ev = collections.Counter(); com_chegada = com_inicio = 0
    for aid in list(agendas)[:max_logs]:
        r = cron("log agenda", lambda: s.get("/Prime/Agendamento/DetalhesLogAgenda.aspx", params={"AgendaId": aid}))
        acoes = re.findall(r">\s*(Acolhimento realizado|Atendimento Iniciado|Agendar|Desagendar|Atendimento Finalizado)\s*<", r.text)
        ev.update(set(acoes)); com_chegada += "Acolhimento realizado" in acoes; com_inicio += "Atendimento Iniciado" in acoes
        time.sleep(0.2)
    n = min(max_logs, len(agendas))
    print(f"logs lidos: {n} | com chegada: {com_chegada} | com início: {com_inicio} | eventos: {dict(ev)}")
    # 4) relatórios do dia
    for nome, url, params in (
        ("atendidos(dia)", "/Prime/Relatorios/RelatorioPacientesAtendidosRPT.aspx", {"dataInicio": dia, "dataFim": dia, "funcao": "", "prof": "", "idGrupoPrioritario": "", "extensao": "CSV", "unidades": g}),
        ("em aberto(dia)", "/Prime/Relatorios/RelatorioAtendimentoEmAbertoRPT.aspx", {"dataInicio": dia, "dataFim": dia, "equipeId": "", "ProfissionalId": "", "FuncaoId": "", "unidades": g, "extensao": "CSV"}),
        ("faltantes(dia)", "/Prime/Relatorios/RelatorioPacientesFaltantesRPT.aspx", {"dataInicio": dia, "dataFim": dia, "funcao": "", "prof": "", "extensao": "CSV", "grupoPrioritario": "0", "unidades": g})):
        r = cron(nome, lambda: s.get(url, params=params))
        cab, rows = csv_regs(r)
        (OUT / f"{nome.split('(')[0].replace(' ','_')}.csv").write_bytes(r.content)
        print(f"{nome}: {r.status_code} {r.headers.get('content-type','')[:25]} registros={len(rows) if cab else '-'} cabeçalho={cab if cab else sopa(r.text).get_text(' ',strip=True)[:120]}")
        time.sleep(0.2)
    print("\nCUSTOS (por chamada):")
    tot = 0
    for k, v in T.items():
        ts = [x[0] for x in v]; tot += sum(ts)
        print(f"  {k:24} n={len(v):3}  mediana={statistics.median(ts):.2f}s  max={max(ts):.2f}s  total={sum(ts):.1f}s  tamanho mediano={int(statistics.median(x[1] for x in v))/1024:.0f} KB")
    print(f"  TOTAL rede: {tot:.1f}s")
    s.close()

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
