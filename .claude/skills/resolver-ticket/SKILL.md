---
name: resolver-ticket
description: Resolve um ticket do módulo Suporte do SMSMarica ponta a ponta. Use quando o usuário disser "resolver ticket #N", "resolve o ticket N", "atender/tratar o chamado #N". Lê o ticket pelo NÚMERO (descrição + conversa completa, incluindo comentários INTERNOS não visíveis ao autor), trata a causa (investiga/implementa/testa/deploya com OK), e ao final CONCLUI o ticket com resposta simples para o autor (enduser), report técnico interno (backstage) e ARQUIVA.
---

# Resolver ticket do Suporte (#N)

Fluxo completo: **ler → tratar → concluir → arquivar**. O `#N` é o número curto
(coluna `numero`, ex.: `#12`), não o Guid.

## 1. Ler o ticket

Direto do banco (SELECT; conexão do user-secrets `smsmarica-api-dev` = Postgres de prod
— ver memória "Consultar o DB de PROD direto"). Script padrão no scratchpad:

```python
import json, os, psycopg2
caminho = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
segredos = json.load(open(caminho, encoding="utf-8-sig"))
cs = next(v for k, v in segredos.items() if k.lower().startswith("connectionstrings") and isinstance(v, str))
m = {k.strip().lower(): v.strip() for k, v in (p.split("=", 1) for p in cs.split(";") if "=" in p)}
conn = psycopg2.connect(host=m["host"], port=m.get("port", 5432), dbname=m["database"],
                        user=m.get("username") or m.get("user id"), password=m["password"], sslmode="require")
cur = conn.cursor()

NUMERO = 12  # <- nº do ticket
cur.execute("""
    SELECT t.id, t.numero, t.titulo, t.descricao, t.tipo, t.status, t.prioridade,
           t.resposta_final, t.criado_em, u.nome_completo AS autor
    FROM smsmarica.ticket t
    LEFT JOIN smsmarica.usuario u ON u.id = t.criado_por
    WHERE t.numero = %s AND t.excluido_em IS NULL
""", (NUMERO,))
print(cur.fetchone())

cur.execute("""
    SELECT c.criado_em, c.interno, u.nome_completo, c.texto
    FROM smsmarica.ticket_comentario c
    LEFT JOIN smsmarica.usuario u ON u.id = c.autor_id
    WHERE c.ticket_id = (SELECT id FROM smsmarica.ticket WHERE numero = %s)
    ORDER BY c.criado_em
""", (NUMERO,))
for r in cur.fetchall():
    print(("[INTERNO] " if r[1] else "") + f"{r[0]:%d/%m %H:%M} {r[2]}: {r[3]}")

cur.execute("""
    SELECT nome_arquivo FROM smsmarica.ticket_anexo
    WHERE ticket_id = (SELECT id FROM smsmarica.ticket WHERE numero = %s)
""", (NUMERO,))
print("anexos:", [r[0] for r in cur.fetchall()])
```

Enums (int no banco): tipo 1=Bug 2=Mudanca 3=Sugestao 4=Duvida; status 1=Aberto
2=EmAnalise 3=Concluido 4=Negado; prioridade 1=Baixa 2=Normal 3=Alta.

**Os comentários INTERNOS (não visíveis ao autor) são o detalhamento técnico da
equipe — trate-os como contexto prioritário do chamado.** Anexos (prints) podem ser
citados pelo nome; se precisar VER a imagem, pergunte ao usuário ou abra o ticket no
painel (`/app/tickets/gestao/{id}`) com o navegador.

## 2. Tratar

Conforme o tipo:
- **Bug** → reproduzir/diagnosticar no código, implementar o fix, build 0/0 (`dotnet build`)
  + `npx tsc -b` no front, testes quando houver.
- **Dúvida** → só investigar e responder; normalmente sem código.
- **Sugestão/Mudança** → se o escopo for pequeno e claro, implementar; se for grande ou
  ambíguo, alinhar com o usuário ANTES de codar.

Regras de produção do projeto valem integralmente: **commit/deploy/migration só com OK
explícito do usuário**; migrations não são aplicadas pelo deploy (aplicar manual e
conferir `smsmarica.__migrations`). Não concluir o ticket antes do tratamento estar
EM PROD (ou de o usuário concordar que a resposta basta, ex.: Dúvida).

## 3. Concluir + arquivar (nesta ordem)

Autenticação: sessão do painel no Chrome — abrir/usar aba em `https://smsmarica.online/app`
e, via `javascript_tool`, ler o token de `localStorage['smsmarica.auth']` e chamar a API
pública (`https://api.smsmarica.online`). JSON em camelCase; enums como string.

1. **Report técnico interno** (backstage — o autor NÃO vê):
   `POST /tickets/gestao/{id}/comentarios` body `{"texto": "...", "interno": true}`
   Conteúdo: causa raiz, arquivos/commits/migrations envolvidos, decisões tomadas,
   efeitos colaterais e o que observar depois. Pode ter jargão.
2. **Concluir com resposta ao autor** (enduser — linguagem SIMPLES, sem jargão):
   `PUT /tickets/gestao/{id}` body `{"status": "Concluido", "respostaFinal": "..."}`
   A respostaFinal é obrigatória para Concluido (o back valida). 2–5 frases: o que
   mudou NA PRÁTICA para ele, onde ver/como usar, e agradecimento pelo report.
   Ex.: "Pronto! Agora o botão Anamnese fica verde quando o questionário já foi
   preenchido. Basta recarregar a tela uma vez. Obrigado por avisar!"
3. **Arquivar** (some da gestão, sem excluir):
   `POST /tickets/gestao/{id}/arquivar?arquivar=true` (sem body)

## 4. Reportar ao usuário (no chat)

Resumo curto: ticket #N concluído e arquivado, o que foi tratado (1 linha), a resposta
dada ao autor e o link `/app/tickets/gestao/{id}`. Se algo ficou de fora do escopo do
ticket, sugerir abrir outro ticket em vez de esticar este.

## Cuidados

- Ticket que depende de decisão de produto → perguntar ao usuário antes; não chutar.
- Se o pedido do ticket for INVÁLIDO/não fazível, propor `status: "Negado"` com
  justificativa gentil na respostaFinal (também obrigatória) — só com aval do usuário.
- Nunca expor detalhes técnicos/PII na respostaFinal (o autor pode ser qualquer usuário).
- Um ticket = um assunto: não agrupar fixes não relacionados na mesma conclusão.
