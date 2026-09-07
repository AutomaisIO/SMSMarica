# Runbook — encher o catálogo e migrar os rascunhos legados

> Quatro chamadas, na ordem. Todas exigem um usuário com o módulo **`RegulacaoConfiguracao` (51)**
> e podem ser feitas pelo **`/docs`** (Scalar) já autenticado, em `https://api.smsmarica.online/docs`.
>
> Escrito em 07/09/2026, depois que o deploy entregou as tabelas. **Nenhum destes passos foi
> executado** — dependem do OK e de uma sessão autenticada.

## Por que nesta ordem

O migrador casa cada rascunho do SER/SERNIT com um procedimento do catálogo canônico, pela chave
da origem. **Com o catálogo vazio, todos os rascunhos são recusados** com "recurso não existe no
catálogo canônico" — e o relatório pareceria um defeito do migrador, quando é só falta de dado.

Medido em produção em 06/09: `regulacao_procedimento` = **0 linhas**; rascunhos = **2 do SER**
(1 `Rascunho`, 1 `Pronto`), **0 do SERNIT**, **0 anexos**.

## 1. Encher o catálogo canônico

```
POST /regulacao/procedimentos/sincronizar
```

Lê os catálogos já espelhados (SER, SERNIT, SIGTAP do SISREG) — **não fala com sistema externo
nenhum** (D-11 preservada) — e gera os embeddings na Voyage.

⚠️ **Custo:** o primeiro sync gera **~560 embeddings pagos**, em 5 lotes. É a única chamada desta
lista que custa dinheiro.

Confirmar depois: `GET /regulacao/procedimentos/buscar?termo=cardiologia` deve devolver resultados.

## 2. Prévia da migração (não grava nada)

```
GET /regulacao/legado/rascunhos/previa
```

Devolve `{ ser, sernit, jaMigrados, foraDoEscopo, naoMigrados[], telasAntigasFechadasEm }`.
`naoMigrados[]` traz, por rascunho, o motivo exato. Os motivos possíveis e o que fazer:

| Motivo | O que fazer |
|---|---|
| "não existe no catálogo canônico" | rodar o passo 1 |
| "Nenhum paciente no cadastro com o CNS …" | cadastrar o paciente e repetir |
| "sem CNS do paciente" | o rascunho é irrecuperável por aqui; refazer pelo wizard |
| "O autor do rascunho não tem unidade vinculada" | vincular o autor a uma unidade, **ou** passar `unidadeFallbackId` no passo 3 |

## 3. Migrar

```
POST /regulacao/legado/rascunhos/migrar
{ "unidadeFallbackId": null, "fecharTelasAntigas": false }
```

Idempotente: rodar de novo só alcança o que ainda não migrou (índice único em `origem_legado_id`).
As tabelas legadas **não são tocadas** — o `DropTable` é de uma release posterior.

Conferir o resultado no banco:

```sql
select numero_local, status, fluxo, sistema_destino, unidade_solicitante_id, origem_legado_id
from smsmarica.regulacao_solicitacao
where origem_legado_id is not null;
```

## 4. Fechar as telas antigas

```
POST /regulacao/legado/rascunhos/migrar
{ "unidadeFallbackId": null, "fecharTelasAntigas": true }
```

Grava `regulacao_configuracao.rascunhos_legados_migrados_em`. A partir daí, "Nova solicitação
(SER)" e "(SERNIT)" abrem em somente-leitura com link para o wizard, e as rotas de escrita dos
rascunhos respondem **410**.

**A rota recusa fechar se `naoMigrados` não estiver vazio** — fechar com pendência deixaria alguém
sem acesso ao próprio trabalho.

## Desfazer

```
POST /regulacao/legado/rascunhos/reabrir
```

Devolve a escrita às telas antigas. As solicitações já migradas continuam existindo (não são
apagadas) — se for preciso refazer a migração do zero, apagar as linhas de
`regulacao_solicitacao` com `origem_legado_id is not null` **antes** de rodar de novo.

## Depois disto

Conceder o módulo **`Regulacao` (47)** a um perfil pela tela de Perfis — a ordem "push antes do
perfil" já está cumprida (as tabelas existem desde 06/09 23h). O agente regulador recebe **47 + 48**.
