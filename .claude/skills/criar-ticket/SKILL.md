---
name: criar-ticket
description: Abre (cria) um ticket do módulo Suporte do SMSMarica direto no banco de produção, carimbado com o operador da sessão. Use quando o operador pedir "abre um ticket", "cria um chamado", "registra isso como ticket", ao FIM de um diagnóstico para deixar um ticket para avaliação/aprovação, ou SEMPRE que um operador SEM autorização de escrita pedir algo que exige mudança (código, deploy, configuração, dado) — nesse caso ofereça e abra o ticket com o pedido + diagnóstico já estruturados, em nome dele. Espelha a regra de negócio de AbrirAsync (status Aberto, prioridade Normal, criado_por = operador). NÃO conclui nem responde ticket — para isso é a skill fechar-ticket.
---

# Criar (abrir) um ticket

Cria um `smsmarica.ticket` espelhando o `TicketService.AbrirAsync`. O `numero` (referência
humana "#N") é **identity do Postgres** — gerado sozinho, não se informa.

## Quem é o autor (carimbo)

O `criado_por` é o **operador desta sessão** — o `usuario_id` está no seu system prompt, seção
**"## Operador desta sessão"**. Use esse Guid. **Se essa seção não existir, PARE e peça o
`usuario_id` ao operador** — nunca use uma conta genérica.

## Enums (int no banco)

- `tipo`: 1=Bug, 2=Mudanca, 3=Sugestao, 4=Duvida
- (o skill fixa `status`=1 Aberto e `prioridade`=2 Normal, como o AbrirAsync)

## Inserir

Conecte como na skill `acessar-banco-no-servidor`, depois:

```python
import uuid

OP_ID   = "<usuario_id do operador — seção 'Operador desta sessão'>"
TITULO  = "..."   # <= 200 chars, objetivo
DESCRICAO = "..." # o problema/pedido em texto claro
TIPO    = 1       # 1=Bug 2=Mudanca 3=Sugestao 4=Duvida

cur.execute(
    """INSERT INTO smsmarica.ticket
         (id, titulo, descricao, tipo, status, prioridade, criado_em, criado_por)
       VALUES (%s, %s, %s, %s, 1, 2, now(), %s)
       RETURNING numero""",
    (str(uuid.uuid4()), TITULO[:200], DESCRICAO.strip(), TIPO, OP_ID),
)
numero = cur.fetchone()[0]
conn.commit()
print(f"ticket #{numero} criado")
```

`unidade_id` fica nulo (o AbrirAsync usa a unidade ativa do autor; aqui normalmente não se
aplica). `criado_em` = `now()` (instante UTC).

## Depois

Reporte ao operador só o essencial: **"Abri o ticket #N"** + link `/app/tickets/gestao/{id}` se
tiver o id (o `RETURNING` também aceita `, id`). Sem narrar cada passo.

## Pedido de mudança vindo de operador SEM autorização de escrita

Quando o operador da sessão (que não é o administrador) pedir algo que exigiria alterar
código, deployar, mudar configuração ou dado: **não recuse e abandone** — proponha na hora
"posso abrir um ticket com isso para o administrador tratar?" e, com o sim, abra o ticket
**em nome dele** com a descrição já estruturada:

1. **O pedido** — o que o operador precisa, nas palavras dele (sem jargão);
2. **Contexto** — tela/módulo, quando acontece, quem é afetado;
3. **Diagnóstico já feito** — o que você investigou nesta conversa (resumo; o detalhe
   técnico vira comentário interno depois, se houver).

`TIPO`: mudança/melhoria = 2, defeito confirmado = 1. Responda ao operador com o `#N` e diga
que o administrador (Bernardo) vai avaliar.

## Cuidados

- Um ticket = um assunto. Não junte pedidos não relacionados.
- Título e descrição vão ser lidos por gente não-técnica: claro, sem stack trace, **sem PII**
  (nome/CPF/CNS de paciente). Detalhe técnico, se precisar, entra como comentário interno depois.
- Criar ticket é a **única escrita** permitida quando você está em modo diagnóstico (operador que
  não é o Bernardo): você investiga e deixa o ticket para ele avaliar — não altera mais nada.
