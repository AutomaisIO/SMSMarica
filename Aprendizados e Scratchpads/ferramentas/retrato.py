"""Retrato do estado da varredura/fila. Roda antes e depois de cada teste para comparar."""
from db import q

q("""
select to_char(now() at time zone 'America/Sao_Paulo','DD/MM HH24:MI:SS') as agora_brasilia,
       (now() at time zone 'America/Sao_Paulo')::time > time '15:00' as janela_sisreg_aberta
""", title='relogio')

q("""
select sincronismo_automatico_ativo as sincronismo_ligado
from smsmarica.sisreg_configuracao
""", title='chave-mestra')

q("""
select tipo, count(*) filter (where tratada_em is null) as pendentes, count(*) as total
from smsmarica.sisreg_alteracao_agenda
group by 1 order by 1
""", title='fila de alteracoes (1=Remarcado 3=Procedimento 4=Sumiu)')

q("""
select to_char(iniciado_em at time zone 'America/Sao_Paulo','DD/MM HH24:MI') as iniciou,
       unidade_nome, disparo, status, janela_inicio, janela_fim,
       requisicoes, registros_encontrados, validos, duracao_segundos,
       left(coalesce(mensagem_erro,''),150) as mensagem
from smsmarica.sisreg_varredura_execucao
order by iniciado_em desc limit 5
""", title='ultimas execucoes (disparo 1=Manual 2=Agendado | status 3=Concluida 4=Parcial 5=Erro 6=Cancelada)')
