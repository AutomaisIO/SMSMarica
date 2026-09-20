# Lote de confirmação do CDT — estratégia de medição (setembro/2026)

> **O que é:** o primeiro envio em volume de confirmação de agendamento por WhatsApp, feito de
> propósito como **experimento fechado**, para medir acerto, erro e o que volta de conversa antes
> de abrir para a rede.
>
> Decidido com o Bernardo em 17/09/2026. Regras do canal: [ADR-0057](../adr/0057-destinatario-correto-e-contato-negado.md).

## Por que assim

O envio estava desligado por insegurança com **as respostas** — não com a mensagem. O que não se
sabe medir, não se sabe soltar: quantos confirmam, quantos avisam que não vão, quantos números
estão errados, quanta conversa volta para a Central e o que o robô faz com ela.

Por isso: **uma unidade, um lote, uma vez.** Nada entra depois — o que for medido nos próximos
dias é resposta a **este** envio, e não a um fluxo contínuo que ninguém consegue separar.

## Desenho do experimento

| | |
|---|---|
| **Unidade** | CDT - DR ALBERTO LUIS M. BORGES (única com aviso configurado) |
| **Coorte** | agendamentos de **21/09 a 27/09** (segunda a domingo) |
| **Tamanho** | **1.024** agendamentos · **993** pacientes distintos (133 consultas, 891 exames) |
| **Fora de propósito** | **18/09 (sexta)** — em cima da hora; o paciente receberia o aviso na véspera |
| **Disparo** | manual, pela aba *Disparar lote* → modo **forçar** (ignora as chaves) |
| **Aviso automático da unidade** | **DESLIGADO** em 17/09 21:04 — nada novo entra na coorte |
| **Varredura da agenda** | **LIGADA** (18:10 diário) — a agenda segue em dia, mas não gera aviso |

O modo forçado inclui os agendamentos cujo **procedimento** está com o aviso desligado (336 dos
1.024). É deliberado: a medição é do canal, não do recorte de procedimento.

### O que o lote NÃO ignora

Forçar ignora as chaves de operação, nunca as regras de privacidade ([ADR-0057](../adr/0057-destinatario-correto-e-contato-negado.md)):

- número marcado como **inválido** não recebe;
- número **não verificado** recebe primeiro o **desafio de identificação** (4 dígitos do CPF →
  mês/ano → nome → vínculo), e a confirmação com data e local só sai depois que ele passa;
- a resposta do paciente vive **só no SMSMais** — nada vai para o SISREG.

Isso importa para ler os números: **a maior parte da coorte não tem contato verificado**, então o
primeiro contato da maioria será o desafio, não a confirmação. Um "acerto" aqui é a pessoa
**concluir o desafio**; a confirmação é o passo seguinte.

## Ritmo esperado

Fila de 100 por rodada, uma rodada por minuto, janela de **08:00–18:00**. Disparado à noite, o
lote fica empilhado e **começa a sair às 8h**, escoando em ~11 minutos.

## O que medir

### 1. Entrega (o canal funciona?)
- enviadas, entregues, lidas, falhas;
- **sem celular válido** e **número inválido** — qualidade do cadastro, que é o que trava a régua;
- falhas por erro da Meta (número inexistente, fora do WhatsApp).

### 2. Identificação (a régua de LGPD é atravessável por gente comum?)
- quantos ficaram em *aguardando o paciente se identificar*;
- quantos **concluíram** o desafio (e viraram confirmação enviada);
- quantos **esgotaram** as 3 chances;
- quantos responderam **"não sou essa pessoa"** → número inválido;
- quantos pediram **atendente** durante o desafio;
- **vínculo declarado**: quantos "próprio", "mãe/pai/responsável", "outro parente" — a primeira
  medida real de quantos pacientes são atendidos pelo telefone de outra pessoa.

### 3. Resposta ao agendamento (o que o serviço ganha)
- **confirmaram** × **avisaram que não vão** × **sem resposta**;
- canal da resposta (link, botões, app, robô);
- **motivos de cancelamento** — texto livre, lido a olho: é aqui que aparece o que a fila não conta;
- tempo entre envio e resposta.

### 4. Carga de atendimento (o que custa)
- conversas abertas no período × média dos dias anteriores;
- mensagens recebidas por paciente da coorte;
- quantas o **robô** respondeu e quantas foram para **humano**;
- quantas viraram **pendência de cadastro**.

### 5. Efeito no dia (o que interessa de verdade)
- comparecimento nos dias 21–27 × semana anterior — pela autorização na recepção
  (`autorizado_em`), que é o que marca a presença;
- vagas liberadas por cancelamento com antecedência.

## Como medir

**Na tela:** menu **Confirmações** → aba *Fila de envio* (números do dia, filtros por situação) e
aba *Respostas dos pacientes* (confirmaram / não vão, com motivo). A coorte é reconhecível por
`origem = Manual` e a data do disparo.

**No banco** (`scripts/` deste diretório não existe de propósito — são consultas de leitura, para
rodar pelo caminho de sempre):

```sql
-- 1. ENTREGA — a coorte é o lote manual disparado no dia
with coorte as (
  select c.* from smsmarica.comunicacao_paciente c
  where c.finalidade = 1 and c.origem = 2          -- confirmação, origem Manual
    and c.criado_em >= timestamp '2026-09-17 00:00'
)
select status, count(*) from coorte group by 1 order by 1;
-- 1=na fila 2=enviada 3=entregue 4=lida 5=falha 6=sem celular
-- 7=aguardando verificado 8=aguardando identificação 9=número inválido

-- 2. IDENTIFICAÇÃO — quem está no meio do desafio agora
select e.etapa, count(*) from smsmarica.verificacao_cadastral_estado e
where e.criado_em >= timestamp '2026-09-18 00:00' group by 1 order by 1;
-- 1=CPF 2=nascimento 3=nome 4=nova tentativa 5=esgotado 6=vínculo

-- 3. RESPOSTA ao agendamento (a coorte pela agenda)
select s.status_confirmacao, s.confirmado_canal, count(*)
from smsmarica.solicitacao s join smsmarica.unidade u on u.id = s.unidade_executante_id
where u.nome like 'CDT%'
  and s.data_agendada >= timestamp '2026-09-21' and s.data_agendada < timestamp '2026-09-28'
group by 1, 2 order by 1, 2;   -- 1=pendente 2=confirmada 3=cancelada

-- 4. MOTIVOS de quem não vai (ler a olho, é o que a contagem não diz)
select s.motivo_cancelamento_paciente, s.confirmacao_cancelada_em
from smsmarica.solicitacao s join smsmarica.unidade u on u.id = s.unidade_executante_id
where u.nome like 'CDT%' and s.status_confirmacao = 3
  and s.confirmacao_cancelada_em >= timestamp '2026-09-18' order by 2;

-- 5. CARGA — conversas abertas por dia (comparar com a semana anterior)
select date_trunc('day', primeiro_contato_em)::date dia, count(*)
from smsmarica.conversa where primeiro_contato_em > now() - interval '14 days'
group by 1 order by 1;

-- 6. NÚMEROS INVÁLIDOS surgidos no período
select date_trunc('day', criado_em)::date dia, count(*)
from smsmarica.pendencia_cadastro where tipo = 1 and criado_em > now() - interval '14 days'
group by 1 order by 1;

-- 7. BLOQUEIOS da guarda de LGPD (o que deixou de sair, e por quê)
select date_trunc('day', ocorrido_em)::date dia, count(*)
from smsmarica.whatsapp_mensagem
where erro_meta like 'BLOQUEADO%' and ocorrido_em > now() - interval '14 days'
group by 1 order by 1;
```

## Cronograma de leitura

| Quando | O que olhar | Decisão possível |
|---|---|---|
| **D+0** (dia do disparo, 8h–9h) | escoamento da fila, falhas, sem celular | parar o resto se a falha passar de 10% |
| **D+0 tarde** | desafios em andamento, conversas abertas, o robô | pausar o robô se ele estiver errando com a coorte |
| **D+1** (sexta) | identificações concluídas, primeiras confirmações | ajustar texto/fluxo antes da segunda |
| **D+3** (segunda, 21/09) | confirmados × cancelados do dia, comparecimento | comparar com a semana anterior |
| **D+7** | quadro fechado da semana 21–27 | decidir se abre para outras unidades |

## Critérios

**Sinal de que funciona:** entrega acima de 85% dos que têm celular; mais de um terço da coorte
concluindo o desafio; cancelamentos com antecedência aparecendo (vaga que dá para remanejar);
carga de atendimento absorvível pela equipe.

**Sinal de parar:** falha de entrega acima de 10%; enxurrada de "não sou essa pessoa" (cadastro
pior do que se imagina); fila da Central estourando; robô dando resposta errada sobre agendamento.

**Como parar:** a chave da unidade já está desligada — nada novo entra sozinho. Para interromper o
que ainda não saiu, basta desligar o envio de confirmação na configuração (menu Confirmações →
*Regras e parâmetros*), que a fila para de escoar sem perder nada.

## Em observação (não tratado de propósito)

### Remarcação pela regulação depois do aviso

**A preocupação (Bernardo, 20/09/2026):** a regulação **altera agendamento**, principalmente os que
estão longe na agenda. Quando isso acontece depois de o paciente já ter sido avisado — e pior,
depois de ele ter **confirmado** —, o que ele tem no WhatsApp é uma data que não vale mais.

**Por que não se trata agora:** ainda não se sabe o tamanho do problema. Pode ser raro (e aí um
aviso manual pela tela de Alterações de Agenda resolve), ou frequente o bastante para exigir
reenvio automático em toda remarcação. Medir primeiro, decidir depois.

**O que já existe:** a varredura detecta a mudança e registra em `sisreg_alteracao_agenda`; a tela
*Alterações de Agenda* tem o botão **Comunicar**, que refaz a mensagem com a data nova e revoga os
links antigos. É manual e depende de alguém olhar a fila.

**O que ainda não existe:** nada avisa que aquele paciente **já havia confirmado** a data velha. A
confirmação continua marcada como válida para um horário que mudou — é o caso que mais incomoda.

**Como acompanhar (rodar junto com a leitura semanal):**

```sql
-- 1. Remarcações que atingiram gente já avisada (e quantas já tinham confirmado)
select date_trunc('day', a.detectada_em)::date dia,
       count(*) remarcacoes,
       count(*) filter (where c.id is not null) ja_avisados,
       count(*) filter (where s.status_confirmacao = 2) ja_confirmados,
       count(*) filter (where a.comunicada_em is not null) recomunicados
from smsmarica.sisreg_alteracao_agenda a
join smsmarica.solicitacao s on s.id = a.solicitacao_id
left join smsmarica.comunicacao_paciente c
       on c.solicitacao_id = s.id and c.finalidade = 1 and c.enviado_em is not null
where a.detectada_em > now() - interval '30 days'
group by 1 order by 1 desc;

-- 2. O estoque do risco AGORA: confirmados cuja data mudou depois da confirmação
select count(*)
from smsmarica.sisreg_alteracao_agenda a
join smsmarica.solicitacao s on s.id = a.solicitacao_id
where s.status_confirmacao = 2 and s.confirmado_em < a.detectada_em
  and s.data_agendada > now();
```

**Gatilho de decisão:** se aparecer remarcação de gente já confirmada com alguma regularidade
(digamos, mais de 5 por semana), vira trabalho: reenvio automático na remarcação + derrubar a
confirmação antiga, para a pessoa responder de novo sobre a data certa.

## Registro do que foi feito

| Quando | O quê |
|---|---|
| 17/09 21:04 | chave de aviso do CDT **ligada** pelo Bernardo |
| 17/09 ~21:40 | chave **desligada** (coorte fechada); varredura mantida ligada |
| 17/09 | publicado o disparo em lote com prévia e modo forçado (commit `dc985b4`) |
| 18/09 00:45 e 17:29 | **lote disparado** pelo Bernardo: 1.824 mensagens (CDT 1.024 + Centro Materno Infantil 800), agendas de 21/09 a 30/09 |
| 20/09 | primeira mensagem nova (`confirmacao_exame`/`confirmacao_consulta`), lembrete de 2 dias LIGADO e **as 42 unidades** com aviso ligado |

### Resultado do lote (leitura de 20/09, D+2)

| | CDT | Centro Materno Infantil |
|---|---|---|
| Enviadas | 1.024 | 800 |
| Entregues/lidas | 349 | 297 |
| Aguardando identificação | 372 | 285 |
| Falha de entrega (todas 131026) | 142 | 129 |
| Sem celular no cadastro | 149 | 80 |
| Número inválido | 12 | 9 |
| **Confirmaram** | **264** | **223** |
| Avisaram que não vão | 9 | 3 |

**487 confirmações (27%)**, quase todas pelo link (474 link · 8 app · 4 botões). Só 12 disseram que
não vão. **657 pararam no pedido do CPF** — o atrito que a mensagem nova veio resolver, e a
primeira coisa a reavaliar na próxima leva.

**Alcance do canal:** 520 pessoas (29%) não têm como ser avisadas hoje — 270 com número que não é
WhatsApp (131026), 229 sem celular no cadastro e 21 com número marcado como inválido. Nenhuma falha
por teto da Meta: o dia teve hora com 1.247 mensagens sem recusa por volume.
