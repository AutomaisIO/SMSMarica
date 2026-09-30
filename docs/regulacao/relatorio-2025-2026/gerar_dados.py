"""Calcula os indicadores de regulação (jan/2025–ago/2026) para os 3 relatórios em PDF.

Só LEITURA: SELECT no banco de produção (Aprendizados e Scratchpads/ferramentas/db.py) + os JSONs das
coletas oficiais (Automais.SISREG/capturas/indicadores/serie/progresso.json e
Automais.SER/capturas/judicial_ids.json — ambos gitignored, têm código de solicitação).

Saída: dados.json nesta pasta — SÓ AGREGADOS (nenhum nome, CNS, telefone ou código de solicitação,
exceto o ID das solicitações judiciais do SER, que é o que o gestor precisa para localizá-las).

As definições são as da Parte A do plano (C:\\Users\\berna\\.claude\\plans\\twinkly-tumbling-kite.md) e
serão reaproveitadas pela tela da plataforma. Cada série sai com um SELO:
  Oficial      — número lido do próprio sistema de origem (ou espelho fiel dele)
  Calculado    — derivado por nós a partir de dados oficiais (a regra vai na nota)
  Parcial      — sabidamente incompleto (piso); a nota diz o que falta
  Indisponível — o sistema de origem não fornece

Uso: python gerar_dados.py
"""
from __future__ import annotations

import datetime as dt
import json
import pathlib
import re
import statistics
import sys
import unicodedata
from collections import Counter, defaultdict

AQUI = pathlib.Path(__file__).parent
RAIZ = AQUI.parents[2]
sys.path.insert(0, str(RAIZ / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn  # noqa: E402
from psycopg2.extras import execute_values  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

INICIO, FIM = dt.date(2025, 1, 1), dt.date(2026, 8, 31)
MESES = []
_a, _m = 2025, 1
while (_a, _m) <= (2026, 8):
    MESES.append(f"{_a}-{_m:02d}")
    _m += 1
    if _m == 13:
        _a, _m = _a + 1, 1
FUSO = "America/Sao_Paulo"
CODIGO_VALIDO = r"^[0-9]{9,10}$"

COLETA = RAIZ / "Automais.SISREG" / "capturas" / "indicadores" / "serie" / "progresso.json"
JUDICIAL_SER = RAIZ / "Automais.SER" / "capturas" / "judicial_ids.json"


# ----------------------------------------------------------------------------------------- utilidades
def q(cur, sql, args=None):
    cur.execute(sql, args or ())
    return cur.fetchall()


def fim_do_mes(mes: str) -> dt.date:
    a, m = map(int, mes.split("-"))
    prox = dt.date(a + (m == 12), m % 12 + 1, 1)
    return prox - dt.timedelta(days=1)


def serie(rotulo, valores: dict, selo, nota="", formato="int", destaque=False, anual=None):
    """`anual`: valor do ano calculado do jeito certo (razão de somas, mediana de todos os casos) — o PDF usa
    ele no lugar de somar ou tirar média dos meses, que distorce percentual e tempo."""
    return {"rotulo": rotulo, "selo": selo, "nota": nota, "formato": formato, "destaque": destaque,
            "valores": {m: valores.get(m) for m in MESES}, "anual": anual}


def anual_razao(num: dict, den: dict) -> dict:
    """% do ano = soma do numerador ÷ soma do denominador, só nos meses em que os dois existem."""
    out = {}
    for ano in ("2025", "2026"):
        ms = [m for m in MESES if m.startswith(ano) and num.get(m) is not None and den.get(m)]
        n, d = sum(num[m] for m in ms), sum(den[m] for m in ms)
        out[ano] = round(100.0 * n / d, 1) if d else None
    return out


def anual_espera(listas: dict, chave: str) -> dict:
    """Mediana/menor/maior do ANO sobre todos os casos (não a média das medianas mensais)."""
    return {ano: resumo_espera([d for m in MESES if m.startswith(ano) for d in listas.get(m, [])])[chave]
            for ano in ("2025", "2026")}


def pct(num: dict, den: dict) -> dict:
    return {m: (round(100.0 * num[m] / den[m], 1) if den.get(m) and num.get(m) is not None else None) for m in MESES}


def sem_acento(s: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", s or "") if unicodedata.category(c) != "Mn").upper()


# Motivo em CATEGORIA — texto livre nunca vai para o PDF (pode carregar nome/telefone).
REGRAS_MOTIVO = [
    ("Óbito", ["OBITO", "FALECI", "FALECEU", "FALECIMENTO"]),
    ("Sem cota PPI para a especialidade", ["SEM PPI", "NAO POSSUI PPI", "SEM COTA", "NAO POSSUI COTA"]),
    ("Plano Estadual de Redução de Filas (CIB)", ["PLANO ESTADUAL DE REDUCAO", "PALNO ESTADUAL", "CIB N"]),
    ("Já em fila em outro sistema", ["FILA SISREG", "AGENDAMENTO SISREG", "NO SISTEMA SERNIT", "NO SISREG",
                                     "FILA DO SISREG", "FILA NO SER"]),
    ("Recurso não ofertado pela unidade executora", ["NAO TEMOS ESSE RECURSO", "NAO TEMOS MAIS", "NAO REALIZAMOS",
                                                     "NAO DISPOMOS", "COMPETENCIA DA UNIDADE", "NAO OFERT"]),
    ("Solicitação em recurso/especialidade errado", ["FAVOR INSERIR", "INSERIR EM", "RECURSO ERRADO", "RECURSO INCORRETO",
                                                     "OUTRO TIPO DE AGENDAMENTO", "INDICADA A FAZER"]),
    ("Fora do perfil / protocolo", ["ATENCAO PRIMARIA", "FORA DO PERFIL", "PROTOCOLO", "CRITERIO", "FAIXA ETARIA",
                                    "INADEQUAD", "EM CRIANCAS"]),
    ("Já atendido / em tratamento", ["JA EM TRATAMENTO", "EM TRATAMENTO"]),
    ("Sem contato com o paciente", ["SEM CONTATO", "NAO COMPLETA", "TENTATIVAS SEM SUCESSO", "NAO LOCALIZ",
                                    "NAO ATENDE", "CAIXA POSTAL", "NUMERO INEXISTENTE", "TELEFONE ERRADO"]),
    ("Mudança de endereço / município", ["MUDOU-SE", "MUDOU SE", "MUDOU DE", "MUDANCA DE ENDERECO", "MUDANCA DE MUNICIPIO",
                                         "RESIDE EM OUTRO", "OUTRO MUNICIPIO", "NAO MORA MAIS"]),
    ("Duplicidade", ["DUPLIC", "NOVO CODIGO", "OUTRO CODIGO", "DUAS VEZES", "EM DOBRO", "CONSULTA NO MESMO DIA"]),
    ("Cancelado pela unidade solicitante", ["PROPRIO SOLICITANTE", "PELA UNIDADE SOLICITANTE", "ACS SOLICITOU",
                                            "SOLICITADO PELA UNIDADE"]),
    ("Desistência / impedimento do paciente", ["NAO AGUARDA", "DESIST", "RECUSA", "RECUSOU", "NAO DESEJA",
                                               "NAO TEM INTERESSE", "NAO QUER", "SEM INTERESSE", "NAO PODERA",
                                               "NAO VAI PODER", "NAO PODE IR", "NAO VAI FAZER", "NAO VAI COMPARECER",
                                               "INFORMOU QUE NAO", "MOTIVO PESSOAL", "VIAJANDO", "VIAGEM",
                                               "PEDIU PARA CANCELAR", "NAO GOSTOU", "TRABALHO", "NAO CONSEGUIRA",
                                               "SEM TEMPO HABIL"]),
    ("Sem resposta no prazo", ["NAO RESPONDID", "RESPONDIDA NO PRAZO", "PRAZO ESTABELECIDO", "SEM RESPOSTA"]),
    ("Já atendido / realizado", ["ATENDID", "JA REALIZ", "REALIZOU", "JA FEZ", "JA FOI", "REALIZADO", "JA TEVE"]),
    ("Desistência / impedimento do paciente", ["A PEDIDO", "SOLICITACAO DO PACIENTE", "PACIENTE SOLICITOU",
                                               "PACIENTE PEDIU", "MEIOS PROPRIOS", "PARTICULAR"]),
    ("Erro de marcação / agenda", ["ERRO", "EQUIVOC", "ERRAD", "INCORRET", "INDEVID"]),
    ("Ausência do profissional", ["PROFISSIONAL", "MEDICO", "MÉDICO", "FERIAS", "LICENCA", "AFASTAMENTO",
                                  "AUSENCIA", "FALTA DO", "ATESTADO"]),
    ("Equipamento / estrutura indisponível", ["EQUIPAMENTO", "APARELHO", "MANUTENC", "QUEBRAD", "DEFEITO",
                                              "SEM ENERGIA", "LUZ", "AGUA", "INTERDI"]),
    ("Remarcação / reagendamento", ["REMARC", "REAGEND", "REMANEJ", "AGENDADO", "AGENDADA", "NOVA DATA", "TROCA DE DATA"]),
    ("Transferência / outra unidade", ["TRANSFER", "OUTRA UNIDADE", "UNIDADE:", "ENCAMINHAD"]),
    ("Reclassificação / avaliação do regulador", ["RECLASSIFIC", "REGULADOR", "AVALIAD", "ANEXO", "LAUDO",
                                                   "EXAME COMPLEMENTAR", "INFORMAC"]),
    ("Sem vaga / oferta suspensa", ["SEM VAGA", "PRESTADOR", "SUSPENS", "BLOQUEI", "CANCELAMENTO DA AGENDA"]),
]


def categoria_motivo(texto: str) -> str:
    t = sem_acento(texto).strip()
    if not t or len(re.sub(r"[^A-Z]", "", t)) < 3 or re.sub(r"[^A-Z ]", "", t).strip() in {"CANCELAR", "CANCELADO", "CANCELAMENTO", "CANCELADA"}:
        return "Sem motivo informado"
    for cat, chaves in REGRAS_MOTIVO:
        if any(sem_acento(k) in t for k in chaves):
            return cat
    return "Outros"


# Quando o texto do cancelamento é genérico, o último FollowUP antes dele diz o porquê (SER/SERNIT).
GENERICOS = {"Sem resposta no prazo", "Outros", "Sem motivo informado"}
POR_FOLLOWUP = {"FalhaContato": "Sem contato com o paciente", "SemVaga": "Sem vaga / oferta suspensa",
                "ReclassificacaoRisco": "Reclassificação / avaliação do regulador",
                "SolicitacaoAoSolicitante": "Reclassificação / avaliação do regulador",
                "CancelamentoOuReagendamento": "Remarcação / reagendamento"}


def motivo_externo(obs_cancel: str, fu_cat: str | None, fu_obs: str | None) -> str:
    cat = categoria_motivo(obs_cancel)
    if cat not in GENERICOS or not fu_cat:
        return cat
    if fu_cat == "ContatoRealizado":
        c2 = categoria_motivo(fu_obs or "")
        return c2 if c2 not in GENERICOS else cat
    return POR_FOLLOWUP.get(fu_cat) or (categoria_motivo(fu_obs or "") if categoria_motivo(fu_obs or "") not in GENERICOS else cat)


def tabela_motivos(por_ano: dict[int, Counter], titulo: str, nota: str, estimar: dict[int, int] | None = None) -> dict:
    """Linhas: categoria | 2025 | 2026 | total. Com `estimar`, os valores viram % da amostra + estimativa."""
    cats = Counter()
    for c in por_ano.values():
        cats.update(c)
    if estimar:
        n = {a: sum(por_ano.get(a, Counter()).values()) for a in (2025, 2026)}
        linhas_m = []
        for cat, _ in cats.most_common():
            linha = [cat]
            for a in (2025, 2026):
                p = 100.0 * por_ano.get(a, Counter())[cat] / n[a] if n[a] else None
                linha += [None if p is None else round(p, 1), None if p is None or not estimar.get(a) else round(p * estimar[a] / 100)]
            linhas_m.append(linha)
        return {"titulo": titulo, "nota": nota, "amostra": n,
                "colunas": ["Motivo", "% 2025", "Estimativa 2025", "% 2026", "Estimativa 2026"], "linhas": linhas_m}
    return {"titulo": titulo, "nota": nota,
            "colunas": ["Motivo", "2025", "2026 (até ago)", "Total"],
            "linhas": [[cat, por_ano.get(2025, Counter())[cat], por_ano.get(2026, Counter())[cat], tot]
                       for cat, tot in cats.most_common()]}


def resumo_espera(dias: list[int]) -> dict:
    if not dias:
        return {"n": 0, "mediana": None, "p90": None, "menor": None, "maior": None}
    s = sorted(dias)
    return {"n": len(s), "mediana": int(statistics.median(s)), "p90": s[min(len(s) - 1, int(0.9 * len(s)))],
            "menor": s[0], "maior": s[-1]}


def carregar_coleta() -> dict:
    return json.loads(COLETA.read_text(encoding="utf-8")) if COLETA.exists() else {}


# ------------------------------------------------------------------------------------------- SISREG
def sisreg(cur, coleta: dict) -> dict:
    secoes = []
    base = f"""from smsmarica.solicitacao s where s.excluido_em is null and s.codigo_solicitacao ~ '{CODIGO_VALIDO}'"""

    # --- vagas ofertadas (escalas) ---------------------------------------------------------------
    linhas = q(cur, """
        with dias as (select d::date d from generate_series(%s::date, %s::date, interval '1 day') d)
        select to_char(d.d,'YYYY-MM'),
          sum(case when not e.agenda_local then coalesce(e.vagas_primeira_vez,0)+coalesce(e.vagas_reserva,0) else 0 end),
          sum(case when not e.agenda_local then coalesce(e.vagas_retorno,0) else 0 end),
          sum(case when e.agenda_local then coalesce(e.vagas_total,0) else 0 end),
          sum(coalesce(e.vagas_total,0))
        from dias d join smsmarica.sisreg_escala e
          on extract(dow from d.d) = e.dia_semana and d.d between e.vigencia_inicio and e.vigencia_fim
        where e.status in (1, 3) and not e.ausente
        group by 1""", (INICIO, FIM))
    v_reg = {r[0]: int(r[1]) for r in linhas}; v_ret = {r[0]: int(r[2]) for r in linhas}
    v_loc = {r[0]: int(r[3]) for r in linhas}; v_tot = {r[0]: int(r[4]) for r in linhas}

    # --- vagas utilizadas (agendamentos com data no mês) -----------------------------------------
    linhas = q(cur, f"""
        select to_char(x.dag,'YYYY-MM'), count(*), count(*) filter (where x.vaga='0'), count(*) filter (where x.vaga='1')
        from (select (s.data_agendada at time zone '{FUSO}')::date dag,
                     coalesce(case s.tipo_vaga when 0 then '0' when 1 then '1' end, nullif(split_part(s.raw_sisreg,';',9),'')) vaga
              {base} and s.cancelado_em is null and s.data_agendada is not null) x
        where x.dag between %s and %s group by 1""", (INICIO, FIM))
    util = {r[0]: r[1] for r in linhas}; util1 = {r[0]: r[2] for r in linhas}; utilr = {r[0]: r[3] for r in linhas}

    # --- PPI ---------------------------------------------------------------------------------------
    ppi_tot, ppi_usada = {}, {}
    for m in MESES:
        itens = coleta.get(f"ppi:{m}")
        if itens is not None:
            ppi_tot[m] = sum(i["total"] for i in itens); ppi_usada[m] = sum(i["usada"] for i in itens)

    ocup = pct(util, v_tot)
    secoes.append({
        "id": "vagas", "titulo": "Vagas disponibilizadas e utilizadas",
        "texto": "Oferta das escalas ambulatoriais cadastradas no SISREG para as unidades executantes da rede, "
                 "comparada aos agendamentos com data de atendimento no mês.",
        "series": [
            serie("Vagas ofertadas (total)", v_tot, "Calculado", "Soma, dia a dia, das escalas vigentes (ativas ou já expiradas) — retrato atual das escalas: alteração feita no próprio registro e bloqueio de agenda não aparecem.", destaque=True),
            serie("  para a regulação (1ª vez + reserva)", v_reg, "Calculado"),
            serie("  retorno", v_ret, "Calculado"),
            serie("  agenda local da unidade", v_loc, "Calculado"),
            serie("Cotas PPI pactuadas (SISREG)", ppi_tot, "Oficial" if ppi_tot else "Indisponível",
                  "Consulta de PPI do SISREG, central Maricá. O valor é o mesmo em todas as competências — é o teto configurado, a confirmar se corresponde aos contratos."),
            serie("Cotas PPI utilizadas", ppi_usada, "Oficial" if ppi_usada else "Indisponível"),
            serie("Vagas utilizadas (agendamentos)", util, "Parcial", "Agendamentos com data no mês. Marcações canceladas antes de agosto/2026 não constam da base espelhada.", destaque=True),
            serie("Ocupação da oferta", ocup, "Calculado", "Agendamentos ÷ vagas ofertadas.", formato="pct", anual=anual_razao(util, v_tot)),
        ],
        "grafico": {"tipo": "barras", "series": ["Vagas ofertadas (total)", "Vagas utilizadas (agendamentos)"]},
    })

    # --- absenteísmo (lista oficial de faltas) -------------------------------------------------------
    faltas = []
    for k, rows in coleta.items():
        if k.startswith("faltas:"):
            faltas.extend(rows)
    semanas_lidas = {k for k in coleta if k.startswith("faltas:")}
    cur.execute("create temp table tmp_falta (codigo text, data date) on commit drop")
    if faltas:
        execute_values(cur, "insert into tmp_falta values %s",
                       [(f["codigo"], f["data_execucao"]) for f in faltas], template="(%s, to_date(%s,'DD/MM/YYYY'))", page_size=5000)
    cur.execute("analyze tmp_falta")
    # o índice de codigo_solicitacao é PARCIAL (not null, <> '0000', excluido_em is null): repetir o predicado
    # na busca, senão o planner não usa o índice e varre a tabela inteira para cada falta.
    linhas = q(cur, f"""
        select to_char(f.data,'YYYY-MM'), count(*), count(s.ok)
        from (select distinct codigo, data from tmp_falta) f
        left join lateral (
            select 1 ok from smsmarica.solicitacao s
            where s.codigo_solicitacao = f.codigo and s.codigo_solicitacao is not null
              and s.codigo_solicitacao <> '0000' and s.excluido_em is null
              and (s.data_agendada at time zone '{FUSO}')::date = f.data
            limit 1) s on true
        group by 1""")
    f_tot = {r[0]: r[1] for r in linhas}; f_cas = {r[0]: r[2] for r in linhas}
    # só vale o mês com as 4 janelas lidas
    meses_completos = {m for m in MESES if sum(1 for k in semanas_lidas if k.split(":")[1][:7] == m) >= 4}
    f_tot = {m: v for m, v in f_tot.items() if m in meses_completos}
    f_cas = {m: v for m, v in f_cas.items() if m in meses_completos}
    for m in meses_completos:
        f_tot.setdefault(m, 0); f_cas.setdefault(m, 0)
    atend = {m: (util[m] - f_cas[m]) if m in f_cas and util.get(m) is not None else None for m in MESES}
    secoes.append({
        "id": "absenteismo", "titulo": "Absenteísmo",
        "texto": "Faltas pela Consulta de Absenteísmo por Unidade de Saúde do SISREG (agendamentos cuja chegada "
                 "não foi confirmada pela unidade executante), casadas com os agendamentos do mês.",
        "series": [
            serie("Agendamentos no mês", util, "Parcial"),
            serie("Faltas (lista oficial do SISREG)", f_tot, "Oficial" if f_tot else "Indisponível", destaque=True),
            serie("Absenteísmo", pct(f_cas, util), "Calculado" if f_cas else "Indisponível",
                  "Faltas casadas com os agendamentos (mesmo código e mesma data) ÷ agendamentos do mês.", formato="pct", destaque=True,
                  anual=anual_razao(f_cas, util) if f_cas else None),
            serie("Atendidos (agendamentos − faltas)", atend, "Calculado"),
            serie("Faltas sem agendamento correspondente na base", {m: f_tot[m] - f_cas[m] for m in f_tot}, "Calculado",
                  "Linha de auditoria: falta oficial cujo agendamento não está na base espelhada (em geral, remarcado)."),
        ],
        "grafico": {"tipo": "barras", "series": ["Faltas (lista oficial do SISREG)"]},
    })

    # --- quantitativo regulado ---------------------------------------------------------------------
    linhas = q(cur, f"""
        select to_char(s.data_regulacao,'YYYY-MM'),
          case when s.categoria = 1 then 'Consultas' else 'Exames e demais procedimentos' end, count(*)
        {base} and s.data_regulacao between %s and %s group by 1, 2""", (INICIO, FIM))
    reg = defaultdict(dict)
    for mes, cat, n in linhas:
        reg[cat][mes] = n
    reg_tot = {m: sum(reg[c].get(m, 0) for c in reg) for m in MESES}
    cats_reg = [c for c in ("Consultas", "Exames e demais procedimentos") if c in reg]
    secoes.append({
        "id": "regulados", "titulo": "Quantitativo regulado de consultas e exames",
        "texto": "Solicitações autorizadas (com data de regulação no mês), por tipo de procedimento. A base histórica "
                 "distingue apenas consulta × demais procedimentos (exames, métodos gráficos, endoscopias, cirurgias).",
        "series": [serie("Total regulado", reg_tot, "Parcial", "Autorizações cuja marcação foi cancelada antes de agosto/2026 não constam da base espelhada.", destaque=True)]
        + [serie(f"  {c}", reg[c], "Parcial") for c in cats_reg],
        "grafico": {"tipo": "empilhado", "series": [f"  {c}" for c in cats_reg]},
    })

    # --- desfechos pré-agendamento (coleta por unidade) ----------------------------------------------
    desf = []
    unidades_lidas = set()
    for k, v in coleta.items():
        if k.startswith("desf:") and isinstance(v, dict):
            sit = int(k.split(":")[1])
            unidades_lidas.add(k.split(":")[2])
            for r in v["linhas"]:
                desf.append((r["codigo"], sit, r["data_solicitacao"]))
    total_unidades = len(coleta.get("unidades") or [])
    desf_completo = total_unidades > 0 and len(unidades_lidas) >= total_unidades
    cur.execute("create temp table tmp_desf (codigo text, sit int, ds date) on commit drop")
    if desf:
        execute_values(cur, "insert into tmp_desf values %s", desf,
                       template="(%s, %s, to_date(nullif(%s,''),'DD/MM/YYYY'))", page_size=5000)
    exc = defaultdict(dict)
    for mes, sit, n in q(cur, """select to_char(ds,'YYYY-MM'), sit, count(distinct codigo) from tmp_desf
                                 where ds between %s and %s group by 1,2""", (INICIO, FIM)):
        exc[sit][mes] = n
    selo_desf = ("Oficial" if desf_completo else "Parcial") if desf else "Indisponível"
    nota_desf = ("" if desf_completo else f"Coleta por unidade solicitante em andamento ({len(unidades_lidas)} de {total_unidades} unidades).") if desf else ""

    # --- solicitações no mês e fila no fim do mês ------------------------------------------------
    solic = dict(q(cur, f"""
        with c as (
          select s.codigo_solicitacao cod, s.data_solicitacao ds {base} and s.data_solicitacao between %s and %s
          union select f.codigo_solicitacao, f.data_solicitacao from smsmarica.sisreg_fila_pendente f where f.data_solicitacao between %s and %s
          union select codigo, ds from tmp_desf where ds between %s and %s)
        select to_char(ds,'YYYY-MM'), count(distinct cod) from c group by 1""", (INICIO, FIM, INICIO, FIM, INICIO, FIM)))
    fila = {}
    for m in MESES:
        fim = fim_do_mes(m)
        fila[m] = q(cur, f"""
          select (select count(*) from smsmarica.sisreg_fila_pendente f where f.saiu_em is null and f.data_solicitacao <= %s)
               + (select count(*) from smsmarica.sisreg_fila_pendente f where f.saiu_para = 2 and f.data_solicitacao <= %s
                    and (f.saiu_em at time zone '{FUSO}')::date > %s)
               + (select count(*) {base} and s.data_solicitacao <= %s and s.data_regulacao > %s)""",
                    (fim, fim, fim, fim, fim))[0][0]
    secoes.append({
        "id": "fila", "titulo": "Fila e solicitações",
        "texto": "Pacientes aguardando regulação no último dia de cada mês e solicitações registradas no mês.",
        "series": [
            serie("Pacientes em fila no fim do mês", fila, "Parcial",
                  "Reconstruída: quem ainda aguarda hoje e já tinha pedido naquela data + quem foi autorizado depois daquela data. "
                  "Falta quem saiu da fila sem agendamento (negada, devolvida ou cancelada) antes de 10/09/2026 — o número é um piso.", destaque=True),
            serie("Solicitações registradas no mês", solic, "Parcial",
                  "Códigos distintos pela data da solicitação (agendados + fila + devolvidas/negadas/canceladas coletadas)."),
        ],
        "grafico": {"tipo": "barras", "series": ["Pacientes em fila no fim do mês"]},
    })

    # --- atendidas, canceladas, excluídas + motivos -------------------------------------------------
    canc = {m: coleta.get(f"canc:{m}") for m in MESES if coleta.get(f"canc:{m}") is not None}
    mot_ano = defaultdict(Counter)
    meses_amostra = []
    for m in MESES:
        v = coleta.get(f"amostra:{m}")
        if isinstance(v, dict) and v.get("linhas"):
            meses_amostra.append(m)
            for r in v["linhas"]:
                mot_ano[int(m[:4])][categoria_motivo(r.get("justificativa", ""))] += 1
    canc_ano = {a: sum(v for m, v in canc.items() if m.startswith(str(a))) for a in (2025, 2026)}
    motivos_tab = tabela_motivos(
        mot_ano, "Motivos das marcações canceladas no SISREG",
        f"Justificativa registrada no cancelamento, agrupada por categoria (texto livre não reproduzido). AMOSTRA: "
        f"{sum(sum(c.values()) for c in mot_ano.values())} cancelamentos lidos em {len(meses_amostra)} meses "
        f"(6 páginas espalhadas por mês); estimativa = % da amostra × total oficial de canceladas do ano.",
        estimar=canc_ano) if mot_ano else None
    rot_sit = {3: "  canceladas antes do agendamento", 4: "  devolvidas pelo regulador", 6: "  negadas pelo regulador"}
    exc_tot = {m: sum(exc[s].get(m, 0) for s in exc) for m in MESES} if exc else {}
    secoes.append({
        "id": "desfechos", "titulo": "Atendidas, canceladas e excluídas",
        "texto": "Desfecho das solicitações: atendidas (agendamento cumprido), marcações canceladas (pela data do "
                 "cancelamento) e solicitações excluídas da fila sem agendamento (pela data da solicitação).",
        "series": [
            serie("Atendidas (agendamentos − faltas)", atend, "Calculado" if f_cas else "Indisponível", destaque=True),
            serie("Marcações canceladas", canc, "Oficial" if canc else "Indisponível",
                  "Consulta de Marcações Canceladas do SISREG, pela data do cancelamento.", destaque=True),
            serie("Excluídas da fila sem agendamento", exc_tot, selo_desf, nota_desf, destaque=True),
        ] + [serie(rot_sit[s], exc[s], selo_desf) for s in (4, 6, 3) if s in exc],
        "grafico": {"tipo": "barras", "series": ["Marcações canceladas", "Excluídas da fila sem agendamento"]},
        "motivos": motivos_tab,
    })

    # --- tempo de espera -----------------------------------------------------------------------------
    espera = q(cur, f"""
        select to_char(s.data_regulacao,'YYYY-MM') mes, extract(year from s.data_regulacao)::int ano,
               (s.data_regulacao - s.data_solicitacao) fila_d,
               ((s.data_agendada at time zone '{FUSO}')::date - s.data_solicitacao) atend_d,
               s.categoria, coalesce(nullif(trim(s.procedimento_texto),''),'(sem nome)') proc,
               nullif(trim(s.especialidade_texto),'') esp
        {base} and s.data_regulacao between %s and %s and s.data_solicitacao is not null
          and s.data_regulacao >= s.data_solicitacao""", (INICIO, FIM))
    # Duas esperas: (1) até o atendimento marcado — a que o paciente sente; (2) em fila, só de quem de fato
    # aguardou regulação (autorização em dia posterior ao pedido). Autorização no mesmo dia (agenda direta)
    # puxaria a mediana "em fila" para ~0 e esconderia a fila real.
    por_mes_at = defaultdict(list); por_mes_fila = defaultdict(list); mesmo_dia = Counter(); total_mes = Counter()
    por_proc = defaultdict(lambda: defaultdict(list)); por_esp = defaultdict(lambda: defaultdict(list))

    def grupo(cat, proc, esp):
        if cat == 1:
            e = esp or re.sub(r"^.*?CONSULTA\s+EM\s+", "", proc, flags=re.I).split(" - ")[0].strip() or proc
            return f"Consulta — {e.title()}"
        p = re.sub(r"^\s*GRUPO\s*-\s*", "", proc, flags=re.I).split(" - ")[0].strip()
        return f"Exame/procedimento — {p.title()}"

    for mes, ano, fd, ad, cat, proc, esp in espera:
        total_mes[mes] += 1
        if fd == 0:
            mesmo_dia[mes] += 1
        else:
            por_mes_fila[mes].append(fd)
        if ad is not None and ad >= 0:
            por_mes_at[mes].append(ad)
            por_proc[ano][proc].append(ad)
            por_esp[ano][grupo(cat, proc, esp)].append(ad)
    rf = {m: resumo_espera(por_mes_fila.get(m, [])) for m in MESES}
    ra = {m: resumo_espera(por_mes_at.get(m, [])) for m in MESES}

    def tabela_top(dic, titulo, n=15):
        linhas_t = []
        for nome, dias in sorted(dic.items(), key=lambda kv: -len(kv[1]))[:n]:
            r = resumo_espera(dias)
            linhas_t.append([nome[:70], r["n"], r["mediana"], r["menor"], r["maior"]])
        return {"titulo": titulo, "colunas": ["", "Regulados", "Mediana (dias)", "Menor", "Maior"], "linhas": linhas_t}

    aguard = q(cur, """
        select coalesce(nullif(trim(procedimento_nome),''),'(sem nome)'), count(*),
               percentile_disc(0.5) within group (order by current_date - data_solicitacao),
               max(current_date - data_solicitacao)
        from smsmarica.sisreg_fila_pendente where saiu_em is null and data_solicitacao is not null
        group by 1 order by 2 desc limit 15""")
    secoes.append({
        "id": "espera", "titulo": "Tempo de espera",
        "texto": "Espera até o atendimento: dias entre a solicitação e a data do atendimento marcado, das solicitações "
                 "reguladas no mês. Espera em fila: dias entre a solicitação e a autorização, só de quem aguardou "
                 "regulação (autorizações no mesmo dia do pedido — agenda direta — aparecem à parte).",
        "series": [
            serie("Espera até o atendimento — mediana (dias)", {m: ra[m]["mediana"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "mediana"), destaque=True),
            serie("Espera até o atendimento — 90% até (dias)", {m: ra[m]["p90"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "p90")),
            serie("Espera até o atendimento — menor (dias)", {m: ra[m]["menor"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "menor")),
            serie("Espera até o atendimento — maior (dias)", {m: ra[m]["maior"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "maior")),
            serie("Espera em fila (quem aguardou) — mediana (dias)", {m: rf[m]["mediana"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_fila, "mediana"), destaque=True),
            serie("Espera em fila (quem aguardou) — maior (dias)", {m: rf[m]["maior"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_fila, "maior")),
            serie("Autorizadas no mesmo dia do pedido", pct({m: mesmo_dia[m] for m in MESES}, {m: total_mes[m] for m in MESES}),
                  "Calculado", "Percentual das autorizações do mês feitas no mesmo dia da solicitação.", formato="pct",
                  anual=anual_razao({m: mesmo_dia[m] for m in MESES}, {m: total_mes[m] for m in MESES})),
        ],
        "grafico": {"tipo": "barras", "series": ["Espera até o atendimento — mediana (dias)", "Espera em fila (quem aguardou) — mediana (dias)"]},
        "tabelas": [
            tabela_top(por_esp[2025], "Por especialidade — 2025 (dias até o atendimento)"),
            tabela_top(por_esp[2026], "Por especialidade — 2026 até agosto (dias até o atendimento)"),
            tabela_top(por_proc[2025], "Por procedimento — 2025, 15 mais regulados (dias até o atendimento)"),
            tabela_top(por_proc[2026], "Por procedimento — 2026 até agosto, 15 mais regulados (dias até o atendimento)"),
            {"titulo": "Quem ainda aguarda (fila em 30/09/2026), 15 procedimentos com mais pacientes",
             "colunas": ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
             "linhas": [[r[0][:70], r[1], r[2], r[3]] for r in aguard]},
        ],
    })

    secoes.append({
        "id": "judicial", "titulo": "Demandas judicializadas",
        "texto": "O SISREG não possui marcador de solicitação por mandado judicial — nem na solicitação, nem na "
                 "fila do regulador. O indicador depende de registro próprio da Secretaria (ex.: lista da Procuradoria).",
        "series": [], "indisponivel": True,
    })

    cob = q(cur, f"""select max(s.criado_em) {base}""")[0][0]
    fila_lida = q(cur, "select max(ultimo_visto_em) from smsmarica.sisreg_fila_pendente")[0][0]
    return {"sistema": "SISREG", "nome": "SISREG III — Sistema Nacional de Regulação (Ministério da Saúde)",
            "cobertura": [f"Agendamentos: espelho do Arquivo de Agendamentos do SISREG, última carga em {cob:%d/%m/%Y}.",
                          f"Fila de espera: lida do SISREG diariamente desde 10/09/2026 (última leitura {fila_lida:%d/%m/%Y}).",
                          "Faltas, cotas PPI, marcações canceladas e desfechos pré-agendamento: coletados do SISREG em 30/09/2026."],
            "secoes": secoes}


# --------------------------------------------------------------------------------------- SER / SERNIT
# Conferência feita em 30/09/2026 com Automais.SERNIT/probe_totais.py (soma dos totais por situação na tela).
TOTAL_SERNIT_CONFERIDO = 1511


def externo(cur, p: str, sigla: str, nome: str, judiciais: list[str] | None, total_sistema: int | None = None) -> dict:
    s_tab, e_tab, fk = f"smsmarica.{p}_solicitacao", f"smsmarica.{p}_evento", f"{p}_solicitacao_id"
    secoes = []
    tipos = {1: "Consultas", 2: "Exames"}

    # agendamentos (1º Agendar) e solicitações por tipo
    ag = defaultdict(dict)
    for mes, tipo, n in q(cur, f"""
        with pa as (select e.{fk} sid, min(e.data_evento) d from {e_tab} e where e.tipo_evento = 5 group by 1)
        select to_char((pa.d at time zone '{FUSO}')::date,'YYYY-MM'), s.tipo, count(*)
        from pa join {s_tab} s on s.id = pa.sid and s.excluido_em is null
        where (pa.d at time zone '{FUSO}')::date between %s and %s group by 1,2""", (INICIO, FIM)):
        ag[tipos.get(tipo, "Outros")][mes] = n
    ag_tot = {m: sum(ag[t].get(m, 0) for t in ag) for m in MESES}
    so = defaultdict(dict)
    for mes, tipo, n in q(cur, f"""select to_char(data_solicitacao,'YYYY-MM'), tipo, count(*) from {s_tab}
                                  where excluido_em is null and data_solicitacao between %s and %s group by 1,2""", (INICIO, FIM)):
        so[tipos.get(tipo, "Outros")][mes] = n
    so_tot = {m: sum(so[t].get(m, 0) for t in so) for m in MESES}

    secoes.append({
        "id": "vagas", "titulo": "Vagas disponibilizadas e utilizadas",
        "texto": f"A oferta de vagas do {sigla} pertence ao gestor do sistema e não é exibida ao município solicitante. "
                 "Utilização = solicitações de Maricá que receberam agendamento no mês.",
        "series": [serie("Vagas ofertadas", {}, "Indisponível", f"O {sigla} não expõe a oferta ao município solicitante."),
                   serie("Vagas utilizadas (agendamentos no mês)", ag_tot, "Oficial", "Primeiro agendamento de cada solicitação (remarcações não somam).", destaque=True)],
        "grafico": {"tipo": "barras", "series": ["Vagas utilizadas (agendamentos no mês)"]},
    })

    # absenteísmo: último registro de chegada de cada solicitação
    cheg = q(cur, f"""
        with uc as (select distinct on (e.{fk}) e.{fk} sid, e.data_evento d, e.estado_atual est
                    from {e_tab} e where e.tipo_evento = 6 order by e.{fk}, e.data_evento desc)
        select to_char((uc.d at time zone '{FUSO}')::date,'YYYY-MM'),
               count(*) filter (where uc.est ilike '%%n_o confirmada%%'), count(*) filter (where uc.est not ilike '%%n_o confirmada%%')
        from uc join {s_tab} s on s.id = uc.sid and s.excluido_em is null
        where (uc.d at time zone '{FUSO}')::date between %s and %s group by 1""", (INICIO, FIM))
    nconf = {r[0]: r[1] for r in cheg}; conf = {r[0]: r[2] for r in cheg}
    sem_reg = dict(q(cur, f"""
        select to_char(to_date(substring(agendado_para_texto from '^(\\d{{2}}/\\d{{2}}/\\d{{4}})'),'DD/MM/YYYY'),'YYYY-MM'), count(*)
        from {s_tab} where excluido_em is null and situacao = 3
          and agendado_para_texto ~ '^\\d{{2}}/\\d{{2}}/\\d{{4}}'
          and to_date(substring(agendado_para_texto from '^(\\d{{2}}/\\d{{2}}/\\d{{4}})'),'DD/MM/YYYY') between %s and least(%s, current_date - 1)
        group by 1""", (INICIO, FIM)))
    tot_cheg = {m: (nconf.get(m, 0) + conf.get(m, 0)) or None for m in MESES}
    secoes.append({
        "id": "absenteismo", "titulo": "Absenteísmo",
        "texto": f"Registro de chegada feito pela unidade executora no {sigla} ('Chegada no destino').",
        "series": [
            serie("Chegadas registradas no mês", tot_cheg, "Oficial"),
            serie("  chegada confirmada (compareceu)", conf, "Oficial"),
            serie("  chegada não confirmada (faltou)", nconf, "Oficial", destaque=True),
            serie("Absenteísmo", pct(nconf, tot_cheg), "Calculado", "Chegadas não confirmadas ÷ chegadas registradas no mês.", formato="pct", destaque=True,
                   anual=anual_razao({m: nconf.get(m, 0) for m in MESES}, tot_cheg)),
            serie("Agendados sem registro de chegada", sem_reg, "Calculado",
                  "Solicitações ainda 'Agendada' cuja data já passou — a unidade executora não registrou a chegada."),
        ],
        "grafico": {"tipo": "barras", "series": ["  chegada não confirmada (faltou)"]},
    })

    secoes.append({
        "id": "regulados", "titulo": "Quantitativo regulado de consultas e exames",
        "texto": "Solicitações de Maricá que receberam agendamento no mês, por tipo.",
        "series": [serie("Total regulado", ag_tot, "Oficial", destaque=True)]
        + [serie(f"  {t}", ag[t], "Oficial") for t in ("Consultas", "Exames") if t in ag],
        "grafico": {"tipo": "empilhado", "series": [f"  {t}" for t in ("Consultas", "Exames") if t in ag]},
    })

    # fila no fim do mês
    fila, sem_trilha = {}, {}
    for m in MESES:
        fim = fim_do_mes(m)
        r = q(cur, f"""
          select count(*) filter (where le.estado in ('Em fila','Pendente')
                                  or (le.estado is null and b.trilha and b.primeiro > %s)
                                  or (not b.trilha and b.situacao in (1,2))),
                 count(*) filter (where not b.trilha and b.situacao in (1,2))
          from (select s.id, s.situacao,
                       (s.eventos_count > 0 and not s.historico_indisponivel) trilha,
                       (select min((e.data_evento at time zone '{FUSO}')::date) from {e_tab} e where e.{fk} = s.id) primeiro
                from {s_tab} s where s.excluido_em is null and s.data_solicitacao <= %s) b
          left join lateral (select e.estado_atual estado from {e_tab} e
                             where e.{fk} = b.id and e.estado_atual is not null and e.tipo_evento not in (2, 9)
                               and (e.data_evento at time zone '{FUSO}')::date <= %s
                             order by e.data_evento desc limit 1) le on true""", (fim, fim, fim))[0]
        fila[m], sem_trilha[m] = r[0], r[1]
    secoes.append({
        "id": "fila", "titulo": "Fila e solicitações",
        "texto": "Solicitações de Maricá aguardando no último dia do mês (situação 'Em fila' ou 'Pendente' naquela data) "
                 "e solicitações registradas no mês.",
        "series": [
            serie("Solicitações em fila no fim do mês", fila, "Calculado",
                  "Reconstruída pela trilha de eventos de cada solicitação (último estado até a data).", destaque=True),
            serie("  das quais sem trilha de eventos", sem_trilha, "Calculado",
                  "Sem histórico legível: contadas pela situação atual (em fila/pendente)."),
            serie("Solicitações registradas no mês", so_tot, "Oficial"),
        ] + [serie(f"  {t}", so[t], "Oficial") for t in ("Consultas", "Exames") if t in so],
        "grafico": {"tipo": "barras", "series": ["Solicitações em fila no fim do mês"]},
    })

    # desfechos
    # "Cancelar" que termina em 'Cancelada' = solicitação cancelada; o que volta para 'Em fila' é marcação desfeita.
    cancs = q(cur, f"""
        with uc as (select distinct on (e.{fk}) e.{fk} sid, e.data_evento d, coalesce(e.observacao,'') obs
                    from {e_tab} e where e.tipo_evento = 4 and e.estado_atual ilike 'cancelad%%'
                    order by e.{fk}, e.data_evento)
        select to_char((uc.d at time zone '{FUSO}')::date,'YYYY-MM'), extract(year from (uc.d at time zone '{FUSO}'))::int,
               uc.obs, fu.followup_categoria, fu.observacao
        from uc join {s_tab} s on s.id = uc.sid and s.excluido_em is null
        left join lateral (select e.followup_categoria, e.observacao from {e_tab} e
                           where e.{fk} = uc.sid and e.tipo_evento = 2 and e.data_evento <= uc.d
                           order by e.data_evento desc limit 1) fu on true
        where (uc.d at time zone '{FUSO}')::date between %s and %s""", (INICIO, FIM))
    canc = Counter(r[0] for r in cancs)
    mot = defaultdict(Counter)
    com_fu = 0
    for _mes, ano, obs, fu_cat, fu_obs in cancs:
        mot[ano][motivo_externo(obs, fu_cat, fu_obs)] += 1
        com_fu += 1 if fu_cat else 0
    desfeitas = dict(q(cur, f"""select to_char((e.data_evento at time zone '{FUSO}')::date,'YYYY-MM'), count(distinct e.{fk})
                               from {e_tab} e where e.tipo_evento = 4 and e.estado_atual not ilike 'cancelad%%'
                                 and (e.data_evento at time zone '{FUSO}')::date between %s and %s group by 1""", (INICIO, FIM)))
    dev = dict(q(cur, f"""select to_char((e.data_evento at time zone '{FUSO}')::date,'YYYY-MM'), count(distinct e.{fk})
                         from {e_tab} e where e.tipo_evento = 8 and (e.data_evento at time zone '{FUSO}')::date between %s and %s group by 1""",
                 (INICIO, FIM)))
    motivos_tab = tabela_motivos(
        mot, f"Motivos dos cancelamentos no {sigla}",
        f"Observação registrada no cancelamento, completada pelo último FollowUP anterior quando o texto do cancelamento "
        f"é genérico (ex.: 'não respondida no prazo' + FollowUP 'sem contato: diversas tentativas'). {com_fu} de "
        f"{len(cancs)} cancelamentos têm FollowUP antes. Texto livre não reproduzido.") if cancs else None
    secoes.append({
        "id": "desfechos", "titulo": "Atendidas, canceladas e excluídas",
        "texto": f"Desfechos registrados no {sigla} no mês. O {sigla} não tem situação de 'excluída': a saída da fila sem "
                 "atendimento é o cancelamento (com motivo).",
        "series": [
            serie("Atendidas (chegada confirmada)", conf, "Oficial", destaque=True),
            serie("Canceladas", dict(canc), "Oficial", "Solicitações cujo cancelamento levou à situação 'Cancelada'.", destaque=True),
            serie("Marcações desfeitas (voltaram à fila)", desfeitas, "Oficial", "Cancelamento do agendamento com retorno à fila — não é exclusão."),
            serie("Devolvidas para a regulação", dev, "Oficial", "Voltaram à fila para nova regulação (não é exclusão)."),
        ],
        "grafico": {"tipo": "barras", "series": ["Atendidas (chegada confirmada)", "Canceladas"]},
        "motivos": motivos_tab,
    })

    # espera
    esp = q(cur, f"""
        with pa as (select e.{fk} sid, min(e.data_evento) d from {e_tab} e where e.tipo_evento = 5 group by 1)
        select to_char((pa.d at time zone '{FUSO}')::date,'YYYY-MM'), extract(year from (pa.d at time zone '{FUSO}'))::int,
               ((pa.d at time zone '{FUSO}')::date - s.data_solicitacao), s.tipo, coalesce(s.recurso,'(sem recurso)')
        from pa join {s_tab} s on s.id = pa.sid and s.excluido_em is null
        where (pa.d at time zone '{FUSO}')::date between %s and %s and s.data_solicitacao is not null
          and (pa.d at time zone '{FUSO}')::date >= s.data_solicitacao""", (INICIO, FIM))
    por_mes = defaultdict(list); por_rec = defaultdict(lambda: defaultdict(list)); por_esp = defaultdict(lambda: defaultdict(list))

    def especialidade(tipo, rec):
        r = " ".join(rec.split())
        m1 = re.match(r"(?i)consulta\s+em\s+(.+?)(\s+-\s+|$)", r)
        m2 = re.match(r"(?i)ambulat[óo]rio\s+1[ªa]\s*vez\s*(?:-|em)\s*(.+?)(\s+-\s+|\s*\(|$)", r)
        if m1:
            return "Consulta — " + m1.group(1).strip().title()
        if m2:
            return "Consulta — " + m2.group(1).strip().title()
        if tipo == 2:
            return "Exame — " + re.split(r"\s+-\s+|\s*\(|\s+de\s+|\s+do\s+|\s+da\s+", r, maxsplit=1)[0].strip().title()
        return "Consulta — " + r.split(" - ")[0].title()

    for mes, ano, d, tipo, rec in esp:
        por_mes[mes].append(d)
        por_rec[ano][rec].append(d)
        por_esp[ano][especialidade(tipo, rec)].append(d)
    rm = {m: resumo_espera(por_mes.get(m, [])) for m in MESES}

    def tabela_top(dic, titulo, n=15):
        linhas_t = []
        for nome_, dias in sorted(dic.items(), key=lambda kv: -len(kv[1]))[:n]:
            r = resumo_espera(dias)
            linhas_t.append([nome_[:70], r["n"], r["mediana"], r["menor"], r["maior"]])
        return {"titulo": titulo, "colunas": ["", "Agendados", "Mediana (dias)", "Menor", "Maior"], "linhas": linhas_t}

    aguard = q(cur, f"""select coalesce(recurso,'(sem recurso)'), count(*),
                          percentile_disc(0.5) within group (order by current_date - data_solicitacao),
                          max(current_date - data_solicitacao)
                        from {s_tab} where excluido_em is null and situacao in (1,2) and data_solicitacao is not null
                        group by 1 order by 2 desc limit 15""")
    secoes.append({
        "id": "espera", "titulo": "Tempo de espera",
        "texto": "Dias entre a solicitação e o primeiro agendamento, das solicitações agendadas no mês.",
        "series": [
            serie("Espera — mediana (dias)", {m: rm[m]["mediana"] for m in MESES}, "Calculado", anual=anual_espera(por_mes, "mediana"), destaque=True),
            serie("Espera — 90% até (dias)", {m: rm[m]["p90"] for m in MESES}, "Calculado", anual=anual_espera(por_mes, "p90")),
            serie("Espera — menor (dias)", {m: rm[m]["menor"] for m in MESES}, "Calculado", anual=anual_espera(por_mes, "menor")),
            serie("Espera — maior (dias)", {m: rm[m]["maior"] for m in MESES}, "Calculado", anual=anual_espera(por_mes, "maior")),
        ],
        "grafico": {"tipo": "barras", "series": ["Espera — mediana (dias)"]},
        "tabelas": [
            tabela_top(por_esp[2025], "Por especialidade — 2025 (dias até o agendamento)"),
            tabela_top(por_esp[2026], "Por especialidade — 2026 até agosto (dias até o agendamento)"),
            tabela_top(por_rec[2025], "Por procedimento — 2025, 15 mais agendados"),
            tabela_top(por_rec[2026], "Por procedimento — 2026 até agosto, 15 mais agendados"),
            {"titulo": "Quem ainda aguarda (em fila ou pendente hoje), 15 procedimentos com mais pacientes",
             "colunas": ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
             "linhas": [[r[0][:70], r[1], r[2], r[3]] for r in aguard]},
        ],
    })

    # judicializadas
    if judiciais is None:
        secoes.append({"id": "judicial", "titulo": "Demandas judicializadas", "series": [], "indisponivel": True,
                       "texto": "Não coletado."})
    else:
        cur.execute("drop table if exists tmp_jud; create temp table tmp_jud (id text) on commit drop")
        if judiciais:
            execute_values(cur, "insert into tmp_jud values %s", [(i,) for i in judiciais])
        jl = q(cur, f"""
            select s.id_{p}, s.tipo, left(s.recurso, 60), s.data_solicitacao, s.situacao,
              (select min((e.data_evento at time zone '{FUSO}')::date) from {e_tab} e where e.{fk} = s.id and e.tipo_evento = 5) - s.data_solicitacao,
              (select min((e.data_evento at time zone '{FUSO}')::date) from {e_tab} e where e.{fk} = s.id and e.tipo_evento = 6) - s.data_solicitacao
            from {s_tab} s join tmp_jud j on j.id = s.id_{p} where s.excluido_em is null order by s.data_solicitacao""")
        sit_nome = {1: "Em fila", 2: "Pendente", 3: "Agendada", 4: "Chegada não confirmada", 5: "Chegada confirmada",
                    6: "Cancelada", 7: "Alta"}
        jm = Counter(f"{r[3]:%Y-%m}" for r in jl if r[3])
        no_periodo = [r for r in jl if r[3] and INICIO <= r[3] <= FIM]
        tempos = [r[5] for r in no_periodo if r[5] is not None and r[5] >= 0]
        secoes.append({
            "id": "judicial", "titulo": "Demandas judicializadas",
            "texto": f"Solicitações marcadas no {sigla} como 'Mandado judicial' (filtro 'Somente com mandado judicial' da "
                     f"pesquisa de solicitações). Total no {sigla}: {len(jl)}; com solicitação entre jan/2025 e ago/2026: {len(no_periodo)}.",
            "series": [serie("Solicitações judicializadas (pela data da solicitação)", {m: jm.get(m, 0) for m in MESES},
                             "Oficial", destaque=True)],
            "grafico": {"tipo": "barras", "series": ["Solicitações judicializadas (pela data da solicitação)"]},
            "resumo_judicial": resumo_espera(tempos),
            "tabelas": [{"titulo": "Solicitações judicializadas com solicitação no período",
                         "colunas": ["ID", "Tipo", "Recurso", "Solicitada em", "Situação atual", "Dias até agendar", "Dias até a chegada"],
                         "linhas": [[r[0], tipos.get(r[1], "—"), r[2], f"{r[3]:%d/%m/%Y}", sit_nome.get(r[4], "—"),
                                     r[5] if r[5] is not None and r[5] >= 0 else "—",
                                     r[6] if r[6] is not None and r[6] >= 0 else "—"] for r in no_periodo]}]
            if no_periodo else [],
        })

    exec_ = q(cur, f"select max(finalizado_em) from smsmarica.{p}_varredura_execucao where status = 3")[0][0]
    n_sol = q(cur, f"select count(*) from {s_tab} where excluido_em is null")[0][0]
    return {"sistema": sigla, "nome": nome,
            "cobertura": [f"Espelho das solicitações de Maricá no {sigla} ({f'{n_sol:,}'.replace(',', '.')} solicitações), "
                          f"última varredura completa em {exec_:%d/%m/%Y}.",
                          "Mandado judicial: pesquisa no próprio sistema em 30/09/2026."]
            + ([f"Conferência em 30/09/2026: o {sigla} informa {total_sistema:,} solicitações de Maricá e o espelho tem "
                f"{n_sol:,} ({100 * n_sol / total_sistema:.0f}%). A diferença está sendo corrigida; os volumes deste "
                f"relatório são um piso.".replace(",", ".")] if total_sistema else []),
            "secoes": secoes}


# ------------------------------------------------------------------------------------- ESUS São Gonçalo
def esussg(cur) -> dict:
    """ESUS de São Gonçalo (ADR-0063): espelho da fila e dos agendados de Maricá no ESUS de SG (PPI).
    Traz entrada e saída da fila — a fila mês a mês sai inteira —, mas não traz comparecimento, oferta
    nem o motivo de quem sai da fila sem agendamento."""
    s_tab = "smsmarica.esussg_solicitacao"
    base = f"from {s_tab} s where s.excluido_em is null"
    tipos = {1: "Consultas", 2: "Exames"}
    secoes = []

    ag = defaultdict(dict)
    for mes, tipo, n in q(cur, f"""select to_char(s.data_agendada,'YYYY-MM'), s.tipo, count(*) {base}
                                   and s.data_agendada between %s and %s group by 1,2""", (INICIO, FIM)):
        ag[tipos.get(tipo, "Outros")][mes] = n
    ag_tot = {m: sum(ag[t].get(m, 0) for t in ag) for m in MESES}
    secoes.append({
        "id": "vagas", "titulo": "Vagas disponibilizadas e utilizadas",
        "texto": "A oferta de vagas do ESUS de São Gonçalo (cotas da PPI) pertence ao município executor e não é exibida a "
                 "Maricá. Utilização = agendamentos de pacientes de Maricá com data de atendimento no mês.",
        "series": [serie("Vagas ofertadas", {}, "Indisponível", "O ESUS de São Gonçalo não expõe a oferta ao município solicitante."),
                   serie("Vagas utilizadas (agendamentos no mês)", ag_tot, "Oficial", "Pela data do atendimento agendado.", destaque=True)]
        + [serie(f"  {t}", ag[t], "Oficial") for t in ("Consultas", "Exames") if t in ag],
        "grafico": {"tipo": "barras", "series": ["Vagas utilizadas (agendamentos no mês)"]},
    })
    secoes.append({"id": "absenteismo", "titulo": "Absenteísmo", "series": [], "indisponivel": True,
                   "texto": "O ESUS de São Gonçalo não informa a Maricá se o paciente compareceu ao atendimento agendado."})

    reg = defaultdict(dict)
    for mes, tipo, n in q(cur, f"""select to_char(coalesce(s.agendamento_cadastrado_em, s.data_saida_fila),'YYYY-MM'), s.tipo, count(*)
                                   {base} and s.situacao = 3
                                   and coalesce(s.agendamento_cadastrado_em, s.data_saida_fila) between %s and %s group by 1,2""",
                          (INICIO, FIM)):
        reg[tipos.get(tipo, "Outros")][mes] = n
    reg_tot = {m: sum(reg[t].get(m, 0) for t in reg) for m in MESES}
    secoes.append({
        "id": "regulados", "titulo": "Quantitativo regulado de consultas e exames",
        "texto": "Solicitações de Maricá agendadas pela regulação de São Gonçalo no mês (data em que o agendamento foi feito).",
        "series": [serie("Total regulado", reg_tot, "Oficial", destaque=True)]
        + [serie(f"  {t}", reg[t], "Oficial") for t in ("Consultas", "Exames") if t in reg],
        "grafico": {"tipo": "empilhado", "series": [f"  {t}" for t in ("Consultas", "Exames") if t in reg]},
    })

    fila = {}
    for m in MESES:
        fim = fim_do_mes(m)
        fila[m] = q(cur, f"""select count(*) {base} and s.data_entrada_fila <= %s
                             and (s.data_saida_fila is null or s.data_saida_fila > %s)""", (fim, fim))[0][0]
    so = defaultdict(dict)
    for mes, tipo, n in q(cur, f"""select to_char(s.data_entrada_fila,'YYYY-MM'), s.tipo, count(*) {base}
                                   and s.data_entrada_fila between %s and %s group by 1,2""", (INICIO, FIM)):
        so[tipos.get(tipo, "Outros")][mes] = n
    so_tot = {m: sum(so[t].get(m, 0) for t in so) for m in MESES}
    secoes.append({
        "id": "fila", "titulo": "Fila e solicitações",
        "texto": "Pacientes de Maricá na fila do ESUS de São Gonçalo no último dia do mês (entraram até a data e ainda não "
                 "tinham saído) e solicitações que entraram na fila no mês.",
        "series": [
            serie("Solicitações em fila no fim do mês", fila, "Parcial",
                  "Reconstruída pelas datas de entrada e saída da fila. Quem saiu da fila sem agendamento antes do primeiro "
                  "espelho (30/09/2026) já não aparece no ESUS — o número é um piso.", destaque=True),
            serie("Solicitações registradas no mês", so_tot, "Parcial", "Pela data de entrada na fila; exclusões anteriores a 30/09/2026 não aparecem."),
        ] + [serie(f"  {t}", so[t], "Parcial") for t in ("Consultas", "Exames") if t in so],
        "grafico": {"tipo": "barras", "series": ["Solicitações em fila no fim do mês"]},
    })

    secoes.append({
        "id": "desfechos", "titulo": "Atendidas, canceladas e excluídas",
        "texto": "O ESUS de São Gonçalo mostra a Maricá só duas listas — fila e agendados. Quem sai das duas sem agendamento "
                 "(exclusão, cancelamento, transferência) some sem motivo visível; o espelho registra essas saídas desde "
                 "30/09/2026. Por isso canceladas, excluídas e motivos não estão disponíveis para o período.",
        "series": [serie("Agendadas (saíram da fila com agendamento)", reg_tot, "Oficial", destaque=True),
                   serie("Canceladas / excluídas", {}, "Indisponível", "O ESUS não exibe cancelamento nem exclusão ao município solicitante.")],
        "grafico": {"tipo": "barras", "series": ["Agendadas (saíram da fila com agendamento)"]},
        "motivos": None,
    })

    esp = q(cur, f"""select to_char(coalesce(s.agendamento_cadastrado_em, s.data_saida_fila),'YYYY-MM'),
                            extract(year from coalesce(s.agendamento_cadastrado_em, s.data_saida_fila))::int,
                            (coalesce(s.agendamento_cadastrado_em, s.data_saida_fila) - s.data_entrada_fila),
                            (s.data_agendada - s.data_entrada_fila), coalesce(s.recurso,'(sem recurso)')
                     {base} and s.situacao = 3 and s.data_entrada_fila is not null
                       and coalesce(s.agendamento_cadastrado_em, s.data_saida_fila) between %s and %s""", (INICIO, FIM))
    por_mes = defaultdict(list); por_mes_at = defaultdict(list); por_rec = defaultdict(lambda: defaultdict(list))
    for mes, ano, fd, ad, rec in esp:
        if fd is not None and fd >= 0:
            por_mes[mes].append(fd)
        if ad is not None and ad >= 0:
            por_mes_at[mes].append(ad)
            por_rec[ano][rec].append(ad)
    rf = {m: resumo_espera(por_mes.get(m, [])) for m in MESES}
    ra = {m: resumo_espera(por_mes_at.get(m, [])) for m in MESES}

    def tabela_top(dic, titulo, n=15):
        return {"titulo": titulo, "colunas": ["", "Agendados", "Mediana (dias)", "Menor", "Maior"],
                "linhas": [[nome[:70], r["n"], r["mediana"], r["menor"], r["maior"]]
                           for nome, dias in sorted(dic.items(), key=lambda kv: -len(kv[1]))[:n] for r in [resumo_espera(dias)]]}

    aguard = q(cur, f"""select coalesce(s.recurso,'(sem recurso)'), count(*),
                          percentile_disc(0.5) within group (order by current_date - s.data_entrada_fila),
                          max(current_date - s.data_entrada_fila)
                        {base} and s.situacao in (1,2) and s.data_entrada_fila is not null group by 1 order by 2 desc limit 15""")
    secoes.append({
        "id": "espera", "titulo": "Tempo de espera",
        "texto": "Espera em fila: dias entre a entrada na fila e o agendamento. Espera até o atendimento: dias entre a entrada "
                 "na fila e a data do atendimento marcado. Por recurso (o ESUS organiza a fila por recurso/especialidade).",
        "series": [
            serie("Espera até o atendimento — mediana (dias)", {m: ra[m]["mediana"] for m in MESES}, "Calculado", destaque=True,
                  anual=anual_espera(por_mes_at, "mediana")),
            serie("Espera até o atendimento — menor (dias)", {m: ra[m]["menor"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "menor")),
            serie("Espera até o atendimento — maior (dias)", {m: ra[m]["maior"] for m in MESES}, "Calculado", anual=anual_espera(por_mes_at, "maior")),
            serie("Espera em fila — mediana (dias)", {m: rf[m]["mediana"] for m in MESES}, "Calculado", destaque=True,
                  anual=anual_espera(por_mes, "mediana")),
            serie("Espera em fila — maior (dias)", {m: rf[m]["maior"] for m in MESES}, "Calculado", anual=anual_espera(por_mes, "maior")),
        ],
        "grafico": {"tipo": "barras", "series": ["Espera até o atendimento — mediana (dias)", "Espera em fila — mediana (dias)"]},
        "tabelas": [
            tabela_top(por_rec[2025], "Por recurso — 2025 (dias até o atendimento)"),
            tabela_top(por_rec[2026], "Por recurso — 2026 até agosto (dias até o atendimento)"),
            {"titulo": "Quem ainda aguarda (fila em 30/09/2026), por recurso",
             "colunas": ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
             "linhas": [[r[0][:70], r[1], r[2], r[3]] for r in aguard]},
        ],
    })

    jl = q(cur, f"""select s.id_esussg, s.tipo, left(s.recurso, 60), s.data_entrada_fila, s.situacao,
                          (coalesce(s.agendamento_cadastrado_em, s.data_saida_fila) - s.data_entrada_fila),
                          (s.data_agendada - s.data_entrada_fila)
                    {base} and s.prioridade ilike '%%judicial%%' order by s.data_entrada_fila""")
    sit_nome = {1: "Em fila", 2: "Pendente", 3: "Agendada", 4: "Saiu da fila"}
    no_periodo = [r for r in jl if r[3] and INICIO <= r[3] <= FIM]
    jm = Counter(f"{r[3]:%Y-%m}" for r in no_periodo)
    tempos = [r[5] for r in no_periodo if r[5] is not None and r[5] >= 0]
    secoes.append({
        "id": "judicial", "titulo": "Demandas judicializadas",
        "texto": f"Solicitações com prioridade 'Mandado judicial' no ESUS de São Gonçalo. Total no espelho: {len(jl)}; "
                 f"com entrada na fila entre jan/2025 e ago/2026: {len(no_periodo)}.",
        "series": [serie("Solicitações judicializadas (pela entrada na fila)", {m: jm.get(m, 0) for m in MESES}, "Oficial", destaque=True)],
        "grafico": {"tipo": "barras", "series": ["Solicitações judicializadas (pela entrada na fila)"]},
        "resumo_judicial": resumo_espera(tempos),
        "tabelas": [{"titulo": "Solicitações judicializadas (todas as do espelho)",
                     "colunas": ["ID", "Tipo", "Recurso", "Entrada na fila", "Situação atual", "Dias até agendar", "Dias até o atendimento"],
                     "linhas": [[r[0], tipos.get(r[1], "—"), r[2], f"{r[3]:%d/%m/%Y}" if r[3] else "—", sit_nome.get(r[4], "—"),
                                 r[5] if r[5] is not None and r[5] >= 0 else "—", r[6] if r[6] is not None and r[6] >= 0 else "—"]
                                for r in jl]}] if jl else [],
    })

    n_sol = q(cur, f"select count(*) {base}")[0][0]
    exec_ = q(cur, "select min(iniciado_em), max(finalizado_em) from smsmarica.esussg_varredura_execucao where status = 3")[0]
    return {"sistema": "ESUS SG", "nome": "ESUS de São Gonçalo — regulação da PPI de São Gonçalo",
            "cobertura": [f"Espelho da fila e dos agendados de Maricá no ESUS de São Gonçalo ({f'{n_sol:,}'.replace(',', '.')} "
                          f"solicitações, histórico desde 2017), primeira varredura em {exec_[0]:%d/%m/%Y}, última em {exec_[1]:%d/%m/%Y}.",
                          "O ESUS não informa comparecimento, oferta de vagas nem motivo de exclusão ao município solicitante."],
            "secoes": secoes}


def main() -> int:
    coleta = carregar_coleta()
    so_estes = set(sys.argv[1:])  # ex.: python gerar_dados.py esussg  → recalcula só esse e mescla no dados.json
    if so_estes:
        dados = json.loads((AQUI / "dados.json").read_text(encoding="utf-8"))
        with conn() as c, c.cursor() as cur:
            cur.execute("set local statement_timeout = '600s'")
            if "esussg" in so_estes:
                dados["relatorios"]["esussg"] = esussg(cur)
            c.rollback()
        dados["gerado_em"] = dt.datetime.now().strftime("%d/%m/%Y %H:%M")
        (AQUI / "dados.json").write_text(json.dumps(dados, ensure_ascii=False, indent=1, default=str), encoding="utf-8")
        print("dados.json atualizado:", ", ".join(sorted(so_estes)))
        return 0
    jud_ser = None
    if JUDICIAL_SER.exists():
        jud_ser = [x["id"] for v in json.loads(JUDICIAL_SER.read_text(encoding="utf-8")).values() for x in v]
    with conn() as c, c.cursor() as cur:
        cur.execute("set local statement_timeout = '600s'")
        dados = {
            "gerado_em": dt.datetime.now().strftime("%d/%m/%Y %H:%M"),
            "periodo": {"inicio": "01/01/2025", "fim": "31/08/2026", "meses": MESES},
            "relatorios": {
                "sisreg": sisreg(cur, coleta),
                "ser": externo(cur, "ser", "SER", "SER — Sistema Estadual de Regulação (SES-RJ)", jud_ser),
                "sernit": externo(cur, "sernit", "SERNIT", "SER Niterói — Sistema de Regulação de Niterói", [],
                                  total_sistema=TOTAL_SERNIT_CONFERIDO),
                "esussg": esussg(cur),
            },
        }
        c.rollback()  # temp tables; nada é gravado
    (AQUI / "dados.json").write_text(json.dumps(dados, ensure_ascii=False, indent=1, default=str), encoding="utf-8")
    print("dados.json gerado")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
