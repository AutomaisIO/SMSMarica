# ADR-0023 — Bases remotas por agente proxy (WSS) e conhecimento orientado ao banco

**Status:** aceito · **Data:** 2026-07-24

## Contexto

O módulo IA consulta bases de dados read-only e responde perguntas em linguagem natural. Até aqui
uma base era alcançada **diretamente** pelo smsmarica (ex.: Oracle do Salux pelo túnel WireGuard).
Surgiram duas necessidades:

1. Consultar bases (ex.: SQL Server de outro prontuário) que o smsmarica **não alcança** e para as
   quais não se quer abrir porta de entrada nem guardar credenciais do banco na nuvem.
2. Alimentar a IA com o **modelo do banco** (tabelas, colunas, relacionamentos) e documentação
   curada por base, sem despejar tudo no prompt.

## Decisão

### 1. Agente proxy por WSS reverso

Uma base pode ser marcada **"via agente"**. Um agente Python roda no servidor de destino, **disca
para fora** até o smsmarica por WebSocket seguro, e executa as consultas contra o banco local.

- **Sem porta de entrada** no destino (conexão de saída na 443).
- **Credenciais do banco só no `.env` do destino** — o smsmarica nunca as vê. O agente autentica por
  um **token por base** (hash SHA-256, comparação em tempo constante), gerado no cadastro ou
  auto-provisionado pelo próprio agente no primeiro run (login do admin usado uma vez e descartado).
- **Read-only nos dois lados:** o smsmarica passa o SQL pelo `SqlReadOnlyGuard` antes de enviar; o
  agente guarda de novo antes de tocar no banco.
- **Sem driver de banco no smsmarica:** ele manda texto SQL e recebe linhas. Serve para qualquer
  banco que o agente alcance (SQL Server, MySQL, …).

Peças: endpoint WSS `/agentes/sql`, `IAgenteSqlRegistry` (singleton em memória), `ProxyAgenteFonte`
(`IFonteDados`), colunas `via_agente`/`agente_token_hash` em `ia_fonte`, endpoint de
provisionamento. Contrato do agente em `docs/proxy-sql-agente-wss.md`.

### 2. Conhecimento orientado ao banco

O sistema de conhecimento (RAG por fonte, já existente) ganha:

- **Extração automática do modelo:** lê o schema pela conexão configurada (inclusive pelo agente),
  por dialeto (SQL Server / Postgres / Oracle), e gera **um documento markdown por tabela**
  (colunas + relacionamentos) mais um **catálogo**. Sem truncar em silêncio — há teto de tabelas e
  aviso do que ficou de fora.
- **Documentos geridos pela tela** (não só arquivos do repo): regras de negócio, relacionamentos que
  o schema não expressa, exemplos de SQL. Guardados no banco (`manual/…`), re-fatiados e re-embeddados
  ao salvar.
- O modelo extraído **não vai inteiro no prompt** — vira documentos recuperáveis por RAG. Bases
  grandes (milhares de tabelas) devem rodar com embeddings ligados; o modo sem embeddings passou a
  incluir os docs do banco (com o alerta de que concatena tudo).

Separação de propósito: **"IA pura"** = perguntar sobre dados; **"Gestão de bases"** = configurar a
conexão/tipo, levantar a estrutura, e curar a documentação `.md` por banco.

## Consequências

- O smsmarica hospeda o WSS dos agentes; o nginx encaminha o upgrade em `/agentes/sql` (como já faz
  em `/hubs/`).
- Um agente por processo/base; a lista de conexões vive em memória (processo único). Se o servidor
  reiniciar, os agentes reconectam.
- A extração de modelo é uma consulta pesada (lê o schema inteiro) — roda sob demanda, sem retry.
- Pendente (fases seguintes): loop de correções → interpretação que atualiza os `.md`; e tratar cada
  pergunta da IA pura como sessão.
