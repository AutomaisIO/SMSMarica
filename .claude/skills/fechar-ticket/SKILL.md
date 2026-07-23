---
name: fechar-ticket
description: Conclui ou nega um ticket do Suporte do SMSMarica a partir do SERVIDOR (sem navegador/painel), carimbando o operador da sessão. Use quando o operador disser "fecha/conclui/nega o ticket #N", "encerra o chamado #N". Faz numa transação — comentário interno (report técnico) + conclusão/negação (RespostaFinal obrigatória) + registro de auditoria + arquivamento. É a forma server-native que substitui o passo por navegador do resolver-ticket quando você roda no servidor.
---

# Fechar (concluir/negar) um ticket — no servidor

Você roda **no servidor**, sem o navegador do painel — então o passo 3 do `resolver-ticket`
(token do Chrome) **não funciona aqui**. Faça direto no banco, espelhando fielmente o
`TicketService.AtualizarGestaoAsync` + `AdicionarComentarioAsync` + `ArquivarComoAdminAsync`, e
**gravando a auditoria** (que hoje o `TicketService` ainda não grava — é você que estabelece a
trilha, no formato do `AuditoriaService.RegistrarAsync`).

## Só com instrução explícita

Concluir/negar é escrita em produção e vira resposta ao autor. Faça **apenas** quando o operador
mandar nesta conversa. `RespostaFinal` é **obrigatória** para Concluido/Negado (o service lançaria
`ValidacaoException` sem ela) — linguagem simples ao autor, **sem jargão nem PII**. O detalhe
técnico vai no comentário interno.

## Carimbo = operador da sessão

`autor_id`, `atualizado_por` e a auditoria usam o **operador desta sessão** (`usuario_id` na seção
**"## Operador desta sessão"** do seu system prompt). Se a seção não existir, PARE e peça — nunca
use conta genérica (`admin`).

## Enums (int)

- `status`: 3=Concluido, 4=Negado (só estes fecham). `tipo` 1..4; `prioridade` 1..3.

## Execução (uma transação)

Conecte como na skill `acessar-banco-no-servidor`, depois:

```python
import uuid

NUMERO         = 42            # #N do ticket
OP_ID          = "<usuario_id do operador>"
NOVO_STATUS    = 3             # 3=Concluido, 4=Negado
RESPOSTA_AUTOR = "..."         # OBRIGATÓRIA; simples, sem jargão/PII (feedback ou justificativa)
REPORT_INTERNO = "..."         # detalhe técnico: causa, arquivos/commits, o que observar

assert RESPOSTA_AUTOR.strip(), "RespostaFinal é obrigatória para Concluido/Negado"

# 0) estado atual (idempotência)
cur.execute("SELECT id, status FROM smsmarica.ticket WHERE numero=%s AND excluido_em IS NULL", (NUMERO,))
row = cur.fetchone()
assert row, f"ticket #{NUMERO} não encontrado"
tid, status_atual = row
if status_atual in (3, 4):
    print(f"#{NUMERO} já está fechado (status={status_atual}) — nada a fazer")
else:
    # nome do operador na MESMA fonte da regra de negócio (usuario.nome_completo)
    cur.execute("SELECT nome_completo FROM smsmarica.usuario WHERE id=%s", (OP_ID,))
    op_nome = (cur.fetchone() or [None])[0]

    # 1) comentário interno (report técnico) — espelha AdicionarComentarioAsync(interno=true)
    cur.execute(
        """INSERT INTO smsmarica.ticket_comentario (id, ticket_id, autor_id, texto, interno, criado_em)
           VALUES (%s, %s, %s, %s, true, now())""",
        (str(uuid.uuid4()), tid, OP_ID, REPORT_INTERNO.strip()),
    )
    # 2) conclusão/negação — espelha AtualizarGestaoAsync
    cur.execute(
        """UPDATE smsmarica.ticket
             SET status=%s, resposta_final=%s, respondido_em=now(), resposta_reconhecida_em=NULL,
                 visto_pela_gestao_em=now(), atualizado_em=now(), atualizado_por=%s
           WHERE id=%s""",
        (NOVO_STATUS, RESPOSTA_AUTOR.strip(), OP_ID, tid),
    )
    # 3) auditoria — espelha AuditoriaService.RegistrarAsync (entidade "Ticket")
    acao = "Concluido" if NOVO_STATUS == 3 else "Negado"
    cur.execute(
        """INSERT INTO smsmarica.registro_auditoria
             (id, entidade, entidade_id, acao, valor_anterior, valor_novo, usuario_id, usuario_nome, ip, criado_em)
           VALUES (%s, 'Ticket', %s, %s, %s, %s, %s, %s, NULL, now())""",
        (str(uuid.uuid4()), str(tid), acao, f"status={status_atual}",
         f"status={NOVO_STATUS}; resposta enviada ao autor", OP_ID, op_nome),
    )
    # 4) arquivar (some da gestão, sem excluir) — espelha ArquivarComoAdminAsync
    cur.execute("UPDATE smsmarica.ticket SET arquivado_pelo_admin_em=now() WHERE id=%s", (tid,))

    conn.commit()
    print(f"#{NUMERO} {acao.lower()} + auditado + arquivado")
```

Tudo numa transação: se algo falhar antes do `commit()`, nada é aplicado (rode `conn.rollback()`).

## Depois

Reporte ao operador em 1 linha: **"#N concluído (ou negado), auditado e arquivado"** + o que foi
dado como resposta ao autor. Sem narrar cada `INSERT`.

## Cuidados

- Não conclua um ticket cujo tratamento não esteja realmente feito (ou que o operador não tenha
  aprovado). Dúvida sobre o desfecho → pergunte, não chute.
- `RESPOSTA_AUTOR` é lida por qualquer usuário: sem nome de tabela, stack trace, PII.
- Se for **negar**, `RESPOSTA_AUTOR` é a justificativa gentil; `NOVO_STATUS=4`.
