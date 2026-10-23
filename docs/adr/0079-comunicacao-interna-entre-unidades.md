# ADR-0079 — Comunicação interna: chat entre unidades e entre pessoas, dentro do painel

**Status:** aceito · **Data:** 2026-10-23 · **Implementação:** branch `feat/chat-interno`
**Relacionado:** [ADR-0047](./0047-posse-de-conversa-filas.md) (Central de Atendimento — o chat com o
cidadão, que **não** muda) · [ADR-0043](./0043-instancia-por-municipio.md) (uma instância por município:
presença em memória basta) · [ADR-0064](./0064-extensao-chrome-distribuida-pela-plataforma.md) (extensão do
Chrome — a fase 2 do "trazer a janela para frente") · [ADR-0066](./0066-acervo-do-paciente-e-midia-do-whatsapp.md)
(anexos no armazenamento de objeto)

## Contexto

As unidades da rede (UPAs, hospital, policlínicas, regulação, transporte) conversam hoje por WhatsApp
pessoal e telefone. O que se combina ali — vaga, paciente a caminho, exame liberado — some do registro
da Secretaria, mistura número pessoal com assunto de trabalho e frequentemente carrega dado de paciente
fora de qualquer controle.

Pedido do Bernardo (23/10/2026): um chat **dentro do painel** para as unidades falarem entre si, "tipo um
WhatsApp" — balão próprio (laranja, acima do balão da Central de Atendimento), escolher a unidade de
destino e mandar para **todos** dela ou para **uma pessoa**, ver quem está com o painel aberto, a janela
"pula" quando chega mensagem (ou notificação do Windows), tudo registrado e buscável, anexos, áudio,
emojis (populares e corporativos) e som com opção de desligar.

## Decisão

### 1. Módulo próprio, separado da Central de Atendimento

`ModuloPermissao.ComunicacaoInterna = 86`. **Consulta** = ver e ler; **Inclusão** = enviar. A Central de
Atendimento (módulo `Conversas`, chat com o cidadão pelo WhatsApp) não é tocada: outro hub, outras tabelas,
outro balão. Misturar os dois faria a regra de posse/fila do ADR-0047 vazar para uma conversa entre colegas.

A migration concede o 86 (Consulta + Inclusão) a **todo perfil existente** — o pedido é que as unidades
conversem, e um módulo que nasce desligado em todo lugar não é usado. O recorte (tirar de um perfil) é
feito na tela de Perfis. Não é seed institucional: vale igual em qualquer instância.

### 2. Dois tipos de conversa, uma sessão por par

| Tipo | Quem participa | Chave |
|---|---|---|
| **Unidade ↔ unidade** ("Todos da unidade") | todos que têm qualquer das duas como **unidade padrão** (`usuario_unidade.principal`) — ver a nota do ADR-0081 abaixo | par **não ordenado** de unidades — A→B e B→A são a mesma sessão |
| **Direta** (pessoa ↔ pessoa) | as duas pessoas | par não ordenado de usuários |

- **A sessão é única por par**, como no WhatsApp: escrever de novo para a mesma unidade cai no mesmo
  histórico, e a busca encontra tudo num lugar só.
- **Unidade de origem** = a unidade em que a pessoa está (cabeçalho `X-Unidade-Id`), ou a principal; quem
  tem mais de uma escolhe ("falando como"). Sem vínculo com unidade, só conversa direta.

> **Mudou em 23/10/2026 — [ADR-0081](./0081-unidade-padrao-obrigatoria.md).** No chat cada pessoa é de
> uma unidade só: a **unidade padrão** (obrigatória). Participação, lista de pessoas, grupo do hub e
> unidade de origem vêm dela; os demais vínculos (escopo de dados) não contam aqui. O "falando como"
> saiu. Motivo: o técnico de regulação, vinculado às 56 unidades para enxergar os pedidos, aparecia na
> equipe de todas elas.
- **Origem = destino** é permitido: vira o canal da própria equipe.
- **Participação é derivada na hora**, não copiada: quem entra na equipe vê o histórico da unidade (é a
  continuidade do plantão); quem sai deixa de ver. `Usuario.AcessoGlobal` **não** dá acesso a conversa de
  unidade — não existe "ver tudo" nesta fase.

### 3. Nada se apaga

Mensagem não é editada nem excluída (fase 1). "Fica tudo registrado" é requisito; com dado de paciente
circulando, a trilha vale mais que a conveniência de apagar.

### 4. Anexos e áudio no armazenamento de objeto

Arquivo e áudio vão para `IArmazenamentoArquivos` (Spaces), chave
`{prefixo}/comunicacao-interna/{conversaId}/{mensagemId}.{ext}`, teto de **25 MB**. Um arquivo por
mensagem (vários selecionados = várias mensagens, como no WhatsApp). O áudio é gravado no navegador
(`MediaRecorder`, webm/opus) e é um anexo com `duracao_segundos`. O download passa **sempre** pelo backend,
que confere se quem pede participa da conversa.

### 5. Tempo real e presença

- Hub próprio `/hubs/comunicacao-interna`, conectado em qualquer tela do painel. Grupos
  `ci:usuario:{id}` e `ci:unidade:{id}`; a mensagem vai para os grupos das duas unidades (ou dos dois
  usuários). O hub só entra nos grupos se a pessoa tem o módulo 86 — o gate do front é cosmético.
- **Presença em memória** (singleton): online = ao menos uma aba do painel conectada ao hub. É o "está com
  o navegador aberto" do pedido. Uma instância por município (ADR-0043) torna isso suficiente; se um dia a
  API rodar em mais de uma instância, a presença precisa de backplane — **esta é a premissa a revisitar**.
- "Digitando…" e confirmação de leitura (✓✓ na conversa direta) saem do mesmo hub. A leitura vive em
  `conversa_interna_leitura` (`lida_ate` por pessoa), que também guarda o "silenciar esta conversa".

### 6. Chamar a atenção sem roubar o foco

O navegador **não deixa** uma página trazer a própria janela para frente nem abrir janela sem clique. O que
a fase 1 faz:

1. **A janela pula dentro do painel**: o chat abre sozinho, encaixado no canto, já na conversa que chegou
   (sem tirar o cursor de onde a pessoa está digitando). Desligável ("abrir sozinho").
2. **Notificação do Windows** quando a aba não está visível; o clique abre a conversa.
3. **Som próprio** (diferente do bip da Central), liga/desliga persistido no usuário.
4. Título da aba e balão laranja com contador.

A fase 2 — trazer a janela do Chrome para frente de verdade (`chrome.windows.update` com `focused` /
`drawAttention`) — é da **extensão** (ADR-0064) e só sai publicada com OK.

### 7. Busca

`f_unaccent(texto) ILIKE` com índice trigram (como a Central, ver `ConversasBuscaTrgm`), sempre restrita às
conversas que a pessoa enxerga. Também acha pelo nome do arquivo anexado.

### 8. Emojis e selos

Seletor próprio, sem biblioteca: os emojis mais usados por categoria + uma categoria "Trabalho e saúde". E
**selos** — figurinhas corporativas desenhadas pela plataforma ("Recebido", "Em análise", "Urgente",
"Resolvido", "Me liga", "Paciente a caminho"…), que viajam no texto como código `:selo:<nome>:` e o front
desenha. Viajar como texto mantém o selo buscável e não exige tabela.

## Consequências

- Três tabelas novas em `smsmarica`: `conversa_interna`, `mensagem_interna`, `conversa_interna_leitura`.
- Conversa entre unidades pode conter dado de paciente: o acesso é só de participante e o download só pelo
  backend. Auditoria de leitura por terceiros não existe porque não há leitura por terceiros.
- Presença some num restart da API (todo mundo aparece offline até reconectar, segundos depois).
- Fase 2, fora deste ADR: extensão trazendo a janela para frente; responder citando mensagem; encaminhar;
  supervisão/auditoria das conversas.

## Adendo — 23/10/2026: áudio que não tocava e transcrição

**O que aconteceu.** No primeiro uso em produção, um áudio de 4 s gravado pelo painel não tocava. O
arquivo guardado chegou **sem o primeiro byte**: o WebM começava em `45 DF A3` em vez de `1A 45 DF A3`, e
nenhum navegador abre isso. O resto do arquivo estava íntegro: com o `0x1A` recolocado, ele toca. A origem
não apareceu. O mesmo fluxo na bancada local, do botão de gravar ao download, chega byte a byte; os prints
de ticket em produção também chegam inteiros; e nada na API "espia" o corpo da requisição.

**Decisões.**
- **Conserto na entrada e na leitura.** WebM de áudio que chega começando em `45 DF A3` ganha o `0x1A` de
  volta antes de ir para o armazenamento, e o download faz o mesmo com o que já estava gravado assim.
  Nenhum formato válido começa com esses três bytes, então o conserto não altera arquivo certo.
- **Diagnóstico.** O navegador manda `tamanhoBytes` junto do arquivo. Se não bater com o que o servidor
  recebeu, fica no log (`Comunicação interna: anexo chegou com…`) com os primeiros bytes. É por esse aviso
  que se acha a origem na próxima vez.
- **Player próprio.** O WebM do `MediaRecorder` não traz a duração no cabeçalho, e o player nativo mostrava
  `0:00` com a barra parada, dando a impressão de que não tocava. O painel passou a ter play/pausa,
  barra com a duração que o servidor guarda (`duracaoSegundos`), velocidade 1×/1,5×/2× e aviso + Baixar
  quando o navegador recusa.
- **Transcrição sob demanda.** O botão "Transcrever" usa a mesma fala-para-texto dos áudios do WhatsApp
  (ElevenLabs, `IElevenLabsTranscricaoService`). O texto fica **guardado na mensagem**
  (`mensagem_interna.transcricao`, com `transcrita_em`/`transcrita_por`), aparece para todos da conversa
  pelo hub (`transcricaoInterna`) e entra na busca (GIN trigram próprio). Quem pedir depois recebe o
  texto guardado, sem novo custo. Só quem participa da conversa pode pedir.
