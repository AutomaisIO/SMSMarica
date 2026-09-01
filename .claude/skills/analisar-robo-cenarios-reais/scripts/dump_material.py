# -*- coding: utf-8 -*-
"""Dump somente-leitura do material do robô para análise (assuntos, treinos, condições, comandos,
erros marcados, estatísticas). Saída em UTF-8 no stdout."""
import re, sys, json

sys.stdout.reconfigure(encoding="utf-8")

s = open("/etc/smsmarica-server/env").read()
m = re.search(r"ConnectionStrings__DefaultDb=(.+)", s)
d = dict(kv.split("=", 1) for kv in m.group(1).strip().split(";") if "=" in kv)

import psycopg2
conn = psycopg2.connect(host=d.get("Host"), port=d.get("Port", "5432"), dbname=d.get("Database"),
                        user=d.get("Username"), password=d.get("Password"))
cur = conn.cursor()

TIPO_COND = {1: "PalavraChave", 2: "Frase", 3: "Regex"}
TIPO_TREINO = {1: "Instrucao", 2: "Exemplo", 3: "Glossario", 4: "Do", 5: "Dont"}
CMD = {1: "ConsultarStatusAgendamento", 2: "ConfirmarPresenca", 3: "CancelarPresenca",
       4: "ReenviarLinkApp", 5: "EncaminharParaHumano", 10: "VerificarCadastro",
       11: "ConsultarCadastro", 12: "ConsultarUnidades"}

print("=" * 90)
print("CONFIGURACAO GLOBAL")
print("=" * 90)
cur.execute("select ativo, modelo_padrao, nome_exibicao, persona_global, mensagem_handoff, "
            "mensagem_fora_horario, hora_atendimento_humano_inicio, hora_atendimento_humano_fim, motor "
            "from smsmarica.robo_configuracao")
r = cur.fetchone()
print(f"ativo={r[0]} modelo={r[1]} nome={r[2]} motor={r[8]} horario_humano={r[6]}-{r[7]}")
print(f"mensagem_handoff={r[4]!r}")
print(f"mensagem_fora_horario={r[5]!r}")
print("--- PERSONA GLOBAL ---")
print(r[3])

print()
print("=" * 90)
print("ASSUNTOS (ordem de classificacao = coluna ordem; primeiro que casa vence)")
print("=" * 90)
cur.execute("""select id, nome, descricao, instrucoes_persona, modelo, ativo, horario_inicio,
                      horario_fim, dias_semana, max_interacoes_sem_resolver, limiar_confianca,
                      ordem, padrao
               from smsmarica.robo_assunto where excluido_em is null
               order by ordem, nome""")
assuntos = cur.fetchall()
for a in assuntos:
    (aid, nome, desc, instr, modelo, ativo, hi, hf, dias, maxi, limiar, ordem, padrao) = a
    print()
    print(f"### ASSUNTO [ordem={ordem}] {nome}  (ativo={ativo} padrao={padrao} max={maxi} "
          f"limiar={limiar} modelo={modelo or '-'} horario={hi}-{hf} dias={dias})")
    print(f"id={aid}")
    if desc: print(f"descricao: {desc}")
    print("--- instrucoes_persona ---")
    print(instr)
    cur.execute("select tipo, valor, ativo, ordem from smsmarica.robo_assunto_condicao "
                "where robo_assunto_id=%s order by ordem", (aid,))
    conds = cur.fetchall()
    print(f"--- condicoes ({len(conds)}) ---")
    for (t, v, at, o) in conds:
        flag = "" if at else " [INATIVA]"
        print(f"  [{TIPO_COND.get(t, t)}]{flag} {v}")
    cur.execute("select tipo, titulo, conteudo, ordem, ativo from smsmarica.robo_assunto_treino "
                "where robo_assunto_id=%s order by ordem", (aid,))
    trs = cur.fetchall()
    print(f"--- treinos ({len(trs)}) ---")
    for (t, ti, co, o, at) in trs:
        flag = "" if at else " [INATIVO]"
        print(f"  [{TIPO_TREINO.get(t, t)}]{flag} {ti or ''}: {co}")
    cur.execute("select comando, habilitado from smsmarica.robo_assunto_comando "
                "where robo_assunto_id=%s order by comando", (aid,))
    cs = cur.fetchall()
    print(f"--- comandos ({len(cs)}) ---")
    for (c, h) in cs:
        print(f"  {CMD.get(c, c)} habilitado={h}")

print()
print("=" * 90)
print("ERROS MARCADOS PELOS OPERADORES (robo_erro_resposta) — feedback direto de treinamento")
print("=" * 90)
cur.execute("""select e.criado_em::date, coalesce(a.nome,'(sem assunto)'), e.status, e.trecho,
                      e.nota, e.revisao_nota
               from smsmarica.robo_erro_resposta e
               left join smsmarica.robo_assunto a on a.id = e.robo_assunto_id
               order by e.criado_em""")
for (dt, an, st, tre, nota, rev) in cur.fetchall():
    st_nome = {1: "Aberto", 2: "Revisado", 3: "Descartado"}.get(st, st)
    print(f"\n[{dt}] assunto={an} status={st_nome}")
    if tre: print(f"  trecho da resposta errada: {tre[:500]}")
    if nota: print(f"  nota do operador: {nota[:500]}")
    if rev: print(f"  nota de revisao: {rev[:500]}")

print()
print("=" * 90)
print("ESTATISTICAS 30 DIAS (robo_tarefa)")
print("=" * 90)
cur.execute("""select coalesce(a.nome,'(sem assunto)') as assunto, count(*) as tarefas,
                      count(*) filter (where t.status = 3) as respondidas,
                      count(*) filter (where t.status = 4) as handoff,
                      count(*) filter (where t.status = 5) as falha,
                      round(avg(extract(epoch from (t.atualizado_em - t.criado_em)))
                            filter (where t.status = 3)) as lat_media_s,
                      sum(t.tokens_entrada) as tok_in, sum(t.tokens_saida) as tok_out,
                      round(sum(t.custo_usd)::numeric, 4) as custo
               from smsmarica.robo_tarefa t
               left join smsmarica.robo_assunto a on a.id = t.robo_assunto_id
               where t.criado_em > now() - interval '30 days'
               group by 1 order by 2 desc""")
print(f"{'assunto':44} {'tarefas':>7} {'resp':>5} {'hand':>5} {'falha':>5} {'lat_s':>6} {'tok_in':>9} {'tok_out':>8} {'custo':>8}")
for row in cur.fetchall():
    print(f"{str(row[0])[:44]:44} {row[1]:>7} {row[2]:>5} {row[3]:>5} {row[4]:>5} "
          f"{str(row[5] or '-'):>6} {str(row[6] or 0):>9} {str(row[7] or 0):>8} {str(row[8] or 0):>8}")

print()
print("=" * 90)
print("AMOSTRA DE MENSAGENS SEM ASSUNTO (14 dias, so texto de entrada, truncado 120c)")
print("(uso interno para minerar condicoes; NAO citar dados pessoais em relatorios)")
print("=" * 90)
cur.execute("""select left(regexp_replace(wm.conteudo, '\\s+', ' ', 'g'), 120)
               from smsmarica.robo_tarefa t
               join smsmarica.whatsapp_mensagem wm on wm.id = t.mensagem_whatsapp_id
               where t.robo_assunto_id is null
                 and t.criado_em > now() - interval '14 days'
                 and wm.conteudo is not null and length(wm.conteudo) > 3
               order by t.criado_em desc limit 120""")
for (txt,) in cur.fetchall():
    print(f"  - {txt}")

conn.close()
print("\nFIM")
