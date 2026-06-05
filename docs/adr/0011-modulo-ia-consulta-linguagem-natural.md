# ADR-0011 — Módulo IA: consulta em linguagem natural multi-alvo

- **Status**: Aceito
- **Data**: 2026-06-04
- **Decisores**: Bruno (product/eng)
- **Relaciona-se com**: [ADR-0001](./0001-schema-isolation.md) (schema único de negócio), [ADR-0007](./0007-schema-fhir-separado.md) (separação `smsmarica`/`fhir`), [ADR-0010](./0010-servico-fhir-autonomo.md) (serviço FHIR autônomo), [visao.md](../visao.md)

## Contexto

A Secretaria precisa **analisar dados em tempo real sem depender da TI**. Hoje, qualquer pergunta de gestão ("quantos pacientes da fila de hemodiálise faltaram esta semana?", "lista dos exames laudados ontem por unidade") exige alguém escrever SQL contra o Salux Oracle ou contra o `smsmarica`, o que cria fila no time técnico e atrasa decisão.

O alvo é o **usuário leigo**: ele escreve a pergunta em pt-BR e recebe a resposta **abstraída** — um número, uma lista, uma tabela ou um gráfico — sem ver SQL, sem saber em que banco a resposta mora, sem saber se o dado veio do Salux ou do `smsmarica`. A inteligência precisa traduzir linguagem natural → consulta executável, rodar a consulta no(s) alvo(s) certo(s), e devolver o resultado já formatado.

Três restrições moldam a decisão:

1. **Os dados clínicos de verdade moram no Salux Oracle** (memória `reference_salux_clinico_infosaude`), que é **read-only intocável em PRODUCAO** (memória `feedback_salux_oracle_producao`) — só `SELECT`, e de preferência contra TREINAMENTO para experimentos.
2. **Não pode virar mais um schema/banco** — ADR-0001 e ADR-0007 já fixaram dois schemas (`smsmarica`, `fhir`); criar um terceiro para a IA seria sprawl sem ganho.
3. **A IA tem que evoluir sozinha** — quando erra uma query, deve aprender com a correção e não repetir o erro, mas de forma **rastreável e reversível** (governança), nunca uma caixa-preta que muda comportamento sem trilha.

## Decisão

### 1. Dois menus, dois módulos de permissão

A IA aparece para o usuário como **dois menus distintos**, cada um atrás de um módulo RBAC próprio (`ModuloPermissao`, já existente):

| Menu | Módulo de permissão | Para quem |
|---|---|---|
| **IA** (perguntar) | `Inteligencia` (19) | usuário de gestão que faz perguntas em linguagem natural |
| **Configuração de IA** | `InteligenciaConfiguracao` (20) | administrador que cadastra token, bases e governa o aprendizado |

A separação é deliberada: perguntar é uso operacional amplo; configurar/governar (token do provedor, hosts de banco, edição do que a IA aprendeu) é poder sensível e fica num módulo à parte. Quem tem `Inteligencia` **não** ganha `InteligenciaConfiguracao` por arrasto.

O **menu de Configuração** cobre:
- **Token genérico de IA** — credencial do provedor (Anthropic) e do provedor de embeddings (Voyage AI), gravados cifrados (ver §6).
- **CRUD de bases de dados** — cada base é um alvo com `host`, `ambiente` (`PRODUCAO`/`TREINAMENTO`) e tipo de fonte. Permite, por exemplo, cadastrar o Salux TREINAMENTO e o Salux PRODUCAO como bases distintas.

O **menu de perguntar** oferece um **dropdown multi-seleção de bases**: o usuário escolhe contra qual(is) base(s) cadastradas a pergunta roda. Multi-alvo desde o desenho — uma pergunta pode mirar mais de uma fonte.

### 2. Arquitetura: abstração `IFonteDados`, primeiro alvo Salux Oracle

A consulta é executada contra uma **abstração `IFonteDados`** — um alvo **executável e read-only**. Cada base cadastrada (§1) resolve para uma implementação de `IFonteDados`. O contrato é mínimo: receber uma consulta gerada pela IA, executá-la **somente leitura**, devolver linhas. Isso desacopla "qual provedor de IA gera a query" de "onde a query roda".

**Primeiro alvo: Salux Oracle.** O `SMSMarica.server` conecta **direto** ao Oracle (driver Oracle managed), **sem worker, sem fila, sem componente on-prem**. O server alcança a rede do Oracle Salux, então não há motivo para intermediar com um agente on-premises consumindo fila (ver Alternativas). Próximos alvos previstos: o próprio `smsmarica` (Postgres) e o hub FHIR (via API do `Automais.Fhir`, ADR-0010) — cada um uma implementação de `IFonteDados`.

### 3. Provedor de IA e provedor de embeddings configuráveis

- **Geração de consulta / resposta**: provedor configurável, primeiro alvo **Anthropic Messages API**. Usa-se **saída estruturada** (tool use / structured output) para a IA devolver a query e o tipo de visualização (número/lista/tabela/gráfico) em formato parseável, e **prompt caching** para amortizar o custo do contexto fixo (schema das bases, exemplos, conhecimento) que repete entre perguntas.
- **Embeddings**: a Messages API **não** expõe embeddings. Para RAG sobre o conhecimento e sobre perguntas passadas, usa-se um **provedor de embeddings separado — Voyage AI** — com vetores guardados em **pgvector** no Postgres do `smsmarica`.

Ambos os provedores são configuráveis (a abstração não amarra Anthropic/Voyage para sempre), mas são os alvos iniciais.

### 4. Dados: tabelas `ia_*` no schema `smsmarica` (sem 3º schema)

Toda a persistência do módulo IA vive no schema **`smsmarica`** com **prefixo `ia_`** — **não** se cria um terceiro schema (respeita ADR-0001 e ADR-0007). As tabelas (nomes indicativos):

- `ia_base` — base de dados cadastrada (host, ambiente, tipo de fonte, segredos cifrados).
- `ia_conhecimento` / `ia_conhecimento_chunk` — base de conhecimento da IA: **arquivos `.md` versionados no repositório** (fonte de verdade), **espelhados no banco** em chunks com seus **embeddings** (pgvector). O `.md` no repo é o canônico; o banco é o índice consultável. Reindexar = reprocessar os `.md`.
- `ia_aprendizado` — correções que a IA incorporou e passou a usar (ver §5).
- `ia_correcao` — histórico das correções aplicadas (ver §5).
- `ia_pergunta` / `ia_consulta` — trilha de perguntas feitas e queries executadas (auditoria de uso).

O conhecimento ser `.md` versionado dá **revisão por PR** e diff legível; o espelho no banco dá **busca semântica** em runtime. As duas representações são mantidas em sincronia por um passo de indexação.

### 5. Governança: auto-correção vira aprendizado rastreável e removível

Quando uma query gerada pela IA **falha** (erro de sintaxe, coluna inexistente, etc.), o módulo tenta **auto-corrigir** (re-prompt com a mensagem de erro). Se a correção produz uma query válida:

1. A correção é promovida a **`ia_aprendizado`** com `Origem = Auto` e **`ativo = true`** — passa a influenciar gerações futuras (entra no contexto/RAG).
2. A mesma correção é registrada em **`ia_correcao`** — um **histórico à parte da auditoria de uso**, que guarda o antes/depois e o motivo.

Essa dupla escrita é o coração da governança: o aprendizado é **rastreável** (sempre dá pra ver de onde veio um comportamento) e **removível** (um administrador, via menu de Configuração, desativa `ia_aprendizado` ruim ou apaga a `ia_correcao`). A IA evolui sozinha, mas nunca de forma opaca ou irreversível. `ia_correcao` é separada da auditoria comum (`criado_em`/`criado_por`...) de propósito: é trilha de **comportamento do modelo**, não de CRUD de entidade.

### 6. Segurança / LGPD

**Read-only absoluto no Salux** — defesa em profundidade:
- Conexão por **conta de banco read-only** (modelo `salux_obs`, memória `reference_salux_observador`).
- **Guard de único `SELECT`** — a query gerada é validada para ser uma e só uma instrução `SELECT`; qualquer DML/DDL/múltiplos statements é rejeitado antes de tocar o Oracle.
- **Timeout** de execução e **cap de linhas** retornadas (proteção contra query que varre a base inteira).
- **Preferir TREINAMENTO** — bases de experimentação miram o Oracle de treinamento; PRODUCAO só quando o dado real é indispensável (memória `feedback_salux_oracle_producao`).

**Segredos**:
- Token de IA, token de embeddings e credenciais de banco são gravados **cifrados** via `IDataProtector` (ASP.NET Core Data Protection).
- Campos de segredo são **write-only** na API: o front envia, nunca lê de volta o valor em claro.

**PII em embeddings**: as **perguntas** dos usuários podem conter dados pessoais (nome de paciente, CPF). Como perguntas viram embeddings (Voyage) e podem ser persistidas para RAG, há atenção explícita a PII — minimização/anonimização antes de embeddar e/ou retenção controlada são tratadas na implementação, não deixadas implícitas.

## Alternativas consideradas

### A. Worker on-prem consumindo fila para alcançar o Oracle
**Prós:** isolaria a rede do Salux; o server na nuvem nunca falaria direto com o Oracle. **Contras:** o `SMSMarica.server` **já alcança** o Oracle Salux na rede — a fila + worker on-prem adicionaria latência, mais um deployable, mais um ponto de falha, sem resolver problema real. **Rejeitada** — o server conecta direto (§2). (Contrasta com a importação clínica on-prem→hub da memória `reference_salux_clinico_infosaude`, que existe porque *aquele* caminho — nuvem alcançando Oracle para *escrever no hub* — tinha outra topologia; aqui é leitura direta pelo próprio server.)

### B. Embeddings locais (modelo on-prem) em vez de Voyage AI
**Prós:** **LGPD** — perguntas com PII nunca sairiam da infra da Secretaria; sem custo por token de embedding. **Contras:** operar um modelo de embedding local (GPU, versionamento, qualidade inferior à Voyage) é peso extra agora. **Adiada** — fica como alternativa concreta de LGPD se a análise de PII em perguntas exigir que nada saia da rede. A abstração de provedor de embeddings (§3) torna a troca barata.

### C. Terceiro schema (`ia`) dedicado
**Prós:** isolamento de naming. **Contras:** contraria ADR-0001/0007 (que já fixaram exatamente dois schemas) sem ganho — prefixo `ia_` em `smsmarica` dá o agrupamento desejado. **Rejeitada.**

### D. Servidor FHIR/Salux pronto + BI tradicional (Metabase/Superset)
**Prós:** ferramentas maduras de dashboard. **Contras:** exigem o usuário montar a consulta/dashboard — exatamente o que a Secretaria **não** consegue fazer sem TI. O diferencial pedido é **linguagem natural → resposta abstraída**, não um construtor de gráficos. **Rejeitada** para o caso de uso; BI tradicional pode coexistir depois.

## Consequências

### Positivas
- Gestão pergunta em pt-BR e recebe número/lista/tabela/gráfico sem fila na TI.
- Multi-alvo desde o desenho (`IFonteDados`): Salux hoje, `smsmarica`/FHIR amanhã, sem reescrever o núcleo.
- A IA melhora sozinha (auto-correção → `ia_aprendizado`) mas de forma **auditável e reversível** (`ia_correcao` + toggle `ativo`).
- Conhecimento em `.md` versionado: revisão por PR, diff legível, reindexação reprodutível.
- Sem 3º schema, sem banco novo, sem worker on-prem — encaixa na infra atual.

### Negativas
- O `SMSMarica.server` passa a depender de provedores externos (Anthropic, Voyage) — custo por token, latência, e disponibilidade desses serviços viram preocupação operacional (mitigado por prompt caching).
- Conectar direto ao Oracle Salux a partir do server acopla o server à rede/credenciais do Salux; a quebra dessa rede degrada o alvo Salux.
- LGPD em aberto: perguntas com PII indo para a Voyage exigem disciplina de minimização (ver §6, Alternativa B). É dívida consciente, não esquecimento.
- Mais uma superfície de segredos (tokens de IA) a gerenciar e rotacionar.

### Condições para revisitar
- Se a análise de PII em perguntas concluir que nada pode sair da rede da Secretaria, ativar Alternativa B (embeddings locais).
- Se o server deixar de alcançar o Oracle Salux diretamente (mudança de topologia de rede), reavaliar Alternativa A (worker on-prem por fila).
- Se surgir alvo cujo acesso read-only não seja garantível por conta de banco + guard de `SELECT`, reavaliar o contrato `IFonteDados`.

## Enforcement
- Persistência da IA vive em `smsmarica.ia_*`; **nenhum** schema novo. Code review rejeita schema `ia` dedicado.
- Toda execução contra um alvo passa pelo guard read-only (único `SELECT` + timeout + cap de linhas). Code review rejeita caminho que monte/execute DML/DDL ou múltiplos statements.
- Conexão ao Salux usa conta read-only; experimentos miram TREINAMENTO por padrão.
- Segredos (tokens de IA/embeddings, credenciais de base) cifrados via `IDataProtector` e **write-only** na API. Nunca retornar segredo em claro.
- Auto-correção sempre grava o par `ia_aprendizado` (Origem=Auto, ativo) **e** `ia_correcao` (histórico) — comportamento da IA tem que ser rastreável e removível.
- Menu/endpoints de perguntar exigem `Inteligencia`; menu/endpoints de configurar/governar exigem `InteligenciaConfiguracao`. Não conceder um por arrasto do outro.
- Conhecimento canônico são os `.md` versionados no repo; o espelho no banco (chunks + embeddings) é derivado e reprodutível por reindexação.
