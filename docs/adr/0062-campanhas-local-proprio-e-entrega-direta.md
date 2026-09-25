# ADR-0062 — Campanhas: local próprio sobre a unidade do SISREG e entrega direta com link que só confirma

**Status:** aceito · **Data:** 2026-09-25
**Relacionado:** [ADR-0057](./0057-destinatario-correto-e-contato-negado.md) (destinatário correto —
esta decisão abre uma exceção controlada a ele) · [ADR-0059](./0059-atendimento-humano-de-confirmacao.md)
(Mensageria) · [ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (SISREG só-leitura —
mantido) · [ADR-0043](./0043-instancia-por-municipio.md) (nada institucional em código)

## Contexto

A Secretaria abriu uma campanha de um mês numa **unidade móvel** ("Carreta da Mulher": mamografia e
ultrassons ginecológicos). A regulação agenda no SISREG com a **Secretaria Municipal de Saúde**
(CNES 6886973) como unidade executante, porque a carreta não existe como unidade no SISREG. Só que a
Secretaria não recebe paciente, e a carreta fica em outro endereço. Em 26/09/2026 eram 160 agendamentos
num único dia.

Três coisas do fluxo normal não servem aqui:

1. **O lugar.** Toda comunicação ao paciente (mensagem, card do app, tela de presença confirmada,
   resposta do robô) usa o nome e o endereço da unidade executante. A guia impressa também diz
   Secretaria. O paciente iria para o lugar errado.
2. **A conferência.** Número não verificado recebe primeiro a mensagem curta
   (`confirmacao_exame`) e só vê os dados depois de informar os 4 dígitos do CPF e o nascimento. Em
   20/09, 657 de 1.824 pacientes pararam nesse pedido. Numa campanha com prazo, a mensagem **precisa
   chegar** com o lugar certo.
3. **O disparo.** O aviso automático depende das chaves de unidade e de procedimento, e a campanha
   precisa de envio pelo botão, acompanhamento do alcance e reenvio a quem não respondeu.

## Decisão

### 1. Campanha é uma entidade própria (`smsmarica.campanha`)

Nome, **unidade executante** (FK `unidade`), **início e fim** (UTC), **nome do local** e **endereço**
(uma linha, como vai na mensagem), e três chaves: `exigir_conferencia_cadastral`, `envio_automatico`
e `ativa`. Tem auditoria e exclusão lógica. **Duas campanhas ativas da mesma unidade não podem se
sobrepor.**
Nada institucional em código ou migration: o nome e o endereço da carreta são dados cadastrados na tela.

**Encaixe:** um agendamento é da campanha quando `unidade_executante_id` é a unidade da campanha e
`data_agendada` cai no período. A consulta é única (`CampanhaResolver.VigenteAsync`) e é usada em
todos os pontos que mostram o lugar.

### 2. O local da campanha substitui o da unidade em tudo o que o paciente vê

Vale para a mensagem, o card e o detalhe do exame no app (o telefone da unidade é omitido, porque não é
o de quem atende), a tela de presença confirmada e a lista do robô ("meus agendamentos"). O painel
interno continua mostrando a unidade do SISREG, que é o dado regulado.

### 3. Modelo próprio na Meta: `agendamento_campanha`

Os modelos aprovados não têm variável para local nem endereço, e o `confirmacao_regulacao` afirma que
eles "constam na sua guia", o que aqui é falso. O modelo novo tem 5 variáveis:

| Variável | Conteúdo |
|---|---|
| `{{1}}` | tratamento |
| `{{2}}` | procedimento |
| `{{3}}` | data e hora |
| `{{4}}` | local |
| `{{5}}` | endereço |

O texto é neutro em gênero ("Você tem um agendamento") e avisa que o local vale "mesmo que a sua guia
indique outro endereço". Os botões são:

- URL `/entrar/{token}`;
- "Não poderei ir" (payload `confirma:`, com o fluxo de cancelamento de sempre);
- "Não sou essa pessoa" (volta como texto e é casado pelo contexto; leva ao "você conhece…?" do ADR-0057).

O lembrete para quem não respondeu repete esse mesmo modelo quando a entrega é direta.

### 4. Entrega direta é uma exceção ao ADR-0057, compensada no link

Com `exigir_conferencia_cadastral = false`, a mensagem sai **já com os dados**, sem desafio e sem
exigir telefone verificado. O risco é o mesmo que o ADR-0057 combate: o número do cadastro pode não
ser do paciente. A compensação é o **link que só confirma**:

- o link é gerado com a janela de sessão já fechada (`sessao_ate_em = criado_em`);
- o clique registra a presença e mostra "presença confirmada", **sem abrir o app**;
- a sessão só abre quando o número é o **telefone verificado** do próprio paciente.

Quem recebe por engano descobre a data e o local de um exame, que é o que a campanha aceita expor.
Não descobre resultado, prontuário nem sessão.

O que **não** muda:

- a guarda central de número negado (`WhatsAppCliente` com origem `Automatico`) continua barrando;
- pendência de número errado retém a mensagem;
- "Não sou essa pessoa" segue marcando o número.

Com a chave **ligada**, vale o fluxo normal, e os dados só saem (no modelo da campanha) depois da
identificação.

### 5. Envio

- **Automático:** com `envio_automatico`, a importação do SISREG enfileira todo agendamento futuro
  da campanha, **ignorando as chaves de unidade e procedimento**. A campanha é a decisão explícita
  que essas chaves representam.
- **Botão Enviar:** enfileira o que ainda não recebeu nada e solta o que está empilhado esperando a
  janela. Ignora a janela de horário, porque foi uma pessoa que clicou.
- **Reenviar a quem não respondeu:** revoga os links anteriores, rearma a comunicação e deixa trilha
  em `contato_registro`. Ficam de fora: sem celular, número negado e atendimento humano em curso.
- O ritmo continua sendo o do worker (vazão das Regras).

### 6. Tela

Aba **Campanhas** na Mensageria (módulo 38; criar = Inclusão, editar/enviar = Edição,
encerrar = Exclusão), com:

- a lista e o formulário;
- os contadores do funil: agendados, sem mensagem, na fila, enviadas, entregues, lidas,
  confirmaram, não vão, sem resposta, falhas, sem celular;
- a tabela filtrável do alcance, que se atualiza a cada 15 s.

## Consequências

- A campanha só alcança o que já foi **importado**. A unidade da campanha precisa estar na varredura
  do SISREG; sem isso, a lista fica vazia (a tela diz isso).
- Enquanto `agendamento_campanha` não for aprovado na Meta, o envio falha com o erro do modelo e fica
  na fila de falhas, visível no alcance. Não há fallback para `confirmacao_regulacao`, porque ele
  mandaria a paciente à Secretaria.
- Se a Meta pedir mudança no texto, a **ordem das variáveis** é contrato com o código
  (`ComunicacaoPacienteService.MontarEnvio`, `ConteudoLegivel`).
- A exceção ao ADR-0057 é **por campanha e visível na tela** (selo "Entrega direta"), e não uma
  configuração global.
