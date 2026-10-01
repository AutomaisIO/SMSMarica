# SMSMais · Ponte SISREG — extensão Chrome

Registra no SMSMais as operações que o usuário faz no SISREG, para **antecipar** o que hoje só
chega pela varredura diária. Nesta fase de piloto o objetivo é **capturar tudo** (envio e retorno)
para depois a gente analisar e decidir os endpoints definitivos.

Regra de ouro: a extensão **só observa** o SISREG. Nunca dispara requisição para lá — não gasta o
orçamento anti-robô nem abre sessão. Tudo que ela registra foi o próprio usuário quem fez, com a
senha dele.

## O que ela faz (v0.2.0)

1. **Bloqueia o SISREG (blur) até o SMSMarica estar conectado.** A extensão "acha" a sessão do
   painel `smsmarica.online` já aberta/logada no navegador; se não houver, o modal pede o login
   (o login é feito **no nosso site**, nunca dentro da página do SISREG).
   A sessão fica guardada na extensão (`storage.local`) até vencer (8 h), até o logout no painel
   ou até um 401 da API — sobrevive a recarregar a página, a recarregar/atualizar a extensão e a
   reiniciar o navegador (antes ficava no `storage.session`, que se apaga nesses casos, e o
   operador via "Sem sessão no SMSMarica" toda hora). Ao recarregar/atualizar a extensão, ela
   religa a ponte (`auth-content.js`) nas abas do painel já abertas (permissão `scripting`), para
   um login novo chegar sem F5.
2. **Selo discreto** no canto, com a marca do SMSMarica e um **LED** de status:

   | LED | Significado |
   |---|---|
   | cinza | sem sessão no SMSMarica |
   | verde | conectado |
   | âmbar piscando | enviando capturas |
   | verde com halo | acabou de registrar |
   | vermelho | SMSMarica fora de alcance (as capturas ficam guardadas) |

   O selo **não mostra nome de pessoa** (v0.5.35): quem entrou no painel — ou quem autorizou o
   computador — não é necessariamente quem está operando. A extensão guarda da sessão só o token
   e o vencimento; o nome não chega às páginas, nem ao popup, e sessões guardadas por versões
   antigas perdem o nome na primeira leitura.

3. **Captura** cada operação: o **envio** (URL + campos do formulário, com `etapa` e o evento de
   negócio — cancelamento, confirmação, falta…) e o **retorno** (o HTML da tela que carregou e os
   corpos de AJAX do `sisreg_ajax`).
4. **Envia em lote** (comprimido) para a nossa API, autenticando com o **mesmo login** do painel.

O que o usuário final vê é o selo com o LED. A janela do que é enviado (tráfego) liga e desliga
pelo **clique no selo**, pelo **×** da própria janela ou, no Prime, pelo menu **☰ → Mostrar/Ocultar o
tráfego enviado** — a escolha fica **guardada por site**. Padrão: visível no SISREG (transparência:
só sai comando + nº) e oculta no Prime (lá o modo análise enche a lista a cada leitura).

## Agenda SISREG → Prime (v0.5.0)

A agenda do Prime é **mensal por profissional** e era montada à mão pela recepção — por isso não
bate com o SISREG (medido em 30/09/2026 no CDT). A partir de agora a referência é o SISREG: o
SMSMarica já tem as escalas (`/agenda/*`), e a extensão as transforma nos compromissos do Prime.

**Onde:** no Prime, o selo do canto ganha um **☰** → *Agenda SISREG → Prime*. Abre um painel à
direita (fica aberto entre as telas do Prime até ser fechado).
O cabeçalho do painel mostra a **versão** do código que está rodando naquela página.
**Recarregar a extensão não troca o código das páginas já abertas** — o painel e a página do
Prime seguem na versão antiga até um **F5** (30/09: um teste rodou a 0.5.21 com a 0.5.22
instalada). Quando a extensão é atualizada por baixo (o atualizador troca os arquivos e ela se
recarrega sozinha), o painel velho **não age**: no primeiro clique, **recarrega a página**. O mesmo
quando a **sessão do Prime cai** (a leitura cai em `…/sessaoexpirada` ou `/login.aspx`): a página
recarrega e o próprio Prime leva à entrada. Com a janela de um compromisso aberta, pergunta antes
(*Recarregar agora*), porque o que não foi salvo nela se perde.

**Unidade:** é **sempre** a do cabeçalho do Prime (casada pelo nome com a unidade das escalas do
SISREG). Não se escolhe nem se troca no painel: trocou a unidade no Prime, o painel acompanha e
volta ao começo. Unidade do Prime sem escala no SISREG aparece como tal e para ali.

**Tela 1 — Situação do mês.** Escolha o mês e clique *Comparar SISREG × Prime*. O painel lê quem
tem escala no SISREG na unidade do cabeçalho do Prime (nossa API, ≈30 s) e as agendas daquele mês
no Prime (tela *Consultar Agenda*: pesquisa + abrir cada agenda para ver o profissional de cada
compromisso, ≈2 s por agenda — outubro do CDT, 28 agendas, levou 50 s). Mostra:
- **Sem agenda no Prime** — com vagas e marcados no SISREG; quem já tem paciente marcado vem
  primeiro. Botão **Cadastrar**.
- **Já têm agenda no Prime** — com o título da(s) agenda(s). Botão **Conferir**.
- **Só no Prime** — agenda interna ou profissional sem escala no SISREG no mês (nada a fazer).

A leitura refaz a pesquisa da tela *Consultar Agenda*: se ela estiver aberta em outra aba, clique
Pesquisar de novo lá. O botão **Atualizar** relê o Prime (e **Atualizar tudo**, também o SISREG).

**Fidelidade ao SISREG (regra do Bernardo, 30/09/2026):** o Prime espelha a escala do SISREG
como ela está — horário e vagas vêm do SISREG e **não se editam no painel** (ex.: ECG 08:00–08:20
com 20 vagas de 1 min fica exatamente assim). Se estiver errado, o ajuste é **no SISREG**; o painel
mostra de novo depois da próxima leitura. No cartão só se escolhe o que o SISREG não tem: o tipo do
Prime e os extras (encaixes).

**Tela 2 — Cadastro guiado (um profissional, um compromisso por vez):**
1. Um cartão por compromisso do SISREG, **objetivo**: dias · horário · vagas, o procedimento e um
   selo — **No Prime** (com selos das **datas cadastradas**) ou **Não está no Prime** (com tipo,
   extras e o botão). Dias em que a escala não vale aparecem como selos vermelhos. O tipo vem
   pré-preenchido: procedimento de consulta (CONSULTA… ou OCI…) → **Consulta**; o resto (exame) →
   **Sala de Procedimento**.

   A conferência é **por dia da semana**: o dia está no Prime quando um compromisso do mesmo
   profissional naquele dia contém o horário do SISREG. Se só parte dos dias está, o selo diz
   **Falta seg, ter, qua** e o botão preenche **só esses dias** — os outros duplicariam o que já
   existe. Caso real (Alberto, OUT/2026): o SISREG tem ECG seg–qui 08:00–08:20, e o Prime tem um
   bloco *qui 08:00–12:00* feito à mão; criar seg–qui sobreporia as quintas. Quando o dia está
   coberto por um bloco **maior** que o do SISREG, ou de **outro tipo** (ex.: exame cadastrado como
   "Consulta"), o cartão mostra **no Prime como qui 08:00–12:00 · Consulta** — é a divergência à
   vista; quem corrige é o SISREG ou o Prime, não a extensão. Nos
   dias que faltam, compromisso do Prime que cruza o horário sem conter aparece em vermelho como
   **sobrepõe no Prime**.

   Os selos de data **levam à agenda do Prime** que tem aquele compromisso e o calendário até a
   **semana da data** (pelas setas, com o horário destacado por 2 s) — se a agenda já está na tela,
   só muda a semana; se não, abre a agenda e muda a semana quando ela carregar (até 1 min depois
   do clique). O "no Prime como" só abre a agenda. Nenhum dos dois abre ou cria compromisso. Com a
   janela de um compromisso aberta, não saem da tela (o que não foi salvo se perderia).
   O selo clicado **gira** (e o cursor do painel vira "carregando") do clique até o Prime terminar
   — inclusive depois da troca de página; um segundo clique no meio é ignorado. Se a semana da data
   **já está** no calendário, um aviso pequeno junto do ponteiro diz "15/10 já está na tela" (e o
   horário pisca em azul) e some sozinho.

   **Erro vai para um modal**, no meio da tela, e sai do cartão (ex.: *"O Prime não oferece
   "&lt;profissional&gt;" para o tipo "Sala de Procedimento" nesta unidade. O cadastro do profissional
   no Prime precisa liberar esse tipo."*) — fecha no **Entendi**, no Esc ou clicando fora. As
   **confirmações** também vão ao modal, com os botões: *Outra agenda aberta no Prime (…). Trocar
   perde o que não foi salvo nela* → **Cancelar** / **Trocar para a agenda de …**; e a data da janela
   diferente → **Usar dd/mm mesmo assim**. Só a escolha da **função** (um seletor) fica no cartão.
   Nada de sugerir nomes parecidos.

   **Trabalho em curso nunca entra no meio do conteúdo** (empurrava os cartões e sumia): há uma
   barra fina animada na borda do cabeçalho e, quando há o que dizer (ex.: leitura do SISREG), um
   aviso escuro flutuante no rodapé do painel, por cima.
2. Para cada cartão: *Abrir e preencher no Prime*. A extensão faz o caminho todo:
   - leva a tela até a agenda do profissional no mês — a que já existe no Prime (pelo endereço lido
     na Consultar Agenda) ou, se não existe, a **Nova Agenda** com o período do mês, **Enviar**
     clicado e o título preenchido (o Enviar só monta o calendário; não grava);
   - vai até a semana do primeiro dia válido (pelas setas do calendário), faz o "Novo Evento" no
     horário, abre o compromisso provisório e preenche a janela.

   Quando precisa trocar de página, o painel guarda o passo e **continua sozinho** quando a página
   nova carrega (uma vez só, e só nos 3 minutos seguintes). Se outra agenda estiver aberta, ele
   avisa e oferece trocar — o que não foi salvo nela se perde.
3. Confira a janela e clique **Salvar** nela. O operador não marca nada à mão: quem diz se está
   cadastrado é o Prime. Compromisso que já está no Prime em todos os dias mostra **No Prime** e
   fica sem o botão de preencher (evita cadastro em dobro).
   Se cancelar a janela, o provisório fica no calendário: apague-o (botão direito → Excluir) ou
   saia sem salvar.
4. **São dois "Salvar"** (30/09: o operador salvou só a janela e o cartão "não atualizava"): o da
   **janela** só põe o compromisso no calendário — o cartão passa a **Falta salvar a agenda**; o
   **Salvar da agenda** (embaixo, no rodapé) é que grava. A extensão percebe o clique em qualquer
   "Salvar" do Prime; depois do da agenda, espera o Prime terminar e relê a agenda do profissional
   sozinha — o cartão vira **No Prime**. No fim, **Salvar** no rodapé da agenda do Prime. Nos 30 minutos seguintes a cada preenchimento,
   o painel reconfere sozinho a agenda do profissional a cada carga de página (o Salvar do rodapé
   recarrega); ou clique **Atualizar**.

**Quem grava é o operador.** A extensão preenche os campos da janela (tipo, profissional, função,
horário, vagas, extras, repetição semanal) e mostra o que ficou; ela **não** clica em Salvar,
Excluir nem Enviar. O único botão que ela aciona é o *Adicionar* do profissional dentro da janela
(põe o nome na grade da janela; não grava). Isso muda a regra de ouro **só para o Prime**: lá a
extensão escreve nos campos da tela; no SISREG continua só observando.

**Arquivos:** `prime-agenda.js` (painel, mundo isolado) e `prime-agenda-main.js` (mundo da página:
os controles do Prime são Telerik e só aceitam valor pelo `$find` — escrever no `<input>` não
atualiza o `*_ClientState`). Os dados passam pelo service worker (`api-get`, só `GET /agenda/*`),
porque a página do Prime não pode chamar a nossa API (CORS).

**Armadilhas da tela (medidas em 30/09/2026):**
- A repetição do compromisso fica presa ao mês da agenda: *Encerra em* aceita no máximo o dia 1º do
  mês seguinte, e esse dia não entra (outubro inteiro = encerra em 01/11).
- A lista de profissionais da janela **depende do tipo** (Consulta, Sala de Procedimento…); a de
  funções depende do profissional. Por isso a ordem é tipo → profissional → função → Adicionar.
- **"Novo Evento" não abre janela:** cria no calendário um compromisso PROVISÓRIO ("Consulta",
  1 hora), que só vai ao banco no **Salvar do rodapé** (medido: a Consultar Agenda não o mostra antes
  disso; sair da tela sem salvar o descarta). Para editar, duplo clique nele — por isso a janela vem
  como "Edit" mesmo num compromisso novo.
- **Ações do Telerik rodam "fora da pilha" da extensão** (`foraDaPilha`: `setTimeout` + `function`
  comum). O handler do "Tipo" da janela percorre a pilha com `fn.caller`; com função async/arrow nossa
  na pilha, o Chrome lança *"'caller', 'callee', and 'arguments' properties may not be accessed on
  strict mode functions"*. Trocar o Tipo **limpa o profissional** — por isso a ordem é tipo →
  profissional → função → Adicionar.
- **"Adicionar" sem profissional abre um `alert` nativo** ("Informe o profissional") que trava a
  página até alguém clicar OK. A extensão só clica com profissional e função escolhidos.
- A grade de profissionais da janela tem uma 1ª coluna **oculta** ("Código"): ler pelo cabeçalho.
- **A busca do combo de profissionais devolve só a 1ª página de quem contém o texto** (30/09: com
  "ANA LUCIA BAPTISTA PEDROZA DE ALBUQUERQUE" vieram LUCIANA, TATIANA… e não ela). A extensão tenta
  o nome inteiro e depois as palavras mais longas do nome (ALBUQUERQUE, BAPTISTA…) até achar o nome
  exato. Se o profissional já tem outro compromisso no Prime no mês, o **nome** vai escrito como o
  Prime escreve. O **tipo NÃO** se copia do Prime (a 0.5.27 copiava e trouxe o erro do cadastro à
  mão: a colposcopia da Dra. Ana Lucia estava como "Consulta"); vem do procedimento do SISREG ou da
  escolha do operador no cartão.
- **Trocar de semana só pelas setas do calendário.** Mudar a data por script (`set_selectedDate`)
  troca a tela, mas o servidor fica na semana anterior e o "Novo Evento" cai no dia errado (pedido
  08/10, nasceu 01/10).
- O Prime guarda o estado da tela na sessão: não abra a mesma agenda em duas abas.
- **Erro de dentro do Telerik** volta ao cartão como *"Erro do Prime ao &lt;passo&gt;: …"* (escolher
  o tipo, buscar/escolher o profissional, buscar/escolher a função, adicionar, ir até a semana,
  criar o "Novo Evento", abrir o compromisso…), e a pilha completa vai ao console da página com a
  marca `[SMSMais agenda]`.
- **"Novo Evento" quebra com *"Cannot read properties of undefined (reading 'get_index')"***
  quando o botão direito não foi dado numa célula da **grade** de horários. **Causa medida
  (30/09):** o `getTimeSlotFromDomElement` do Telerik devolve um "horário" para **qualquer** `td`
  do calendário (calcula pela linha/coluna do `td` na tabela em que ele está). O calendário da
  semana tem 179 `td`, mas a grade (`table.rsContentTable`) tem só 126 (18 horários × 7 dias); o
  resto é o **mini-calendário escondido do cabeçalho** (o seletor de data, 6 × 7), a linha "dia
  inteiro" e `td` de layout. O cabeçalho vem antes no HTML, então a busca pegava um dia do
  mini-calendário sempre que o horário caía nas linhas que ele tem (08:30 a 11:00): o botão direito
  ia numa célula 31 × 27 escondida atrás do título da semana, o menu não abria e o "Novo Evento"
  quebrava. A quinta 13:00 funcionava porque o mini-calendário não tem linha para ela. Quatro
  versões (0.5.21 a 0.5.25) tentaram afastar o que estava "por cima" (painel, seta, camadas) até
  o diagnóstico do cartão mostrar a célula (`célula=261,256 31x27`, `camadas=H2>rsHeader…`).
  **Correção (0.5.26):** só contam os `td` da grade (`rsContentTable`; sem essa classe, a maior
  tabela fora do cabeçalho, do mini-calendário e do "dia inteiro"), conferidos pelo próprio
  Telerik (`slot.get_domElement()` tem que ser o `td`), e o clique voltou a ser o validado no Prime
  real: só o evento `contextmenu` na célula. O selo de data também passou a destacar a célula
  certa (antes, das 08:30 às 11:00, destacava a célula escondida).

## Decisões desta fase

- **API fora do ar → o SISREG continua liberado** (o blur exige o login, não a API). As capturas
  ficam guardadas na memória da extensão e são reenviadas quando a API volta.
- **Guardar tudo**, sem expurgo — é material para o agente analisar; depois se descarta.
- **Só modo desenvolvedor**, sem distribuição em massa por enquanto.

## Instalar (modo desenvolvedor)

1. `chrome://extensions` → ligar **Modo do desenvolvedor**.
2. **Carregar sem compactação** → escolher a pasta `SMSMais.chrome/`.
3. Fixar o ícone na barra.

Ao editar arquivos: suba a versão no `manifest.json` e a extensão **se recarrega sozinha** em até
30 s (ver abaixo) — ou **↻** no card, para não esperar. Para o painel/blur (`content.js`), dar
**F5** na aba do SISREG também.

## Recarga automática (v0.5.33)

A extensão vigia a própria pasta: a cada batimento (30 s) ela lê o `manifest.json` **do disco** e,
se a versão de lá é diferente da que está rodando, envia o que ainda tem guardado e chama
`chrome.runtime.reload()` — o **↻** sem ninguém clicar. É assim que o atualizador dos PCs
(`SMSMais.atualizador/`) entrega uma versão nova: ele troca os arquivos da pasta, deixa o
`manifest.json` **por último**, e a extensão faz o resto.

Medido em 01/10/2026 (Chrome 154, extensão carregada sem compactação):
- a troca dos arquivos virou extensão recarregada em 5 a 18 s (o teto é o batimento, 30 s), e o
  manifest novo **é relido** (a versão mostrada em `chrome://extensions` muda);
- **o "Modo do desenvolvedor" tem de ficar ligado.** Com ele desligado o Chrome **desativa** a
  extensão na hora (e a recarga a deixa desativada). O atualizador percebe isso e avisa;
- recarregar a extensão não troca o código das páginas **já abertas**: continua valendo o **F5**
  nelas (o selo do SISREG fica parado e o painel do Prime avisa que a extensão foi atualizada).

Isto vale também na máquina de desenvolvimento: subir a versão no `manifest.json` recarrega a
extensão sozinha — convém subir a versão **depois** de terminar os outros arquivos.

## Publicar para os computadores (ADR-0064)

Mexer nesta pasta **não entrega nada a ninguém**. Os computadores recebem o que foi publicado na
plataforma, primeiro no canal de teste e depois em produção:

1. `bash empacotar.sh` → `../SMSMais.atualizador/dist/extensao-<versão>.zip`. Entram só os arquivos
   da extensão; o script confere o manifest e recusa pacote com arquivo faltando.
2. No painel, **Sistema → Extensão Chrome → Versões → Publicar versão**. A versão é lida do
   manifest do pacote e entra em **teste**.
3. Conferir num computador marcado como de teste; depois, **Pôr em produção**.

Os computadores baixam e recarregam sozinhos, pelo atualizador (`SMSMais.atualizador/`). O
repositório público e o `publicar.sh` são o caminho antigo, anterior a essa decisão.

## Configurar (antes de testar)

`config.js`:
- `API_BASE` — para onde as capturas vão. Padrão `http://localhost:5080` (backend na sua máquina).
- `PAINEL_ORIGIN` — de onde a extensão lê o login. Padrão `https://smsmarica.online`.

## Como funciona (arquivos)

| Arquivo | Papel |
|---|---|
| `manifest.json` | MV3; hosts do SISREG, do painel e da API |
| `config.js` | endereços e parâmetros de lote |
| `endpoints.js` | catálogo de endpoints/etapas conhecidos e campos mascarados (`senha`) |
| `background.js` | o cérebro: guarda o login, observa o tráfego, envia os lotes, mantém o LED |
| `capture-hook.js` | roda no mundo da página (todos os frames) para copiar as respostas de fetch/XHR |
| `content.js` | roda em todos os frames: captura o HTML da tela; no frame de cima desenha o selo e o blur |
| `auth-content.js` | roda em `smsmarica.online`: lê a sessão do painel e entrega ao cérebro |
| `prime-agenda.js` | só no Prime: painel "Agenda SISREG → Prime" (proposta a partir do SISREG) |
| `prime-agenda-main.js` | só no Prime, mundo da página: preenche a janela do compromisso (Telerik) |
| `popup.*` | status rápido ao clicar no ícone |

### Por que o envio sai do "cérebro", e não da página

Enviar de dentro da página do SISREG seria bloqueado pela política de segurança dela (CSP) e pelo
CORS. O service worker não sofre nenhum dos dois (tem permissão do host), então é ele quem fala
com a nossa API. A senha do SMSMarica só é digitada no nosso site; a extensão nunca a vê.

## Testar

1. ↻ na extensão e F5 no SISREG. O selo deve mostrar **v0.2.0** no popup.
2. Sem sessão no SMSMarica: o SISREG aparece **borrado** com o modal "Entrar no SMSMarica".
3. Clicar em **Entrar** → logar no painel → o blur some e o LED fica **verde**.
4. Navegar/operar no SISREG → o LED pisca **âmbar** ao enviar. Alt+clique no selo para ver as
   capturas.
5. Sem backend rodando, o LED fica **vermelho** e as capturas se acumulam (é o esperado).

## Armadilhas já vistas

- **Cada pessoa com o SEU operador do SISREG — nunca o do robô de produção** (sessão única).
- **Sessão do SISREG derrubada não muda a URL:** `/cgi-bin/index` vira a tela de login com "logon
  em outra estação de trabalho".
- O iframe principal é `id="f_main"` **com `name="f_principal"`**.

## Pendente (backend)

A rota `POST /extensao/sisreg/capturas` **ainda não existe** — é o próximo passo (tabela nova,
permissão nova e a habilitação de descompressão do corpo no servidor). Enquanto isso, o LED do
piloto fica vermelho e as capturas ficam só na memória da extensão.
