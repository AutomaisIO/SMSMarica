# Visão — SMS Maricá

> Esse documento existe para manter o **norte estratégico** visível em decisões cotidianas. Quando uma decisão técnica ou de produto puder ser interpretada de várias formas, escolha a que avança o objetivo descrito aqui.

## 1. Objetivo final

**Tornar o SMSMais o hub central de informação clínica da Secretaria Municipal de Saúde de Maricá**, concentrando as funções de gestão de saúde do município e recebendo **cópia das informações** dos sistemas PEP (Prontuário Eletrônico do Paciente) hoje espalhados pelas unidades de saúde.

A forma técnica desse hub é um **servidor FHIR R4**: o modelo padrão internacional (HL7 FHIR Release 4) é a moeda de troca entre os diferentes sistemas legados e o repositório central.

## 2. Escopo do que deve fluir para o hub

Tudo que compõe o prontuário eletrônico do cidadão maricaense, vindo dos vários sistemas operados pelas unidades:

- **Cadastros de pacientes** (demografia, contatos, documentos, CNS)
- **Atendimentos** (consultas, encontros, motivo, profissional, unidade)
- **Prontuários clínicos** (anamnese, evolução, condutas, prescrições)
- **Internações** (admissão, alta, transferência, leito)
- **Exames laboratoriais** (pedidos e resultados)
- **Exames de imagem** (estudos DICOM via PACS — ver [`pacs.md`](./pacs.md))
- **Vacinação, dispensação de medicamentos, encaminhamentos** e demais eventos clínicos relevantes

A intenção é que, com o tempo, qualquer profissional autorizado consiga consultar a história completa do cidadão **independentemente da unidade onde ele foi atendido**.

## 3. Estratégia: migração incremental

Não há tentativa de "big bang". O modelo é:

1. **Construir um módulo de cada vez** dentro do SMSMais, com seu próprio domínio interno bem modelado.
2. **À medida que o módulo amadurece**, expor/consumir seus dados via recursos FHIR R4 equivalentes (`Patient`, `Encounter`, `Observation`, `DiagnosticReport`, `ImagingStudy`, `MedicationRequest`, …).
3. **Conectar progressivamente** com cada sistema PEP das unidades — começando pelos mais críticos ou pelos que oferecem integração mais fácil — recebendo cópia dos eventos clínicos.
4. **Ir compreendendo o posicionamento** de cada subsistema (cidadão, agente, painel administrativo) dentro desse hub à medida que ele cresce.

> O módulo atual em desenvolvimento (transporte sanitário — Pacientes, Tratamentos, Sessões, Translados, Avaliações) é o **primeiro vetor** desse caminho: ele entrega valor operacional imediato à Secretaria **e** já modela o cidadão de uma forma que conversa naturalmente com `Patient` do FHIR.

## 4. Princípios de decisão

Quando estiver em dúvida entre alternativas técnicas, prefira a que:

- **Aproxima o modelo de dados de FHIR R4**, mesmo que ainda não exista um endpoint FHIR exposto. Nomes, cardinalidades e identificadores devem caminhar nessa direção.
- **Preserva a história completa do cidadão.** O hub é fonte de verdade longitudinal — não sobrescrever silenciosamente, preferir append/versionamento sobre update destrutivo em dados clínicos.
- **Mantém identificadores estáveis e federáveis** (CPF, CNS, identificadores das unidades). São a cola entre sistemas heterogêneos.
- **Não acopla a regra de negócio a um sistema legado específico.** Adaptadores ficam na borda; o core fala FHIR/domínio próprio.
- **Permite que um sistema externo continue funcionando** se o SMSMais ficar offline. Cópia, não SPOF de operação clínica.

## 5. O que NÃO é objetivo (escopo negativo)

- **Substituir os PEPs das unidades.** Eles continuam operando localmente; o hub recebe cópia.
- **Ser PEP de unidade.** O SMSMais é hub agregador + sistema operacional para funções municipais (transporte, gestão), não o PEP de ponta-de-cuidado.
- **Resolver tudo na primeira versão.** Cada módulo é integrado quando faz sentido — não há cronograma rígido para "ter tudo em FHIR".

## 6. Relação com o roadmap atual

O [`roadmap.md`](./roadmap.md) descreve marcos M1..M7 do módulo de transporte sanitário. M7 ("Integrações externas") é o ponto natural onde a estratégia FHIR R4 começa a aparecer no plano operacional. Antes disso, o trabalho é **construir a base de identidade do cidadão (Usuário + Paciente)** que servirá como `Patient` no hub.

Decisões posteriores sobre como expor recursos FHIR (camada própria, biblioteca como Firely .NET SDK, servidor FHIR dedicado tipo HAPI ou Microsoft FHIR Server, etc.) serão tomadas via novos ADRs quando o momento chegar. Por ora, é suficiente que **o modelo interno não se afaste** desse destino.
