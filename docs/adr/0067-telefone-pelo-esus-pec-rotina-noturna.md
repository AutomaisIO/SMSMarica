# ADR-0067 — Correção automática de telefone pelo e-SUS PEC (rotina noturna) e reenvio da mensagem

**Status:** aceito · **Data:** 2026-10-04 · fase 1 implementada em 04/10/2026
**Relacionado:** [ADR-0059](./0059-atendimento-humano-de-confirmacao.md) (confirmações, posse, Mensageria) ·
[ADR-0057](./0057-destinatario-correto-e-contato-negado.md) (destinatário correto) ·
[ADR-0047](./0047-posse-de-conversa-filas.md) (conversas) ·
[ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md) (proveniência por fonte) ·
[ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (sistemas externos só leitura) ·
`SistemaRegulacao.Esus = 4` (reservado no [ADR-0063](./0063-esus-sao-goncalo-e-analise-de-regras-dos-espelhos.md))

## Contexto

Quando a mensagem do agendamento não chega (a Meta recusa porque o número não é WhatsApp, o cadastro não
tem celular, ou quem atende diz "não sou essa pessoa"), o paciente cai na aba **Telefone comprometido** e
fica esperando alguém da recepção conseguir outro número. Hoje ninguém procura esse número em lugar nenhum.

O **e-SUS APS PEC** do município (o e-SUS do governo, `esus.marica.rj.gov.br`) é onde a atenção básica
atualiza o cadastro do cidadão, e é a fonte mais fresca de celular e filiação. Em 02–04/10/2026 o
laboratório `Automais.esus/` (somente leitura) mediu isso de ponta a ponta e, com o OK do Bernardo, a
correção foi feita à mão em produção:

- **4.064** pacientes com contato comprometido, número errado ou mensagem retida → o PEC tinha **outro
  celular válido para 67%** (2.704). Cadastro do PEC atualizado em 2026 para 95% deles.
- **2.677** telefones trocados (o antigo vai para o histórico), **849** mensagens reenviadas: das
  reenviadas, ~400 chegaram na primeira hora.
- O mesmo levantamento completou **mãe 1.784, pai 1.791** e trocou **262 CNS** (só os confirmados no
  CADSUS pelo SER).
- O PEC é sessão única por usuário: logar derruba quem estiver usando a mesma conta. A credencial foi
  cedida por uma servidora; **de madrugada** ela não está no sistema.

## Decisão

Uma rotina noturna no backend repete, todo dia, o que foi feito à mão — só leitura no PEC, escrita só no
nosso hub.

1. **Credencial.** Usuário e senha do PEC cadastrados em Integrações (`integracao_credencial`, cifrados),
   como SISREG/SER/SERNIT. Acesso de leitura de prontuário/cadastro (lotação de coordenação/diretor).
2. **Janela.** Só de madrugada (padrão 02:00–05:00, configurável). Fora da janela a rotina **não loga**.
   Dentro dela o login pode forçar a entrada (`force`), porque a dona da credencial não está usando.
3. **Quem entra.** Pacientes, desde a última rodada, com: contato comprometido aberto (sem celular /
   não é WhatsApp), pendência "número errado" aberta, ou comunicação retida aguardando correção do
   contato. Cada paciente consultado fica registrado e não é reconsultado em menos de N dias.
4. **Consulta no PEC.** Um por vez, intervalo aleatório de 1–2 s, retentativa em 502/504. Por CPF (ou CNS),
   lendo `cidadaos` + `cidadao(id)` (celular, residencial, contato, data de atualização). Só `query`; a
   trava do cliente recusa qualquer mutation e as consultas que regravam o cadastro pelo CADSUS.
5. **Decisão por paciente** (a mesma de 02–04/10):
   - telefone **validado** (OTP) → nunca é alterado;
   - o PEC tem **outro celular válido** → troca: vira o principal (`rank 1`, `contato-origem = esus-pec`,
     `period.start`); o antigo vai para o histórico (`period.end`); auditoria `TrocouTelefoneEsusPec`;
     baixa no contato comprometido;
   - o número do PEC é o confirmado (próprio) de **outra pessoa** → só troca se houver **sobrenome em
     comum** (é o celular da família); senão, fica para a recepção;
   - o PEC traz **o mesmo número que falhou** ou é o número negado → marca **"e-SUS também errado"** e o
     paciente fica na aba Telefone comprometido com esse motivo (ligar);
   - não achado no PEC / sem celular válido → fica na aba, com o motivo.
6. **Reenvio.** No horário de envio do dia seguinte, a mensagem que falhou **por número** volta para a fila,
   pelo mesmo caminho do botão Reenviar (revoga links antigos, zera, re-resolve o telefone atual). Só
   reenvia agendamento **futuro** ainda **sem resposta**. Não reenvia quando o motivo não foi o número
   ("já respondeu por outro canal", "sem data futura", "sem CPF para o link").
7. **Histórico de tentativas.** Cada reenvio grava na trilha de contatos a tentativa anterior (número,
   resultado, motivo) e a nova. **Fase 2:** o card da mensagem mostra essa sequência
   ("1ª para (21) 9…-1234 — não é WhatsApp · 2ª para o celular do e-SUS — entregue/lida/falhou").

## Consequências

- O telefone furado deixa de esperar a recepção: no dia seguinte o paciente recebe pelo número do PEC.
- Toda troca é rastreável: origem no próprio telecom, auditoria e trilha de contatos; o número antigo nunca
  some.
- O PEC continua sem receber nenhuma escrita nossa.
- Se a servidora desistir da cessão, a rotina para (credencial inválida → aviso de erro), e o resto do
  sistema segue igual.
- Custo no PEC: ~2 consultas por paciente, de madrugada, em sequência.

## Fora de escopo (decisões separadas)

- **Remanejamento do SISREG:** a vaga nova chega, mas a antiga não é cancelada no nosso sistema (medido em
  04/10: 54 tireoides de 06/10 e 38 tomografias de crianças). Precisa de ajuste na conciliação, não nesta rotina.
- Completar filiação/CNS automaticamente (feito à mão em 03/10; regra própria: só completa vazio, CNS
  só com confirmação do CADSUS).
- Validação de nome/nascimento divergentes (Hub do Desenvolvedor).

## Plano

1. `Core/Integracoes/EsusPec/` — cliente GraphQL só-leitura (porta do `Automais.esus/pec/client.py`:
   XSRF, `Api-Consumer-Id: ESUS_WEB_CLIENT`, trava de mutation, retentativa, intervalo).
2. Serviço de decisão + `BackgroundService` na janela da madrugada; registro por paciente consultado.
3. Troca no hub pelo caminho normal de gravação do Patient (não por SQL), com auditoria.
4. Reenvio: reaproveitar `RearmarParaNovoEnvio` + revogação de links; agendar para o horário de envio.
5. Tela: credencial em Integrações; contadores da última rodada; motivo "e-SUS também errado" na aba.
6. Manual: artigo de Confirmações / Telefone comprometido (skill `confrontar-manual`).
7. Fase 2: histórico de tentativas no card da mensagem.

## Implementação (fase 1 — 04/10/2026)

- `Core/Integracoes/EsusPec/EsusPecCliente.cs` — cliente só-leitura (documentos GraphQL fixos: 3 mutações
  de sessão + 2 consultas; nada mais sai dali).
- `TrocaTelefoneEsus.cs` — a regra pura (decisão + troca no `Patient`), coberta por
  `TrocaTelefoneEsusTests`.
- `CorrecaoTelefoneEsusService.cs` — fila, consulta, troca pelo `IPacienteFhirClient` (retentativa em
  conflito de versão), auditoria (`ConsultouEsusPec` por paciente, `TrocouTelefoneEsusPec` na troca),
  baixa do contato comprometido, resolução da pendência de número errado (que já solta as mensagens
  retidas) e rearme da mensagem que falhou por número; `CorrecaoTelefoneEsusWorker` acorda a cada 15 min
  e só entra com credencial ativa + janela + fila não vazia. Teto de 500 pacientes por passagem;
  reconsulta após 7 dias; desiste após 5 erros seguidos.
- **Desvios do plano, de propósito:**
  - "e-SUS também errado" não virou motivo novo (seria enum + tela): vai como **observação** na marca
    "Número não é WhatsApp" — sem migration.
  - O rearme acontece na própria madrugada (`próxima tentativa = agora`); quem segura até o horário de
    envio é o enviador, que já respeita a janela de horário.
  - Contadores da última rodada na tela: ainda não (só log). O card ganha a anotação na trilha de
    contatos ("telefone corrigido pelo e-SUS… reenviada"); a sequência de tentativas segue na fase 2.

