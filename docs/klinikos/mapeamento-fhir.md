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
| `UPA_Atendimento_Medico` | **173.654** | **VIVA** — é o BOLETIM MÉDICO: anamnese, exame físico, hipótese, conduta |
| `Prescricao` + `Item_Prescricao_Medicamento` | 237.873 / **337.723** | **VIVAS** — prescrição estruturada: insumo, dose, unidade, via |
| `TB_CID` | 14.242 | **VIVA** — catálogo CID-10 (é daqui que sai o NOME do diagnóstico) |
| `Emergencia` | **0** | morta — o módulo de emergência não é usado |
| `Atendimento_Emergencia` | **0** | morta — **é aqui que estaria o CID**, se o módulo fosse usado |
| `atendimento_spa` | **0** | morta — o atendimento médico **não** mora aqui |
| `Internacao` | — | parada desde 25/01/2026 (medido em 26/07) |

> **Correção de 11/08/2026.** As três primeiras linhas acima foram descobertas tarde, e a
> omissão custou caro: entre 06/08 e 11/08 o Klinikos entrou no hub **sem boletim médico e sem
> medicamento**. `UPA_Atendimento_Medico` era lida — mas só para pegar o `tipsai_codigo` do
> desfecho, e seus cinco campos de texto eram descartados. Preenchimento medido nas 173.654
> linhas: exame físico 96,8%, hipótese 96,7%, conduta 96,7%, anamnese 89,4%.
> `upaatemed_Reavaliacao` está **vazia nas 173.654** — reavaliar aqui é evento, não narrativa.

**Consequência de projeto:** o atendimento médico e o CID **não** vêm de uma tabela de
atendimento. Vêm da **evolução**. O conector precisa reconstruir o Encounter a partir do boletim
e derivar diagnóstico das linhas de `UPA_Evolucao`.

### 3.1 A anatomia da evolução

`UPA_Evolucao.Tipo` diz o que cada linha é:

| `Tipo` | Linhas | Com CID primário | Boletins distintos | Papel no FHIR |
|---|---:|---:|---:|---|
| REAVALIAÇÃO | 184.222 | 176.724 | 153.911 | Condition (atualiza) — **e só** |
| INÍCIO DO ATENDIMENTO MÉDICO | 165.777 | 139.000 | 165.777 | **início do atendimento** — 1 por boletim |
| RECEITA | 141.917 | 0 | 130.462 | **nada** — ver o aviso abaixo |
| PRESCRIÇÃO | 100.506 | 0 | 82.169 | **nada** — ver o aviso abaixo |
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

> ### ⚠ `upaevo_descricao` é o RÓTULO da linha, não o conteúdo dela
>
> Esta é a armadilha central desta base, e ela custou dois meses de prontuário oco. Para os três
> tipos mais volumosos, `upaevo_descricao` repete a própria categoria:
>
> | `Tipo` | `upaevo_descricao` | Tamanho |
> |---|---|---|
> | REAVALIAÇÃO | `Reavaliação` | 11 bytes, em 100% das linhas |
> | RECEITA | `Receita` | idem |
> | PRESCRIÇÃO | `Prescrição` | idem |
>
> Só os tipos narrativos (EVOLUÇÃO MÉDICA, EVOLUÇÃO DE ENFERMAGEM, EVOLUÇÃO, PARECER) trazem
> texto real ali — até ~4 KB. Mapear a coluna direto produziu, no hub, **414.086
> MedicationRequest cujo medicamento era a palavra "Receita"** e **296.475 DocumentReference
> cujo conteúdo era a palavra "Reavaliação"** (84% de todos os documentos do Klinikos).
>
> O conteúdo de verdade mora em OUTRAS tabelas: narrativa em `UPA_Atendimento_Medico`,
> medicamento em `Item_Prescricao_Medicamento`. **Antes de mapear qualquer coluna desta base,
> conferir o tamanho e o valor distinto do que ela realmente guarda.**

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
| **Boletim médico** | `UPA_Atendimento_Medico` | `rv_atualizacao` |
| **Prescrição** | `Item_Prescricao_Medicamento` | `rv_atualizacao` **do item** |
| Catálogo CID | `TB_CID` | (sem corte — varredura **paginada**, ver abaixo) |

O ponteiro da prescrição é o do ITEM, não o da `Prescricao`: acrescentar um medicamento não toca
o cabeçalho, e um CDC pelo pai perderia o item novo — o mesmo desenho do fechamento do boletim.

> **O agente corta a resposta no teto dele, em silêncio.** `TB_CID` tem 14.242 linhas e o teto é
> 5.000: pedir a tabela inteira devolveu exatamente 5.000, sem erro nenhum. Toda leitura de
> tabela de domínio precisa paginar (`OFFSET/FETCH`) — catálogo curto não levanta exceção, só um
> `GetValueOrDefault` que não acha, e o resultado é diagnóstico sem nome no prontuário.

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
| `period.end` | `atendimento_ambulatorial.atendamb_datafinal` | ver 6.1 |
| `hospitalization.dischargeDisposition` | `UPA_Atendimento_Medico.tipsai_codigo` → `Tipo_Saida` | ver 6.1 |

#### 6.1 O fechamento do boletim (medido em 08/08/2026)

`Pronto_Atendimento` **só tem a chegada**. Quando a pessoa saiu e por quê está em outras duas
tabelas, ambas 1:1 com o boletim (90 dias na UPA: 36.075 linhas para 36.075 boletins distintos):

| Campo | UPA Inoã | Santa Rita | Serve? |
|---|---|---|---|
| `atendimento_ambulatorial.atendamb_datafinal` | 95,1% | 96,9% | **sim** — é a hora da saída |
| `UPA_Atendimento_Medico.tipsai_codigo` | 89,9% | 97,0% | **sim** — é o desfecho |
| `UPA_Atendimento_Medico.upaatemed_DataSaida` | 6,0% | 0,2% | **não** — nome certo, campo morto |

`atendamb_datafinal` é evento real, não fechamento em lote: se espalha pelas 24h do dia e 97,0%
cai dentro de 24h da chegada. Os 3% restantes são fechamento administrativo tardio — quem usar
isso como gatilho de contato com o paciente precisa da guarda de janela.

`Tipo_Saida` tem 17 desfechos nomeados; na UPA em 30 dias: 9.820 "A.1 – Atendimento em
consultório concluído", 385 evasão, 101 evasão sem atendimento médico, 67 alta por decisão
médica, 31 transferência, 12 óbito, 3 alta a pedido, 1 chegou cadáver. O conector traduz para o
ValueSet `discharge-disposition` do R4 **e preserva o código da origem** numa segunda `Coding`
(`urn:klinikos:tiposaida`): o R4 achata em `aadvice` tanto a evasão quanto a alta a pedido, e a
diferença importa para quem consome.

**Fechar o boletim não toca o boletim.** Dos 2.350 boletins fechados em 7 dias, **zero** tiveram
o `rv_atualizacao` do `Pronto_Atendimento` avançado — a escrita acontece na
`atendimento_ambulatorial`. Por isso o fechamento tem **fase de CDC própria** (`fechamento`),
com ponteiro no `rv_atualizacao` daquela tabela. Um CDC só pelo boletim importaria a chegada de
todo mundo e a saída de ninguém, que foi o estado do hub até 08/08/2026 (295 mil Encounters do
Klinikos, nenhum com `period.end`).

### Condition ← `UPA_Evolucao.cid_codigo_primario` / `_secundario` + `TB_CID`

Só das linhas com CID (INÍCIO, REAVALIAÇÃO, salas). A **reavaliação** pode mudar o CID — a
última prevalece; as anteriores não somem, viram histórico da mesma Condition.

A origem guarda **só o código** (`Z008`, sem ponto e com padding). O nome vem de `TB_CID`
(`CO_CID` → `NO_CID`), carregado uma vez por run. Sem ele o `text` repetia o código e o
prontuário exibia **"M545 · M545"** no lugar de "M54.5 · Dor lombar baixa". No `coding` o código
sai no formato canônico do ICD-10, com ponto depois da categoria de 3 caracteres; a chave de
busca no catálogo **ignora o ponto dos dois lados**, para não depender de as duas pontas
gravarem no mesmo formato. Catálogo indisponível **não derruba o run** — a Condition entra com o
código, porque enriquecimento que falha não pode custar dado clínico.

### Observation ← `UPA_SinaisVitais`

Colunas separadas (`pressaoarterial`, `pulso`, `temperatura`, `frequenciarespiratoria`, `hgt`,
`saturacaoO2`, `peso`) — **muito** melhor que o EAV do eDoc do Salux. Cada uma vira uma
Observation LOINC com `encounter` amarrado. A pressão vem como texto ("120/80") e precisa ser
partida em sistólica/diastólica.

### DocumentReference ← `UPA_Atendimento_Medico` (o boletim médico)

**É o documento principal do atendimento** — o análogo do eDoc "Boletim de Atendimento de
Urgência" do Salux. Um por atendimento (identifier `urn:klinikos:atendimento-medico` sobre o
`atendamb_codigo`), **não** um por edição: o médico reescreve o mesmo registro durante a
passagem, e cada gravação tem de atualizar o documento em vez de empilhar cópias.

Vai como **HTML**, com uma seção por campo — `Anamnese`, `Exame físico`, `Hipótese diagnóstica`,
`Conduta`, `Observação`. Texto puro não serve: são cinco campos distintos, e sem os títulos
ninguém sabe onde termina o exame físico e começa a conduta. O texto da origem é escapado antes
de entrar no HTML (campo livre digitado por humano). Boletim aberto e ainda **sem nada escrito
não vira documento** — documento vazio no prontuário é pior que documento nenhum.

`atendamb_codigo` **não é** o `spa_codigo`: na Santa Rita os dois divergem (`072504080003` vs
`072504080004`), então o JOIN com `atendimento_ambulatorial` é obrigatório.

### DocumentReference ← `UPA_Evolucao` (tipos narrativos)

`upaevo_descricao` é o texto **só nos tipos narrativos** (EVOLUÇÃO MÉDICA, EVOLUÇÃO DE
ENFERMAGEM, EVOLUÇÃO, PARECER SOLICITADO, PROTOCOLO DENGUE). Uma DocumentReference por linha,
`type` derivado do `Tipo`.

**Não viram documento:** `ESTORNO` (anulação — a origem considera cancelado), `INÍCIO DO
ATENDIMENTO MÉDICO` e as entradas de sala (texto constante), e **`REAVALIAÇÃO`** — cujo texto é
sempre a palavra "Reavaliação". O que a reavaliação carrega de real (CID revisado e sinais
vitais) já entra pelos caminhos próprios.

### MedicationRequest ← `Prescricao` + `Item_Prescricao_Medicamento`

Um por ITEM prescrito, com `ins_descricao` (o remédio), quantidade, unidade e via. **Não sai
mais de `UPA_Evolucao`** — ver o aviso do §3.1.

`Dosage.text` é montado do que a origem preenche; não se promete `timing` estruturado.
`itpresc_frequencia` é **intervalo em minutos** (medido: 1440, 720, 480, 360 = 24h, 12h, 8h, 6h),
com `0` dominando (221.012 de 337.723) no sentido de dose única na unidade — não "a cada zero
minutos". Valores negativos aparecem na cauda (−120, −180) sem semântica conhecida e viram nada.

Só a especialização de **medicamento** entra: `Item_Prescricao_Dieta`, `_Oxigenoterapia`,
`_Cuidados_Especiais` e `_Sinais_Vitais` não são MedicationRequest. Por isso a cobertura cai de
153.836 boletins com prescrição para **92.903 com item de medicamento** — o resto prescreveu
outra coisa, e inventar remédio ali seria pior que não ter.

### Higiene medida no dado real

O primeiro registro da base já mostra o padrão: `pac_telefone` = `0000000000`. É campo de
preenchimento obrigatório que a recepção completa com lixo quando o paciente não informa. O
conector descarta telefone com menos de 8 dígitos ou de dígito único — telefone falso no hub é
pior que telefone nenhum, porque alguém tenta ligar e o paciente entra em relatório de
"contactável" sem ser.

Outros formatos vistos e tratados: peso com vírgula decimal (`11,30`), pressão arterial como
`/` (sem medida), CID com padding à direita (`B34      `).

> **P2 — parcialmente resolvida em 11/08/2026.** A prescrição estruturada está viva e é a fonte
> do MedicationRequest desde então: `Prescricao` (237.873) + `Item_Prescricao_Medicamento`
> (337.723), com insumo, dose, unidade e via. O que **continua aberto** é o aprazamento
> (`Frequencia_Aprazamento_Prescricao` tem só 13 linhas) — sem ele não há
> `MedicationAdministration`, que segue sendo a pendência mais antiga do BAU clínico. Vale medir
> `ItemPrescricaoMedicamento_Lote` (12.281) e `Item_Prescricao_Medicamento_Insulina` (14.215)
> antes de prometer administração.

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
| ~~P1~~ | ~~Agente de Santa Rita offline~~ | **resolvido 04/08** — instância medida (§1.1) |
| P13 | **Os agentes caem sozinhos.** O da UPA caiu 8× em 04/08 (intervalos de 12 a 60 min) e não volta quando o processo morre na ponta — não parece ter supervisor (serviço/tarefa com reinício). Sem agente, o conector nem começa | **bloqueia carga e ciclo** |
| P2 | Prescrição estruturada / aprazamento não medidos nesta instância | não bloqueia a Fase 1 |
| P3 | CNS provisório (17.361): o que significa? | não — ficam de fora até decidir |
| P4 | ~~`MarcaDagua` precisa de watermark numérica por fase~~ | **resolvido 03/08** — ponteiros em `jsonb` |
| P5 | ~~Guarda de credencial do orquestrador rejeita base `ViaAgente`~~ | **resolvido 03/08** — guarda condicional |
| P8 | Primeiro run da UPA ainda **não foi disparado**: a base não tem agenda, então nada roda sozinho | decisão de quando ligar |
| P6 | Semântica dos códigos de administração de dose (`C`/`V`) | só quando P2 abrir |
| P9 | **ESTORNO não retroage**: a evolução anulada permanece `current` no hub (só a nova não entra). Retração precisa do vínculo estorno→alvo, que a origem não expõe | qualidade, não bloqueia |
| P10 | CNS não é âncora secundária de dedup (cobertura de 0,16% no cadastro; provisórios sem semântica) | decidir com validação CADSUS |
| P11 | Entradas de sala amarela/vermelha não viram `statusHistory` (o CID delas JÁ é extraído) | Fase 2 |
| P12 | `MedicationRequest.status` fixo em `completed` para prescrição histórica | aceito para importação |
| P7 | Internação: morta no Klinikos desde 25/01/2026. Se o Conde migrar como a UPA roda hoje, o hub **perde a internação no dia do cutover** | não bloqueia hoje; **bloqueia o cutover do Conde** |
