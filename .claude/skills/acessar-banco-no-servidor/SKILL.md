---
name: acessar-banco-no-servidor
description: Conecta ao Postgres de PRODUÇÃO a partir do servidor (onde o Agente IA roda) para rodar consultas. Use SEMPRE que precisar "olhar o banco", "consultar a base", "rodar um SQL/SELECT", "ver/conferir um registro ou tabela" — em vez de ficar procurando como conectar ou dizer que não sabe/não tem. A connection string vem de /etc/smsmarica-server/env e o cliente é psycopg2 (já instalado no host). Leitura por padrão; escrita só quando outra skill ou o operador mandar. NÃO confundir com o banco Oracle do Salux.
---

# Acessar o banco de produção (no servidor)

Você roda **no servidor** `smsmarica.online`. O Postgres de produção é o mesmo que a API
`smsmarica-server` usa. A credencial **não** está no ambiente do seu serviço — está no env do
`smsmarica-server`.

## Conexão (psycopg2 — já instalado)

```python
import psycopg2

# A connection string (formato Npgsql) mora no EnvironmentFile do smsmarica-server.
# NÃO aparece em `systemctl show -p Environment` (é EnvironmentFile), por isso lê-se o arquivo.
with open("/etc/smsmarica-server/env", encoding="utf-8") as fh:
    cs = next(l.split("=", 1)[1].strip()
              for l in fh if l.startswith("ConnectionStrings__DefaultDb="))

m = {k.strip().lower(): v.strip() for k, v in (p.split("=", 1) for p in cs.split(";") if "=" in p)}
conn = psycopg2.connect(
    host=m["host"], port=m.get("port", 25060), dbname=m["database"],
    user=m.get("username") or m.get("user id"), password=m["password"], sslmode="require",
)
cur = conn.cursor()
cur.execute("SELECT count(*) FROM smsmarica.ticket")
print(cur.fetchone())
```

Rode com o `python3` do sistema (tem psycopg2). Alternativa de linha de comando: `psql` também
está instalado (`/usr/bin/psql`).

## Regras

- **Banco compartilhado** com outros produtos da Prefeitura (Centralia, Automais.Fhir). Uma query
  pesada afeta sistemas que não são seus — filtre, use `LIMIT`, evite varrer tabela inteira.
- **Leitura é livre; escrita não.** `SELECT` à vontade para diagnóstico. `INSERT/UPDATE/DELETE`
  só quando uma skill específica (ex.: `criar-ticket`/`fechar-ticket`) ou o operador mandar — e
  mesmo assim espelhando a regra de negócio (auditoria, soft-delete), nunca "na mão" sem critério.
- **Mudança de dado fora do fluxo de ticket é EXCLUSIVA do administrador Bernardo Almeida**
  (`usuario_id 019dc264-7de1-78cc-b6ff-0be0c0e8b714`), com confirmação por ação. Com outro
  operador na sessão: diagnóstico (SELECT) + skill `criar-ticket` para registrar a necessidade.
- **Dois schemas:** `smsmarica` (negócio, pt-BR) e `fhir` (identidade clínica, en). Identidade de
  paciente/profissional vive em `fhir.*`.
- **Nunca imprima a connection string nem a senha** em respostas, logs ou comentários de ticket.
- PII: prefira contagens e IDs a listar nome/CPF/CNS de paciente.
