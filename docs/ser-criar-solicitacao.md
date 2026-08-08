# SER — a tela de CRIAR solicitação (aba *Editar*)

Levantado por sonda somente-leitura em 08/08/2026 (`Automais.SER/probe_campos_dinamicos.py`).
Complementa [`docs/ser.md`](./ser.md), que cobre a leitura da fila.

> **Nada foi criado no SER.** A sonda só troca aba e combos, que apenas re-renderizam a view.
> O botão `Gravar` (`form0:j_id313`) nunca foi acionado — e a trava da sonda recusa qualquer
> parâmetro com verbo de escrita.

## 1. Como se chega lá

Mesma tela da fila (`solicitar-consulta-pesquisar.seam`), aba **Editar**. A troca de aba é
`_JSFFormSubmit` — **POST comum**, sem `AJAXREQUEST`, mandando os campos do form mais:

```
form0:editar_server_submit = form0:editar_server_submit
```

## 2. O formulário tem duas partes

Um **bloco fixo**, igual para todo pedido, e um **bloco dinâmico** (`form0:camposDinamicos`)
que muda conforme o Recurso escolhido. É o bloco dinâmico que responde à pergunta "por que
oncologia pede campos diferentes".

### 2.1 Bloco fixo

| Campo | id JSF | Tipo | Obrigatório |
|---|---|---|---|
| É Ambulatório Estadual? | `form0:comboSisReg` | select (Sim/Não) |  |
| Tipo | `form0:comboTipoRecurso` | select (CONSULTA/EXAME) | sim |
| Recurso | `form0:comboRecurso` | select (populado por Tipo) | sim |
| Recurso (autocomplete) | `form0:suggRecurso` | suggestionbox |  |
| CNS do paciente | `form0:numeroCADSUS` | text |  |
| Médico solicitante identificado? | `form0:booleanMedicoSolicitanteIdentificado_radio` | radio S/N |  |
| Médico responsável | `form0:medicoResp` | select (876 opções) |  |
| Telefone do médico | `form0:telefoneCelularMedico` | text |  |
| Especialidade do médico | `form0:especialidadeMedico` | text |  |
| Classificação de Risco | `form0:classificacao_risco` | select (Prioridade 1–4) | sim |
| Hipótese | `form0:procedimento` | text | sim |
| Mandado Judicial | `form0:naturezaSolicitacaoMandato_radio` | radio S/N |  |
| Unidade de origem identificada? | `form0:unidadeDeOrigemIdentificada_radio` | radio S/N |  |
| Unidade de origem (texto livre) | `form0:unidadeNaoIdentificada` | text | sim |

Ações da aba: `form0:addMedico` (Adicionar médico), `form0:j_id299` (Anexar Arquivo) e
**`form0:j_id313` (Gravar)** — este último é escrita e está fora de qualquer uso nosso.

### 2.2 Bloco dinâmico

Cada campo é um par `form0:container_dinamico_id_<N>` (rótulo) + `form0:dinamico_id_<N>`
(o campo). Trocar o Recurso dispara A4J (`form0:j_id57`, com `ajaxSingle=form0:comboRecurso`)
e o SER devolve o bloco re-renderizado. O `oncomplete` chama `verificarPetCt()` — o PET-CT tem
regra própria, e de fato é o único recurso com formulário exclusivo (§4).

> **A resposta é PARCIAL**: não traz `<form id="form0">`. Vale a regra de sempre — usar a
> última página completa como fonte dos campos e o parcial só como fonte do conteúdo novo.

## 2.3 Anexar arquivo — como o upload funciona

Levantado em 08/08/2026 lendo o `ui.pack.js` do próprio SER. **Nunca exercitado**: subir arquivo
é escrita, e a sonda para antes disso. O que está aqui é o protocolo, não uma prova de execução.

O botão *Anexar Arquivo* (`form0:j_id299`) é um A4J cujo `oncomplete` abre o modal
`modalAnexarArquivo`. Dentro do modal vive **outro form**, separado do `form0`:

| | |
|---|---|
| Form | `formAnexar`, `enctype="multipart/form-data"` |
| Componente | `rich:fileUpload` (`formAnexar:upload`) |
| Campo do arquivo | `formAnexar:upload:file` |
| Botões | `formAnexar:j_id320` (**Anexar** — escrita) e `formAnexar:j_id319` (Cancelar) |
| Limites declarados | `maxFileBatchSize: 2`, `noDuplicate: true` |

**O envio dos bytes não usa o `action` do form.** O RichFaces reescreve o `action` na hora e
submete num iframe escondido — um POST `multipart/form-data` por arquivo, para:

```
/ser/pages/consultas-exames/solicitacao/solicitar-consulta-editar.seam
  ?_richfaces_upload_uid=<uid aleatório>
  &formAnexar:upload=formAnexar:upload
  &_richfaces_upload_file_indicator=true
  &AJAXREQUEST=_viewRoot
```

Antes de submeter, o componente **desabilita todos os outros `input[type=file]`** do form, para
que vá exatamente um arquivo por requisição. O progresso e o cancelamento andam por fora, num A4J
paralelo com `_richfaces_file_upload_action` + `_richfaces_upload_uid`.

Há um caminho alternativo por **Flash** (`FileUploadComponent.swf`), que monta a mesma URL mas
embute `;jsessionid=` no caminho e acrescenta `_richfaces_size` e `_richfaces_send_http_error`.
Para isso, **o `JSESSIONID` é impresso em texto claro no HTML da página**, como argumento do
construtor do componente — o plugin não enxerga cookie. É observação de segurança do alvo, não
algo que a gente use.

Os anexos já enviados aparecem em `form0:anexoList`, com as colunas **Data, Nome do Arquivo,
Usuário e Ação**.

> **Para uma futura integração de escrita**, o anexo é o passo mais delicado: são duas conversas
> distintas (o `form0` do pedido e o `formAnexar` do arquivo) amarradas pela mesma sessão Seam,
> e o arquivo sobe *antes* de o pedido ser gravado.

## 3. O catálogo medido

**203 recursos** (120 consultas + 83 exames)
produzem **21 formulários distintos**, com **163 campos dinâmicos únicos**
(90 obrigatórios).

O formulário padrão — **Queixa Principal, Resultado de Exames, Observações**, todos
obrigatórios — cobre 138 dos 203 recursos. O resto é especialidade pedindo dado clínico.


## 4. CONSULTA — 120 recursos, 15 formulários

### 63 recurso(s) · 3 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto)
- Ambulatório 1ª vez em Cardiologia - Cirurgia Cardíaca Pediátrica
- Ambulatório 1ª vez em Cardiologia - Hipertensão Arterial Resistente (Adulto)
- Avaliação de Cardiopatia Congênita Pediátrica (Internados)
- Reabilitação Cardíaca
- Ambulatório 1ª vez em Cardiologia - Doenças Neuromusculares
- *… e mais 57*

### 30 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia)
- Ambulatório 1ª vez - Hematologia (Oncologia)
- Ambulatório 1ª vez - Oncologia Geral (Adulto)
- Ambulatório 1ª vez - Mastologia (Oncologia)
- Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia)
- Ambulatório 1ª vez - Urologia (Oncologia)
- *… e mais 24*

### 4 recurso(s) · 5 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Descreva o Tratamento Conservador realizado, se houver | `textarea` |
| **obrig.** | Quanto tempo durou o tratamento? | `text` |
| **obrig.** | Observações | `textarea` |
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |

- Ambulatório 1ª vez - Cranio Maxilo Facial (Infantil)
- Ambulatório 1ª vez - Cranio Maxilo Facial (Adulto)
- Ambulatório 1ª vez - Microcirurgia Reconstrutora (Adulto)
- Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo)

### 4 recurso(s) · 8 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | IMC do Paciente | `text` |
| **obrig.** | Comorbidades: Diabetes Hipertensão Doenças articulares Doenças vasculares Depressão | `checkbox` |
|  | Exames Complementares (Data e Laudo) | `textarea` |
|  | Medicação em Uso (especificar Droga, Dosagem, e Tempo de Uso) | `textarea` |
|  | Laudo/Anamnese | `textarea` |
| **obrig.** | Altura do Paciente (cm) | `text` |
|  | Outras comorbidades | `textarea` |

- Readequação Corporal Pós-Cirurgia Bariátrica
- Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto)
- Ambulatório 1ª vez - Cirurgia Bariátrica - Superobesidade (IMC acima 55)
- Ambulatório 1ª Vez em Gestação pós cirurgia bariátrica

### 4 recurso(s) · 4 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |

- Avaliação diagnóstica infecção congênita Zika/Storch/Oropuche
- Ambulatório de 1ª Vez - Transplante de Fígado (Infantil)
- Ambulatório de 1ª Vez - Transplante Renal (Adulto)
- Ambulatório de 1ª Vez - Transplante de Fígado (Adulto)

### 3 recurso(s) · 13 campos

| | Campo | Tipo |
|---|---|---|
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do Laudo do Ultrasson Doppler Arterial | `textarea` |
|  | Descrição do Laudo da Arteriografia | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Arterial Periférica
- Ambulatório 1ª vez em Cirurgia Vascular - Pé diabético
- Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Carotídea

### 2 recurso(s) · 6 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Resultado de exames pré-operatórios de rotina (hemograma completo e coagulograma) | `textarea` |
| **obrig.** | Descrição do laudo do ECG | `textarea` |
| **obrig.** | Telefone de Contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Arritimias (Infantil)
- Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação

### 2 recurso(s) · 13 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do laudo do ECG e ou Holter | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Implante de Marcapasso
- Ambulatório 1ª vez em Cardiologia - Implante de Ressincronizador Cardíaco

### 2 recurso(s) · 14 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação de Stanford: Tipo A) Dissecções em que há o comprometimento da aorta ascende | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do Laudo do Cateterismo | `textarea` |
|  | Descrição do Laudo da TC ou Ultrasson detalhado com medidas do diâmetro da Aorta | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Cardiovascular - Aneurisma / Dissecção de Aorta Torácica
- Ambulatório 1ª vez em Cirurgia Vascular - Aneurisma / Dissecção de Aorta Abdominal

### 1 recurso(s) · 14 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do laudo do Cateterismo | `textarea` |
|  | Descrição do laudo do Ecocardiograma | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Cardiovascular - Cirurgia Orovalvar

### 1 recurso(s) · 15 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia ( CSCC ): I Paciente cardiop | `select` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
| **obrig.** | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
| **obrig.** | Hemograma Completo | `textarea` |
| **obrig.** | Coagulograma | `textarea` |
| **obrig.** | Glicose | `text` |
| **obrig.** | Ureia | `text` |
| **obrig.** | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
| **obrig.** | Descrição do laudo do Cateterismo | `textarea` |
| **obrig.** | Descrição do laudo do Ecocardiograma | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Cirurgia de Revascularização do Miocárdio

### 1 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Classificação ;funcional da New York Heart Associaton (NYHA): I Atividade física comum com | `select` |
| **obrig.** | Resultado de exames pré-operatórios de rotina (Rx de tórax, hemograma completo, coagulogra | `textarea` |
| **obrig.** | Descrição do laudo do Ecocardiograma, com função de VE [colocando o % da fração de ejeção  | `textarea` |
| **obrig.** | Descrição do laudo do ECG e ou Holter, com a duração do QRS | `textarea` |
| **obrig.** | Resultado do estudo eletrofisiológico | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª Vez em Cardiologia - Implante de Cardiodesfibrilador (CDI)

### 1 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia (CSCC), para angina: I Pacie | `radio` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA), para insuficiência cardíaca:  | `radio` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Laudo da Ergometria, Cintilografia, Ecocardiograma com Dobutamina ou ECG e Enzimas | `textarea` |
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Peso do paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |

- Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana

### 1 recurso(s) · 6 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |
|  | Descrição do laudo da TC | `textarea` |
|  | Descrição do laudo da Angiografia Cerebral | `textarea` |

- Ambulatório 1ª vez em Neurocirurgia - Neurovascular

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Resultado do histopatológico | `textarea` |
| **obrig.** | Exames complementares | `textarea` |
|  | Observações | `textarea` |
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |

- Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil)


## 5. EXAME — 83 recursos, 6 formulários

### 75 recurso(s) · 3 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ecocardiograma Transesofágico (ambulatorial)
- Ecocardiograma de Estresse
- Toracocentese
- Biópsia de Pleura
- Biópsia de Gânglio
- Escarro Induzido
- *… e mais 69*

### 3 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia (CSCC), para angina: I Pacie | `radio` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA), para insuficiência cardíaca:  | `radio` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Laudo da Ergometria, Cintilografia, Ecocardiograma com Dobutamina ou ECG e Enzimas | `textarea` |
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Peso do paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |

- Cateterismo Cardíaco (Internados)
- Cateterismo Cardíaco (Ambulatorial)
- Cateterismo Cardíaco Pediatrico (Ambulatorial)

### 2 recurso(s) · 5 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Descrição do Laudo do Ultrasson Doppler Arterial e/ou TC | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Arteriografia Periférica (Ambulatorial)
- Arteriografia Periférica (Internados)

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Presença de massa ou neoformação pulmonar? SIM NÃO | `radio` |
| **obrig.** | Necessita de biópsia brônquica ou transbrônquica? SIM NÃO | `radio` |
| **obrig.** | Paciente com hemoptise ou escarro hemoptoico? SIM NÃO | `radio` |
| **obrig.** | Suspeita de Estenose de traquéia/estridor/cornagem? SIM NÃO | `radio` |
| **obrig.** | Paciente intubado? SIM NÃO | `radio` |
| **obrig.** | Paciente traqueostomizado? SIM NÃO | `radio` |
| **obrig.** | Paciente com idade menor ou igual a 16 anos? SIM NÃO | `radio` |
|  | Observações | `textarea` |
|  | Hipótese diagnóstica | `textarea` |
|  | Quadro Clínico | `textarea` |

- Broncoscopia (Internados)

### 1 recurso(s) · 4 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |

- Cintilografias (Internados)

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Laudo Histopatológico/Exame Imagem | `textarea` |
| **obrig.** | Grau Histopatológico Gx G1 G2 G3 G4 | `select` |
| **obrig.** | PSA/Outros | `text` |
|  | Observação | `textarea` |
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |

- Tomografia por Emissão de Pósitrons (PET-CT)
