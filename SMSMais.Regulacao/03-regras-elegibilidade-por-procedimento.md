# 03 — Regras de elegibilidade por procedimento (incremento 4)

## Objetivo

Trazer para dentro do SMSMais os critérios dos manuais do solicitante (SER hoje; SERNIT e SISREG à medida que existirem) e aplicá-los **antes** de a solicitação avançar: o que dá para deduzir do cadastro é deduzido; o que é clínico vira pergunta; o que é documento vira caixinha de anexo — com a oferta de reaproveitar exame que já existe no próprio sistema. Quando um sistema externo bloqueia e outro não, a solicitação passa com ressalva de destino.

## Requisitos cobertos

- **R-06 (A5)** regras por procedimento × sistema; dedutíveis e não dedutíveis; ressalva "só SERNIT".
- **R-07 (A6)** questionário sim/não/não sei; caixinhas individuais; vários arquivos por caixinha; pendência → rascunho.
- **R-09 (A7)** regras também no interno; conferir exames internos e o operador marca válido/não válido.

## Decisões aplicadas

D-5 (regras vêm depois do paciente e antes do formulário). Escolhas próprias: **campos tipados em vez de DSL** nesta fase (o manual só traz idade, sexo e CID como dedutíveis); **entrada manual** das regras a partir dos manuais, com o CSV do spike e como apoio — sem OCR/IA.

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Os manuais | sim | `Automais.SER/documentacao/CRECE_MANUAL DO SOLICITANTE_Versão1 30.11.2022.pdf`, `REUNI_MANUAL DO SOLICITANTE_V3 29.12.2022.pdf` — por recurso: "Critérios de inclusão", "Critérios de exclusão", exigências de exame |
| Recurso do SER como âncora | sim | `ser_catalogo_recurso.rotulo` (as regras do manual são por recurso, não por procedimento canônico) |
| Dados do paciente para deduzir | sim | `fhir.patient` via `IPacientesService` (nascimento, sexo, CPF, endereço) |
| Exames e laudos do paciente | sim | `SMSMais.Core/Cidadao/ICidadaoClinicoService.cs` (`ListarExamesAsync`, `ListarLaudosAsync`, `ObterLaudoPdfAsync`); `GET /solicitacoes-exame?pacienteId=&tipoExameId=`; `GET /pacientes/{id}/anexos-exame` |
| Correlato entre tipo de exame e procedimento | sim | `tipo_exame.ModalidadeDicom`, `tipo_exame.ProcedimentoSigtapId` |
| PDF do exame completo | sim | `ExameCompletoPdfService` (`GET /solicitacoes-exame/{id}/exame-completo-pdf`) |
| Padrão de anexo por dono com versão | parcial | `SerRascunhoAnexo` (sem versão); `DocumentoExame` (hash, status) |

O que **não** existe: qualquer tabela de regra; motor; caixinhas; integração de laboratório (só imagem/laudo).

## Desenho

### Tabelas

**`regulacao_regra`**

| Coluna | Nota |
|---|---|
| id | |
| procedimento_id (FK canônico) | obrigatório |
| procedimento_origem_id (FK origem) NULL | regra específica de um recurso de um sistema (é o caso dos manuais do SER); nula = vale para o canônico inteiro |
| sistema | enum `SistemaRegulacao` ou `Todos` (0) |
| tipo | `Dedutivel=1`, `NaoDedutivel=2`, `Documental=3`, **`Informativa=4`** — o quarto valor entrou pelo spike e: 83% do corpus dos manuais é texto clínico que, virando pergunta, faria um recurso com 20 critérios pedir 20 respostas. `Severidade = Aviso` **não** resolve (a pergunta continua sendo feita, só não bloqueia); `Informativa` é "mostra como texto de apoio e não pergunta nada". Ver [`revisoes/spike-e-manual-regras.md`](revisoes/spike-e-manual-regras.md) §7. |
| severidade | `Bloqueia=1`, `Ressalva=2`, `Aviso=3` |
| descricao | texto do manual, literal |
| fonte | "CRECE v1 30/11/2022 p.13" |
| **dedutível**: idade_min_anos, idade_max_anos, sexo (M/F/null), exige_cpf, cids_permitidos_json, cids_excluidos_json, municipios_ibge_json | avaliados contra paciente + CID da hipótese; `expressao_json` reservada (nula) para um DSL futuro |
| **não dedutível**: pergunta, resposta_bloqueia (`Sim`/`Nao`), nao_sei_vira (`Ressalva`/`Pendencia`) | opções fixas Sim / Não / Não sei |
| **documental**: documento_rotulo ("Ecocardiograma recente"), tipo_exame_id NULL, validade_dias NULL, obrigatorio | correlato interno para o R-09 |
| ordem, ativo, versao | alteração = nova linha com `versao+1` e a anterior `ativo=false` (respostas antigas guardam a versão que responderam) |
| criado/atualizado/excluido_em/por | ADR-0006 |

Índice `(procedimento_id, sistema, ativo)`.

**`regulacao_solicitacao_resposta_regra`**: solicitacao_id, regra_id, regra_versao, resposta (`Sim`/`Nao`/`NaoSei`/`Deduzido`), valor_deduzido (ex.: "idade=31"), resultado (`Atende`/`Bloqueia`/`Ressalva`/`Indefinido`), respondido_por/em. Unique `(solicitacao_id, regra_id)`.

**`regulacao_solicitacao_exigencia`** (caixinha): solicitacao_id, regra_id NULL (nula = "Anexos gerais"), titulo, obrigatoria, situacao (`Pendente`/`Atendida`/`AtendidaPorExameInterno`/`Dispensada`/`Criticada`), exame_interno_ref (`exame_imagem_id` ou `laudo_id`), validado_exame_por/em, critica_texto, ordem. Unique parcial `(solicitacao_id, regra_id)`.

**`regulacao_exigencia_arquivo`**: exigencia_id, chave_armazenamento (ou midia_id, conforme plano 02), nome, content_type, tamanho, sha256, versao (sequencial na caixinha), substitui_arquivo_id (self-FK), situacao (`Atual`/`Substituido`/`Criticado`/`Removido`), origem (`Upload`/`ExameInterno`), enviado_ao_sistema_em, criado_por/em. Índice `(exigencia_id, versao)`.

**`regulacao_solicitacao_destino`** (resultado por sistema): solicitacao_id, sistema, situacao (`Elegivel`/`Bloqueado`/`ComRessalva`), motivo, avaliado_em. Unique `(solicitacao_id, sistema)`. É daqui que sai a ressalva "só SERNIT" que o agente vê.

### Motor (`AvaliadorElegibilidade`, função pura em `SMSMais.Core/Regulacao/Regras/`)

Entrada: paciente (nascimento, sexo, CPF, município), CID da hipótese, respostas já dadas, exames/laudos do paciente (lista com `tipo_exame_id` e `realizado_em`), regras ativas do procedimento (por sistema), fluxo/destino escolhido.

Saída `AvaliacaoElegibilidadeDto { PorSistema: { Sisreg | Ser | Sernit → Bloqueios[], Ressalvas[], Avisos[] }, PerguntasPendentes[], DocumentosPendentes[], DestinosPermitidos[] }`.

Decisão sobre o conjunto:
- Interno bloqueado no SISREG → **bloqueia** o Enviar (motivos na tela).
- Externo bloqueado em **todos** os sistemas externos com origem → bloqueia.
- Externo bloqueado em **um** e permitido em outro → admite e grava `regulacao_solicitacao_destino` (ressalva). O agente vê "só pode ir para SERNIT: SER exige idade ≥ 35 (CRECE p.13)".
- "Não sei" → conforme `nao_sei_vira`: Ressalva (segue, o agente decide) ou Pendencia (não envia até responder).
- Documental obrigatória `Pendente` → não impede salvar Rascunho; **impede Enviar**.

Reavaliação: sempre que paciente, CID, respostas ou caixinhas mudam, e quando o agente abre a solicitação (regras podem ter mudado de versão — a tela mostra "avaliado com a versão N; há versão N+1").

### Exames internos (R-09)

Para cada regra documental com `tipo_exame_id`: buscar em `ICidadaoClinicoService.ListarExamesAsync/ListarLaudosAsync` os exames do paciente desse tipo com `realizado_em` dentro de `validade_dias`. A caixinha mostra "Encontrado no sistema: RX de tórax em 12/07/2026 (laudado)" com três ações: **Usar este** (gera `regulacao_exigencia_arquivo` com `origem=ExameInterno` a partir de `ObterLaudoPdfAsync`/`ExameCompletoPdfService`, situação `AtendidaPorExameInterno`, `validado_exame_por` = operador), **Anexar novo** (upload normal) ou **Deixar pendente**. Laboratório: não existe integração; a regra documental "hemograma" fica sempre por anexo até haver fonte — o modelo já suporta (`tipo_exame_id` nulo).

### Tela de cadastro (Regulação → Configuração → "Regras de elegibilidade", módulo 51)

- Busca do procedimento (mesmo `BuscaProcedimento` do plano 01) → abas por sistema (SISREG / SER / SERNIT / Todos).
- Lista de regras com tipo, severidade, resumo, fonte, versão; formulário por tipo (dedutível: idade/sexo/CID; não dedutível: pergunta + o que bloqueia + o que "não sei" vira; documental: rótulo, tipo de exame correlato, validade, obrigatória).
- **Importar CSV** do spike e: pré-visualização linha a linha, casa o `recurso` com `ser_catalogo_recurso.rotulo` (normalizado), cria as regras como `ativo=false` para revisão, o operador ativa.
- Histórico de versões por regra.

### Onde entra no wizard (plano 02, passo 4)

Depois do paciente: dedutíveis avaliadas e mostradas (verde/vermelho com motivo); perguntas em sequência; caixinhas criadas para o passo 5. Bloqueio total encerra o wizard com o relatório (o usuário pode salvar rascunho para registro, não pode enviar).

## Tarefas

- [ ] **4.1** Entidades `RegulacaoRegra`, `RegulacaoSolicitacaoRespostaRegra` (as de exigência/arquivo/destino nascem no incremento 2/3, se ainda não existirem criar aqui); enums; configurações; migration `RegrasDeElegibilidade`.
- [ ] **4.2** `AvaliadorElegibilidade` puro + testes unitários (idade nos limites, sexo, CID permitido/excluído, "não sei" nas duas políticas, ressalva por destino, documental com validade vencida, versão de regra).
- [ ] **4.3** Integração no wizard e no detalhe do agente: `GET /regulacao/solicitacoes/{id}/elegibilidade`, `PUT .../respostas` (47 Edição); bloqueio do Enviar; gravação de `regulacao_solicitacao_destino`.
- [ ] **4.4** Exames internos na caixinha: endpoint `GET /regulacao/solicitacoes/{id}/exigencias/{exigenciaId}/exames-internos`, ação "Usar este" (gera arquivo `origem=ExameInterno`).
- [ ] **4.5** Tela de cadastro de regras (51) + importação do CSV do spike e + histórico de versões.
  O CSV existe: `revisoes/spike-e-manual-regras.csv`, **1.169 regras de 204 recursos**. Três regras que o importador tem de seguir, do [relatório do spike e](revisoes/spike-e-manual-regras.md):
  1. a coluna **`secao`** decide `resposta_bloqueia` — `exclusao` → `Sim` bloqueia, `inclusao` → `Nao` bloqueia. Ignorar isso inverte o sentido de **295** regras;
  2. **102 recursos vêm com `pareamento = SEM_PAR`** e `recurso_catalogo` vazio: caem na curadoria, nunca em pareamento por adivinhação de nome — ligar regra clínica ao procedimento errado é o pior defeito possível neste módulo;
  3. a frase *"Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso"* foi descartada como boilerplate (**179 ocorrências**) e deve virar **uma** regra documental global, não 179.
  Cobertura real a esperar: só **19% do catálogo SER** tem regra escrita (27% no ramo AE, 11% no outro). O resto do catálogo entra sem regra e depende de cadastro manual.
- [ ] **4.6** (plano 09) configurações restantes.

## Dependências

Planos 01 (procedimento canônico/origem), 02 (wizard e caixinhas), spike e (conteúdo das regras). O motor não depende de nada externo.

## Riscos e pontos a confirmar

- Os manuais são de 2022 e do SER; SERNIT e SISREG não têm manual conhecido — as regras do interno serão escritas pela própria regulação (R-09 diz isso). Cada regra carrega `fonte` para saber de onde veio.
- Regra por **recurso** (origem) × regra por **canônico**: uma regra do CRECE vale para "AMBULATÓRIO 1ª VEZ EM CARDIOLOGIA" do SER; se o canônico agrupa também um recurso SERNIT, a regra **não** se estende sozinha. Por isso `procedimento_origem_id` e `sistema` na regra.
- "Não sei" precisa de política por regra; o padrão é `Ressalva` (deixa o agente decidir), não `Pendencia`, para não travar a ponta.
- Idade: calcular na data da solicitação, com regra explícita para "a partir de 15 anos" (≥ 15 completos).

## Testes

Motor: puro, sem banco (tarefa 4.2). Integração: seed de duas regras (SER bloqueia idade < 35; SERNIT sem regra) → solicitação de paciente de 30 anos vai para a fila com destino `Ser=Bloqueado`, `Sernit=Elegivel`. Documental com `tipo_exame_id` de RX e paciente com RX de 20 dias → caixinha oferece "usar este".

## Fora de escopo

DSL de regras; OCR dos manuais; integração de laboratório; regras que dependam de dados que o SMSMais não tem (peso, comorbidades) — essas são não dedutíveis por definição.

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoRegra.cs`, `RegulacaoSolicitacaoRespostaRegra.cs` | criar |
| `SMSMais.Data/Entities/Enums/TipoRegraRegulacao.cs`, `SeveridadeRegraRegulacao.cs`, `RespostaRegraRegulacao.cs`, `ResultadoRegraRegulacao.cs`, `NaoSeiViraRegulacao.cs` | criar |
| `SMSMais.Data/Configurations/Regulacao/RegulacaoRegraConfiguration.cs`, `RegulacaoSolicitacaoRespostaRegraConfiguration.cs` | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSets `RegulacaoRegras`, `RegulacaoSolicitacaoRespostasRegra` |
| `SMSMais.Data/Migrations/<ts>_RegrasDeElegibilidade.cs` | gerar |
| `SMSMais.Core/Regulacao/Regras/AvaliadorElegibilidade.cs` (estático, puro), `Modelos/{PacienteParaRegras,ExameParaRegras,AvaliacaoElegibilidadeDto}.cs` | criar |
| `SMSMais.Core/Regulacao/Regras/IRegulacaoRegraService.cs`, `RegulacaoRegraService.cs` (CRUD + versões + importação CSV), `IRegulacaoElegibilidadeService.cs`, `RegulacaoElegibilidadeService.cs` (junta paciente + regras + exames + respostas → avaliação; persiste respostas e destinos) | criar |
| `SMSMais.Core/Regulacao/Regras/Dtos/RegulacaoRegraDtos.cs`, `Validators/SalvarRegulacaoRegraValidator.cs` | criar |
| `SMSMais.Core/DependencyInjection.cs` | registrar |
| `SMSMais.Api/Controllers/RegulacaoRegrasController.cs` | criar |
| `SMSMais.Api/Controllers/RegulacaoSolicitacoesController.cs` | rotas de elegibilidade/respostas/exames internos |
| `SMSMais.front/src/features/regulacao/components/wizard/PassoRegras.tsx` (substitui o placeholder), `components/CaixinhaExigencia.tsx`, `components/ExamesInternosSugeridos.tsx`, `pages/RegrasElegibilidadePage.tsx`, `components/regras/{FormularioRegra,ListaRegras,ImportarRegrasCsv}.tsx` | criar |
| `tests/SMSMais.Tests/Regulacao/Regras/*.cs` | criar |

### B. Entidades

```csharp
public sealed class RegulacaoRegra
{
    public Guid Id { get; set; }
    public Guid ProcedimentoId { get; set; }                      // FK regulacao_procedimento
    public Guid? ProcedimentoOrigemId { get; set; }               // FK regulacao_procedimento_origem (SetNull) — regra de um recurso específico
    public SistemaRegulacao? Sistema { get; set; }                // null = todos
    public TipoRegraRegulacao Tipo { get; set; }
    public SeveridadeRegraRegulacao Severidade { get; set; }
    public string Descricao { get; set; } = string.Empty;         // max 2000, texto literal do manual
    public string? Fonte { get; set; }                            // max 300
    // dedutível
    public int? IdadeMinAnos { get; set; }  public int? IdadeMaxAnos { get; set; }
    public string? Sexo { get; set; }                             // "M" | "F" | null
    public bool ExigeCpf { get; set; }
    public string? CidsPermitidosJson { get; set; }  public string? CidsExcluidosJson { get; set; }   // jsonb string[] (prefixos: "I10", "C50")
    public string? MunicipiosIbgeJson { get; set; }
    public string? ExpressaoJson { get; set; }                    // reservado, sempre null nesta fase
    // não dedutível
    public string? Pergunta { get; set; }                         // max 500
    public RespostaRegraRegulacao? RespostaBloqueia { get; set; } // Sim | Nao
    public NaoSeiViraRegulacao? NaoSeiVira { get; set; }          // Ressalva | Pendencia
    // documental
    public string? DocumentoRotulo { get; set; }                  // max 200
    public Guid? TipoExameId { get; set; }                        // FK tipo_exame (SetNull)
    public int? ValidadeDias { get; set; }
    public bool Obrigatorio { get; set; } = true;
    public int Ordem { get; set; }
    public int Versao { get; set; } = 1;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }  public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }  public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }  public Guid? ExcluidoPor { get; set; }
}
// índice ix_regulacao_regra_proc_sistema_ativo (procedimento_id, sistema, ativo); HasQueryFilter(ExcluidoEm == null)

public sealed class RegulacaoSolicitacaoRespostaRegra
{
    public Guid Id { get; set; }
    public Guid SolicitacaoId { get; set; }   // Cascade
    public Guid RegraId { get; set; }          // Restrict
    public int RegraVersao { get; set; }
    public RespostaRegraRegulacao Resposta { get; set; }   // Sim=1, Nao=2, NaoSei=3, Deduzido=4
    public string? ValorDeduzido { get; set; }             // max 200 ("idade=31")
    public ResultadoRegraRegulacao Resultado { get; set; } // Atende=1, Bloqueia=2, Ressalva=3, Indefinido=4
    public Guid? RespondidoPor { get; set; }  public DateTime RespondidoEm { get; set; }
}
// unique ux_regulacao_resposta (solicitacao_id, regra_id)
```

Enums: `TipoRegraRegulacao { Dedutivel = 1, NaoDedutivel = 2, Documental = 3, Informativa = 4 }` · `SeveridadeRegraRegulacao { Bloqueia = 1, Ressalva = 2, Aviso = 3 }` · `RespostaRegraRegulacao { Sim = 1, Nao = 2, NaoSei = 3, Deduzido = 4 }` · `ResultadoRegraRegulacao { Atende = 1, Bloqueia = 2, Ressalva = 3, Indefinido = 4 }` · `NaoSeiViraRegulacao { Ressalva = 1, Pendencia = 2 }`.

### C. Motor puro

```csharp
public sealed record PacienteParaRegras(DateOnly? Nascimento, string? Sexo, string? Cpf, string? MunicipioIbge);
public sealed record ExameParaRegras(Guid Id, Guid? TipoExameId, DateOnly RealizadoEm, bool Laudado, string Descricao, Guid? LaudoId);
public sealed record RegraAvaliadaDto(Guid RegraId, int Versao, TipoRegraRegulacao Tipo, SeveridadeRegraRegulacao Severidade, SistemaRegulacao? Sistema, string Descricao, ResultadoRegraRegulacao Resultado, string? Motivo);
public sealed record PerguntaPendenteDto(Guid RegraId, string Pergunta, SistemaRegulacao? Sistema);
public sealed record DocumentoPendenteDto(Guid RegraId, string Rotulo, bool Obrigatorio, Guid? TipoExameId, int? ValidadeDias, IReadOnlyList<ExameParaRegras> ExamesInternosCandidatos);
public sealed record AvaliacaoElegibilidadeDto(
    IReadOnlyDictionary<SistemaRegulacao, IReadOnlyList<RegraAvaliadaDto>> PorSistema,
    IReadOnlyList<PerguntaPendenteDto> PerguntasPendentes,
    IReadOnlyList<DocumentoPendenteDto> DocumentosPendentes,
    IReadOnlyList<SistemaRegulacao> DestinosPermitidos,
    IReadOnlyList<SistemaRegulacao> DestinosComRessalva,
    bool BloqueiaEnvio);

public static class AvaliadorElegibilidade
{
    public static AvaliacaoElegibilidadeDto Avaliar(
        IReadOnlyList<RegulacaoRegra> regras, PacienteParaRegras paciente, string? cid,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas, IReadOnlyList<ExameParaRegras> exames,
        IReadOnlyList<SistemaRegulacao> sistemasCandidatos, DateOnly hoje, NaoSeiViraRegulacao naoSeiPadrao);
}
```
Regras de avaliação:
- Idade = anos completos em `hoje`; `IdadeMinAnos` inclusivo (≥), `IdadeMaxAnos` inclusivo (≤). Sem nascimento → `Indefinido` (vira pergunta implícita "confirmar nascimento").
- CID: `CidsPermitidosJson` = lista de prefixos; atende se o CID informado começa por algum; `CidsExcluidosJson` bloqueia se começa por algum. Sem CID informado e regra de CID → `Indefinido`.
- Não dedutível: sem resposta → `PerguntasPendentes`; `Resposta == RespostaBloqueia` → `Bloqueia`/`Ressalva` conforme severidade; `NaoSei` → conforme `NaoSeiVira ?? naoSeiPadrao` (`Ressalva` = resultado Ressalva; `Pendencia` = continua pendente).
- Documental: se `TipoExameId` e existe exame com esse tipo e `RealizadoEm >= hoje - ValidadeDias` → candidatos listados (não resolve sozinho; quem resolve é a caixinha); sempre entra em `DocumentosPendentes` até a exigência estar `Atendida/AtendidaPorExameInterno/Dispensada` (esse estado vem do service, não do motor).
- Por sistema: regra com `Sistema == null` aplica a todos os candidatos. `DestinosPermitidos` = candidatos sem `Bloqueia`; `DestinosComRessalva` = com Ressalva. `BloqueiaEnvio` = candidatos vazios após remover bloqueados (todos bloqueados) **ou** há pendência de pergunta com `Pendencia`.

`RegulacaoElegibilidadeService.AvaliarAsync(solicitacaoId, ct)`: carrega solicitação (escopo), paciente (`IPacientesService.ObterPorIdAsync` → mapear para `PacienteParaRegras`), CID de `Formulario.canonico.cid10 ?? hipotese`, regras ativas do `ProcedimentoId` (+ `ProcedimentoOrigemId` das origens do canônico), respostas, exames (`ICidadaoClinicoService.ListarExamesAsync(pacienteId)` + `ListarLaudosAsync` → `ExameParaRegras`), candidatos (`Interno` → `[Sisreg]`; `Externo` → sistemas com origem ativa; `Nar` → `[Sisreg]`); chama o motor; persiste `RegulacaoSolicitacaoRespostaRegra` (deduzidas) e `RegulacaoSolicitacaoDestino`; cria exigências que faltam (uma por regra documental, `Titulo = DocumentoRotulo`, `Obrigatoria = regra.Obrigatorio`). `ResponderAsync(solicitacaoId, IReadOnlyDictionary<Guid, RespostaRegraRegulacao>)` grava e reavalia. `UsarExameInternoAsync(solicitacaoId, exigenciaId, exameId, laudoId?)`: gera PDF (`ICidadaoClinicoService.ObterLaudoPdfAsync` ou `ExameCompletoPdfService`), grava como arquivo `Origem = ExameInterno` via `IRegulacaoExigenciaService`, exigência → `AtendidaPorExameInterno`, `ValidadoExamePor/Em`.

### D. Serviço de regras e DTOs

```csharp
public interface IRegulacaoRegraService
{
    Task<IReadOnlyList<RegulacaoRegraDto>> ListarAsync(Guid procedimentoId, SistemaRegulacao? sistema, bool incluirInativas, CancellationToken ct);
    Task<RegulacaoRegraDto> ObterAsync(Guid id, CancellationToken ct);
    Task<RegulacaoRegraDto> CriarAsync(SalvarRegulacaoRegraRequest req, CancellationToken ct);
    Task<RegulacaoRegraDto> NovaVersaoAsync(Guid id, SalvarRegulacaoRegraRequest req, CancellationToken ct);  // desativa a anterior, cria versao+1
    Task AtivarAsync(Guid id, bool ativo, CancellationToken ct);
    Task ExcluirAsync(Guid id, CancellationToken ct);  // soft delete
    Task<ImportacaoRegrasResultadoDto> ImportarCsvAsync(Stream csv, CancellationToken ct);  // cria inativas; casa recurso por rótulo normalizado
    Task<IReadOnlyList<RegulacaoRegraDto>> VersoesAsync(Guid id, CancellationToken ct);
}
public sealed record SalvarRegulacaoRegraRequest(Guid ProcedimentoId, Guid? ProcedimentoOrigemId, SistemaRegulacao? Sistema, TipoRegraRegulacao Tipo, SeveridadeRegraRegulacao Severidade, string Descricao, string? Fonte, int? IdadeMinAnos, int? IdadeMaxAnos, string? Sexo, bool ExigeCpf, string[]? CidsPermitidos, string[]? CidsExcluidos, string? Pergunta, RespostaRegraRegulacao? RespostaBloqueia, NaoSeiViraRegulacao? NaoSeiVira, string? DocumentoRotulo, Guid? TipoExameId, int? ValidadeDias, bool Obrigatorio, int Ordem);
public sealed record RegulacaoRegraDto(/* todos os campos acima + */ Guid Id, int Versao, bool Ativo, DateTime CriadoEm, string? CriadoPorNome);
public sealed record ImportacaoRegrasResultadoDto(int Lidas, int Criadas, int SemRecurso, IReadOnlyList<string> Avisos);
```
Validador (FluentValidation, padrão `Core/*/Validators`): `Dedutivel` exige ao menos um critério; `NaoDedutivel` exige `Pergunta` e `RespostaBloqueia`; `Documental` exige `DocumentoRotulo`; `Informativa` exige só `Descricao` e ignora `Pergunta`/`RespostaBloqueia`; `IdadeMin <= IdadeMax`; `Sexo ∈ {M, F, null}`.

CSV do spike e (`;`, UTF-8, cabeçalho): `recurso;sistema;tipo;texto_original;idade_min;idade_max;sexo;pergunta;documento;fonte`. `ImportarCsvAsync` casa `recurso` com `regulacao_procedimento_origem.RotuloExterno` normalizado do `sistema`; sem casamento → conta em `SemRecurso` e aviso com a linha.

### E. Endpoints

`RegulacaoRegrasController`, `[Route("regulacao/regras")]`: `GET ?procedimentoId=&sistema=&inativas=` (`RegulacaoConfiguracao`, Consulta), `GET {id}`, `GET {id}/versoes`, `POST` (Inclusao), `POST {id}/nova-versao` (Edicao), `PATCH {id}/ativo {ativo}` (Edicao), `DELETE {id}` (Exclusao), `POST importar-csv` multipart (Edicao).

No `RegulacaoSolicitacoesController`: `GET {id}/elegibilidade` (`Regulacao`, Consulta) → `AvaliacaoElegibilidadeDto`; `PUT {id}/respostas` body `{ respostas: { regraId: 'Sim'|'Nao'|'NaoSei' } }` (`Regulacao`, Edicao) → avaliação; `GET {id}/exigencias/{exigenciaId}/exames-internos` (`Regulacao`, Consulta) → `ExameParaRegras[]`; `POST {id}/exigencias/{exigenciaId}/usar-exame-interno` body `{ exameId, laudoId? }` (`Regulacao`, Edicao) → exigência. `GET regulacao/procedimentos/{id}/regras` (`Regulacao`, Consulta) → regras ativas resumidas (para o wizard mostrar antes do paciente o que será exigido).

### F. Front

- `PassoRegras.tsx`: chama `GET elegibilidade` ao entrar (e após cada resposta); seção "Avaliadas automaticamente" (verde/vermelho/amarelo com motivo, por sistema); seção "Perguntas" (radio Sim/Não/Não sei por pergunta, `PUT respostas` com debounce); seção "Documentos exigidos" (lista das caixinhas que serão criadas; para cada uma com candidatos internos, `ExamesInternosSugeridos` com "Usar este"); bloqueio total → alerta com motivos e botão "Salvar como rascunho" apenas.
- `CaixinhaExigencia.tsx` (usada no `PassoFormulario` e no detalhe): título, obrigatória, situação, `UploadAnexo`, crítica (quando `Criticada`), botão "Dispensar" (só 48).
- `RegrasElegibilidadePage.tsx` (aba "Regras de elegibilidade" em `RegulacaoConfiguracaoPage`): `BuscaProcedimento` → `Tabs` por sistema (Todos/SISREG/SER/SERNIT) → `ListaRegras` (tipo, severidade, resumo, fonte, versão, ativo) → `FormularioRegra` (campos por tipo) → `ImportarRegrasCsv` (upload + pré-visualização do resultado).
- `types.ts`/`regulacaoApi.ts`/`queries.ts`: `useElegibilidade(id)`, `useResponderRegras`, `useExamesInternos(id, exigenciaId)`, `useUsarExameInterno`, `useRegras(procedimentoId, sistema)`, `useSalvarRegra`, `useNovaVersaoRegra`, `useAtivarRegra`, `useImportarRegrasCsv`.

### G. Testes (`tests/SMSMais.Tests/Regulacao/Regras/`)

`AvaliadorElegibilidadeTests` (puro): `Idade_minima_e_inclusiva`; `Idade_maxima_e_inclusiva`; `Sem_nascimento_fica_indefinido`; `Cid_permitido_por_prefixo`; `Cid_excluido_bloqueia`; `Pergunta_sem_resposta_fica_pendente`; `Resposta_que_bloqueia_bloqueia_so_naquele_sistema`; `Nao_sei_vira_ressalva_por_padrao`; `Nao_sei_vira_pendencia_quando_configurado`; `Bloqueado_em_um_externo_permite_o_outro_com_ressalva`; `Bloqueado_em_todos_bloqueia_envio`; `Documental_lista_exame_interno_dentro_da_validade`; `Documental_ignora_exame_vencido`; `Regra_sem_sistema_vale_para_todos`.
`RegulacaoRegraServiceTests`: `Nova_versao_desativa_a_anterior`; `Importar_csv_cria_inativas_e_conta_sem_recurso`; `Validador_exige_criterio_por_tipo`.
`RegulacaoElegibilidadeServiceTests` (fixture): `Cria_uma_exigencia_por_regra_documental`; `Usar_exame_interno_gera_arquivo_e_atende`; `Grava_destinos_com_ressalva`.

### H. Passo a passo

1. Entidades/enums/configs/DbSets → build → `dotnet ef migrations add RegrasDeElegibilidade …` → build.
2. Motor puro + `AvaliadorElegibilidadeTests` (todos verdes antes de qualquer integração).
3. `IRegulacaoRegraService` + validador + importação CSV + testes.
4. `IRegulacaoElegibilidadeService` + integração no `EnviarParaFilaAsync` (plano 04) + endpoints + testes.
5. Front: `PassoRegras`, `CaixinhaExigencia`, `ExamesInternosSugeridos`, página de regras + aba → `npm run build`.
6. Carregar as regras do spike e pela importação (em homologação primeiro).
7. `PROGRESSO.md` 4.1–4.5.

### I. Critério de pronto

Regra "SER: idade ≥ 35" + paciente de 30 anos + procedimento com origem SER e SERNIT → wizard segue com destino `Ser = Bloqueado`, `Sernit = Elegivel`, e o agente vê a ressalva; pergunta não respondida impede Enviar; caixinha de RX com exame de 20 dias oferece "Usar este" e, ao usar, a caixinha fica atendida com o PDF anexado.
