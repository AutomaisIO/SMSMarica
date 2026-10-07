# ADR-0069 — Envio automático da solicitação ao SER (e catálogo identificado pelo nome)

**Status:** aceito · **Data:** 2026-10-07 · implementado em 07/10/2026 (não deployado; para teste da equipe)
**Relacionado:** [ADR-0052](./0052-fila-pre-regulacao-e-agente-regulador.md) (fila de pré-regulação; supersede
parcial da D-11) · [ADR-0055](./0055-catalogo-canonico-e-embeddings.md) (catálogo canônico) ·
[ADR-0065](./0065-medicos-do-ser-espelho-separado.md) (médico guardado pelo NOME) · `docs/ser-criar-solicitacao.md`
§2.4–2.5 · `docs/ser.md` §9–10 (escrita assinada pelo operador)

## Contexto

Até aqui, aceitar uma solicitação com destino SER era trabalho manual: o regulador abria o site do SER,
redigitava tudo (recurso, paciente, médico, risco, unidade, CID, campos do recurso), anexava os
documentos e trazia o número de volta pelo "Registrar envio" (D-11 do ADR-0052: nada nosso escrevia no
sistema de regulação). O Bernardo pediu que o envio ao SER funcione **como o envio ao SISCAN na
anamnese**: a plataforma preenche tudo, anexa os documentos, grava e trata os erros que o SER devolver.

O ensaio com uma solicitação real (PR-20, 07/10/2026) mostrou que a tela inteira amarra — e mostrou
também que o `value` do combo de Recurso do SER é **posição**: o 1130 do nosso espelho (Buco-Maxilo) era
Odontopediatria no SER naquele dia, e as 422 ligações do catálogo apontavam números que já eram de
outro recurso.

## Decisão

1. **Identidade dos catálogos externos é o NOME.** As linhas de `ser_catalogo_recurso`,
   `sernit_catalogo_recurso` e `esussg_catalogo_recurso` são identificadas por `rotulo_chave` (rótulo
   normalizado, único por tipo e ramo). O `valor` vira "o número de hoje". A chave da origem no catálogo
   canônico passa a ser **a nossa numeração** (`{tipo}|{ramo}|{id da linha do espelho}`). Quem conversa
   com o sistema ao vivo acha o recurso pelo nome na hora. Renomeação na SES = recurso novo para nós:
   falha visível, nunca o pedido no lugar errado.
2. **Cópia diária** dos catálogos do SER e do SERNIT (agendador a partir das 05h; a primeira solicitação
   aberta no dia também confere). Frescor, não correção: nada depende do número copiado.
3. **Envio automático ao SER**, em duas etapas como o SISCAN:
   - **prévia** (`POST /regulacao/solicitacoes/{id}/ser/preparar`): percorre a tela de criação do SER
     inteira e para antes de anexar — devolve campo a campo o que iria, os anexos e os pedidos parecidos
     que o SER já tem para o paciente (a "crítica");
   - **envio** (`POST …/ser/enviar`): preenche, anexa, grava, **relê do SER** para provar e registra o
     número. Estados: `EmAnalise → EnviandoAoSistema → EnviadaAoSistema | FalhaEnvio`.
4. **Quem assina é o regulador**, com o usuário e a senha DELE no SER (sessão em memória amarrada ao
   login no SMSMais, a mesma do FollowUP — `ISerSessaoOperadorStore`). A credencial de sincronismo nunca
   escreve.
5. **Erro diz se o Gravar chegou ao SER.** Recusa antes do Gravar ou mensagem de validação no Gravar =
   "nada foi gravado". Resposta sem número depois do Gravar = o pedido PODE existir lá: a solicitação
   fica em `FalhaEnvio` com o aviso de conferir, e o agente pode registrar o número que achou
   (`FalhaEnvio → EnviadaAoSistema`, nova transição do agente).
6. **Anexos pela regra que a tela do SER declara:** até 2 arquivos de 5 MB; com mais, a plataforma junta
   tudo num PDF; acima de 5 MB é recusa antes de tocar o SER.

## Consequências

- A D-11 do ADR-0052 deixa de valer **para o SER**: SISREG e SERNIT continuam só pelo "registrar envio".
- O upload de anexo (`rich:fileUpload`) foi implementado pelo protocolo lido do `ui.pack.js` do SER, mas
  **nunca tinha sido exercitado** antes do teste da equipe. A trava é ver o arquivo listado em
  `form0:anexoList` antes do Gravar: se não aparecer, o envio para sem gravar.
- Entrar com a senha do SER na plataforma derruba a aba do SER do mesmo usuário no navegador (o SER só
  aceita uma sessão por usuário).
- Campo dinâmico de múltipla escolha com mais de uma opção ainda não é enviado automaticamente (o envio
  para com mensagem).
