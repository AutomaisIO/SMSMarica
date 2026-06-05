# ADR-0012 — Agendamento local (fonte da verdade) + integração SISREG só-leitura

- **Status**: Aceito
- **Data**: 2026-06-05
- **Decisores**: Bernardo (product/eng)
- **Relaciona-se com**: [ADR-0001](./0001-schema-isolation.md) (schema de negócio `smsmarica`), [ADR-0006](./0006-papel-derivado-e-auditoria-explicita.md) (auditoria/soft-delete), [ADR-0007](./0007-schema-fhir-separado.md) (separação `smsmarica`/`fhir`), [ADR-0010](./0010-servico-fhir-autonomo.md) (serviço FHIR autônomo), [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (config cifrada gerida por tela)

## Contexto

A SMS Maricá precisa de um **sistema de agendamento próprio** — cada Unidade tem Especialidades, cada Especialidade tem Médicos, cada Médico tem uma **Agenda** (grade) numa unidade, e pacientes ocupam horários (**Agendamentos**) — e precisa **integrar com o SISREG** (Sistema de Regulação do DATASUS).

Ao estudar o *Manual de uso da API SISREG (v2.1)*, um fato reposicionou o desenho:

> **A API-SISREG (`sisreg-es.saude.gov.br`) é exclusivamente de LEITURA (GET).** É um espelho Elasticsearch para extração/relatório de solicitações e marcações ambulatoriais/hospitalares. **Não existe endpoint para gravar de volta** — não há como confirmar atendimento, "dar check", marcar falta ou agendar via esta API. A escrita continua no SISREG web ou em outro mecanismo do DATASUS.

Três restrições moldam a decisão:

1. **Não dá para tratar o SISREG como destino de escrita** por esta API — só feed de leitura.
2. **Identidade clínica é FHIR** (memória `régua smsmarica vs FHIR`, [ADR-0010](./0010-servico-fhir-autonomo.md)): Paciente e Médico vivem no hub `Automais.Fhir` (Patient/Practitioner). Agendamento é **regra de negócio**, não recurso clínico canônico.
3. **A integração deve ser reutilizável** por outras prefeituras — sem Maricá hardcoded.

## Decisão

### 1. O agendamento local é a fonte da verdade; SISREG é feed de leitura

Construímos um domínio de agendamento em `smsmarica` (pt-BR). Ele **não escreve** no SISREG. No futuro (fora desta entrega), um job de **conciliação** lê o feed SISREG e cruza com os agendamentos locais para um painel de divergências — leitura apenas.

### 2. Agendamento é domínio `smsmarica`; paciente e médico entram só por referência FHIR

Novas entidades em `smsmarica.*` (pt-BR), seguindo auditoria/soft-delete do [ADR-0006](./0006-papel-derivado-e-auditoria-explicita.md):

- **`Especialidade`** — tabela de referência (nome, CBO opcional).
- **`Agenda`** — grade de 1 médico, numa unidade, para 1 especialidade.
- **`DisponibilidadeRecorrente`** — regra semanal que gera o pool de horários.
- **`DisponibilidadeAvulsa`** — horário extra fora da recorrência.
- **`BloqueioAgenda`** — janela que subtrai do pool (férias/feriado/ausência).
- **`Agendamento`** — consulta marcada para um paciente (também ocupa o pool).

`Agenda.MedicoId` e `Agendamento.PacienteId` são **Guids dos recursos FHIR** (Practitioner/Patient), **sem FK cross-system** — coerente com a proibição de FK `fhir → smsmarica` invertida e com o fato de o FHIR ser serviço autônomo. Guardamos apenas um **snapshot denormalizado** (nome/CNS) para exibir a grade sem ir ao hub a cada render; a existência é validada via proxy FHIR (`IMedicosService`/`IPacientesService`) ao criar agenda/agendamento. **Sem projeção FHIR do agendamento** nesta fase.

### 3. Horários livres são calculados sob demanda (slots virtuais)

Não materializamos slots em tabela nem rodamos worker de geração. Horários livres de uma agenda num intervalo =
`(recorrências ∪ avulsos) − bloqueios − agendamentos ativos`, fatiados em slots de `DuracaoConsultaMinutos`. Edição de recorrência é instantânea, sem storage de slots, e a marcação valida ausência de double-booking por sobreposição.

### 4. Integração SISREG é um namespace desacoplado dirigido por configuração

`Core/Integracoes/Sisreg/` não depende do domínio de agendamento. Um `SisregClient` (HttpClient tipado) faz **POST `/{indice}/_search`** (o Elasticsearch aceita POST igual ao GET-com-body do manual), montando o nome do índice (`{tipo}-{uf}-{municipio}` ou `{tipo}-nacional`) a partir da configuração. Um `SisregConsultaService` expõe as 6 consultas do manual como métodos tipados com DTOs por índice. Tudo parametrizável (UF, município, centrais reguladoras, base URL, escopo) → reutilizável por outra prefeitura trocando só a config.

### 5. Credenciais do SISREG cifradas no banco, geridas por tela

Seguindo o padrão de config cifrada singleton do [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (`IProtetorSegredos` + campo `*Cifrado` write-only): `SisregConfiguracao` guarda BaseUrl, UF, município, centrais, tipo de autenticação (`Basic`/`Bearer`/`ApiKey` — o esquema real é confirmado na homologação) e login/senha/token cifrados. A API nunca devolve a senha/token; só sinaliza se estão definidos.

### 6. Quatro módulos de permissão novos

`ModuloPermissao` (valores estáveis, append-only): `Especialidades = 22`, `Agendamentos = 23`, `Sisreg = 24`, `SisregConfiguracao = 25`. Consultar o feed (`Sisreg`) é separado de configurar credenciais (`SisregConfiguracao`), poder sensível.

## Consequências

**Positivas**
- A integração SISREG fica "pronta" e testável mesmo antes das credenciais (homologação): sem credenciais, as consultas devolvem erro tratado em vez de 500.
- Namespace SISREG autônomo e config-driven → caminho aberto para generalizar a outras prefeituras.
- Slots virtuais evitam um worker de materialização e mantêm a edição de grade instantânea.

**Negativas / a vigiar**
- Cálculo de horários livres é computado a cada consulta; se uma agenda tiver muitas regras/intervalos longos, otimizar (cache/limite de janela).
- Snapshot denormalizado de nome/CNS pode ficar defasado se o cadastro FHIR mudar; tratar como dado de exibição, não autoritativo.
- A conciliação SISREG↔local fica para entrega futura (roadmap), não nesta.

## Alternativas consideradas

- **Materializar slots em tabela + worker**: descartado — mais storage e complexidade de regeneração ao editar recorrência, sem ganho nesta escala.
- **Tratar SISREG como sistema de escrita/sincronização bidirecional**: impossível por esta API (só leitura).
- **Modelar Especialidade só como string do FHIR**: descartado — precisamos de tabela controlada local para montar agendas e vincular médicos por unidade.
