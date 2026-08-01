# Marco 1 e linha de corte — inventário de 31/07/2026

Levantamento do que existe no monorepo, do que está de fato em produção, do que foi
documentado e nunca implementado, e do que está implementado e nunca entregue.
Objetivo: **fechar o Marco 1**, passar uma linha, e sair daqui com uma lista curta
do que fica, do que sai e do que entra na fila.

Este documento é uma **folha de decisão**, não um registro do que já foi decidido.
As seções §7 e §9 esperam o seu OK.

---

## 1. Como foi medido

Tudo abaixo é medição, não estimativa:

| Fonte | O que foi lido |
|---|---|
| Banco de **produção** (DigitalOcean, `defaultdb`) | contagem de linhas das 93 tabelas de `smsmarica`; histórico de migrations de `smsmarica.__migrations` e `fhir."__EFMigrationsHistory"` |
| `git` | `HEAD` vs `origin/main`, working tree sujo, 39 refs de branch, commits fora de `main` |
| Repositório | 37 ADRs, 47 features do front, 68 controllers, 52 valores de `ModuloPermissao`, 11 migrations locais |

**Fato âncora:** `HEAD` local = `origin/main` = `83f8abb`. Nada pendente de push.
Logo, **os 217 arquivos não commitados são 100% inéditos em produção** — o deploy é
automático no push para `main`.

---

## 2. Marco 1 — o que já está em produção e vivo

Estas são as entregas reais. Números são de produção, hoje.

### 2.1 Núcleo clínico de imagem (a espinha dorsal)

| Entrega | Evidência em prod |
|---|---|
| Solicitação de exame → Worklist → PACS → Laudo | 1.560 solicitações · 890 exames de imagem · 664 laudos |
| Assinatura digital de laudo (PAdES/VIDaaS, ADR-0015) | 452 assinaturas · 2 médicos assinantes |
| Associação exame↔pedido↔paciente (ADR-0016) | 267 associações |
| Anamnese | 351 |
| Templates + cabeçalho/rodapé global do laudo | 1 template · 1 configuração |

### 2.2 Canal com o cidadão

| Entrega | Evidência em prod |
|---|---|
| Central de Atendimento WhatsApp multi-operador (ADR-0026) | **5.632 conversas** · 5.632 eventos |
| Comunicações ao paciente (confirmação/autorização) | 1.548 comunicações · 26.038 mensagens WhatsApp |
| PWA do cidadão + identidade/sessão própria (ADR-0018/0036) | 1.358 sessões · 991 magic links · 891 acessos |
| Consentimento LGPD | 846 consentimentos |
| Anexos de exame via QR (ADR-0019) | 105 tokens de upload |

### 2.3 Dados, integração e governança do sistema

| Entrega | Evidência em prod |
|---|---|
| Hub FHIR autônomo `Automais.Fhir` (ADR-0010) | schema `fhir` em prod na 5081, 9 migrations aplicadas |
| Importação SISREG (ADR-0012) | alimenta as 1.560 solicitações |
| Módulo IA + conhecimento por base (ADR-0011/0023) | 18.051 chunks · 7.276 documentos · 3 fontes |
| Indicadores contratuais HMCML (ADR-0022) | 108 indicadores · 425 execuções |
| Painel do Secretário (PWA público) | em prod desde 24/07 |
| Auditoria (ADR-0006) | 480 registros |
| Tratamento global de 500 + código de referência | 764 erros catalogados |
| Multitenancy por unidade | 199 vínculos usuário↔unidade |
| Suporte/Tickets | módulo 37 em prod |

**Isso é o Marco 1.** É um sistema de gestão clínica e regulação de exames com canal
de WhatsApp e hub FHIR — **não** o sistema de transporte sanitário que o
`docs/roadmap.md` ainda descreve.

---

## 3. Em produção, mas sem uso — candidatos a corte ou congelamento

Tabelas com **0 ou quase 0 linhas** depois de meses no ar. Cada uma tem tela, endpoint,
módulo de permissão e código de manutenção.

> **Leitura correta destes números (revisão de 31/07):** "sem uso" aqui **não** quer dizer
> "desnecessário". Quer dizer **ainda não definido e ainda não implantado** — outras frentes
> entraram na frente. Nada nesta seção é candidato a remoção; é candidato a **fila com
> definição**. A única ação imediata que estas tabelas justificam é a correção de modelagem
> da §10, que deve ser feita **agora, enquanto o domínio está vazio e barato de mexer**.

### 3.1 Núcleo TFD / transporte sanitário — ainda não implantado

| Tabela | Linhas |
|---|---|
| `avaliacao` | **0** |
| `rastreamento_ponto_gps` | **0** |
| `rastreamento_geofence` | **0** |
| `rastreamento_evento_chegada` | **0** |
| `tfd_registro_faturamento` | **0** |
| `veiculo` | 1 |
| `motorista` | 2 |
| `tipo_tratamento` | 2 |
| `tratamento` / `tratamento_periodicidade` | 3 / 3 |
| `translado_alocacao` | 4 |
| `translado_rota_diaria` | 5 |
| `veiculo_fileira` | 5 |
| `sessao_de_tratamento` | 112 (legado, sem movimento) |

Isso é o **produto original inteiro** (M2..M6 do roadmap) ainda sem definição fechada e
sem implantação. Ele permanece no escopo: falta decidir o desenho, não decidir se fica.

> **Atenção — não confundir o TFD com a mensageria que ele usaria.** `tfd_mensagem_whatsapp`
> (26.045) e `tfd_geocodigo` (696) têm muito volume, mas **não são do TFD**: são
> infraestrutura compartilhada que nasceu dentro da pasta do TFD e ficou com o prefixo
> errado. A prova está na §10.1. Isso é um **erro de modelagem a corrigir**, não uma
> reclassificação do TFD.

### 3.2 Agendamento local — natimorto

| Tabela | Linhas |
|---|---|
| `bloqueio_agenda` | **0** |
| `disponibilidade_avulsa` | 1 |
| `especialidade` | 2 |
| `agenda` | 3 |
| `agendamento` | 3 |

ADR-0012 e ADR-0013 (agenda multi-recurso) foram implementados e **nunca adotados**.
Na prática a regulação real acontece no SISREG e no processo regulatório (§5).

### 3.3 Construído nas últimas semanas e ainda sem adoção

| Tabela | Linhas | Observação |
|---|---|---|
| `sisreg_profissional_unidade` | **0** | Módulo 52 (`SisregMapeamento`) — tela pronta, zero dado |
| `sisreg_procedimento_profissional` | **0** | idem |
| `sisreg_credencial_unidade` | **0** | idem |
| `ia_consulta_feedback` | **0** | feedback da Consulta Inteligente nunca usado |
| `download_token` | **0** | sem função aparente |

### 3.4 Sinal de alarme operacional (não é código morto — é dado ruim)

`sisreg_importacao_falha` = **4.382 linhas** contra 1.560 solicitações importadas.
Mais falhas do que sucessos. Isso é a pendência de qualidade de dados de importação,
e é um item de produto, não de limpeza.

---

## 4. Implementado e NÃO entregue — o WIP local

> **Atualizado em 01/08:** a frente do TFD (§10) saiu daqui e **está em produção**. As demais
> seguem paradas — a lista viva está em **§11.2**, que é a que vale para planejar.

217 arquivos no working tree, zero commitados. É a maior "ponta solta" do repositório.
Agrupando por frente:

| Frente | ADR | Estado | Migration pendente |
|---|---|---|---|
| **Painel de Início** (lentes, raias, cancelamento, pendências) | 0033 / 0034 / 0035 | back + front completos | `20260730132138_AddCausaFalhaImportacaoEIndicesPainel` |
| **Sincronismo contínuo Salux + Internação** | 0024 / 0025 | server + FHIR (Location, Encounter IMP) | `20260727122701_AddPepSincronizacaoContinua` + FHIR `20260727020900_AddIdentifierClinicoELocation` |
| **Processo Regulatório** | 0021 | só entidades + máquina de estado | `20260725152753_AddProcessoRegulatorio` |
| **Escopo de unidade fail-closed** | 0037 | Core/Common/Unidades + testes | — |
| **Trilha de falhas de importação + fix CID** | — | server + front | — |
| **Correção SIGTAP (US mamas bilateral)** | — | migration de dados | `20260728132644_CorrigeSigtapUsMamasBilateral` |

### 4.1 Risco concreto: migration fora de ordem

Produção tem aplicada a `20260725**154214**_AddSisregMapeamentoECredencialUnidade`,
mas **não** a `20260725**152753**_AddProcessoRegulatorio` — que é **anterior** no
timestamp. Quando a de regulação for aplicada, ela entra "no passado" do histórico.
O EF tolera, mas qualquer `dotnet ef database update` seletivo ou rollback fica
imprevisível. **Decidir isto antes de deployar** (§9, D3).

### 4.2 Estado das migrations

- **`smsmarica`** — prod parou em `20260729152102_DispensaVerificacaoContato`. **4 pendentes** no repo.
- **`fhir`** — prod parou em `20260616155731_AddTelefoneSearch` (16/06!). **1 pendente**, que traz Location e o identifier clínico.

Lembrete que já custou caro 3 vezes: **o AutoMigrate do startup não aplica migration**.
Conferir `smsmarica.__migrations` depois de todo deploy.

---

## 5. Documentado e NÃO implementado — ADRs sem código

De 37 ADRs, **12 estão marcados "proposto"**. Cruzando com o código:

| ADR | Assunto | Código encontrado | Veredito |
|---|---|---|---|
| 0029 | Unidade habilitada + capacidades | **nenhum** | papel puro |
| 0030 | Robô de triagem em camadas | **nenhum** | papel puro |
| 0031 | Costura do motor de triagem | **nenhum** | papel puro |
| 0032 | Intenção reaproveitável para voz | **nenhum** | papel puro |
| 0021 | Ecossistema de Solicitação / regulação | entidades + máquina de estado, **0 endpoint, 0 tela** | meio-caminho |
| 0027 | Ciclo de vida da conversa e fila | enums e eventos existem e rodam em prod | **status errado — já é realidade** |
| 0028 | Roteamento por unidade | `Conversa.UnidadeId` existe em prod | **status errado — já é realidade** |
| 0033/0034/0035 | Painel de Início | implementado, não deployado | vira "aceito" no deploy |
| 0037 | Escopo fail-closed | implementado, não deployado | idem |
| 0020 | Paciente FHIR nativo | **em produção** | **status errado — é "aceito"** |

### 5.1 A dívida específica da Regulação

`ModuloPermissao` já tem **5 valores reservados** (47–51) para Regulação. O front,
honestamente, só expõe 3 deles e deixa comentário explicando que 47 e 51 "existem no
enum do backend mas ainda não têm nenhum endpoint nem tela".

Ou seja: o enum de permissões virou lista de intenções. Isso confunde quem administra perfis.

---

## 6. Dívida de governança — a causa-raiz das pontas soltas

### 6.1 O roadmap está morto

`docs/roadmap.md` é de 22/04 e descreve um produto de transporte sanitário em M1..M8.
O checklist final tem **`[ ] F1 (scaffold Vite + React)` desmarcado** — enquanto o front
tem 47 features em produção. Nenhum dos marcos M2..M7 corresponde ao que foi construído.
Ninguém consulta, e por isso ninguém atualiza. **Documento a substituir, não a corrigir.**

### 6.2 ADR não tem status de implantação

O campo `Status` de um ADR responde "a decisão foi aceita?" — nunca "isso está rodando?".
Por isso ADR-0020 está "Proposto" estando em prod, e ADR-0030 está "proposto" sem uma
linha de código. **São duas dimensões diferentes e hoje há só um campo.**

### 6.3 Numeração de ADR duplicada e uso indevido da pasta

- `0020-paciente-fhir-nativo.md` **e** `0020-plano-fase1.md`
- `0021-ecossistema-solicitacao-e-fulfillment.md` **e** `0021-runbook-deploy.md`

Plano de fase e runbook de deploy **não são decisões arquiteturais**. Estão inflando a
contagem de ADRs e quebrando a numeração.

### 6.4 39 branches, 33 delas lixo

De 17 branches locais + 22 remotas, **33 não têm um único commit fora de `main`** — já
foram integradas. Só 6 têm conteúdo próprio, e várias são versões pré-squash do que já
entrou. Restam de verdade: `feat/tfd-implementacao` (2), `origin/fix/pool-conexoes-500` (3),
`origin/feat/laudos-filtros` (2), `origin/feat/solicitacao-paciente-ux` (2),
`origin/feat/resync-associacao` (1), `origin/feat/solicitante-coren` (1) — e estas duas
últimas descrevem trabalho que a memória do projeto já dá como em produção.

### 6.5 Lixo na raiz e em `docs/`

- Um arquivo de scratchpad com nome corrompido (`C:Usersberna...pacs_flow.txt`) commitável na raiz do repo.
- Pasta `alterações entre sessoes/` com 4 notas de sessão soltas — não é documentação nem código.
- `docs/` na raiz mistura documentação canônica com `.docx`, `.csv`, `.png` e credenciais.
  As credenciais **estão corretamente gitignoradas** (verificado), mas o ruído atrapalha.

---

## 7. Proposta de linha de corte

### 7.1 FICA — núcleo do produto (investir)

Clínico de imagem (solicitação → PACS → laudo → assinatura) · Central de Atendimento
WhatsApp · Comunicações ao paciente · PWA do cidadão · hub FHIR + sincronismo Salux ·
Importação SISREG · IA/conhecimento · Indicadores HMCML · Auditoria/Erros/Tickets ·
Multitenancy por unidade.

### 7.2 FICA NA FILA — necessário, sem definição fechada (arrumar a casa agora)

**Núcleo TFD/transporte** (Tratamentos, Translados, Rotas, Rastreamento, Avaliações,
Motoristas, Veículos, Faturamento) e **Agendamento local** (Agendas, Especialidades,
Agendamentos, ADR-0013).

Estes módulos **continuam no produto**. O que falta é definição — e outras frentes
entraram na frente. Como estão **vazios**, esta é a janela mais barata que vai existir
para corrigir modelagem:

1. **Corrigir a fronteira TFD × infraestrutura compartilhada agora** (§10). Custo hoje:
   um rename. Custo depois de o TFD entrar em operação: migração de dados com sistema no ar.
2. Marcar ADR-0013 e ADR-0017 como *Aceito — implantação não iniciada* (§8.5), para parar
   de parecerem entregues.
3. Não investir em feature nova nestes módulos antes da definição — mas **não** removê-los
   do menu nem do escopo.
4. `agente.app` (ADR-0003) segue no escopo, aguardando a definição do TFD.

### 7.3 SAI — remover de verdade

| Item | Por quê |
|---|---|
| 33 branches já mergeadas (locais e remotas) | ruído puro |
| `C:Usersberna...pacs_flow.txt` na raiz | lixo de scratchpad |
| `alterações entre sessoes/` | notas de sessão; arquivar fora do repo |
| `docs/roadmap.md` na forma atual | substituir pelo roadmap real |
| `0020-plano-fase1.md` e `0021-runbook-deploy.md` fora de `adr/` | mover para `docs/runbooks/` |
| `download_token`, `ia_consulta_feedback` | avaliar remoção — 0 linhas, sem função observável |
| O prefixo `tfd_` de tabelas que não são TFD | erro de modelagem — ver §10 |

### 7.4 DECIDE — meio-caminho que precisa de rumo

| Item | Escolha |
|---|---|
| **Processo Regulatório (ADR-0021)** | terminar (endpoint + tela) **ou** reverter o schema e liberar os módulos 47–51 |
| **Mapeamento SISREG (módulo 52)** | tem tela e 0 dados: falta adoção ou falta função? |
| **Robô de triagem (0029–0032)** | 4 ADRs de papel: viram fila real, ou marcam-se "adiado" |
| **Qualidade da importação SISREG** | 4.382 falhas × 1.560 sucessos é um problema de produto |

---

## 8. Plano de virada de página (ordem sugerida)

Cada passo é pequeno e verificável; nenhum depende do seguinte.

1. **Limpar o git** — apagar as 33 branches mergeadas, tirar o lixo da raiz. Zero risco.
2. **Fatiar e commitar o WIP** em 6 commits por frente (§4), sem deployar ainda.
   *Atenção ao snapshot do EF, que é compartilhado entre frentes.*
3. **Resolver a migration fora de ordem** da Regulação (§4.1) antes de qualquer deploy.
4. **Deployar por frente**, do menor risco ao maior:
   correção SIGTAP → trilha de falhas → escopo fail-closed → Painel de Início →
   sincronismo contínuo (inclui a migration do FHIR, parada desde 16/06).
   Conferir `smsmarica.__migrations` **a cada** deploy.
5. **Acertar a governança de ADR** — acrescentar o campo `Implantação:`
   (`não iniciada` / `parcial` / `em produção`) e corrigir de uma vez os status errados
   (0020, 0027, 0028) e os de papel puro (0029–0032).
6. **Reescrever o roadmap** a partir da §2 e da §7: Marco 1 fechado com o que está em
   prod, e M2 sendo a fila de fato.
7. **Corrigir a fronteira TFD × infraestrutura compartilhada** (§10) — enquanto o domínio
   TFD está vazio e a correção custa um rename.

---

## 9. Decisões que dependem de você

| # | Decisão | Recomendação |
|---|---|---|
| **D1** | ~~Congelar o TFD?~~ **Decidido em 31/07: o TFD fica.** Falta definição, não escopo. | fila (§7.2) |
| **D1b** | Executar agora a correção TFD × mensageria/geo (§10)? | **sim — a janela é agora** |
| **D2** | Regulação (ADR-0021): terminar ou reverter? | — |
| **D3** | Como tratar a migration `AddProcessoRegulatorio` fora de ordem? | depende de D2 |
| **D4** | Deployar o WIP inteiro ou só as frentes de baixo risco? | fatiar e deployar por frente (§8.4) |
| **D5** | ADRs 0029–0032 (triagem): fila real ou "adiado"? | — |
| **D6** | Apagar as 33 branches mergeadas? | sim |
| **D7** | Substituir o `roadmap.md` pelo roadmap real? | sim |

---

## 10. Correção de modelagem: TFD **usa** mensageria, TFD **não é** mensageria

O canal de WhatsApp e a geocodificação nasceram dentro da pasta do TFD, porque o TFD foi o
primeiro a precisar deles. Ficaram com o prefixo `tfd_` e nunca saíram de lá — mesmo depois
de virarem infraestrutura de todo o sistema.

Isso não é cosmético. Enquanto a mensageria se chamar `tfd_*`, qualquer pessoa (ou IA)
lendo o schema conclui que o WhatsApp do município pertence ao transporte sanitário — e
quem for mexer no TFD acha que pode mexer nas 26 mil mensagens.

### 10.1 A prova

`tfd_mensagem_whatsapp` tem uma FK `sessao_id` para `sessao_de_tratamento` (a sessão de TFD):

| Coluna | Preenchidas |
|---|---|
| `sessao_id` — **o vínculo com o TFD** | **0** de 26.045 |
| `conversa_id` — Central de Atendimento | 22.928 |
| `autor_usuario_id` — operador humano | 11.850 |

**A tabela chamada "tfd_" nunca carregou uma única mensagem de TFD.** São 26 mil mensagens
da Central de Atendimento e das comunicações ao paciente. O `sessao_id` é o cordão
umbilical do nascimento, e está vazio desde sempre.

### 10.2 O que é TFD de verdade e o que não é

| Tabela | Veredito | Por quê |
|---|---|---|
| `tfd_registro_faturamento` | **é TFD** | tem `motorista_id`, `veiculo_id`, `sessao_id`, km, BPA |
| `tfd_config_faturamento` | **é TFD** | valor por 50 km, SIGTAP do transporte |
| `tfd_mensagem_whatsapp` | **não é** | log de mensagens de todo o sistema |
| `tfd_config_whatsapp` | **não é** | credenciais da WhatsApp Cloud API do município |
| `tfd_geocodigo` | **não é** | cache de geocodificação de endereço |
| `tfd_config_google` | **não é** | chave da API do Google Maps |

Detalhe importante: **a camada `Core` já está certa** — o código vive em `Core/Notificacoes/WhatsApp/`
e `Core/Geo/`, não em `Core/Tfd/`. O erro está confinado à camada `Data` (nome da tabela +
pasta da entidade) e ao `TfdConfigService`/`TfdConfigController`, que empacotam três
configurações de domínios diferentes só porque as três nasceram juntas.

### 10.3 Renomeações propostas

Nomes finais, derivados da convenção real do banco (prefixo = dono; `snake_case` singular;
config singleton com sufixo `_configuracao` em pt-BR, como `ia_configuracao`,
`laudo_configuracao`, `sisreg_configuracao`, `ticket_configuracao`):

| Hoje | Passa a ser | Entidade |
|---|---|---|
| `tfd_mensagem_whatsapp` | `whatsapp_mensagem` | `MensagemWhatsApp` → `Entities/Notificacoes/` |
| `tfd_config_whatsapp` | `whatsapp_configuracao` | `WhatsAppConfiguracao` → `Entities/Notificacoes/` |
| `tfd_geocodigo` | `geo_endereco` | `GeoEndereco` → `Entities/Geo/` |
| `tfd_config_google` | `geo_configuracao` | `GeoConfiguracao` → `Entities/Geo/` |
| `tfd_config_faturamento` | `tfd_configuracao` | `TfdConfiguracao` — **continua no TFD** |
| `tfd_registro_faturamento` | *(sem mudança)* | `RegistroFaturamento` — **continua no TFD** |

> As três `tfd_config_*` eram a **única** exceção de nomenclatura do banco inteiro, e erravam
> duas vezes: `config` em inglês e dono errado.

**Cortar o cordão umbilical:** remover a FK `mensagem_whatsapp.sessao_id`. É ela que
inverte a dependência. Quando o TFD entrar em operação e precisar avisar o paciente, ele
referencia a mensagem — exatamente como `comunicacao_paciente.mensagem_whatsapp_id` já faz
hoje. A mensagem não deve saber que o TFD existe. **Essa remoção é a materialização
técnica de "TFD usa mensageria".**

**Separar o serviço de configuração:** quebrar `TfdConfigService` em três, cada um no seu
domínio (WhatsApp → `Core/Notificacoes`, Google Maps → `Core/Geo`, faturamento → `Core/Tfd`).
Isso mexe nas rotas `/tfd/config/*` que o front consome em `features/integracoes`
(`WhatsAppCard`, `GoogleMapsCard`, `NavigationSdkCard`) — back e front precisam ir juntos.

### 10.4 Por que é seguro fazer agora (medido, não estimado)

| Dependência oculta | Encontrado |
|---|---|
| SQL de indicadores citando `tfd_` (colunas `sql` e `sql_analitico`) | **0** |
| Documentos / chunks de conhecimento da IA citando `tfd_` | **0 / 0** |
| Views ou materialized views dependentes | **0** |
| Arquivos `.cs` afetados (fora de `Migrations`) | 19 (mensagem) · 8 (geo) · 7 (configs) |
| Linhas de TFD a migrar | **0** — o domínio está vazio |

Ou seja: nada além do EF conhece esses nomes. É `ALTER TABLE ... RENAME TO`, que preserva
os dados e é instantâneo — **não há cópia de 26 mil linhas**.

### 10.5 Riscos e como tratar

1. **Janela de indisponibilidade.** Rename de tabela é *breaking*: entre a migration rodar e
   o serviço reiniciar, o código antigo quebra. São segundos, mas com a Central de
   Atendimento ativa. → **Executar fora do horário de atendimento.**
2. **Snapshot do EF é compartilhado entre frentes.** Há 4 migrations pendentes e WIP de
   outras frentes no mesmo snapshot. → **Entrar depois do deploy do WIP atual** (§8.4),
   nunca no meio dele.
3. **Front e back acoplados pelas rotas `/tfd/config/*`.** → Deployar juntos, ou manter as
   rotas antigas respondendo por uma versão antes de removê-las.
4. **A migration precisa renomear também índices e constraints**, senão fica `ix_tfd_...`
   apontando para `mensagem_whatsapp` — que é a mesma sujeira, só que escondida.

### 10.6 Merece ADR

"Mensageria e geocodificação são infraestrutura compartilhada, não submódulos do TFD" é uma
decisão de fronteira de domínio, e é exatamente o tipo de coisa que se perde sem registro —
foi a ausência dela que criou o problema. Proposta: **ADR-0038**, já com o campo de
implantação da §8.5.

---

## 11. Pontas soltas — backlog vivo

> **Atualizado em 01/08/2026.** Esta é a seção para consultar antes de decidir o próximo passo.
> As de cima destravam as de baixo.

### 11.0 O que FOI resolvido (sai da lista)

| Item | Quando |
|---|---|
| **Rename TFD × mensageria/geo** — 6 tabelas, ADR-0038 | **em produção 31/07**, verificado id-a-id, 0 perda |
| **Drift do snapshot do EF no `main`** — declarava tabelas de regulação sem migration | corrigido no mesmo commit (`70c8405`) |
| **Inventário do Marco 1** e ADR-0038 documentados e commitados | 31/07 (`1d5bd46`) |
| **Bancada de ensaio de migration** (schemas no cluster do Maestro) | disponível para reuso |

### 11.1 Imediatas — destravam o resto

**A. Reconciliar a árvore principal.**
`C:\Projetos GIT\SMSMarica` ficou em `1d5bd46`; `origin/main` está em `70c8405`. Há ~252
arquivos não commitados, incluindo duplicatas do rename **que já estão no `main`**.
O cuidado: `SmsMaricaDbContext.cs` tem o bloco do rename **e** os DbSets de Regulação/PEP de
outras frentes — um `git checkout` cego perde o trabalho alheio. Fazer com instância única
aberta na pasta.

**B. Apagar o schema `backup_20260731_tfd`** depois de alguns dias de confiança. O backup CSV
em `%USERPROFILE%\Backups\SMSMarica\pre-rename-20260731-final\` pode ficar.

**C. Docker não roda nesta máquina.** 83 testes de integração (Testcontainers) não executam.
Enquanto isso, toda validação de banco depende da bancada do Maestro — que funciona, mas é
manual. Instalar Docker Desktop devolve a suíte inteira.

### 11.2 Entrega — código pronto que nunca foi ao ar

Cinco frentes seguem no working tree, **nenhuma commitada**. Cada uma vira uma sessão própria,
no método que funcionou: worktree limpo do `main` → ensaio na bancada → deploy → verificação.

| Frente | ADR | Migration pendente | Observação |
|---|---|---|---|
| Painel de Início | 0033/0034/0035 | `AddCausaFalhaImportacaoEIndicesPainel` | back + front completos |
| Sincronismo contínuo Salux + Internação | 0024/0025 | `AddPepSincronizacaoContinua` | **inerte no deploy**: o scheduler sai pelo `if (agendas.Count == 0) return`, e a tabela nasce vazia |
| Processo Regulatório | 0021 | `AddProcessoRegulatorio` | só entidades e máquina de estado; **0 endpoint, 0 tela** |
| Escopo de unidade fail-closed | 0037 | — | `Core/Common/Unidades` + testes |
| Trilha de falhas de importação | — | — | server + front |

**Schema `fhir` de produção parado em 16/06.** A migration `AddIdentifierClinicoELocation`
(Location + Encounter IMP) nunca foi aplicada. É outro serviço (`Automais.Fhir`), outro deploy,
outra solução — trata-se separadamente.

### 11.3 Governança — a causa-raiz das pontas soltas

**D. `docs/roadmap.md` está morto.** É de 22/04, descreve um produto de transporte em M1..M8 e
ainda tem `[ ] F1 (scaffold do front)` desmarcado com 47 features do front em produção.
Reescrever a partir da §2 (Marco 1 real) e da §7.

**E. ADR não tem status de implantação.** O ADR-0038 já nasceu com o campo
`Implantação: não iniciada | parcial | em produção`. Falta aplicar aos outros 37 e corrigir os
errados: 0020 está "Proposto" **estando em produção**; 0027/0028 "proposto" com código rodando;
0029–0032 "proposto" com **zero linha de código**.

**F. `docs/database.md` está desatualizado** — manda `removido_em` (o real é `excluido_em`),
prefixo `eh_`/`tem_` em booleano (o real é `ativo`), cita tabelas que não existem e afirma um
`DbContext` por módulo, revogado pelo ADR-0004. Foi ele que deixou nascer o `tfd_config_*`.
Reescrever com a convenção real, derivada das 93 tabelas.

**G. Numeração de ADR duplicada e uso indevido da pasta**: `0020-plano-fase1.md` e
`0021-runbook-deploy.md` não são decisões arquiteturais. Mover para `docs/runbooks/`.

**H. 33 branches já mergeadas** (locais e remotas) sem um único commit fora de `main`.

**I. Lixo no repositório**: `alterações entre sessoes/` (4 notas de sessão) e um arquivo de
scratchpad com nome corrompido na raiz — este último **tem conteúdo real** (fluxo do PACS),
então renomear, não apagar.

### 11.4 Produto — decisões suas

| # | Decisão | Contexto |
|---|---|---|
| **P1** | **Regulação (ADR-0021)**: terminar ou reverter? | 5 módulos de permissão reservados (47–51), 0 endpoint, 0 tela; o front já expõe só 3 e documenta a lacuna |
| **P2** | **Qualidade da importação SISREG** | 4.382 falhas contra 1.560 solicitações importadas — mais falhas que sucessos |
| **P3** | **ADRs 0029–0032** (capacidades, robô de triagem, intenção para voz) | 4 ADRs de papel puro: viram fila real ou marcam-se "adiado" |
| **P4** | **Mapeamento SISREG** (módulo 52) | tela pronta, 3 tabelas com 0 linhas: falta adoção ou falta função? |
| **P5** | **TFD e Agendamento local** | ficam no escopo (decidido 31/07); falta a definição para saírem do papel |

### 11.5 Herdadas do ADR-0038

**J. Chaves de configuração ainda dizem `Tfd:`** — `Tfd:Otp:ModoTeste`,
`Tfd:Otp:WhatsAppTemplate`, `Tfd:WhatsApp:Simular`, `Tfd:Cidadao:SessaoDias`. Mesmo vazamento
do prefixo, mas trocar exige atualizar o env do servidor **no mesmo instante**, senão a chave
nova lê o default em silêncio.

**K. `TfdConfigService` / rotas `/tfd/config/*` empacotam três domínios** (WhatsApp, Google
Maps, faturamento). Quebrar em três exige deploy coordenado back+front.

**L. `WhatsAppNotificador` mora em `Core/Notificacoes/`, mas é código do TFD** (fala de
`SessaoDeTratamento`). O lugar é `Core/Tfd/`. Cosmético, sem risco.

### 11.6 Lições que viraram regra

1. **O EF não infere rename.** `migrations add` gera `DropTable` + `CreateTable` e apagaria os
   dados. Toda migration que renomeia tabela é **escrita à mão** e tem o SQL conferido antes.
2. **O deploy não aplica migration** — o workflow só para o serviço, copia e sobe. Migration é
   passo manual. Conferir `smsmarica.__migrations` depois de todo deploy.
3. **Verificar por id, nunca por contagem.** O sistema está vivo durante a janela: a contagem
   cresce legitimamente e mascara perda.
4. **Ensaiar antes.** Worktree limpo do `main` + bancada com dados reais + teste de rollback.
   Foi o ensaio que revelou o drift do snapshot, que nenhuma inspeção de código teria mostrado.
5. **Push só do que se entende.** Empurrar 250 arquivos de 7 frentes junto é o oposto de tratar
   pendência a pendência.

---

*Levantamento de 31/07/2026; §11 atualizada em 01/08/2026 após o deploy do ADR-0038.*
*Números de produção medidos nas respectivas datas.*
