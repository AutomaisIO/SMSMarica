# ADR-0076 — Fonte de dados é cadastro da plataforma: cria-se em Sistema, o recurso só escolhe e configura o uso

**Status:** aceito · **Data:** 2026-10-21 · **Implementação:** branch `feat/fontes-de-dados`
**Relacionado:** [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (o CRUD de bases nasceu dentro da IA) ·
[ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (API-SISREG em `sisreg_configuracao`) ·
[ADR-0014](./0014-importacao-salux-no-backend.md) (a importação de prontuários usa a base cadastrada na IA) ·
[ADR-0023](./0023-proxy-sql-agente-e-conhecimento.md) (base via agente, token, provisionamento) ·
[ADR-0042](./0042-ser-segunda-fonte-de-regulacao.md) · [ADR-0063](./0063-esus-sao-goncalo-e-analise-de-regras-dos-espelhos.md) ·
[ADR-0067](./0067-telefone-pelo-esus-pec-rotina-noturna.md) (logins em `integracao_credencial`) ·
[ADR-0046](./0046-rename-smsmais.md) (purpose `SMSMarica.Ia.Segredos` congelado)

## Contexto

"De onde a plataforma lê dado" era cadastrado em seis telas e três tabelas, e cada cadastro
misturava a conexão com o uso que um recurso faz dela:

| O quê | Onde se editava | Tabela |
|---|---|---|
| Bancos (Oracle/Postgres), agentes SQL Server, Klinikos Web, Regulação/Atendimento | Inteligência → Configuração IA → Bases de dados **e** Importar Prontuários → Fontes / Conectores | `ia_fonte` |
| Login do SISREG (web) e do e-SUS PEC | Integrações → Credenciais | `integracao_credencial` |
| Login do SER, SERNIT e ESUS SG | aba de configuração de cada um (Regulação) | `integracao_credencial` |
| API-SISREG (Elasticsearch) | Configuração SISREG | `sisreg_configuracao` |

Problemas que isso produziu (todos conferidos no código em 21/10/2026):

- **Duas telas para o mesmo registro**, e com permissões diferentes: o item "Fontes / Conectores"
  aparecia para quem tinha `SincronizacaoPep`, mas a API exigia `InteligenciaConfiguracao`.
- **O formulário mentia**: as bases SQL Server do Klinikos apareciam como "Salux / Oracle /
  TREINAMENTO" porque as opções do select não tinham o valor gravado.
- **Salvar login apagava a agenda dos motores.** O JSON de `integracao_credencial` guarda, além
  da URL, a agenda da fila, escalas, lote de mapeamento, coleta de indicadores, estatística de
  operadores e a varredura do SER/SERNIT/ESUS SG. Salvar o login do SER/SERNIT mandava
  `ParametrosJson: null` e zerava tudo isso.
- **Não havia um lugar para ver todas as fontes** da instância — nem saber qual recurso usa qual.

Pedido do Bernardo (21/10/2026): um cadastro e um menu em **Sistema** onde se cria e configura
toda fonte de dado; a plataforma "ganha uma fonte de dado"; IA, Importar Prontuários e os demais
recursos **escolhem** a fonte, e o tratamento, o agendamento e o uso ficam no recurso.

## Decisão

### 1. Fonte de dados é um conceito da plataforma, não da IA

Existe **um** cadastro, **Sistema → Fontes de dados** (`/app/fontes-dados`), com permissão própria
`FontesDados` (módulo 82, nasce só no perfil Administrador). Ali se cria, edita, desativa, testa e
gera o token de agente de toda fonte. Nenhuma outra tela edita conexão ou credencial de fonte.

### 2. O limite: fonte é de onde se LÊ dado

Entram: bancos (Oracle, Postgres, SQL Server por agente), Klinikos Web, os recortes do próprio
banco (Regulação, Atendimento), SISREG (login web e API), SER, SERNIT, ESUS SG e e-SUS PEC.

Ficam em **Integrações**, porque são serviço que a plataforma usa e não fonte de dado:
armazenamento (Spaces), voz (ElevenLabs), mapa (Google Maps), WhatsApp/Zap, provedores de IA
(Anthropic/Voyage), motores de CPF/CEP, login social (Google/Microsoft/Facebook) e tokens de API.

### 3. Conexão fica na fonte; uso fica no recurso

| Fonte | Conexão (Fontes de dados) | Uso (no recurso) |
|---|---|---|
| Banco / agente | host, porta, serviço, usuário, senha, dialeto, slug, token do agente | agenda do PEP, `indicador.fonte_id`, conhecimento da IA |
| Klinikos Web | URL, login, senha, `appRoot`, `unidCodigo`, `metaSource`, `build` | `webPrimaria`, `periodicoLigado`, `periodicoIntervaloMin`, `deepThrottleSeg` (Importar Prontuários) |
| SISREG web | login, senha, `baseUrl`, `autoLogin` | fila, escalas, lote de mapeamento, coleta de indicadores, estatística de operadores (telas de cada motor) |
| SER / SERNIT | login, senha, `baseUrl` | varredura, estatística de operadores, limite de anexo (Regulação) |
| ESUS SG | login, senha, `cliente`, `apiUrl`, `legadoUrl` | varredura (Regulação) |
| e-SUS PEC | CPF, senha, `baseUrl`, `acessoId` | janela da rotina noturna (`janelaInicio/Fim`) |
| API-SISREG | URL, tipo de autenticação, login, senha, token, ativo | escopo, UF, município, centrais, sincronismo automático, fonte do cadastro, consultas simultâneas (Configuração SISREG) |

O recurso **escolhe** a fonte (a Consulta Inteligente pergunta a base, Importar Prontuários tem o
seletor, o indicador guarda a sua) e mostra a fonte que usa com um atalho para Fontes de dados —
mas não edita a conexão.

### 4. Duas entregas

**Entrega 1 (esta):** a tela única com todas as fontes, os formulários espalhados saem, e o
salvamento deixa de apagar o uso. **Os dados continuam nas tabelas atuais**: um serviço de fachada
(`SMSMais.Core/Fontes/FontesDadosService`) lê e grava em `ia_fonte`, `integracao_credencial` e
`sisreg_configuracao`. Os motores, sessões e importadores não mudam.

- Gravar o login do SER, SERNIT e ESUS SG continua **testando antes de salvar** e reiniciando a
  sessão (a fachada delega aos `*MotorService`): credencial que não entra faria o motor falhar de
  madrugada.
- `IntegracaoCredencialService.AtualizarAsync` passa a tratar `ParametrosJson` **nulo como "mantém"**.
  Não vira merge automático: `ColetaIndicadoresConfig` retoma a coleta **removendo** a chave de pausa,
  e um merge faria a pausa nunca sair. Quem altera parte do JSON lê, altera e grava o JSON inteiro
  (como já faziam os motores); a fachada faz o mesmo com as chaves de conexão.
- Na fonte Klinikos Web, editar a conexão preserva as chaves de uso e vice-versa (mesmo campo
  `parametros_json`, cada tela só mexe nas suas).
- SISREG web, Klinikos Web e e-SUS PEC **não têm botão de teste**: todos são sessão única por
  operador, e testar derrubaria o robô (ou a servidora que cedeu o login, no e-SUS PEC).
- A rota `ia/configuracao/agentes/provisionar` continua existindo: o instalador do agente a chama
  ([proxy-sql-agente-wss.md](../proxy-sql-agente-wss.md)). Há a equivalente em `fontes-dados/`.

**Entrega 2 (depois de conferida a 1, plano próprio):** juntar o armazenamento numa tabela de
fontes com uma tabela de uso por recurso, sem mudar a tela. Ficam registrados aqui os cuidados que
a revisão da proposta "mover tudo junto" levantou, para não serem redescobertos:

- **Migration de dados no startup falha calada** (`Program.cs` loga e segue; o deploy só olha
  `systemctl is-active`). Se a cópia falhar, a migration de schema fica aplicada, o `DbSeeder` não
  roda e SISREG/SER param com "não configurado". Conferir `__EFMigrationsHistory` e `/health` depois
  do deploy; ensaiar num clone da produção antes.
- `ia_fonte` tem `NOT NULL` sem default (`dialeto`, `ambiente`, `nome`, `criado_em`);
  `parametros_json` é `text` (validar com `pg_input_is_valid` antes de `::jsonb`); slug é único
  entre não excluídas; `sisreg_configuracao.login` é varchar(200) e `ia_fonte.usuario` varchar(120).
- Linha nova com tipo novo fica visível ao binário anterior e às telas de Importar Prontuários,
  Indicadores e Consulta Inteligente — filtrar por tipo/dialeto; gravar `dialeto = 0`.
- **"Ativo" tem semânticas diferentes por consumidor**: uns leem por `ObterContextoAsync` (inativo =
  desligado), outros por `ObterAsync` (ignoram). Definir e testar.
- Vários leitores engolem `ValidacaoException` e caem no padrão — um consumidor esquecido muda de
  comportamento sem erro (a fila do SISREG religaria às 03h). Apagar as constantes `Provedor` para o
  compilador achar todos.
- O login do operador (SER/SERNIT/SISREG/e-SUS PEC por usuário) só lê a URL da credencial — o
  resolvedor novo precisa de uma leitura "só conexão", sem exigir a senha institucional.
- Voltar de versão depois da cópia perde o que foi gravado depois — gravação dupla até a limpeza.

### 5. Permissão

`FontesDados = 82` (o 81 está em uso em dois branches abertos). Nasce só no Administrador; os
demais perfis recebem pela tela de Perfis. Quem hoje trocava a senha do SER/SERNIT/ESUS SG com
`RegulacaoConfiguracao`, ou do SISREG/e-SUS PEC com `IntegracoesConfig`, precisa ganhar o módulo
novo para continuar.

## Consequências

- Um lugar para ver e mexer em todas as fontes da instância (ADR-0043: cada município cadastra as
  suas), com "usada por" para saber o impacto antes de desativar.
- As telas de IA, Importar Prontuários, SER, SERNIT, ESUS SG, Configuração SISREG e Integrações
  ficam menores: mostram a fonte e cuidam só do uso.
- O bug que zerava a agenda dos motores ao trocar senha deixa de existir.
- Na Entrega 1 convivem três armazenamentos atrás de uma fachada — a Entrega 2 é que tira isso.

## Pendências

- Entrega 2 (tabela única + uso por recurso), com os cuidados da seção 4.
- Manual: Integrações, Configuração IA e Importar Prontuários ainda não têm artigo.
- Spaces perde `forcePathStyle` ao salvar (fica em Integrações; bug à parte).

## Alternativas descartadas

- **Mover os dados na mesma entrega da tela.** Faria o pedido de uma vez, mas uma cópia que
  falhasse no startup pararia SISREG/SER sem erro visível, e voltar de versão perderia o que fosse
  gravado depois. A tela não depende de onde o dado mora.
- **Merge automático do JSON em `AtualizarAsync`.** Corrigiria o salvamento do SER com uma linha,
  mas quebraria quem remove chave de propósito (retomar a coleta de indicadores).
- **Deixar as credenciais de serviço (Spaces, ElevenLabs, Maps, WhatsApp, IA) também em Fontes de
  dados.** Misturaria "de onde se lê dado" com "o que a plataforma usa" — a tela perderia o sentido.
