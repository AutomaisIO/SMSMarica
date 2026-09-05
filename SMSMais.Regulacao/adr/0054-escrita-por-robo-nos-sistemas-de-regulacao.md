# ADR-0054 — O SMSMais inclui e altera solicitações no SER, no SERNIT e no SISREG por raspagem autenticada com a credencial de um humano logado, confirmando cada escrita por releitura

- **Status:** rascunho (promover no incremento 5; revisar no 7)
- **Data:** 04/09/2026
- **Contexto:** [ADR-0042](../../docs/adr/0042-ser-segunda-fonte-de-regulacao.md) (SER sem API; espelho fiel), ADR-0040 (SISREG por raspagem; orçamento do operador), ADR-0053 (cofre), `docs/ser.md` §7 (trava de somente-leitura) e §9 (follow-up).
- **Documento de apoio:** planos 11, 12, 13.

## Contexto

Nenhum dos três sistemas tem API de escrita que possamos usar. O SER e o SERNIT são JSF/RichFaces; o SISREG é CGI. Já escrevemos follow-up e telefone no SER/SERNIT em produção, assinados pelo operador, e a lição de 10/08 ficou: **"Registrado!" não é prova** — só a releitura do histórico confirma.

Agora precisamos criar solicitações (com anexos) e, no SISREG, incluir com a credencial do solicitante (D-8) ou de uma unidade (NAR, D-9). O SISREG tem sessão única por operador e CAPTCHA por volume; o SER aceita várias sessões.

## Decisão

### 1. Credencial de humano logado, sempre

A escrita usa a sessão do usuário que está clicando (cofre, ADR-0053). A credencial institucional de sincronismo **nunca** escreve: toda ação do município sairia no nome de uma pessoa só na trilha do Estado.

### 2. Escrita confirmada por releitura, ou não aconteceu

Gravar → capturar número → reler a solicitação/histórico → só então gravar `numero_externo` e mudar o estado. Anexo → conferir na lista de anexos. Sem confirmação, `FalhaEnvio` com o motivo e nada gravado como sucesso.

### 3. Idempotência sem chave natural

Antes do número existir não há chave. Trava de estado (`EnviandoAoSistema`) impede duplo clique; retentativa depois de falha exige conferência no sistema (busca por paciente + recurso + data) antes de novo Gravar.

### 4. Escrita nominalmente liberada, o resto continua bloqueado

`SerWebSessao` mantém a trava de somente-leitura em duas camadas; o Gravar e o upload entram por liberação nominal. No SISREG, a sessão institucional continua sem nenhum método de escrita; a escrita vive numa classe separada criada só pela fábrica por operador, com as URLs liberadas por nome e teste que prova que a institucional não as alcança.

### 5. SISREG: sessão, orçamento e CAPTCHA por login

Cada login tem sua sessão, seu gate e seu orçamento. O aviso "vamos entrar como LOGIN; sua sessão no navegador cairá" é obrigatório. Não se reloga em ping-pong. Credencial de unidade faz logout ao fim. CAPTCHA bloqueia a credencial por 24h e avisa o usuário; se o spike b mostrar CAPTCHA por IP, vira pausa global.

### 6. Laboratório antes de código

Gravar/anexo no SER e a inclusão no SISREG só viram código depois dos spikes a e b, executados uma vez, com OK explícito, tudo capturado.

## Consequências

- Interno: a solicitação nasce no SISREG com operador e unidade reais (D-8); a fila guarda "já no SISREG, aguardando OK". O que o OK faz no SISREG depende do spike.
- Externo: SER/SERNIT registram o agente e "gestor SMS Maricá"; a origem real só existe no SMSMais (ADR-0052).
- Ids voláteis (`j_id…`) são localizados por rótulo/estrutura; aviso pendente na home do SER precisa de mensagem própria (incidente 28/08).
- Custo de orçamento do SISREG por inclusão é medido no spike e projetado por unidade antes de liberar o Interno em massa.

## Alternativas consideradas

- **Esperar API oficial.** A do SISREG está instável e sem token há meses; SER/SERNIT não têm. Rejeitado.
- **Operador humano copia do SMSMais para o sistema** (envio assistido permanente). É o incremento 3 e continua disponível como alternativa; como regime permanente, mantém o retrabalho que o módulo veio eliminar.
- **Escrever com a credencial institucional.** Rejeitado pelo motivo do item 1.

## Verificação

Envio de teste ao SER com anexo: número capturado, anexo na lista, histórico relido, evento local; duplo clique não duplica; sessão institucional chamando URL de escrita lança exceção; CAPTCHA simulado bloqueia a credencial e não afeta as demais.
