# -*- coding: utf-8 -*-
"""Dump somente-leitura de conversas reais (14 dias) para simulação do robô.
Threads com atendente humano e/ou mensagens fora do expediente + corpus de 30 dias p/ classificador."""
import re, sys
sys.stdout.reconfigure(encoding="utf-8")

s = open("/etc/smsmarica-server/env").read()
m = re.search(r"ConnectionStrings__DefaultDb=(.+)", s)
d = dict(kv.split("=", 1) for kv in m.group(1).strip().split(";") if "=" in kv)
import psycopg2
conn = psycopg2.connect(host=d.get("Host"), port=d.get("Port", "5432"), dbname=d.get("Database"),
                        user=d.get("Username"), password=d.get("Password"))
cur = conn.cursor()

cur.execute("select url_app from smsmarica.instituicao limit 1")
r = cur.fetchone()
print("URL_APP:", r[0] if r else None)

# Conversas candidatas: entrada de cidadão nos últimos 14 dias E (humano atuou OU entrada fora do
# expediente 08:00-17:10 Brasília OU fim de semana)
cur.execute("""
with cand as (
  select m.conversa_id,
         bool_or(m.direcao = 1 and m.autor_usuario_id is not null) as tem_humano,
         bool_or(m.direcao = 2 and (
            (m.ocorrido_em at time zone 'America/Sao_Paulo')::time < time '08:00'
            or (m.ocorrido_em at time zone 'America/Sao_Paulo')::time >= time '17:10'
            or extract(dow from (m.ocorrido_em at time zone 'America/Sao_Paulo')) in (0,6)
         )) as tem_fora,
         max(m.ocorrido_em) filter (where m.direcao = 2) as ult_entrada,
         count(*) filter (where m.direcao = 2) as n_entradas
  from smsmarica.whatsapp_mensagem m
  where m.ocorrido_em > now() - interval '14 days' and m.conversa_id is not null
  group by 1
)
select c.conversa_id, c.tem_humano, c.tem_fora, c.n_entradas, c.ult_entrada,
       cv.robo_bloqueado, cv.robo_interacoes_na_janela, (cv.paciente_id is not null) as tem_paciente
from cand c
join smsmarica.conversa cv on cv.id = c.conversa_id
where c.n_entradas >= 1 and (c.tem_humano or c.tem_fora)
order by c.ult_entrada desc
limit 40
""")
convs = cur.fetchall()
print(f"CANDIDATAS: {len(convs)}")

for (cid, tem_h, tem_f, n_in, ult, bloq, inter, tem_p) in convs:
    print()
    print("#" * 100)
    print(f"CONVERSA {cid} humano={tem_h} fora_expediente={tem_f} entradas={n_in} "
          f"robo_bloqueado={bloq} interacoes_janela={inter} paciente_vinculado={tem_p}")
    cur.execute("""
        select to_char(m.ocorrido_em at time zone 'America/Sao_Paulo', 'Dy DD/MM HH24:MI'),
               m.direcao, m.tipo_mensagem, (m.autor_usuario_id is not null), m.template,
               left(regexp_replace(coalesce(m.conteudo,''), '\\s+', ' ', 'g'), 260)
        from smsmarica.whatsapp_mensagem m
        where m.conversa_id = %s
        order by m.ocorrido_em desc limit 18
    """, (cid,))
    linhas = cur.fetchall()[::-1]
    for (ts, dir_, tipo, tem_autor, tpl, txt) in linhas:
        if dir_ == 2: papel = "CIDADAO "
        elif tipo == 9: papel = "ROBO    "
        elif tem_autor: papel = "ATENDENTE"
        elif tipo == 6: papel = f"TEMPLATE({(tpl or '?')[:24]})"
        elif tipo == 8: papel = "SISTEMA "
        elif tipo == 7: papel = "NOTA-INT"
        else: papel = "SAIDA?  "
        print(f"  [{ts}] {papel:26} {txt}")

# Corpus 30 dias para o classificador (entrada de cidadão, texto)
print()
print("=" * 100)
print("CORPUS_30D (uma por linha, tab-separado: timestamp_local <TAB> texto ate 140c)")
print("=" * 100)
cur.execute("""
    select to_char(m.ocorrido_em at time zone 'America/Sao_Paulo', 'DD/MM HH24:MI'),
           left(regexp_replace(m.conteudo, '\\s+', ' ', 'g'), 140)
    from smsmarica.whatsapp_mensagem m
    where m.direcao = 2 and m.ocorrido_em > now() - interval '30 days'
      and m.conteudo is not null and length(trim(m.conteudo)) > 0
    order by m.ocorrido_em desc limit 2500
""")
for (ts, txt) in cur.fetchall():
    print(f"{ts}\t{txt}")
conn.close()
print("FIM")
