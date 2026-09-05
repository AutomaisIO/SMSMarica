# ADR-0055 — Um catálogo canônico de procedimentos une SISREG, SER e SERNIT; a busca é híbrida (lexical + vetorial no Postgres) e o pareamento entre sistemas é sugerido pela máquina e confirmado por pessoa

- **Status:** rascunho (promover no incremento 1)
- **Data:** 04/09/2026
- **Contexto:** ADR-0011 (módulo IA, onde o pgvector e os embeddings já vivem), memória do projeto "Procedimento: nome do SISREG é o eixo", `SugestaoSigtap` (só igualdade exata auto-confirma).
- **Documento de apoio:** planos 01 e 08.

## Contexto

Os três sistemas descrevem procedimentos em vocabulários sem chave comum: SISREG usa código `pa` de 7 dígitos com nome e SIGTAP defasado; SER e SERNIT usam texto com um `valor` de combo interno a cada instância, sem código e sem SIGTAP. O usuário quer digitar "cardiologia" e ver, numa tela só, as unidades internas que executam e se o procedimento existe no SER/SERNIT.

O pgvector já está instalado e em uso no módulo IA (`vector(1024)`, Voyage `voyage-3`), com o token cifrado no banco. `unaccent` está instalada; `pg_trgm` não.

## Decisão

### 1. Canônico + origens

`regulacao_procedimento` (o que o usuário vê como um procedimento) e `regulacao_procedimento_origem` (uma linha por sistema × recurso, com a chave externa de cada sistema). O embedding fica **na origem**, porque a busca precisa recordar cada rótulo como cada sistema o escreve.

### 2. Mesmo modelo de embedding do módulo IA

`IServicoEmbeddings` (Voyage `voyage-3`, 1024 dims), sem provedor novo. Só o que mudou é re-embedado (hash de `modelo|texto`). Falha do provedor não derruba a sincronização nem a busca.

### 3. Busca híbrida com fallback lexical

`ILIKE(unaccent())` primeiro (exatos e prefixos), depois `CosineDistance` com corte calibrado e cache da consulta → vetor. Se a Voyage falhar, a busca devolve só o lexical e se declara degradada. Índice HNSW criado em SQL cru.

### 4. Pareamento é curadoria

Origem nova vira canônico 1:1. Origem de outro sistema com cosine ≥ 0,85 vira **sugestão**; só a tela (módulo 51) confirma. Nunca se funde por similaridade — o mesmo princípio de `SugestaoSigtap`.

### 5. Oferta interna derivada, não armazenada

Unidades executantes vêm de `sisreg_escala` ativa e vigente por `pa` (grupos `…000` expandidos). O lado externo é existência por sistema; não há unidade executante em Maricá.

## Consequências

- Embedar a consulta do usuário é uma chamada HTTP por busca; debounce e cache são obrigatórios. Custo de catálogo inteiro: centavos.
- Dimensão travada em 1024 pelo tipo da coluna; trocar de modelo exige migration.
- O canônico não se amarra ao SIGTAP (o exportado é defasado); SIGTAP é atributo opcional.
- Regras de elegibilidade (plano 03) ancoram na **origem** quando vêm de manual de um sistema, e no canônico quando são gerais.

## Alternativas consideradas

- **Busca só lexical.** Não recorda sinônimos ("RX" × "radiografia"; "1ª vez em cardiologia" × "consulta em cardiologia"). Rejeitado como único caminho; fica como fallback.
- **Agente de IA a cada busca.** Rejeitado pela transcrição (custo e latência); embedding é o que ela pede.
- **Fundir automaticamente por similaridade.** Rejeitado: erro silencioso de pareamento manda paciente para o procedimento errado.

## Verificação

Buscar "cardiologia" retorna canônicos com origens dos três sistemas e unidades internas com vagas; desligar o token da Voyage mantém a busca (degradada); sugestão de par aparece na curadoria e só confirma com clique.
