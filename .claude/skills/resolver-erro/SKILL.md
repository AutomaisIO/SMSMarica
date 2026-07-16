---
name: resolver-erro
description: Marca um erro do log do sistema (ERRO-XXXXXX) como RESOLVIDO no banco de PROD, com data + autor + nota. Use quando o usuário disser "resolvi o ERRO-XXXX", "marca o ERRO-XXXX como resolvido", "esse erro já foi corrigido", ou logo após você corrigir/deployar o fix de um erro que apareceu no módulo Sistema → Erros. Registra resolvido_em (agora), resolvido_por e resolucao_nota; a partir daí uma reincidência do MESMO erro gera um código novo (sinal de regressão).
---

# Marcar erro como resolvido (ERRO-XXXXXX)

O log de erros (`smsmarica.registro_erro`) deduplica erros idênticos: erros com a mesma
`assinatura` **em aberto** (`resolvido_em IS NULL`) reusam o mesmo código e apenas somam
`ocorrencias`. Marcar como **resolvido** fecha o registro; se o mesmo erro voltar depois,
o dedup NÃO o absorve → nasce um código novo (regressão detectável).

Use esta skill **depois** de tratar a causa do erro (idealmente com o fix já deployado),
para anotar que foi resolvido e **quando**.

## Regras

- **PROD é real** ([[feedback_producao_confirmar_antes]]) — mas esta operação **não destrói
  dados** (só seta `resolvido_em/resolvido_por/resolucao_nota` de um registro de log). Ainda
  assim, confirme o código antes de aplicar.
- `resolvido_por`: por padrão, uma referência curta ao que resolveu — o **hash do commit** do
  fix (`git rev-parse --short HEAD`) e/ou "Claude". Se o usuário der um nome, use o dele.
- `resolucao_nota`: 1-2 linhas do que foi feito (ex.: "GroupBy sobre navegação não traduzia;
  passou a projetar e agrupar em memória. commit f6b9104").
- Aceita **vários códigos** de uma vez (mesma nota) quando o usuário listar mais de um.

## Aplicar (SELECT para conferir → UPDATE)

Conexão do user-secrets `smsmarica-api-dev` = Postgres de PROD (ver memória
[[reference_consultar_db_prod]]). Rode no scratchpad:

```python
import json, os, psycopg2, datetime as dt
caminho = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
segredos = json.load(open(caminho, encoding="utf-8-sig"))
cs = next(v for k, v in segredos.items() if k.lower().startswith("connectionstrings") and isinstance(v, str))
m = {k.strip().lower(): v.strip() for k, v in (p.split("=", 1) for p in cs.split(";") if "=" in p)}
conn = psycopg2.connect(host=m["host"], port=m.get("port", 5432), dbname=m["database"],
                        user=m.get("username") or m.get("user id"), password=m["password"], sslmode="require")
cur = conn.cursor()

CODIGOS = ["ERRO-XXXXXX"]                 # <- um ou mais códigos
RESOLVIDO_POR = "Claude · commit <sha>"  # <- quem/como resolveu
NOTA = "<o que foi feito>"               # <- 1-2 linhas

# 1) Confere o(s) alvo(s) antes de escrever.
cur.execute("""
    SELECT codigo_referencia, ocorrencias, criado_em, ultima_ocorrencia_em,
           resolvido_em, status_code, metodo, caminho, mensagem
    FROM smsmarica.registro_erro
    WHERE codigo_referencia = ANY(%s)
    ORDER BY criado_em
""", (CODIGOS,))
for r in cur.fetchall():
    print(r)

# 2) Marca resolvido (só os que ainda estão em aberto; não sobrescreve resolução anterior).
cur.execute("""
    UPDATE smsmarica.registro_erro
    SET resolvido_em = now(), resolvido_por = %s, resolucao_nota = %s
    WHERE codigo_referencia = ANY(%s) AND resolvido_em IS NULL
""", (RESOLVIDO_POR, NOTA, CODIGOS))
print("marcados:", cur.rowcount)
conn.commit()
```

## Alternativa (via API, se preferir)

`POST /erros/{codigo}/resolver` com corpo `{ "resolvidoPor": "...", "nota": "..." }` (exige
sessão com permissão `Erros` + ação de edição). A tela **Sistema → Erros** também tem o
botão **Resolver** / **Reabrir** no detalhe do erro. Para reabrir via SQL:
`UPDATE ... SET resolvido_em = NULL, resolvido_por = NULL, resolucao_nota = NULL WHERE codigo_referencia = ...`.
