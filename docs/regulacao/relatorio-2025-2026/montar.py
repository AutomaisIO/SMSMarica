"""Monta os 3 relatórios HTML (SISREG, SER, SERNIT) a partir do dados.json. O PDF sai do render.mjs.

Uso: python montar.py   ->  relatorio-sisreg.html, relatorio-ser.html, relatorio-sernit.html
A4 paisagem; gráficos SVG inline; nenhum recurso externo (logo embutida em base64).
"""
from __future__ import annotations

import base64
import html
import json
import pathlib
import statistics

AQUI = pathlib.Path(__file__).parent
RAIZ = AQUI.parents[2]
LOGO = RAIZ / "SMSMais.cidadao.pwa" / "public" / "marica_logo.png"
MES_ABR = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"]
CORES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100"]  # paleta validada (dataviz) — slots 1..4
SELOS = {
    "Oficial": "Número lido do próprio sistema de origem (ou do espelho fiel dele mantido pela Secretaria).",
    "Calculado": "Derivado a partir de dados oficiais; a regra do cálculo está na nota do indicador.",
    "Parcial": "Sabidamente incompleto — o valor é um piso; a nota diz o que falta.",
    "Indisponível": "O sistema de origem não fornece esse dado ao município.",
}


def esc(s) -> str:
    return html.escape(str(s))


def num(v, formato="int") -> str:
    if v is None:
        return "—"
    if formato == "pct":
        return f"{v:.1f}%".replace(".", ",")
    if isinstance(v, float) and not v.is_integer():
        return f"{v:,.1f}".replace(",", "X").replace(".", ",").replace("X", ".")
    return f"{int(round(v)):,}".replace(",", ".")


def agregado(serie: dict, meses: list[str]):
    """Total do bloco: soma para contagem; último mês para estoque (fila); média para % e dias."""
    ano = meses[0][:4] if meses else ""
    if (serie.get("anual") or {}).get(ano) is not None:
        return serie["anual"][ano], "no ano"
    vals = [serie["valores"].get(m) for m in meses if serie["valores"].get(m) is not None]
    if not vals:
        return None, ""
    r = serie["rotulo"].lower()
    if "fila no fim" in r or "em fila no fim" in r or "sem trilha" in r or "cotas ppi pactuadas" in r:
        return vals[-1], "último"
    if serie["formato"] == "pct" or "(dias)" in r:
        return round(statistics.mean(vals), 1), "média"
    return sum(vals), "total"


# ------------------------------------------------------------------------------------------ gráficos
def grafico(secao: dict, meses: list[str]) -> str:
    g = secao.get("grafico")
    if not g:
        return ""
    series = [s for s in secao["series"] if s["rotulo"] in g["series"]]
    series = [s for s in series if any(s["valores"].get(m) is not None for m in meses)]
    if not series:
        return ""
    empilhado = g["tipo"] == "empilhado"
    W, H, ML, MR, MT, MB = 1000, 190, 56, 10, 12, 40
    pw, ph = W - ML - MR, H - MT - MB
    n = len(meses)
    gap_ano = 18
    slot = (pw - gap_ano) / n
    if empilhado:
        topo = max((sum((s["valores"].get(m) or 0) for s in series) for m in meses), default=0)
    else:
        topo = max((s["valores"].get(m) or 0 for s in series for m in meses), default=0)
    if topo <= 0:
        return ""
    passo = [1, 2, 2.5, 5, 10]
    mag = 10 ** (len(str(int(topo))) - 1)
    for p in passo:
        if topo <= p * mag * 4:
            tick = p * mag
            break
    else:
        tick = 10 * mag
    ymax = tick * max(1, -(-topo // tick))
    y = lambda v: MT + ph - ph * (v / ymax)
    out = [f'<svg viewBox="0 0 {W} {H}" class="graf" role="img" aria-label="{esc(secao["titulo"])}">']
    k = 0
    while k * tick <= ymax + 1e-9:
        yy = y(k * tick)
        out.append(f'<line x1="{ML}" x2="{W - MR}" y1="{yy:.1f}" y2="{yy:.1f}" class="grade{" base" if k == 0 else ""}"/>')
        out.append(f'<text x="{ML - 6}" y="{yy + 3.5:.1f}" class="eixo" text-anchor="end">{num(k * tick)}</text>')
        k += 1
    for i, m in enumerate(meses):
        x0 = ML + i * slot + (gap_ano if m >= "2026-01" else 0)
        larg_util = slot * 0.72
        xb = x0 + (slot - larg_util) / 2
        if empilhado:
            acum = 0
            for j, s in enumerate(series):
                v = s["valores"].get(m)
                if not v:
                    continue
                y1, y2 = y(acum + v), y(acum)
                altura = max(0.0, y2 - y1 - (2 if acum > 0 else 0))
                out.append(f'<rect x="{xb:.1f}" y="{y1:.1f}" width="{larg_util:.1f}" height="{altura:.1f}" rx="1.5" fill="{CORES[j % 4]}"/>')
                acum += v
        else:
            bw = (larg_util - 2 * (len(series) - 1)) / len(series)
            for j, s in enumerate(series):
                v = s["valores"].get(m)
                if v is None:
                    continue
                y1 = y(v)
                out.append(f'<rect x="{xb + j * (bw + 2):.1f}" y="{y1:.1f}" width="{bw:.1f}" height="{max(0.0, MT + ph - y1):.1f}" rx="1.5" fill="{CORES[j % 4]}"/>')
        a, mm = m.split("-")
        out.append(f'<text x="{x0 + slot / 2:.1f}" y="{MT + ph + 14}" class="eixo" text-anchor="middle">{MES_ABR[int(mm) - 1]}</text>')
    # rótulos de ano
    for ano, ini in (("2025", 0), ("2026", 12)):
        blocos = [i for i, m in enumerate(meses) if m.startswith(ano)]
        if not blocos:
            continue
        xa = ML + (blocos[0] + blocos[-1] + 1) / 2 * slot + (gap_ano if ano == "2026" else 0)
        out.append(f'<text x="{xa:.1f}" y="{MT + ph + 32}" class="ano" text-anchor="middle">{ano}</text>')
    out.append("</svg>")
    legenda = ""
    if len(series) > 1:
        legenda = '<div class="legenda">' + "".join(
            f'<span><i style="background:{CORES[j % 4]}"></i>{esc(s["rotulo"].strip())}</span>' for j, s in enumerate(series)) + "</div>"
    return f'<figure class="figura">{legenda}{"".join(out)}</figure>'


# ------------------------------------------------------------------------------------------- tabelas
def selo(s: str) -> str:
    return f'<span class="selo selo-{esc(s.lower().replace("í", "i"))}">{esc(s)}</span>'


def tabela_serie(secao: dict, meses: list[str], rotulo_bloco: str) -> str:
    series = [s for s in secao["series"]]
    if not series:
        return ""
    cab = "".join(f"<th>{MES_ABR[int(m[5:]) - 1]}</th>" for m in meses)
    linhas = []
    for s in series:
        tot, tipo = agregado(s, meses)
        cls = ' class="dest"' if s.get("destaque") else ""
        sub = " sub" if s["rotulo"].startswith("  ") else ""
        cells = "".join(f"<td>{num(s['valores'].get(m), s['formato'])}</td>" for m in meses)
        linhas.append(f'<tr{cls}><td class="rot{sub}">{esc(s["rotulo"].strip())}</td><td class="sl">{selo(s["selo"])}</td>'
                      f'{cells}<td class="tot">{num(tot, s["formato"])}{f"<small>{tipo}</small>" if tipo else ""}</td></tr>')
    return (f'<table class="serie"><thead><tr><th class="rot">{esc(rotulo_bloco)}</th><th class="sl">Origem</th>{cab}'
            f'<th class="tot">No período</th></tr></thead><tbody>{"".join(linhas)}</tbody></table>')


def tabela_simples(t: dict) -> str:
    if not t or not t.get("linhas"):
        return ""
    cab = "".join(f"<th>{esc(c)}</th>" for c in t["colunas"])
    corpo = "".join("<tr>" + "".join(
        f'<td{" class=txt" if i == 0 or isinstance(v, str) and not v.replace(".", "").replace(",", "").isdigit() else ""}>'
        f'{esc(v) if isinstance(v, str) else num(v)}</td>' for i, v in enumerate(l)) + "</tr>" for l in t["linhas"])
    nota = f'<p class="nota">{esc(t["nota"])}</p>' if t.get("nota") else ""
    return f'<div class="bloco"><h4>{esc(t["titulo"])}</h4><table class="simples"><thead><tr>{cab}</tr></thead><tbody>{corpo}</tbody></table>{nota}</div>'


def tabela_motivos(t: dict) -> str:
    if not t:
        return ""
    cab = "".join(f"<th>{esc(c)}</th>" for c in t["colunas"])
    pct_cols = {i for i, c in enumerate(t["colunas"]) if c.startswith("%")}
    # categorias genéricas vão para o fim — o topo da tabela é para motivo que diz alguma coisa
    fim = ("Outros", "Sem motivo informado")
    t = dict(t, linhas=[l for l in t["linhas"] if l[0] not in fim] + [l for l in t["linhas"] if l[0] in fim])
    corpo = "".join("<tr>" + "".join(
        f'<td class="txt">{esc(v)}</td>' if i == 0 else f'<td>{num(v, "pct" if i in pct_cols else "int")}</td>'
        for i, v in enumerate(l)) + "</tr>" for l in t["linhas"])
    return (f'<div class="bloco motivos"><h4>{esc(t["titulo"])}</h4><table class="simples"><thead><tr>{cab}</tr></thead>'
            f'<tbody>{corpo}</tbody></table><p class="nota">{esc(t["nota"])}</p></div>')


# -------------------------------------------------------------------------------------------- resumo
def achar(rel, sec_id, rot_inicio):
    prefixos = rot_inicio if isinstance(rot_inicio, tuple) else (rot_inicio,)
    for s in rel["secoes"]:
        if s["id"] == sec_id:
            for pref in prefixos:
                for se in s["series"]:
                    if se["rotulo"].strip().startswith(pref):
                        return se
    return None


def tiles(rel: dict, m25: list[str], m26: list[str]) -> str:
    sistema = rel["sistema"]
    itens = []
    if sistema == "SISREG":
        spec = [("vagas", "Vagas utilizadas", "Agendamentos"), ("regulados", "Total regulado", "Regulados"),
                ("fila", "Pacientes em fila no fim", "Fila no fim do período"),
                ("absenteismo", "Absenteísmo", "Absenteísmo no ano"),
                ("desfechos", "Marcações canceladas", "Marcações canceladas"),
                ("espera", "Espera até o atendimento — mediana", "Espera até o atendimento (mediana do ano, dias)")]
    else:
        spec = [("vagas", "Vagas utilizadas", "Agendamentos"), ("fila", "Solicitações em fila no fim", "Fila no fim do período"),
                ("fila", "Solicitações registradas", "Solicitações registradas"),
                ("absenteismo", "Absenteísmo", "Absenteísmo no ano"),
                ("desfechos", "Canceladas", "Canceladas"), ("espera", ("Espera — mediana", "Espera até o atendimento — mediana"), "Espera (mediana do ano, dias)")]
    for sec, ini, rot in spec:
        s = achar(rel, sec, ini)
        if not s:
            continue
        v25, _ = agregado(s, m25); v26, _ = agregado(s, m26)
        itens.append(f'<div class="tile"><div class="t-rot">{esc(rot)}</div>'
                     f'<div class="t-val"><span><b>{num(v25, s["formato"])}</b><small>2025</small></span>'
                     f'<span><b>{num(v26, s["formato"])}</b><small>2026 (jan–ago)</small></span></div>'
                     f'<div class="t-selo">{selo(s["selo"])}</div></div>')
    return '<div class="tiles">' + "".join(itens) + "</div>"


CSS = """
@page { size: A4 landscape; margin: 12mm 12mm 14mm 12mm; }
:root { --tinta:#1f2328; --suave:#5b616b; --linha:#e3e5e8; --marca:#C8102E; --fundo:#ffffff; }
* { box-sizing: border-box; }
html, body { margin:0; background: var(--fundo); color: var(--tinta);
  font-family: "Segoe UI", Arial, sans-serif; font-size: 9.5pt; line-height: 1.35; }
.pagina { break-after: page; }
.capa { height: 180mm; display:flex; flex-direction:column; justify-content:space-between; }
.capa .topo { display:flex; justify-content:space-between; align-items:center; border-bottom: 3px solid var(--marca); padding-bottom: 6mm; }
.capa img { height: 26mm; }
.capa .org { text-align:right; color: var(--suave); font-size: 10pt; }
.capa h1 { font-size: 30pt; margin: 0 0 3mm; letter-spacing: -.5px; }
.capa h2 { font-size: 16pt; margin: 0 0 8mm; color: var(--marca); font-weight: 600; }
.capa .periodo { font-size: 12pt; }
.capa .rodape { color: var(--suave); font-size: 9pt; border-top: 1px solid var(--linha); padding-top: 4mm; }
.cab { display:flex; justify-content:space-between; align-items:center; border-bottom: 2px solid var(--marca);
  padding-bottom: 2mm; margin-bottom: 4mm; }
.cab img { height: 9mm; } .cab span { color: var(--suave); font-size: 8.5pt; }
h3 { font-size: 15pt; margin: 0 0 1.5mm; } h4 { font-size: 10.5pt; margin: 3mm 0 1.5mm; }
p.texto { margin: 0 0 3mm; color: var(--suave); max-width: 250mm; }
.nota { color: var(--suave); font-size: 8pt; margin: 1.5mm 0 0; }
.figura { margin: 0 0 3mm; } .graf { width: 100%; height: auto; display:block; }
.graf .grade { stroke: #eceef1; stroke-width: 1; } .graf .grade.base { stroke: #b9bec5; }
.graf .eixo { font-size: 10px; fill: var(--suave); } .graf .ano { font-size: 11px; fill: var(--tinta); font-weight: 600; }
.legenda { display:flex; gap: 6mm; font-size: 8.5pt; margin-bottom: 1mm; color: var(--tinta); }
.legenda i { display:inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 5px; vertical-align: -1px; }
table { border-collapse: collapse; width: 100%; break-inside: avoid; }
table.serie { font-size: 7.6pt; margin-bottom: 3mm; table-layout: fixed; }
table.serie th, table.serie td { padding: 1.2mm 1mm; border-bottom: 1px solid var(--linha); text-align: right; white-space: nowrap; overflow: hidden; }
table.serie th { color: var(--suave); font-weight: 600; border-bottom: 1.5px solid #c9cdd2; }
table.serie .rot { text-align: left; width: 58mm; white-space: normal; }
table.serie .sub { padding-left: 4mm; color: var(--suave); }
table.serie .sl { width: 19mm; text-align: center; }
table.serie .tot { width: 25mm; font-weight: 600; background: #f6f7f8; }
table.serie .tot small { font-weight: 400; color: var(--suave); font-size: 6.3pt; margin-left: 1mm; }
table.serie tr.dest td { font-weight: 600; }
table.serie tr.dest td.rot { color: var(--tinta); }
.selo { display:inline-block; padding: .3mm 1.6mm; border-radius: 8px; font-size: 6.6pt; font-weight: 600; letter-spacing:.2px; }
.selo-oficial { background:#e4eefb; color:#1b4f8a; }
.selo-calculado { background:#eceff2; color:#394048; }
.selo-parcial { background:#fbeed5; color:#7a4a00; }
.selo-indisponivel { background:#f4f4f4; color:#8a8f96; border: 1px dashed #c3c7cc; }
table.simples { font-size: 8pt; }
table.simples th { text-align: right; color: var(--suave); font-weight: 600; border-bottom: 1.5px solid #c9cdd2; padding: 1.2mm 1.5mm; }
table.simples th:first-child { text-align:left; }
table.simples td { text-align: right; padding: 1mm 1.5mm; border-bottom: 1px solid var(--linha); }
table.simples td.txt { text-align: left; }
.duas { display:grid; grid-template-columns: 1fr 1fr; gap: 6mm; }
.bloco { break-inside: avoid; }
.tiles { display:grid; grid-template-columns: repeat(3, 1fr); gap: 4mm; margin: 4mm 0 5mm; }
.tile { border: 1px solid var(--linha); border-radius: 6px; padding: 3mm 4mm; }
.t-rot { color: var(--suave); font-size: 8.5pt; margin-bottom: 2mm; }
.t-val { display:flex; gap: 8mm; } .t-val b { font-size: 17pt; display:block; line-height: 1.1; }
.t-val small { color: var(--suave); font-size: 7.5pt; } .t-selo { margin-top: 2mm; }
.aviso { border: 1px dashed #c3c7cc; border-radius: 6px; padding: 4mm 5mm; color: var(--suave); background: #fafafa; }
.legenda-selos { display:grid; grid-template-columns: 1fr 1fr; gap: 2mm 8mm; font-size: 8.5pt; margin-top: 2mm; }
.legenda-selos div { display:flex; gap: 3mm; align-items: baseline; }
ul.cob { margin: 1mm 0 0 4mm; padding: 0; color: var(--suave); font-size: 8.5pt; }
.met h4 { margin-top: 4mm; } .met ul { margin: 1mm 0 0 4mm; padding:0; font-size: 8.3pt; color: var(--tinta); }
.met li { margin-bottom: 1mm; } .met li b { font-weight: 600; }
.resumo-jud { display:flex; gap: 10mm; margin: 2mm 0 3mm; } .resumo-jud div b { font-size: 14pt; display:block; }
.resumo-jud div small { color: var(--suave); }
"""


def montar(chave: str, rel: dict, dados: dict, logo_b64: str, secretaria: str) -> str:
    meses = dados["periodo"]["meses"]
    m25 = [m for m in meses if m.startswith("2025")]
    m26 = [m for m in meses if m.startswith("2026")]
    logo = f'<img src="data:image/png;base64,{logo_b64}" alt="Logo">'
    cab = (f'<div class="cab">{logo}<span>Indicadores de Regulação · {esc(rel["sistema"])} · '
           f'jan/2025 a ago/2026</span></div>')
    partes = [f"""<section class="pagina capa">
      <div class="topo">{logo}<div class="org">{esc(secretaria)}<br>Regulação</div></div>
      <div><h1>Indicadores de Regulação</h1><h2>{esc(rel["nome"])}</h2>
        <div class="periodo">Série histórica mensal — <b>janeiro a dezembro de 2025</b> e <b>janeiro a agosto de 2026</b></div>
        <div class="periodo" style="margin-top:2mm;color:#5b616b">Sistema de origem de todos os indicadores deste relatório: <b>{esc(rel["sistema"])}</b></div></div>
      <div class="rodape">Gerado em {esc(dados["gerado_em"])} a partir das bases espelhadas pela Secretaria e de consultas diretas ao {esc(rel["sistema"])}.
        Cada número traz o selo de origem (Oficial, Calculado, Parcial ou Indisponível), explicado na última página.</div>
    </section>"""]
    partes.append(f"""<section class="pagina">{cab}<h3>Resumo</h3>
      <p class="texto">Totais por ano. Contagens somadas; percentuais como razão do ano (soma ÷ soma); tempos como mediana de todos os
      casos do ano; fila como posição no último mês de cada bloco.</p>{tiles(rel, m25, m26)}
      <h4>Cobertura dos dados</h4><ul class="cob">{"".join(f"<li>{esc(c)}</li>" for c in rel["cobertura"])}</ul>
      <h4>Selos de origem</h4><div class="legenda-selos">{"".join(f"<div>{selo(k)}<span>{esc(v)}</span></div>" for k, v in SELOS.items())}</div>
    </section>""")
    for s in rel["secoes"]:
        corpo = [f'{cab}<h3>{esc(s["titulo"])}</h3><p class="texto">{esc(s.get("texto", ""))}</p>']
        if s.get("indisponivel"):
            corpo.append(f'<div class="aviso">{selo("Indisponível")} {esc(s.get("texto", ""))}</div>')
            partes.append(f'<section class="pagina">{"".join(corpo)}</section>')
            continue
        if s.get("resumo_judicial") and s["resumo_judicial"]["n"]:
            r = s["resumo_judicial"]
            corpo.append('<div class="resumo-jud">' + "".join(
                f"<div><b>{num(r[k])}</b><small>{rot}</small></div>" for k, rot in
                (("n", "judicializadas agendadas no período"), ("mediana", "mediana de dias até agendar"),
                 ("menor", "menor tempo (dias)"), ("maior", "maior tempo (dias)"))) + "</div>")
        corpo.append(grafico(s, meses))
        if s["series"]:
            corpo.append(tabela_serie(s, m25, "2025"))
            corpo.append(tabela_serie(s, m26, "2026 (jan–ago)"))
        partes.append(f'<section class="pagina">{"".join(corpo)}</section>')
        extras = []
        if s.get("motivos"):
            extras.append(tabela_motivos(s["motivos"]))
        tabs = s.get("tabelas") or []
        pares = [t for t in tabs if t["titulo"].startswith("Por ")]
        outras = [t for t in tabs if not t["titulo"].startswith("Por ")]
        for i in range(0, len(pares), 2):
            extras.append('<div class="duas">' + "".join(tabela_simples(t) for t in pares[i:i + 2]) + "</div>")
        extras += [tabela_simples(t) for t in outras]
        extras = [e for e in extras if e]
        if extras:
            # cada par de tabelas (ou a de motivos) cabe em meia página: agrupa de dois em dois
            for i in range(0, len(extras), 2):
                partes.append(f'<section class="pagina">{cab}<h3>{esc(s["titulo"])} — detalhamento</h3>{"".join(extras[i:i + 2])}</section>')
    notas = []
    for s in rel["secoes"]:
        itens = [f'<li><b>{esc(se["rotulo"].strip())}</b> ({esc(se["selo"])}): {esc(se["nota"])}</li>' for se in s["series"] if se.get("nota")]
        if s.get("indisponivel"):
            itens.append(f'<li>{esc(s.get("texto", ""))}</li>')
        if itens:
            notas.append(f'<h4>{esc(s["titulo"])}</h4><ul>{"".join(itens)}</ul>')
    partes.append(f"""<section class="met">{cab}<h3>Metodologia e limitações</h3>
      <p class="texto">Período: 01/01/2025 a 31/08/2026, agrupado por mês no horário de Brasília. Dados pessoais (nome, CNS,
      telefone, endereço) não são reproduzidos; motivos em texto livre aparecem apenas agrupados por categoria.</p>
      <div class="legenda-selos">{"".join(f"<div>{selo(k)}<span>{esc(v)}</span></div>" for k, v in SELOS.items())}</div>
      {"".join(notas)}</section>""")
    return (f'<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Indicadores de Regulação — {esc(rel["sistema"])}</title>'
            f'<style>{CSS}</style></head><body>{"".join(partes)}</body></html>')


def main() -> int:
    dados = json.loads((AQUI / "dados.json").read_text(encoding="utf-8"))
    logo_b64 = base64.b64encode(LOGO.read_bytes()).decode()
    secretaria = dados.get("secretaria") or "Secretaria Municipal de Saúde de Maricá"
    for chave, rel in dados["relatorios"].items():
        (AQUI / f"relatorio-{chave}.html").write_text(montar(chave, rel, dados, logo_b64, secretaria), encoding="utf-8")
        print(f"relatorio-{chave}.html")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
