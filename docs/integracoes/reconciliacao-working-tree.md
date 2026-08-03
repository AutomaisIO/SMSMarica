# Reconciliação do working tree com o `origin/main` — mapa arquivo a arquivo (02/08/2026)

> **Status em 03/08:** a entrega NÃO usou o procedimento abaixo. Optou-se pelo caminho mais
> seguro — um **worktree limpo do `origin/main`** (`_wt-entrega-sync`), para onde só o
> trabalho da frente foi copiado, com as migrations **regeradas** ali para nascerem coerentes.
> O working tree principal segue **intocado**, ainda com Regulação, equipamentos,
> sisreg-mapeamento e tickets — e agora bem atrás do `main`, que andou vários commits.
>
> Este documento continua valendo como **mapa** (a classificação A/B/C/D e os achados são
> reais e verificados), mas o passo a passo precisa ser **regerado** antes de uso: as
> distâncias mudaram. O achado que mais importa aqui segue de pé: existe uma migration do
> rename **duplicada e morta** (`20260731214803`) no working tree principal, que jamais pode
> ser commitada — a boa é a `20260731230018`, já aplicada em produção.

Preparado para **revisão antes de qualquer commit**. Nada foi commitado, nada foi apagado.

## Por que existe

Este working tree (`C:\Projetos GIT\SMSMarica`) ficou numa linhagem **anterior** ao commit
`70c8405` (o rename mensageria/geo fora do TFD, ADR-0038 — em produção desde 31/07). O rename
foi feito e commitado a partir de um **git worktree separado** (`_wt-tfd-mensageria`), então
aqui os arquivos criados por ele nunca foram rastreados.

À primeira vista o `git diff origin/main` assusta: **126 arquivos, 4.925 inserções, 9.894
deleções**, incluindo a deleção da migration do rename que **está aplicada em produção**.
Commitar assim reverteria o ADR-0038. Por isso o deploy foi interrompido.

**Depois do mapeamento, o susto passa: não há conflito real.** As "deleções" são artefato de
arquivos *untracked*, que existem no disco e são bit-a-bit idênticos aos do `origin/main`.

## O mapa

| Grupo | Arquivos | O que é | Ação |
|---|---|---|---|
| **A. Trabalho novo** | **113** | só o working tree tem — as 5 frentes | **manter** (é o que vai virar commit) |
| **B. Untracked idênticos** | **9** | existem no disco, `git hash-object` **igual** ao `origin/main`; o git os lê como "deletados" só porque nunca foram rastreados aqui | **nenhuma** — o reset ao `origin/main` os torna "sem alteração" |
| **C. Ausentes no disco** | **2** | a migration do rename (`.cs` + `.Designer.cs`) | **restaurar do `origin/main`** |
| **D. Cirurgia** | **2** | `SmsMaricaDbContext.cs` e `SmsMaricaDbContextModelSnapshot.cs` | **manter a versão do working tree** — verificado abaixo |

### B — os 9 idênticos (verificados por hash)

```
Configurations/GeoConfiguracaoConfiguration.cs      Entities/Geo/GeoConfiguracao.cs
Configurations/GeoEnderecoConfiguration.cs          Entities/Geo/GeoEndereco.cs
Configurations/TfdConfiguracaoConfiguration.cs      Entities/Notificacoes/MensagemWhatsApp.cs
Configurations/WhatsAppConfiguracaoConfiguration.cs Entities/Notificacoes/WhatsAppConfiguracao.cs
Entities/Tfd/TfdConfiguracao.cs
```

### C — os 2 ausentes

```
Migrations/20260731230018_RenomeiaMensageriaEGeoForaDoTfd.cs
Migrations/20260731230018_RenomeiaMensageriaEGeoForaDoTfd.Designer.cs
```

Migration **já aplicada em produção** (é a última em `smsmarica.__migrations`). Vem inteira do
`origin/main`; o working tree não tem versão concorrente dela.

### D — os 2 que precisavam de análise (ambos aprovados)

**`SmsMaricaDbContext.cs`** — o diff contra o `origin/main` é **puramente aditivo**: 1 `using`
de Regulação e 7 `DbSet` novos (2 de PEP + 5 de Regulação). A única linha removida é o `using`
de Sisreg, reinserido em ordem alfabética. **Nada do rename se perde.**

**`SmsMaricaDbContextModelSnapshot.cs`** — é **superset estrito**:

| | Entidades |
|---|---|
| `origin/main` | 171 |
| working tree | **183** |
| no `origin/main` e **ausentes** no working tree | **0** |

As 12 a mais são de PEP e Regulação. O rename está presente e idêntico
(`MensagemWhatsApp → whatsapp_mensagem` nos dois). As "92 linhas removidas" do diff são
reordenação alfabética que o EF faz ao inserir entidades novas, não perda.

Confirmação independente do próprio EF:

```
dotnet ef migrations has-pending-model-changes
→ No changes have been made to the model since the last migration.
```

### O commit local `1afd148`

Mesma mensagem do `38abcc9` do `origin/main` (instâncias paralelas). Verificado por hash: os
**2 arquivos** que ele toca (`docs/marco-1-e-linha-de-corte.md` e `.html`) têm conteúdo
**idêntico** no `origin/main`. Descartá-lo não perde nada.

---

## Procedimento proposto

Pré-requisito: **uma única instância** aberta nesta pasta.

```bash
# 1. Ponto de retorno do estado atual (não commitado) — tarball fora do repo
git stash list                      # conferir que não há stash pendente
tar -czf ~/Backups/smsmarica-wt-20260802.tgz --exclude=node_modules --exclude=bin \
        --exclude=obj --exclude=.git .

# 2. Mover HEAD para origin/main SEM tocar no working tree.
#    --mixed reseta só HEAD e o índice; o disco fica intacto.
git reset --mixed origin/main

# 3. Restaurar os 2 arquivos do grupo C (a migration do rename)
git checkout -- SMSMarica.server/src/SMSMarica.Data/Migrations/20260731230018_RenomeiaMensageriaEGeoForaDoTfd.cs \
                SMSMarica.server/src/SMSMarica.Data/Migrations/20260731230018_RenomeiaMensageriaEGeoForaDoTfd.Designer.cs

# 4. Conferir: o status agora deve mostrar SÓ o trabalho novo.
#    Os 9 do grupo B somem (viram "sem alteração"); nenhuma deleção deve aparecer.
git status --porcelain | grep '^ D\|^D' || echo "OK: nenhuma deleção pendente"

# 5. Build das três soluções antes de commitar
```

Depois disso, commit **fatiado por frente** — cada um com seu escopo de caminhos:

| # | Frente | Caminhos | Deploy que dispara |
|---|---|---|---|
| 1 | Hub FHIR (upsert por identifier + Location + fail-fast) | `Automais.Fhir/**` | `deploy-fhir.yml` |
| 2 | Motor de sincronismo + conciliação de identidade | `SMSMarica.server/src/SMSMarica.Core/Integracoes/Pep/**`, `Data/Entities/Pep/**`, `Data/Configurations/Pep/**`, `Api/Controllers/PepSincronizacaoController.cs`, `tests/.../Integracoes/Pep/**`, front `features/pep-sincronizacao/**` | `deploy-server.yml` + `deploy-front.yml` |
| 3 | Painel de Início | ADR-0033/34/35 | idem |
| 4 | Processo Regulatório | `Data/Entities/Regulacao/**` | idem |
| 5 | Escopo fail-closed | `Core/Common/Unidades/**` | idem |
| 6 | Trilha de falhas + fix CID | — | idem |
| — | Documentação | `docs/**` | nenhum |

⚠️ **Os dois arquivos compartilhados** (`SmsMaricaDbContext.cs` e o snapshot) contêm hunks de
mais de uma frente. Commitar por hunk (`git add -p`) ou concentrá-los no primeiro commit de
servidor e referenciar nos demais.

⚠️ **Migration fora de ordem, ainda pendente de decisão** (§4.1 do plano): produção tem a
`20260725154214_AddSisregMapeamentoECredencialUnidade` aplicada mas **não** a
`20260725152753_AddProcessoRegulatorio`, que é anterior. Qualquer `database update` no servidor
vai arrastar as 5 migrations pendentes de uma vez — inclusive de frentes cujo código não estará
deployado. Tabela sem código é inofensiva, mas **decidir antes**, não durante o deploy.

## Estado do banco (já feito, não depende disto)

Saneamento das duplicatas, migration do hub e backfill dos identifiers **já foram aplicados em
produção em 02/08** e estão verificados — ver `plano-sincronismo-hub.md` §2.5 e §4. O deploy de
código é o que falta, e é só ele que depende desta reconciliação.
