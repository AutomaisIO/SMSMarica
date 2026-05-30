# ADR-0007 — Schema `fhir` canônico isolado de `smsmarica` (regras de negócio)

- **Status**: Aceito
- **Data**: 2026-05-30
- **Decisores**: Bruno (product/eng)
- **Supersede parcialmente**: [ADR-0001](./0001-schema-isolation.md) (princípio 1 — "schema único"), [ADR-0005](./0005-usuario-unificado-com-papeis.md) (Paciente/Medico como side-tables 1:1 de Usuario), [ADR-0006](./0006-papel-derivado-e-auditoria-explicita.md) (papel detectado por linha em `medico`/`motorista`/`paciente`)

## Contexto

O ADR-0001 estabeleceu schema único `smsmarica` com naming em pt-BR. O ADR-0005 modelou Paciente/Médico/Motorista como side-tables 1:1 de Usuario carregando só "campos específicos do papel", com identidade base (nome, CPF, RG, etc.) em `usuario`. O ADR-0006 refinou: papel detectado pela existência da linha 1:1 (sem discriminador), `excluido_em` separado de `ativo`.

Esse desenho funcionou enquanto o SMSMarica era visto como **um sistema de transporte** (papel Paciente = "cidadão atendido pelo programa de translado"). A [visão estratégica](../visao.md) revisada em 2026-05-29 reposicionou a SMS Maricá como **hub FHIR R4 da Secretaria de Saúde**: o cidadão precisa ser representado de forma canônica + estável + interoperável com outros sistemas (Salux HIS, eSUS APS, RNDS, dcm4chee), não como linha de side-table de um Usuario de login.

Forçar tudo em `smsmarica` ficou ruim por três motivos:

1. **Naming pt-BR no domínio clínico viola convenção FHIR.** FHIR é padrão internacional — tabelas/campos `patient_identifier.system`, `practitioner_qualification.council_code` falam o vocabulário que SDKs (.NET Firely, HAPI FHIR), validadores oficiais e equipes externas reconhecem na hora. Renomear para `paciente_identificador`/`sistema` empurra acoplamento gratuito para todo integrador.
2. **Identidade clínica não cabe em Usuario.** Um cidadão pode ter múltiplos nomes (oficial, social, antigo), múltiplos identifiers (CPF + CNS + RG + prontuário externo SGH/CEM/Salux), múltiplos endereços (Patient.address[]), múltiplos contatos de emergência (Patient.contact[]). Usuario é uma tabela achatada — incompatível com a estrutura multivalorada do FHIR R4.
3. **Login + RBAC + Token ≠ identidade clínica.** Usuário é construto operacional (e-mail, senha, perfis, JWT). Patient/Practitioner são entidades de identidade do paciente/profissional. Misturar trava evolução: quando um Patient não tem login (cidadão registrado pela unidade sem app), Usuario forçava colunas vazias; quando aparecer um servidor FHIR externo, expor `usuario` por `Patient` resource vaza dados de login.

## Decisão

### 1. Dois schemas distintos no mesmo banco `defaultdb`

| Schema | Propósito | Naming | O que tem |
|--------|-----------|--------|-----------|
| `fhir.*` | Modelo FHIR R4 canônico (perfil BRIndividuo + extensões eSUS APS / LGPD) | **inglês** (padrão internacional do FHIR) | `patient`, `patient_name`, `patient_identifier`, `patient_address`, `patient_telecom`, `patient_contact`, `patient_communication`, `patient_link`, `patient_photo`, `patient_disability`, `practitioner`, `practitioner_*`, `organization`, `consent` (LGPD), lookups (`municipio_ibge`, `pais_iso`, `cbo_ocupacao`, `etnia_indigena`, `barreira_comunicacao`, `religiao`) |
| `smsmarica.*` | Regras de negócio (transporte, RBAC, motorista) | **pt-BR** ([conventions.md §1](../conventions.md)) | `usuario` (login/RBAC), `motorista` (não é entidade clínica FHIR), `tratamento`, `sessao_de_tratamento`, `rota_diaria`, `alocacao`, `veiculo`, `unidade`, `laudo`, `solicitacao_exame`, `perfil`, `permissao_*`, `avaliacao`, etc. |

`HasDefaultSchema("smsmarica")` continua valendo; cada `EntityTypeConfiguration` em `Data/Configurations/Fhir/` chama `b.ToTable(..., schema: "fhir")` para o seu lado.

### 2. Um único `SmsMaricaDbContext` (não dois contexts)

Os recursos FHIR e as entidades de negócio compartilham `DbContext`. **Por quê:**

- Transações atômicas cross-schema sem dança de `TransactionScope` (criar Patient + Usuario(PatientId) em um `SaveChangesAsync`).
- Navigation cross-schema permitida **só na direção `smsmarica → fhir`** (ex.: `Usuario.Patient`, `Tratamento.Patient`, `Laudo.Practitioner`). Direção contrária é **proibida** — recursos FHIR não conhecem Usuario, login, RBAC, ou conceitos de negócio. Se um dia montarmos um servidor FHIR REST (`GET /fhir/Patient/{id}`), ele só lerá `fhir.*` sem importar nada de `smsmarica`.
- Sem segundo consumidor do hub hoje. Quando aparecer (HAPI FHIR ou Firely Server externo), avaliamos extrair `fhir.*` para banco separado — a regra de naming + direção de FK já está prepared.

### 3. Identidade clínica vive em `fhir.patient` / `fhir.practitioner`

`Usuario` é stripped para o núcleo de **acesso e RBAC**:

| Antes (ADR-0005/0006) | Depois (ADR-0007) |
|---|---|
| `NomeCompleto`, `Cpf`, `Rg`, `DataNascimento`, `Sexo`, `Telefone`, `Endereco`, `FotoBase64` em `usuario` | Removidos. Identidade vive nos recursos FHIR (`patient_name`, `patient_identifier`, `patient_address`, `patient_telecom`, `patient_photo`) ou inline em `motorista` |
| Papel detectado por nav `Usuario.Medico?` / `Motorista?` / `Paciente?` (tabelas em `smsmarica`) | Papel detectado por FK nullable `Usuario.PatientId?` / `PractitionerId?` / `MotoristaId?`. Cross-schema para FHIR. CHECK constraint `ck_usuario_papel_unico` garante no máximo 1 setada. |
| `Paciente` (smsmarica side-table) → `Patient` (FHIR) com `Names/Identifiers/Addresses/Telecoms/Contacts/Photos/Disabilities/Communications` | `smsmarica.paciente` deletada. `fhir.patient` é o agregado canônico. |
| `Medico` (smsmarica side-table) → `Practitioner` (FHIR) com `Qualifications[CouncilCode='CRM']` + identifiers (CPF + RQE via `urn:br:rqe`) | `smsmarica.medico` deletada. `fhir.practitioner` é o agregado canônico. |

`Usuario` ganha `NomeExibicao` denormalizado: snapshot do nome do papel (sincronizado em create/update da role) ou nome digitado direto quando Usuario não tem papel (admin/operador). Listagens e o claim `name` do JWT leem `nome_exibicao` — não precisam de JOIN.

### 4. Motorista NÃO vai pra FHIR

Motorista é **operacional de transporte**, não entidade clínica. Não tem candidato natural no FHIR R4 (Person é genérico demais; Practitioner é para profissional de saúde). Fica em `smsmarica.motorista` com **identidade inline** (NomeCompleto/Cpf/Rg/DataNascimento/Sexo/Telefone/Endereco owned/FotoBase64 + Cnh específico).

FK invertida em relação ao modelo anterior:

| Antes | Depois |
|---|---|
| `motorista.usuario_id` UNIQUE → `usuario.id` | `usuario.motorista_id?` UNIQUE → `motorista.id` |

A inversão é uniforme com Patient/Practitioner: o lado que **referencia** (Usuario) carrega a FK nullable; o lado **referenciado** (Patient/Practitioner/Motorista) não conhece o login.

### 5. CodeSystem (Identifier.system) — referência

| Tipo | System URI |
|---|---|
| CPF | `https://fhir.saude.gov.br/sid/cpf` |
| CNS | `https://fhir.saude.gov.br/sid/cns` |
| RG | `urn:br:gov:rg` (com `issuer_name` + `issuer_state`) |
| CNES | `https://fhir.saude.gov.br/sid/cnes` |
| Certidão Nascimento | `urn:br:gov:certidao-nascimento` (com `registry_*`) |
| Passaporte | `urn:passport` |
| RNE | `urn:br:gov:rne` |
| PIS/PASEP | `urn:br:gov:pis-pasep` |
| CRM | `urn:br:conselho:crm:{UF}` (no Identifier) **ou** `practitioner_qualification(CouncilCode='CRM', CouncilNumber, CouncilState)` |
| COREN | `urn:br:conselho:coren:{UF}` |
| RQE | `urn:br:rqe` |
| Prontuário Salux | `urn:salux:cd_paciente` |
| Prontuário SGH | `urn:sgh:prontuario` |
| Prontuário CEM | `urn:cem:prontuario` |

### 6. Sem servidor FHIR REST por ora

Schema `fhir.*` está pronto, mas **não expomos endpoints REST FHIR** (`GET /fhir/Patient/{id}`, `POST /fhir/Patient/_search`, etc.). Decisão: aguardar caso real (2º consumidor — Salux/eSUS pedindo Patient resource cru). Candidatos quando vier: HAPI FHIR (Java, externo) ou Firely Server (.NET). Tanto faz — o schema já está em formato canônico.

## Alternativas consideradas

### A. Manter tudo em `smsmarica` com naming pt-BR

**Prós:** menos refator imediato, conventions.md uniforme.
**Contras:** quando expusermos REST FHIR (mesmo que via gateway tradutor), todo nome de tabela/coluna vai precisar ser remapeado. Tooling externo (validador HL7 FHIR, IPS guides, SDKs) espera nomes em inglês. Equipes que entram (consultores, integradores) precisariam aprender naming duplo. **Rejeitada** quando a visão virou hub FHIR.

### B. Dois DbContexts (`SmsMaricaDbContext` + `FhirDbContext`)

**Prós:** isolamento mais forte, força explicitamente as transações cross-schema (saga-pattern). Permitiria mover `fhir.*` pra outro banco sem mudar código.
**Contras:** sem 2º consumidor real, paga complexidade hoje (TransactionScope manual, navigation impossível, mapper duplicado) por ganho hipotético. **Rejeitada por ora.** Se aparecer banco dedicado, refatoramos o context split na hora.

### C. Banco PostgreSQL separado para FHIR

**Prós:** isolamento máximo, backup/restore independente, permite Foreign Data Wrapper para queries cross-banco quando precisar.
**Contras:** provisionamento DigitalOcean extra, dois pools de conexão, custo. Não justificado pelo volume atual (~0 registros FHIR; ~1k smsmarica). **Adiada** — mesma lógica do ADR-0001 sobre banco dedicado: a regra de naming + schema já protege; mudar conexão depois é trivial.

### D. Motorista vira `fhir.practitioner` com qualification "Driver"

**Prós:** uniformidade — toda pessoa autenticável estaria em FHIR.
**Contras:** FHIR Practitioner é "qualified professional involved in healthcare". Motorista de translado não é profissional de saúde. Forçar a categoria distorce semântica FHIR e atrapalha integrações que filtram Practitioner por papel clínico. **Rejeitada.**

## Consequências

### Positivas

- Hub FHIR é a fonte canônica de identidade do cidadão e do profissional de saúde. Quando o Salux/eSUS/RNDS pedir Patient resource, é um SELECT direto.
- Naming inglês em `fhir.*` deixa o schema autodescritivo para SDK FHIR — copia-cola de specs HL7 funciona.
- Conventions.md (pt-BR) preservado para `smsmarica.*` (transporte, RBAC) — domínio de negócio interno fica vernáculo, como sempre foi.
- Patient.name[], Patient.identifier[], Patient.address[], Patient.contact[] viram multivalorados como FHIR especifica — sem hacks de "telefone1/telefone2".
- Separação login vs identidade clínica abre caminho para Patient sem Usuario (cidadão registrado na unidade sem app) e Practitioner com múltiplos Usuarios (improvável mas modelável).

### Negativas

- Migration `RefatorUsuarioPractitionerMotorista` é grande (drop 14 colunas de `usuario`, drop tabela `medico`, rename `laudo.medico_* → practitioner_*`, drop `motorista.usuario_id`, adiciona 13 colunas em `motorista`, 3 FKs cross-schema, 4 indexes filtrados, 1 CHECK constraint). Migration anterior (`RefatorPacienteParaFhir`) dropou `paciente`.
- Conventions.md fica com duas regras de naming convivendo (pt-BR em `smsmarica.*`, en em `fhir.*`). Documentado como exceção explícita: FHIR é padrão internacional, não domínio.
- ADR-0005 e ADR-0006 ficaram parcialmente superseded — a essência (papel 1:1, auditoria explícita) continua válida, mas a localização da entidade Paciente/Medico mudou de side-table `smsmarica.*` para recurso `fhir.*`.
- Promover (`POST /pacientes/promover` / `/medicos/promover` / `/motoristas/promover`) descontinuado: Usuario sem papel não carrega mais identidade clínica (CPF/nome/data nasc.), então não dá pra reaproveitar. Endpoints retornam 409 até o front reescrever o fluxo.
- Tests do `PacientesService` ficaram skipados — agregado mudou demais pra adaptação trivial.
- Campos clínicos volumosos (alergias, comorbidades, sinais vitais, plano de saúde) saem do PacienteDto até implementarmos FHIR `AllergyIntolerance` / `Condition` / `Observation` / `Coverage`. cidadao.app aceita degradação silenciosa (memória `project_flutter_nao_producao`).

### Condições para revisitar

- Se aparecer 2º consumidor do hub FHIR (Salux pedindo `GET /fhir/Patient`, eSUS gravando), avaliar (1) expor REST com HAPI/Firely e (2) extrair `fhir.*` para banco dedicado.
- Se decidirmos que Motorista precisa também de identidade canônica externa (improvável — não há cadastro nacional de motorista de translado), avaliar mover para `fhir.practitioner` com `qualification(CouncilCode='CNH')`. Forçaria revisão do escopo "Practitioner = profissional de saúde".

## Enforcement

- Code review: qualquer FK cross-schema só na direção `smsmarica → fhir`. Navegação `fhir.Patient.Tratamentos` ou similar = rejeitada.
- `HasDefaultSchema("smsmarica")` permanece no `OnModelCreating`; cada `IEntityTypeConfiguration` FHIR chama `.ToTable(..., schema: "fhir")` explícito.
- Naming: tabelas/colunas em `fhir.*` em inglês snake_case; em `smsmarica.*` pt-BR snake_case ([conventions.md](../conventions.md)).
- CodeSystem URIs centralizados em constantes (ex.: `PacientesMapper.SystemCpf`, `MedicosMapper.SystemRqe`) — não duplicar strings na codebase.
- `CLAUDE.md` cita este ADR na lista de regras não-negociáveis.
