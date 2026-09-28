# SMSMais · Ponte de Sistemas — extensão Chrome

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
2. **Selo discreto** no canto, com a marca do SMSMarica e um **LED** de status:

   | LED | Significado |
   |---|---|
   | cinza | sem sessão no SMSMarica |
   | verde | conectado |
   | âmbar piscando | enviando capturas |
   | verde com halo | acabou de registrar |
   | vermelho | SMSMarica fora de alcance (as capturas ficam guardadas) |

3. **Captura** cada operação: o **envio** (URL + campos do formulário, com `etapa` e o evento de
   negócio — cancelamento, confirmação, falta…) e o **retorno** (o HTML da tela que carregou e os
   corpos de AJAX do `sisreg_ajax`).
4. **Envia em lote** (comprimido) para a nossa API, autenticando com o **mesmo login** do painel.

O que o usuário final vê é só o selo com o LED. A lista de requisições existe para depuração e
fica escondida: **alt+clique no selo** abre/fecha.

## Decisões desta fase

- **API fora do ar → o SISREG continua liberado** (o blur exige o login, não a API). As capturas
  ficam guardadas na memória da extensão e são reenviadas quando a API volta.
- **Guardar tudo**, sem expurgo — é material para o agente analisar; depois se descarta.
- **Só modo desenvolvedor**, sem distribuição em massa por enquanto.

## Instalar (modo desenvolvedor)

1. `chrome://extensions` → ligar **Modo do desenvolvedor**.
2. **Carregar sem compactação** → escolher a pasta `SMSMais.chrome/`.
3. Fixar o ícone na barra.

Ao editar arquivos: **↻** no card da extensão; para o painel/blur (`content.js`), dar **F5** na
aba do SISREG também.

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

## Sítios observados (config.js → `SITIOS`)

| Sítio | Host | Modo | Blur |
|---|---|---|---|
| SISREG | `sisregiii.saude.gov.br` | `minimo` — só o comando + o nº da solicitação | sim |
| Prime (Eco) | `marica.ecosistemas.com.br` | `analise` + `tudo` — **tudo que vai e volta** | sim |

O **blur é o que garante o envio**: sem sessão do SMSMarica o site fica bloqueado, então não há
como o operador trabalhar horas capturando para uma fila que nunca sobe.

### O que a captura profunda (`tudo`) acrescenta

Pensada para ASP.NET WebForms/Telerik, onde toda operação vira o mesmo POST para a mesma `.aspx`:

- **cabeçalhos** de ida e de volta (`Content-Type`, `Content-Disposition`, `X-Requested-With`);
  `Cookie`/`Authorization` vão como `<omitido>`;
- **cadeia de 302** — invisível para a página e decisiva no gate de unidade do Prime;
- **`__doPostBack`** (qual controle disparou, com que argumento) e o **clique** que o originou —
  sem isso, duas operações diferentes ficam indistinguíveis no acervo;
- **tela recapturada** depois que o AJAX reescreve o DOM (com digest, para não repetir a mesma);
- **downloads** de relatório (`*RPT.aspx`): URL com todos os parâmetros, nome, tipo e tamanho.

**O conteúdo do arquivo baixado NÃO é capturado** — pegá-lo exigiria refazer a requisição, e a
regra de ouro é não disparar nada contra o sistema observado.

## Eventos de negócio (Prime) — `prime.js`

Capturar tudo serve para aprender; **operar** precisa de evento nomeado. O sítio com
`verbos: 'prime'` no `config.js` passa a emitir, ao lado da matéria bruta, uma linha por
operação reconhecida:

| Evento | Como é reconhecido | Chaves |
|---|---|---|
| `paciente-criado` | `__EVENTTARGET` termina em `rbSalvar`, em `CadastroPaciente.aspx` | `pacienteId` (vem da RESPOSTA), `unidadeId` |
| `paciente-agendado` | campo `rbSalvarAgendamento` presente no corpo | `agendaId`, `pacienteId`, `unidadeId` |
| `paciente-acolhido` | `__EVENTARGUMENT = Acolher\|<guid>` | `agendaId`, `unidadeId` |
| `paciente-desagendado` | `__EVENTARGUMENT = Desagendar\|<guid>` | `agendaId`, `unidadeId` |

Detalhes que não são óbvios e custaram medição:

- **O `pacienteId` do cadastro não existe no envio.** Ele nasce na resposta (`hidPacienteId` no
  corpo do `__ASYNCPOST`). O evento fica *aguardando* até 30 s; se o id não vier, sai assim mesmo
  com `idAusente: true` — perder a operação seria pior, e o rótulo impede lê-la como completa.
- **O agendamento não carrega o paciente.** Quem o amarra é o `SELECIONAR_PACIENTE|<guid>` que
  veio antes, guardado por aba. Por isso a seleção é rastreada mesmo sem virar evento.
- **`Acolher` NÃO recebe `pacienteId`.** Ele age sobre a linha da grade, não sobre o paciente
  selecionado na tela; deduzir um seria inventar. O `agendaId` é a chave — e é o mesmo do
  `paciente-agendado`.
- **Acento só está certo no corpo do XHR.** O Chrome entrega o corpo urlencoded do `webRequest`
  já decodificado como UTF-8, e o Prime é latin-1. O reconhecedor relê o corpo copiado no
  `send()`, testando UTF-8 e caindo para latin-1 quando o resultado tem U+FFFD.

Os três campos `evento`, `etapa` e `escrita` são **colunas** no hub (a de evento é indexada),
então consultar operação não exige cavar o jsonb.

## Backend

`POST /extensao/sisreg/capturas` (autenticado, sem permissão de módulo) grava em
`smsmarica.sisreg_captura_navegador` — `payload` jsonb + `conteudo` text. A origem fica em
`payload->>'sitio'`, então dá para separar o acervo do Prime do acervo do SISREG.
