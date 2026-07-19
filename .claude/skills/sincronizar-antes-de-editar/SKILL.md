---
name: sincronizar-antes-de-editar
description: Sincroniza o repositório local com o origin ANTES de ler, editar ou commitar qualquer coisa no SMSMarica. Use no início de toda sessão de trabalho no código, e sempre que o usuário disser "vamos mexer em X", "corrige o ticket #N", "altera a tela Y", "sobe isso", "commita", ou antes de abrir uma worktree. O repositório recebe commits de mais de uma origem (outra máquina do usuário, worktrees paralelas, o agente que roda no servidor), então a cópia local pode estar atrás sem nenhum sinal visível — editar por cima gera conflito, ou pior, desfaz silenciosamente uma correção que já estava em produção.
---

# Sincronizar antes de editar

A cópia local **não é** a fonte da verdade. O `origin` (`AutomaisIO/SMSMarica`) recebe commits
de várias origens: a máquina de trabalho do usuário, worktrees temporárias, e — quando o Agente
IA do servidor estiver ativo — commits feitos direto de lá, a partir do painel, sem passar por
esta máquina.

O risco não é o conflito de merge (esse o git avisa). É o **silencioso**: você lê um arquivo
desatualizado, conclui que o bug ainda existe, "corrige" de novo e desfaz a correção que já
estava rodando em produção. Do lado de fora isso aparece como regressão — e o ticket volta.

## Sempre, antes da primeira edição

```bash
cd "C:/Projetos GIT/SMSMarica"
git fetch origin
git status -sb        # a primeira linha mostra ahead/behind
```

Leia a primeira linha do `status -sb`:

| Saída | Significado | O que fazer |
|---|---|---|
| `## main...origin/main` | em dia | seguir |
| `## main...origin/main [behind N]` | **local atrasado** | `git pull --ff-only` antes de tocar em qualquer arquivo |
| `## main...origin/main [ahead N]` | tem commit local não enviado | ver o que é antes de continuar (`git log origin/main..HEAD --oneline`) |
| `## main...origin/main [ahead N, behind M]` | **divergiu** | parar e resolver com o usuário; não tente merge automático |

Com árvore suja e `behind`, guarde antes:

```bash
git stash push -u -m "wip antes do pull"
git pull --ff-only
git stash pop
```

## Ao terminar

Empurre no fim da tarefa, não no dia seguinte. Quanto mais tempo o commit fica só aqui, maior
a chance de outra origem mexer no mesmo arquivo:

```bash
git push
```

Se o push for rejeitado por `non-fast-forward`, alguém publicou no meio do caminho — **não use
`--force`**. Faça `git pull --rebase`, confira o que veio, e empurre de novo.

## Ao investigar um ticket

Antes de concluir que "o bug ainda existe", confirme que está olhando o código que está em
produção. O deploy publica a partir do `main` do `origin`, não da sua cópia:

```bash
git fetch origin && git log origin/main --oneline -5
```

Se o `origin/main` tem commits que você não tem, o que roda em produção **não é** o que você
está lendo.

## Cuidados

- **Nunca `git push --force`** neste repositório. Ele tem mais de um produtor de commits; um
  force apaga trabalho de outra origem sem aviso.
- **Não é só código que muda fora daqui.** Configuração de servidor (vhosts do nginx, certbot,
  WireGuard, `/etc/<serviço>/env`) e o **schema do banco** (migrations aplicadas à mão pelo
  runbook [ADR-0021](../../docs/adr/0021-runbook-deploy.md)) vivem só no servidor e **não** são
  capturados por `git pull`. Estar em dia com o `origin` não garante estar em dia com a produção.
- Worktrees (`_wt-*`) têm o próprio `HEAD`. Sincronize a que você está usando, não só a raiz.

> **Regra de manutenção:** se surgir uma nova origem de commits (outro agente, outro pipeline),
> acrescente aqui. Esta skill existe para que nenhuma delas seja surpresa.
