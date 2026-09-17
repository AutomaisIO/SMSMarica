# ADR-0057 — Destinatário correto: prova de contato, vínculo declarado e contato negado

- **Status:** aceito
- **Data:** 2026-09-17
- **Contexto:** LGPD. Decisões tomadas pelo Bernardo em 17/09/2026, depois do mapeamento de todos
  os envios automáticos e de todos os caminhos em que um número de WhatsApp vira acesso a dado.

## Problema

O sistema fala com o cidadão por um canal que **não é do cidadão, é do aparelho**: um número de
WhatsApp muda de dono, é da filha, do vizinho, ou foi digitado errado na recepção. Três coisas
dependiam disso sem régua única:

1. **Conteúdo** (nome, procedimento, data, resultado) saía por seis remetentes diferentes, e só um
   deles — a fila de comunicações — checava se o número tinha sido negado.
2. **Acesso**: o link de confirmação abria sessão completa no app, por 30 dias, sem prova nenhuma,
   e de dentro dela dava para trocar o telefone verificado (tomar a conta).
3. **Quem recebe pelo outro** (mãe, pai, responsável) não tinha lugar no modelo: o cadastro só
   aceitava um CPF por número, e o conflito era engolido.

## Decisão

### 1. Prova antes do conteúdo

Nada de agendamento sai para número **não verificado** sem passar pelo desafio cadastral (4 dígitos
do CPF → mês/ano de nascimento → nome). Resultado de exame e laudo continuam exigindo contato
verificado (ou dispensa registrada). Isso já valia e fica mantido.

### 2. Vínculo declarado, e um número pode servir a vários pacientes

Ao final da verificação, a pessoa declara **a que título** recebe: `Proprio`,
`MaeOuPaiOuResponsavel` ou `OutroParenteOuCuidador` (`VinculoContatoVerificado`). O vínculo é
gravado no telecom do Patient (`urn:smsmarica:contato-vinculo`) junto do marcador de confirmado.

Consequência: o mesmo número pode ser o contato verificado de **vários pacientes** — é o celular da
mãe que atende pelos três filhos. A trava de "número já é de outra pessoa" passa a valer **só quando
os dois lados declaram `Proprio`** (o caso das duas Márcias, que é troca de identidade, continua
barrado).

### 3. Contato negado bloqueia o envio automático — por par número × paciente

"Não conheço essa pessoa" (botão do desafio, ou a negação do nome) registra pendência
`NumeroErrado` **e** carimba `urn:smsmarica:contato-negado` no cadastro. A partir daí:

- **nenhuma mensagem automática** sobre aquele paciente sai para aquele número: confirmação,
  resultado, laudo, pesquisa de satisfação, TFD, robô. A guarda é central, no
  `WhatsAppCliente` — o único ponto por onde tudo sai (`IContatoNegadoService`);
- o **dono legítimo do número continua recebendo o que é dele** (o bloqueio é do par, não do
  número). Pendência registrada **sem** paciente bloqueia todo automático daquele número;
- a tentativa bloqueada **fica registrada** (mensagem com o motivo) para a recepção tratar depois;
- o **robô** deixa de tratar a conversa como sendo daquele paciente: não revela, não confirma, não
  cancela, não verifica cadastro por ele.

Só três origens não são bloqueadas, e por isto:

| Origem | Bloqueia? | Por quê |
|---|---|---|
| `Automatico` (worker, gatilho, agendador) | **Sim** | É o sistema que decidiu falar. **É o padrão** de quem não declarar origem. |
| `Resposta` (máquina de estados, robô) | Não | O cidadão escreveu agora e espera resposta. O que não pode é revelar dado do paciente negado — barrado antes, na resolução. |
| `Humano` (operador no painel, código pedido pelo próprio cidadão) | Não | Uma pessoa decidiu. A tela avisa e pede ciência; e o código é o que **conserta** um contato negado. |

### 4. Link do WhatsApp: entra em 24h, confirma até o dia

O magic link de confirmação **abre o app só nas primeiras 24h** (`cidadao_login_link.sessao_ate_em`).
Depois disso, até a data do exame, o clique **ainda confirma a presença** e mostra o cartão do
agendamento, mas **não autentica ninguém**. Uso único continua valendo: repassado, não abre.

`revogado_em` passa a ser distinto de `expira_em`: link **revogado** (reenvio, troca de número,
correção de identidade) não confirma nem devolve destino — pode estar com a pessoa errada. Antes os
dois eram a mesma coisa e um link revogado ainda confirmava presença.

**Sessão aberta por link não troca telefone.** O token carrega o canal (`magic-link` | `otp`); com
`magic-link`, alterar contato responde 409 e orienta entrar com o código. Sem isso, quem recebesse o
link por engano apontava o contato verificado para o próprio número — tomada de conta.

`IgnorarVerificacaoTelefone` ("assumo o risco") vale **por envio**: é zerado no reenvio e no rearme.

### 5. Trocar de número só no posto, quando já existe contato verificado

O passo 2 do login do app (nascimento + nº de uma solicitação + telefone à escolha) **continua
valendo só para quem ainda NÃO tem contato verificado** — é o caminho de quem nunca foi verificado
ou nunca teve número no cadastro.

Quem **já tem** um número verificado não troca por ali: os dados pedidos não são segredo (o nº da
solicitação está impresso na guia de papel), e quem estivesse com a guia apontaria o contato
verificado para o próprio celular, passando a receber laudo e login da pessoa. A resposta é
`telefone.troca_no_posto` e a orientação é procurar o posto com documento com foto. Reenviar para o
**mesmo** número verificado continua funcionando (não é troca).

No app, o atalho "Não tenho mais esse número" da tela do código deixou de abrir o formulário e
passou a dizer o caminho.

**A mesma régua vale para todos os carimbos automáticos de "verificado"** (robô
`VerificarCadastroComando`, máquina determinística da verificação cadastral e clique no magic
link): eles só marcam quando o paciente **ainda não tem** número verificado. Havendo um, e sendo
outro número, não se troca nada — a mudança é no posto. Quem muda número continua sendo: a recepção
(código para o número novo, com a pessoa na frente) e o próprio cidadão de dentro do app, em sessão
aberta por **código** (que prova a posse do número atual).

## Consequências

- O padrão passa a ser restritivo: um remetente novo que esqueça de declarar a origem nasce
  bloqueado para contato negado, em vez de nascer liberado.
- A recepção tem trabalho novo: número marcado como inválido só volta a valer trocando o contato no
  cadastro, ou dando a denúncia por improcedente ("Ignorar" a pendência, que agora tira o carimbo).
- O robô perde alcance em número negado — deliberadamente.
- O que **não** foi mudado nesta rodada, por decisão do produto: as portas de identidade do robô
  (4 dígitos do CPF, mais mês/ano em alguns comandos) seguem como estavam.

## Pendências conhecidas (levantadas no mapeamento, ainda abertas)

1. ~~Passo 2 do login troca o número verificado~~ — **fechado em 17/09/2026** (ver decisão 5).
   Fica em aberto: a recepção, ao trocar o número, não avisa o número antigo; e o passo 2 aceita
   solicitação de qualquer época (uma guia velha serve de chave para quem nunca verificou).
2. ~~Robô marca número como verificado com 4 dígitos do CPF~~ — **fechado em 17/09/2026**: o robô
   (e a máquina determinística, e o clique no magic link) só carimba quando o paciente **ainda não
   tem** contato verificado. Havendo um, e sendo outro número, nada é trocado: o robô orienta o
   posto e apenas libera o que estava retido (que sai para o número já verificado).
3. **Passo 1 do login** revela se um CPF tem cadastro e os últimos 4 dígitos de um telefone.
4. **Sandbox** envia magic link real de login para qualquer número digitado (permissão 39).
5. O **desafio cadastral** mostra primeiro nome + procedimento a número ainda não verificado, e o
   nome completo antes da última etapa.
