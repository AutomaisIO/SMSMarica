# SMSMarica.server

## O que é

**API backend** em **C# (.NET)** com **Entity Framework Core**, persistindo dados em **PostgreSQL**.

## Para que serve

- Expor endpoints REST (e evoluções futuras conforme necessidade) para o **front web**, o **app cidadão** e o **app motorista**.
- Centralizar **cadastros e CRUDs**: pacientes, tratamentos, periodicidade e geração de demandas de translado, unidades (com GPS), motoristas, veículos (fileiras e assentos), usuários e regras de alocação.
- No futuro, servir de **camada comum** para serviços intermediários que integram sistemas de saúde locais — sem acoplar o domínio do SMSMarica a implementações legadas de forma direta.

## Banco de dados (obrigatório)

Usa o **mesmo banco** `defaultdb` compartilhado com outros produtos (ex.: Automais.IO), porém **todo** o modelo EF e as migrations devem usar **exclusivamente** o schema PostgreSQL:

`smsmarica`

**Não** referenciar tabelas, views ou FKs de outros schemas. Isolamento total por schema.

## Relação com o restante do ecossistema

É a **fonte de verdade** dos dados operacionais consumidos por **`SMSMarica.front`**, **`SMSMarica.cidadao.app`** e **`SMSMarica.agente.app`**.
