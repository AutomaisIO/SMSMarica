# Diagnóstico — Worklist (MWL) no mamógrafo Fuji FDR-3000AWS não inicia exame

> **Status:** causa raiz identificada e comprovada (2026-06-22). Resolução pendente de
> **1 ajuste de serviço no console** (engenheiro Fuji) + emissão de 1 código pelo backend SMSMais.
>
> Documento para o **técnico/engenheiro Fuji** e para a equipe SMSMais. Em PT-BR.

## 1. Resumo executivo

A worklist DICOM (MWL) **chega** ao console e os exames **aparecem** na lista. Mas ao
**Iniciar** um exame vindo da worklist, o console falha com erro **31027** ("Falhou a
operação de obter informações de imagem" → "contacte o engenheiro / desligue o sistema").
Cadastro **manual** funciona normalmente.

**Causa raiz (comprovada nos arquivos do próprio console):** a tabela que traduz o código de
procedimento da worklist para um **menu de exposição** do console —
`JJ1017V3CodeMapping` em `IIP\Config\JJMenuCodeMapping.mdb` — está **VAZIA (0 linhas)**.
Sem tradução, o console não resolve o menu, a "lista de exposições" (`IdExposureList`) fica
vazia, e o fluxo aborta. **A worklist nunca foi comissionada nesta máquina.**

**Resolução (1 visita):**
1. **Popular** `JJ1017V3CodeMapping` com (no mínimo) **1 linha**: `FCRCode = FCR0329`
   (= menu **MAMO BILATERAL**) ↔ um **JJCode** combinado com a SMSMais.
2. **Configurar destino de MPPS** apontando para `WORK-CDT` (ou desabilitar o "Inform
   Procedure State"), pois hoje **não há destino de MPPS** configurado.
3. **SMSMais** passa a emitir esse **JJCode fixo** no `Scheduled Protocol Code Sequence`.

---

## 2. Identificação do equipamento e da rede

| Item | Valor |
|------|-------|
| Console (HostName) | `AH57024557` |
| Modelo / software | **FDR-3000AWS**, Mainsoft **V9.3** (perfil **AWS V4.2**) |
| Station Name (0008,1010) | `MCU0` |
| AE local (SCU) | **`FDR-MAMO`** — IP `10.1.92.177` |
| Instituição | `CDT ENF MARIA IVONILDA R COELHO` |
| Detector/reader | `FDR-3500DR` (IP `192.168.0.101`) |

**Rede DICOM (origem: `IIP\Config\NetConfig.mdb`) — está CORRETA, não mexer:**

| Função (`ConnectInfo`) | AE destino | IP : Porta | Papel | Transfer Syntax |
|---|---|---|---|---|
| **MWM** (worklist) | `WORK-CDT` | `104.236.203.40 : 11112` | SCP | Implicit VR LE |
| **ROUXINOL** (envio de imagem / store) | `PACS-CDT` | `104.236.203.40 : 11112` | SCP | Implicit VR LE |
| READER | `FDR-3500DR` | `192.168.0.101 : 104` | — | — |
| **PPS / MPPS** | **(ausente)** | — | — | — |

> A consulta de worklist sai correta para `WORK-CDT@104.236.203.40:11112`. **O problema NÃO
> é de rede.** Falta apenas (a) o mapeamento de menu e (b) o destino de MPPS.

---

## 3. Sintoma observado

1. A worklist é consultada (Modality=MG, `ISO_IR 100`, sem filtro de estação/data) e os
   itens **aparecem** na lista do console. ✅
2. Ao selecionar um item e **Iniciar**, surge **`[31027] Falhou a operação de obter informações
   de imagem → Contacte o seu engenheiro de serviço. Pressione o botão inferior para fechar esta
   caixa de diálogo e desligar o sistema.`**
3. Cadastro **manual** (sem worklist) adquire e armazena normalmente. ✅

> **Nota:** o erro **`[31141] Há informação de paciente inválida`** ocorria numa fase anterior e
> **já foi resolvido** (preenchendo `IssuerOfPatientID` + campos obrigatórios — ver §6.2). Hoje
> **só ocorre o 31027**. Mantido aqui apenas como histórico.

### Cadeia de erro (Visualizador de Eventos do Windows do console)

```
FFIipInput          · EventID 13060 · GetStudyInfo falha buscando "IdExposureList" (retorno 1)
        ↓
FFCustomMessageBox  · EventID 31027 · "Falhou a operação de obter informações de imagem"
```

`FFIipInput` é o módulo de entrada do IIP (Image Information Processing) — o mesmo cujo AE de
worklist é `IIP_MWL_SCU`.

---

## 4. Causa raiz (comprovada)

Ao **Iniciar** um item de worklist, o console executa:

```
1. Lê o item recebido de WORK-CDT (Scheduled Procedure Step Sequence).
2. Pega Scheduled Protocol Code Sequence (0040,0008) > Code Value (0008,0100).   [OBRIGATÓRIO]
3. TRADUZ esse código via  JJ1017V3CodeMapping  (JJCode-16M/16S  →  FCRCode).
4. Usa o FCRCode para localizar o menu em  MenuDataDB  (ExaminationMenu / ExposureMenu).
5. Monta a lista de exposições (IdExposureList) e inicia.
```

**O passo 3 quebra:**

```
Arquivo : IIP\Config\JJMenuCodeMapping.mdb
Tabela  : JJ1017V3CodeMapping
Colunas : ID, FCRCode, JJCode-16M, JJCode-16S, Update
Linhas  : 0   ← VAZIA
```

Sem nenhuma linha, **nenhum** código que a worklist enviar é traduzível para um menu →
`IdExposureList` vazio → `GetStudyInfo` (EventID 13060) falha → **31027**.

Isso explica por que **nenhuma** tentativa de mudar o conteúdo do item funcionou (testamos
`0349`, `MAMO BILATERAL`, JJ1017 canônico, etc.): **não há nada do outro lado para casar**.

### Auditoria completa dos bancos de configuração (51 arquivos `.mdb`)

Todos os 51 `.mdb` exportados do console foram abertos e inventariados. Confirma-se:

- **`JJ1017V3CodeMapping` = 0 linhas em AMBAS as cópias** — `IIP\Config\JJMenuCodeMapping.mdb`
  **e** `IIP\AWS\User\JJMenuCodeMapping.mdb` (config ativa). As duas estão **datadas de
  2018-11-16 (data de instalação) — nunca foram tocadas/preenchidas**, enquanto o `MenuData.mdb`
  é de 2026-06-16 (menus mantidos). Prova de que o mapeamento worklist→menu **nunca foi
  comissionado**.
- **`MassOrder.mdb` → `MassOrderMenu` = 0 e `PreSetMenu` = 0** (colunas `MenuCode`, `NameSbcs`,
  `NameDbcs`, `ExmaMenuFlag`, …) — tabelas de menu/preset **também nunca preenchidas**, reforçando
  que a comissão de worklist nunca foi feita.
- **Nenhum** `.mdb` contém as tabelas de menu de exposição (`ExaminationMenu`/`ExposureMenu`/`SetMenu`
  do `MenuDataDB`); essa base vive no app de aquisição (não exportado) e foi reconstruída via
  `ROUTINE*.INF` (§5).
- **`DicomPpsTag.mdb` → `Pps` = 10 tags** definidas, porém **sem destino** em `NetConfig.ConnectInfo`
  (MPPS configurado em tags, mas sem para onde enviar).

### Evidências de apoio (todas dos arquivos do console)

- `IIP\Config\MWMTagSetting.mdb` → `UserMapping`: o `Scheduled Protocol Code Sequence` e seu
  `Code Value` têm **`RQFlag=True`** (requerido) e `Enabled=1`.
- `IIP\Config\MWMConfig.def` (e `MWMConfig.mdb`/`DicomCustom`): a coluna **`MenuName` é derivada
  de `00400100 > 00400008 > 00080100`** (SPS Seq > Scheduled Protocol Code Seq > Code Value).
- `DicomCustom` (55 regras) contém entradas **`[AWSV4.2]JJ1017Ver3 Protocol Context SQ`**
  mapeando `00400008 > 00400440` — ou seja, o console **usa JJ1017Ver3** e o contexto do
  protocolo vai em **Protocol Context Sequence (0040,0440)**.
- `IIP\Config\JJ1017FRisDef.inf`: o código tem duas partes — **`JJ1017-M`** e **`JJ1017-S`**
  (versão **3.x**), batendo com as colunas `JJCode-16M`/`JJCode-16S` da tabela vazia.
- `IIP\Config\NetConfig.mdb` → `ConnectInfo`: **não há entrada `PPS`/MPPS** (só `MWM`, `ROUXINOL`,
  `READER`). O Conformance diz que o console **cria MPPS automaticamente ao iniciar** um item de
  worklist — sem destino, esse passo não se completa (e a captura de rede no "Iniciar" sai vazia,
  confirmando que nada de MPPS é enviado).

---

## 5. A "outra ponta": cadastro de menus (autoritativo)

O `MenuDataDB` **foi localizado**: `IIP\Param\MenuData.mdb` (cópia ativa em
`IIP\AWS\User\MenuData.mdb`, modificada **2026-06-16** — menus ativamente mantidos). Tabela
`ExaminationMenu`, colunas `Code` / `NameSbcs` — os menus de estudo que aparecem na tela:

| Code (`ExaminationMenu`) | Menu (tela) |
|---|---|
| **`FCR0329-00`** | **MAMO BILATERAL** ← alvo do rastreamento |
| `FCR0329-01` / `FCR0329-02` | MAMOGRAFIA E / MAMOGRAFIA D |
| `FCR0321-00` / `FCR0321-01` | UNILATERAL D / UNILATERAL E |
| `123` | MAMO PROTESE *(código curto custom)* |
| `FCR0339-00` | MAMMOGRAPHY MLO |

Exposições individuais (`ExposureMenu`, com `MpmCode`): `FCR0349-0001`/`0349` = **CCD**,
`FCR0349-0000` = CCE, `FCR0359-0001`/`0359` = MLOD, `FCR0359-0000` = MLOE, `350`/`03F9` =
CCD PROTESE, etc.

> Para **rastreamento mamográfico**, o menu é **`FCR0329-00` = MAMO BILATERAL** (expande nas 4
> incidências CCD/CCE/MLOD/MLOE = RCC/LCC/RMLO/LMLO). Confirmado pela tag
> `AcqDeviceProcessingCode (0018,1401) = 0349` (CCD) gravada nas imagens reais no PACS.

---

## 6. Resolução

### 6.1 Console (engenheiro Fuji) — destrava o problema

**(a) Popular `JJ1017V3CodeMapping`.** Abordagem mínima e robusta (recomendada): **1 linha**
mapeando o menu padrão de mamografia:

```
FCRCode      = FCR0329           (= MAMO BILATERAL)
JJCode-16M   = <código JJ1017-M combinado com a SMSMais>
JJCode-16S   = <código JJ1017-S combinado com a SMSMais>
```

Com isso, **todo** exame de mamografia vindo da worklist abre **já no menu MAMO BILATERAL**, e o
técnico **ajusta na hora** se precisar (trocar incidência, etc.). Opcionalmente, mapear também
`FCR0321` (unilateral), `FCR0331` (spot), etc., uma linha por menu.

> Alternativa (modo "auto puro"): configurar o console para, quando o código não casar, **abrir a
> tela de seleção de menu** em vez de dar erro (fallback manual). Nesse caso o técnico escolhe o
> menu a cada exame. A SMSMais prefere a abordagem da linha única (abre já no menu padrão).

**(b) Configurar destino de MPPS** apontando para `WORK-CDT` (`104.236.203.40:11112`), **ou**
desabilitar o "Inform Procedure State" (MPPS) se não for usado. Hoje o `ConnectInfo` não tem
entrada `PPS`.

### 6.2 SMSMais (backend) — já pronto / a ajustar

O item de MWL emitido pelo backend já está conforme o que o console exige (verificado):

- `SpecificCharacterSet = ISO_IR 100` (Latin-1 — **não** usar `ISO_IR 192`/UTF-8, o console descarta).
- `IssuerOfPatientID` preenchido (sem ele dava o erro **31141** "informação de paciente inválida").
- `AccessionNumber`, `RequestedProcedureID`, `Scheduled Procedure Step ID` com **≤ 10 caracteres**
  (o FDR recusa SH > 10).
- `StudyInstanceUID` único e estável por exame; `ScheduledStationAETitle = FDR-MAMO`; `Modality = MG`.

**A ajustar:** emitir, no `Scheduled Procedure Step Sequence > Scheduled Protocol Code Sequence
(0040,0008)`, o **JJCode** combinado em 6.1(a):
- `Code Value (0008,0100)` = JJCode-16M (SH, ≤10)
- `Coding Scheme Designator (0008,0102)` = conforme JJ1017 (`JJ1017-M` / esquema definido com a Fuji)
- (se exigido) `Protocol Context Sequence (0040,0440)` para o JJ1017Ver3.

> O valor exato do JJCode deve ser **acordado entre SMSMais e o engenheiro Fuji** no momento de
> popular a tabela — os dois lados precisam usar o mesmo par.

---

## 7. O que já foi descartado (para a Fuji não perder tempo)

Nenhum destes altera o resultado — porque a causa é a **tabela de tradução vazia**, não o conteúdo:

- Charset do item (testado `ISO_IR 192` vs `ISO_IR 100`).
- Tamanho/formato do `PatientID` (CPF 11 díg, ID curto numérico).
- Formato do `StudyInstanceUID` (`2.25...` longo vs `1.2.826...` curto).
- Comprimento de `AccessionNumber`/`RequestedProcedureID`/`SPS ID` (todos ≤10).
- Data agendada (incl. dia ≤ 12 — não era ambiguidade de formato de data US/BR).
- Enviar `Code Value = 0349` ou `MAMO BILATERAL` **direto** no worklist (não funciona sem o
  mapeamento, pois a tradução JJ→FCR está vazia).
- A rede DICOM já está correta (worklist→`WORK-CDT`, store→`PACS-CDT`).

---

## 8. Apêndice — arquivos-fonte (exportados do console)

| Caminho (no console / no material exportado) | O que contém |
|---|---|
| `IIP\Config\JJMenuCodeMapping.mdb` → `JJ1017V3CodeMapping` | **Tradução worklist→menu — VAZIA (causa raiz)** |
| `IIP\Config\MWMTagSetting.mdb` → `UserMapping` | Processamento de tags da MWL (SPCS = `RQFlag=True`) |
| `IIP\Config\MWMConfig.def` / `MWMConfig.mdb` | `MenuName ← SPCS Code Value`; modo de consulta `Query=BQ` |
| `IIP\Config\NetConfig.mdb` → `ConnectInfo`/`DeviceInfo` | Nós DICOM (rede OK; **sem MPPS**) |
| `IIP\Config\JJ1017FRisDef.inf` | Formato JJ1017-M / JJ1017-S (v3.x) |
| `IIP\Data\Image\ROUTINE*.INF` | Estudos reais → reconstrução **FCR0329 = MAMO BILATERAL**, `0349 = CCD` |
| Windows Event Log | `FFIipInput` 13060 (GetStudyInfo/IdExposureList) → `FFCustomMessageBox` 31027 |

---

*Investigação conduzida por engenharia reversa dos arquivos de configuração exportados do próprio
console (AnyDesk), do Conformance Statement oficial do FDR-3000AWS, e de captura de rede no
servidor PACS. Documento gerado em 2026-06-22.*

---

## 9. Decisões de produto enquanto a worklist não executa (2026-06-24)

Como o Fuji ainda **não executa** nossas solicitações via worklist (causa raiz: tabela de
tradução `JJ1017V3CodeMapping` vazia — ver §1), tomamos duas decisões:

### 9.1 Toggle "Enviar para Worklist" por Tipo de Exame (IMPLEMENTADO)

`TipoExame.EnviarParaWorklist` (coluna `enviar_para_worklist`, default `true`). Quando **desligado**:
- A solicitação é **criada normalmente** (status `Solicitada`), mas **não é enfileirada** para
  envio (`ProximaTentativaEm = null`) e o worker (`EnviadorWorklistService`) a **ignora** (filtro
  `s.TipoExame.EnviarParaWorklist`). Nada é enviado ao dcm4chee/MWL.
- Configurável em **Tipos de Exame → editar** (checkbox "Enviar para a Worklist do PACS").
- **Plano atual:** manter **desligado** no tipo de mamografia até sabermos o que o Fuji espera
  para rodar via worklist.

### 9.2 Código que define "o que a máquina executa" (PENDENTE — não implementado)

Esclarecimento de conceitos (para não confundir no futuro):

- **SPS ID — Scheduled Procedure Step ID (0040,0009):** é só um **identificador** do passo
  agendado (chave junto com o StudyInstanceUID nos GET/DELETE do MWL). **Não** diz o que a máquina
  executa. Já enviamos um curto e único, derivado do AccessionNumber (`ConstrutorMwlItem.SpsId`).
  Se omitido, o dcm4chee gera um `SPS-XXXXXXXX` (>10 chars) que o Fuji recusa — por isso enviamos
  o nosso.
- **O que o Fuji "executa"** (qual menu/protocolo ele roda) vem de **Modality (0008,0060)** +
  **Scheduled Protocol Code Sequence (0040,0008)** — este último é o código que o console traduz
  para o menu via `JJ1017V3CodeMapping` (a tabela vazia da causa raiz).

Situação no backend: `TipoExame.CodigosProtocolo` **existe** (mapeado conceitualmente para o
0040,0008), mas o `ConstrutorMwlItem` **ainda não envia** a tag 0040,0008 no item MWL.

**A implementar quando soubermos o código exato que o Fuji espera** (provavelmente após o
engenheiro Fuji popular o `JJ1017V3CodeMapping`, mapeando o nosso code value → `FCR0329 = MAMO
BILATERAL`):
1. Expor no Tipo de Exame um campo claro para o **código de protocolo do equipamento (0040,0008)**
   — reaproveitando ou substituindo `CodigosProtocolo`.
2. Passar a **emitir a tag 0040,0008** (Scheduled Protocol Code Sequence) no `ConstrutorMwlItem`,
   só quando preenchido.
3. Religar o toggle §9.1 do tipo de mamografia e validar a execução ponta-a-ponta.
