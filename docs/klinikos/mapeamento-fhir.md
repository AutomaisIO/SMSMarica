# Klinikos → FHIR: levantamento e de-para (03/08/2026)

> **Como isto foi feito.** Tudo aqui foi **medido ao vivo** contra a instância da **UPA Maricá**
> (SQL Server, base `UPA24H`) pelo proxy SQL interno (`/root/pq.sh upa24h-marica-sqlserver`), em
> 03/08/2026. Nada é presumido a partir de documentação do fornecedor. Onde não deu para medir,
> está escrito que não deu.
>
> **PII fica fora deste arquivo.** As consultas devolvem nome de paciente; nenhum entrou aqui.
>
> Este documento substitui a referência quebrada que o
> [plano de sincronismo](../integracoes/plano-sincronismo-hub.md) apontava — a versão de 26/07
> nunca foi commitada.

---

## 1. As duas instâncias

O Klinikos assumiu a **UPA Inoã** e o **PA Santa Rita** em **07/04/2025** (data do primeiro
boletim medido), substituindo o Salux. O **Conde** ainda é Salux e migrará.

| Instância | Slug da base | Estado em 03/08 |
|---|---|---|
| UPA Maricá | `upa24h-marica-sqlserver` | agente WSS **conectado**, tudo abaixo medido aqui |
| PA Santa Rita | `santarita-marica-sqlserver` | agente **offline** — "não está conectado". Estrutura declarada idêntica; **não medida** |

> **Ponta aberta P1.** Enquanto o agente de Santa Rita não subir, o censo daquela instância é
> desconhecido. O conector é por instância (slug próprio), então Santa Rita pode entrar depois
> sem tocar no que já roda.

---

## 2. A unidade: a ponte do CNES existe e fecha

Esta era a pergunta que o [ADR-0039](../adr/0039-unidade-de-saude-eixo-duravel.md) precisava
responder antes de casar unidade por CNES: **a UPA do Klinikos e a UPA do Salux são a mesma
unidade real?**

| | Salux (`infosaude.hospital`) | Klinikos (`unidade`) |
|---|---|---|
| Chave interna | `cd_hospital` = 2 | `unid_codigo` = `0006` |
| Nome | `UPA 24H INOÃ` | `UPA MARICA` |
| **CNES** | **7164440** | **7164440** |

**Fecha.** Nome divergente, unidade idêntica — exatamente o cenário que o ADR previu e a razão
pela qual o nome não pode ser chave. O upsert de `Organization` casa por CNES antes de criar, e
os dois códigos internos acumulam como `identifier` no mesmo recurso.

**Atenção à coluna certa:** o CNES está em `unidade.unid_codigoCNES`. Existe também
`unidade.unidade_municipioCNES`, que está **vazia** — não é essa.

O CNPJ (`unid_cgc` = `42498717011351`) é da mantenedora, não da unidade; não serve de âncora.

---

## 3. O que está vivo e o que está morto

Esta é a correção mais importante do levantamento. O Klinikos tem **1.604 tabelas** e boa parte
do modelo de emergência **não é usada nesta implantação**. Mapear pelo nome da tabela levaria a
um conector que lê tabelas vazias e conclui que a UPA não atende ninguém.

| Tabela | Linhas | Veredicto |
|---|---:|---|
| `Pronto_Atendimento` | **179.522** | **VIVA** — é o boletim. Toda a jornada pendura aqui (`spa_codigo`) |
| `UPA_Evolucao` | **644.732** | **VIVA** — é o registro clínico: nota, CID, sinais, prescrição |
| `UPA_SinaisVitais` | **382.590** | **VIVA** — sinais vitais colunados |
| `paciente` | 83.878 | **VIVA** |
| `profissional` | 495 | **VIVA** (485 ativos) |
| `Emergencia` | **0** | morta — o módulo de emergência não é usado |
| `Atendimento_Emergencia` | **0** | morta — **é aqui que estaria o CID**, se o módulo fosse usado |
| `atendimento_spa` | **0** | morta — o atendimento médico **não** mora aqui |
| `Internacao` | — | parada desde 25/01/2026 (medido em 26/07) |

**Consequência de projeto:** o atendimento médico e o CID **não** vêm de uma tabela de
atendimento. Vêm da **evolução**. O conector precisa reconstruir o Encounter a partir do boletim
e derivar diagnóstico das linhas de `UPA_Evolucao`.

### 3.1 A anatomia da evolução

`UPA_Evolucao.Tipo` diz o que cada linha é:

| `Tipo` | Linhas | Com CID primário | Boletins distintos | Papel no FHIR |
|---|---:|---:|---:|---|
| REAVALIAÇÃO | 184.222 | 176.724 | 153.911 | Condition (atualiza) + nota |
| INÍCIO DO ATENDIMENTO MÉDICO | 165.777 | 139.000 | 165.777 | **início do atendimento** — 1 por boletim |
| RECEITA | 141.917 | 0 | 130.462 | MedicationRequest (texto) |
| PRESCRIÇÃO | 100.506 | 0 | 82.169 | MedicationRequest (texto) |
| EVOLUÇÃO DE ENFERMAGEM | 19.225 | 0 | 2.194 | DocumentReference |
| EVOLUÇÃO | 19.054 | 14 | 2.135 | DocumentReference |
| EVOLUÇÃO MÉDICA | 10.971 | 699 | 3.108 | DocumentReference |
| ESTORNO | 1.529 | 0 | 1.355 | **ignorar** — é anulação |
| ENTRADA NA SALA AMARELA | 1.105 | 1.105 | 1.047 | `statusHistory` |
| ENTRADA NA SALA VERMELHA | 338 | 338 | 324 | `statusHistory` |
| PARECER SOLICITADO | 59 | 0 | 58 | DocumentReference |
| PROTOCOLO DENGUE | 32 | 0 | 32 | DocumentReference |

**165.777 dos 179.522 boletins (92,3%)** chegaram a ter atendimento médico iniciado. Os ~13,7 mil
restantes são evasão/desistência — e **têm de entrar como Encounter mesmo assim**, com status
próprio: o paciente esteve lá, e isso é informação clínica.

---

## 4. Identidade do paciente — o número que preocupava

| | Total | % |
|---|---:|---:|
| Pacientes no cadastro | 83.878 | 100% |
| Com CPF | 69.713 | 83,1% |
| Com CNS no cadastro | 132 | **0,16%** |
| **Sem CPF e sem CNS** | **14.152** | **16,9%** |
| Com data de nascimento | 83.855 | 99,97% |

O CNS do cadastro é praticamente inexistente — a premissa antiga de que "o CNS mora em
`Paciente_CNS`" é falsa, e a coluna `pac_cartao_nsaude` também não é preenchida na prática.

**Os 14.152 são reais e foram atendidos: todos os 14.152 têm pelo menos um boletim.** Não é lixo
de cadastro; são pessoas que passaram pela UPA.

Tentativas de resgate, medidas:

| Caminho | Resgata | Sobra |
|---|---:|---:|
| CNS capturado no boletim (`spa_cartao_nsaude`) | 863 (6,1%) | 13.289 |
| CNS provisório (`pac_cartao_nsaude_provisorio`) | 859 | ~13.3 mil |

Os dois caminhos se sobrepõem quase inteiramente. **Não há resgate em massa** — o dado
simplesmente não foi coletado no atendimento.

**Decisão aplicada** (a mesma já em produção para o Salux): esses pacientes **entram no hub**,
identificados por `identifier` local da base, e marcados com
`meta.tag` = `urn:smsmarica:qualidade|identidade-incompleta`. O painel mostra o selo "sem CPF"
ao lado do nome. Não entram no merge canônico por CPF — não há como deduplicá-los com segurança,
e forçar dedup por nome+nascimento é o caminho mais rápido para fundir dois pacientes distintos.

> **CNS provisório: decidir.** Os 17.361 CNS provisórios da base **não** são identificador
> nacional e não devem virar `identifier` de sistema CNS. Ficam de fora até alguém da operação
> dizer o que eles significam.

Profissionais: 495 (485 ativos), CPF em 464, CNS em 322, nº de conselho em 360.

---

## 5. CDC: `rv_atualizacao` é o melhor watermark que temos

Quase toda tabela relevante tem `rv_atualizacao` (`timestamp`/`rowversion` do SQL Server):
um **bigint monotônico global** que o banco incrementa em toda escrita, sem depender de o
aplicativo lembrar de atualizar uma data.

```sql
SELECT TOP 3 pac_codigo, CONVERT(BIGINT, rv_atualizacao) rv FROM paciente ORDER BY rv_atualizacao DESC
-- 226712823 / 226712766 / 226712364
```

É **estritamente melhor** que o CDC do Salux (janela de 120 dias sobre `dt_atualizacao`, com lag
de segurança): não tem fuso, não tem relógio, não tem registro que escapa por a origem não ter
mexido na data. A marca d'água por fase vira um `long`, e o filtro é
`WHERE rv_atualizacao > @marca`.

| Fase | Tabela | Coluna de corte |
|---|---|---|
| Unidades | `unidade` | (1 linha por instância — upsert integral todo ciclo) |
| Profissionais | `profissional` | **não tem `rv_atualizacao`** — varredura integral (495 linhas) |
| Pacientes | `paciente` | `rv_atualizacao` |
| Atendimentos | `Pronto_Atendimento` | `rv_atualizacao` |
| Clínico | `UPA_Evolucao` | `rv_atualizacao` |
| Sinais vitais | `UPA_SinaisVitais` | `rv_atualizacao` |

> **"Quase" é literal.** Conferido coluna a coluna: `paciente`, `Pronto_Atendimento`,
> `UPA_Evolucao`, `UPA_SinaisVitais` e `unidade` têm; **`profissional` não tem**. Um SQL que
> corte por rowversion ali falha com *Invalid column name* e derruba a fase inteira. São 495
> linhas — varredura integral custa menos que qualquer CDC improvisado sobre outra coluna.

> **Impacto no modelo compartilhado.** `MarcaDagua` hoje só tem campos `DateTime?`. Precisa de
> campos `long?` por fase para o Klinikos. Os nomes já foram neutralizados
> (`AtendimentoEm`, `DocumentoEm`…) — falta a variante numérica.

---

## 6. De-para por recurso

Sistema base dos identifiers internos: `urn:klinikos:<entidade>`, valor prefixado pelo slug da
instância (`upa24h-marica:0006`) — duas instâncias nunca colidem.

### Organization ← `unidade`

| FHIR | Origem | Nota |
|---|---|---|
| `identifier[CNES]` | `unid_codigoCNES` | ponte entre bases (§2) |
| `identifier[interno]` | `unid_codigo` | `upa24h-marica:0006` |
| `name` | `unid_descricao` | "UPA MARICA" |
| `alias` | `Unid_nome_fantasia`, `unid_sigla` | |
| `telecom` | `unid_telefone`, `unid_email` | |

### Practitioner ← `profissional`

| FHIR | Origem |
|---|---|
| `identifier[CPF]` | `PROF_CPF` (464/495) |
| `identifier[CNS]` | `PROF_CNS` (322/495) |
| `identifier[conselho]` | `PROF_NUMCONSELHO` (360/495) |
| `name` | `PROF_NOME` |
| `active` | `PROF_ATIVO` |
| `qualification.code` | `CBO_CODIGO` |

### Patient ← `paciente`

| FHIR | Origem | Nota |
|---|---|---|
| `identifier[CPF]` | `pac_cpf` | âncora do merge canônico |
| `identifier[interno]` | `pac_codigo` | sempre presente |
| `name.text` | `pac_nome` | |
| `name[usual]` | `spa_nomesocial` (boletim) | nome social só existe no boletim |
| `birthDate` | `pac_nascimento` | |
| `gender` | `pac_sexo` | |
| `telecom` | `pac_telefone`, `pac_celular`, `pac_email` | **nunca sobrescreve telefone verificado no hub** |
| `deceased` | `pac_dtobito` + `UPA_Obito` | `pac_obito` é morta; usar `UPA_Obito` |
| `contact` | `pac_responsavel`, `pac_telefone_responsavel` | |
| `meta.tag` | — | `identidade-incompleta` quando sem CPF (§4) |

### Encounter ← `Pronto_Atendimento` (+ evolução)

| FHIR | Origem | Nota |
|---|---|---|
| `identifier` | `spa_codigo` | chave do boletim |
| `class` | — | `EMER` (é UPA 24h) |
| `subject` | `pac_codigo` | FK real, 100% preenchida |
| `serviceProvider` | `unid_codigo` → Organization | ADR-0039 |
| `period.start` | `spa_chegada` | |
| `participant` | `prof_codigo` da evolução de início | |
| `statusHistory` | entradas sala amarela/vermelha | de graça, o Salux não tem |
| `status` | derivado | `finished` com atendimento; sem evolução médica → não atendido |

### Condition ← `UPA_Evolucao.cid_codigo_primario` / `_secundario`

Só das linhas com CID (INÍCIO, REAVALIAÇÃO, salas). A **reavaliação** pode mudar o CID — a
última prevalece; as anteriores não somem, viram histórico da mesma Condition.

### Observation ← `UPA_SinaisVitais`

Colunas separadas (`pressaoarterial`, `pulso`, `temperatura`, `frequenciarespiratoria`, `hgt`,
`saturacaoO2`, `peso`) — **muito** melhor que o EAV do eDoc do Salux. Cada uma vira uma
Observation LOINC com `encounter` amarrado. A pressão vem como texto ("120/80") e precisa ser
partida em sistólica/diastólica.

### DocumentReference ← `UPA_Evolucao` (tipos narrativos)

`upaevo_descricao` é o texto. Uma DocumentReference por linha de evolução, `type` derivado do
`Tipo`. **`ESTORNO` não entra** — é anulação, e importá-lo colocaria no prontuário um registro
que a origem considera cancelado.

### MedicationRequest ← `UPA_Evolucao` tipos RECEITA/PRESCRIÇÃO

Nesta implantação a prescrição é **texto livre** na evolução (242.423 linhas). A prescrição
estruturada (`UPA_ITEM_PRESCRICAO_MEDICA` + `Item_Aprazamento`) existe no esquema mas precisa ser
medida antes de prometer `Dosage` estruturado ou `MedicationAdministration`.

### Higiene medida no dado real

O primeiro registro da base já mostra o padrão: `pac_telefone` = `0000000000`. É campo de
preenchimento obrigatório que a recepção completa com lixo quando o paciente não informa. O
conector descarta telefone com menos de 8 dígitos ou de dígito único — telefone falso no hub é
pior que telefone nenhum, porque alguém tenta ligar e o paciente entra em relatório de
"contactável" sem ser.

Outros formatos vistos e tratados: peso com vírgula decimal (`11,30`), pressão arterial como
`/` (sem medida), CID com padding à direita (`B34      `).

> **Ponta aberta P2.** Medir `UPA_ITEM_PRESCRICAO_MEDICA` e `Item_Aprazamento` nesta instância.
> Se estiverem vivas, destravam o `MedicationAdministration` (hoje vazio no hub) — que é a
> pendência mais antiga do BAU clínico.

---

## 7. O que o hub ainda não tem

O Klinikos não exige recurso novo para a Fase 1: Organization, Practitioner, Patient, Encounter,
Condition, Observation, DocumentReference e MedicationRequest **já existem** no Automais.Fhir.

Fica para depois, quando as pontas P2 e a alergia forem medidas: `AllergyIntolerance`
(`Atendimento_Alergia`), `Procedure`, `ServiceRequest`, `DiagnosticReport`.

---

## 8. Transporte: o conector não fala com o banco

Diferença estrutural em relação ao Salux: **não há rota de rede** do servidor até o SQL Server da
UPA. Tudo passa pelo **agente WSS reverso** ([ADR-0023](../adr/0023-bases-ia-por-agente-proxy.md)),
que já existe e está em produção para o módulo IA.

Consequências para o conector:

1. A estratégia usa `IFonteDados` (`ProxyAgenteFonte`), não uma conexão própria.
2. O orquestrador hoje **exige** host/serviço/usuário/senha antes de rodar — uma base
   `ViaAgente` não tem nada disso. Essa guarda precisa virar condicional.
3. Os limites do módulo IA (`Ia:RowLimit` = 1.000, `Ia:TimeoutSegundos` = 30) são pequenos demais
   para importação em lote. O conector precisa construir o proxy com limites próprios.
4. T-SQL **não tem tuple-IN**. Onde o Salux faz `WHERE (a,b) IN (...)`, aqui vira `JOIN` com
   `VALUES`. E o limite de 1.000 itens por `IN` do Oracle vira o limite de **2.100 parâmetros**
   do SQL Server — o lote continua obrigatório, só muda o número.

---

## 9. Pontas abertas

| # | Ponta | Bloqueia? |
|---|---|---|
| P1 | Agente de Santa Rita offline — instância não medida | não bloqueia a UPA |
| P2 | Prescrição estruturada / aprazamento não medidos nesta instância | não bloqueia a Fase 1 |
| P3 | CNS provisório (17.361): o que significa? | não — ficam de fora até decidir |
| P4 | ~~`MarcaDagua` precisa de watermark numérica por fase~~ | **resolvido 03/08** — ponteiros em `jsonb` |
| P5 | ~~Guarda de credencial do orquestrador rejeita base `ViaAgente`~~ | **resolvido 03/08** — guarda condicional |
| P8 | Primeiro run da UPA ainda **não foi disparado**: a base não tem agenda, então nada roda sozinho | decisão de quando ligar |
| P6 | Semântica dos códigos de administração de dose (`C`/`V`) | só quando P2 abrir |
| P7 | Internação: morta no Klinikos desde 25/01/2026. Se o Conde migrar como a UPA roda hoje, o hub **perde a internação no dia do cutover** | não bloqueia hoje; **bloqueia o cutover do Conde** |
