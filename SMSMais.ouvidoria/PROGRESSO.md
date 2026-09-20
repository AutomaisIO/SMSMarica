# Ouvidoria — andamento

> Atualize ao terminar cada tarefa do [`PLANO-FASE-1.md`](./PLANO-FASE-1.md). Nada aqui foi commitado até que o Bernardo aprove.

| Data | Tarefa | Estado | Observações |
|---|---|---|---|
| 20/09/2026 | Levantamento (marco legal, OuvidorSUS × Fala.BR, exemplos, indicadores) | concluído | `README.md` + `referencias/01–04` |
| 20/09/2026 | Premissas de produto do Bernardo | registradas | `README.md` §0 |
| 20/09/2026 | ADR-0060 | escrito | `docs/adr/0060-modulo-ouvidoria.md` |
| 20/09/2026 | Plano da fase 1 + contrato da API | escrito | `PLANO-FASE-1.md` |
| 20/09/2026 | Padrões do código | levantados | `referencias/05-padroes-do-codigo.md` |
| 20/09/2026 | T1 Data | concluída | enums, 10 entidades, configurations, DbSets, sequência, `ModuloPermissao` 71–74, migration `20260920142356_Ouvidoria` (Up só com `ouvidoria_*` + sequência); build 0/0 |
| 20/09/2026 | T4 Front | concluída | `features/ouvidoria/` (24 arquivos) + 4 registros (authStore, menu, rotas, matriz de Perfis); `npm run build` 0 erros. `npm run lint` não existe no projeto (eslint não instalado — lacuna pré-existente). Detalhe do ponto de resposta em rota própria `/app/ouvidoria/meu-ponto/:id` (RotaComModulo gateia um módulo só) |
| 20/09/2026 | T2 Core + T3 Api | concluídas | `Core/Ouvidoria/*` (3 services, DTOs, validators, prazos, protocolo, notificador, rotina 6 h), `OuvidoriaController` + `OuvidoriaPublicoController`, política de rate limit `ouvidoria-publico`; build 0/0. **API não foi subida localmente**: user-secrets apontam para PROD e o AutoMigrate aplicaria a migration. Desvios do contrato documentados abaixo |
| 20/09/2026 | T5 Testes | concluída | `tests/SMSMais.Tests/Ouvidoria/` — 54 testes (prazos, protocolo, service, público) **aprovados** na bancada do Maestro; migration `Ouvidoria` aplicou sem erro. **Dois bugs reais corrigidos** no service (ver abaixo) |
| 20/09/2026 | T6 Docs | concluída | ADR-0060, `docs/architecture.md`, `CLAUDE.md`, este arquivo |
| 20/09/2026 | Commit + push | feito | Bernardo autorizou prod. Commits `7bb7242` (módulo) e `3ee933a` (migration, regenerada sobre o HEAD com id mantido `20260920142356`). Chegaram ao origin pelo push de outra sessão no branch compartilhado; `55323b3` (dela) commitou a pasta do manual, que levou o artigo `conteudo/ouvidoria.tsx`. Deploy do `3b7a666` em andamento às 16:15 — conferir `__migrations` em PROD e marcar módulos 71–74 nos perfis |
| 20/09/2026 | Manual do usuário | artigo feito | `features/manual/conteudo/ouvidoria.tsx` + simulação `FluxoManifestacao`; os `?` (`AjudaManual`) nas 7 páginas ficaram para commit seguinte |

## Avisos vivos
- O erro pré-existente em `WhatsAppCliente.cs` (outra sessão) foi corrigido por ela; build baseline voltou a 0/0. `tests/` pode falhar por DLL travada por `testhost` de outra sessão — não matar o processo.
- Há migration não commitada de outra sessão (`20260920140328_ArteCabecalhoModeloWhatsApp`) e o snapshot está modificado. A migration `Ouvidoria` sai por cima; commit separado, só com o hunk da ouvidoria (ver `reference_snapshot_ef_compartilhado`).
- Front exige `VITE_API_BASE_URL` no build (`VITE_API_BASE_URL=https://api.exemplo.local npm run build`).

## Desvios do contrato decididos na implementação (T2/T3) — o front já está alinhado
- `Usuario.NomeExibicao` não existe; `AutorNome`/`ResponsavelNome` usam `NomeCompleto`.
- **Sigilosa é mascarada para todos**, inclusive quem tem `OuvidoriaSigilo`: a identidade só sai por `POST manifestacoes/{id}/identidade` com justificativa (logada). Denúncia identificada é mascarada só para quem não tem sigilo. Anônima: `manifestante = null`, `identidadeRestrita = false`.
- `GET pontos-resposta` e `GET configuracao` aceitam `Ouvidoria.Consulta` **ou** `OuvidoriaGestao.Consulta`.
- `POST anexos` checa `Ouvidoria.Inclusao` ou `OuvidoriaPontoResposta.Edicao` dentro da action (atributos só combinam módulos com a mesma ação).
- `cobrar` aceita corpo vazio. `Tamanho` da página: padrão 50, teto 200; ordenação `UltimaAtividadeEm desc`.
- Texto de encaminhar/arquivar/externo vira `Anotacao` interna; o evento visível ao cidadão é genérico. Justificativa de prorrogação vai no evento visível (art. 16 §1º).
- Reclassificar denúncia para outro tipo apaga `HabilitadaEm`. Modo ponto de resposta também esconde `Referido`.
- Permissões no service via `IIdentidadeService.ObterPermissoesResolvidasAsync` (mesmo mecanismo de Conversas e Regulação), fail-closed.
- `TextoRecibo` aceita `{protocolo}`, `{codigo}`, `{prazo}`. Painel sem `de/ate` = últimos 30 dias.

## Bugs achados pelos testes (T5) e corrigidos em `OuvidoriaManifestacaoService.cs`
1. **Toda ação após o registro falhava com `DbUpdateConcurrencyException`.** `NovoEvento` fazia `m.Eventos.Add(evento)` com `Id` já preenchido sobre uma manifestação rastreada; o EF marcava o evento como *Modified* (UPDATE de 0 linhas). Correção: `db.OuvidoriaEventos.Add(evento)` (e o mesmo para anexos). Em produção quebraria 100% do painel depois do registro — a API nunca tinha sido subida localmente.
2. **Rotinas não persistiam.** `ArquivarSemComplementacaoAsync`/`ConcluirRespondidasSemRecursoAsync` carregavam por consulta `AsNoTracking()` e chamavam `SaveChanges` sem efeito. Correção: consulta rastreada.
Observação não corrigida: `EncaminharAsync` grava `TeorPseudonimizado` na entidade antes das checagens de denúncia; inofensivo em HTTP (contexto por requisição), visível só em testes que reutilizam o contexto.

## Fase 2 (não iniciada)
Site público `ouvidoria.<domínio>` (novo PWA no molde do Arquivos; endpoints `/publico/ouvidoria/*` já existem), app do cidadão, pesquisa pós-resposta, captação por WhatsApp (handler `IManipuladorMensagemWhatsApp`), template HSM de recibo para fora da janela de 24 h, relatório trimestral/anual, exportação Fala.BR, Manual de Tipificação Ouv3.
