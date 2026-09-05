# 02 — Fluxo de abertura da solicitação (incremento 2)

## Objetivo

O wizard que a unidade solicitante usa para abrir uma solicitação: procedimento → destino (Interno / Externo / NAR) → paciente → regras → formulário do sistema alvo → anexos → Rascunho ou Enviar. Grava tudo localmente em `regulacao_solicitacao` (formulário em JSON com a versão do catálogo usada). Substitui as telas "Nova solicitação" separadas do SER e do SERNIT.

## Requisitos cobertos

- **R-01 (A2)** menu Solicitações; três fluxos.
- **R-03 (A2)** só Interno/Externo; configuração "Externo mesmo tendo Interno"; sempre mostrar os dois.
- **R-04 (A3)** Interno = tela do SISREG ipsis litteris + anexos.
- **R-05 (A4)** Externo = união dos campos SER ∪ SERNIT; JSON marcado com sistema/fluxo.
- **R-07 (A6)** caixinhas de anexo; pendência → Rascunho; só Enviar sem pendência.
- **R-08 (A6)** ordem paciente × procedimento (decidida em D-5).
- **R-17 (A14)** NAR: "em nome de".
- **R-18 (A14)** "a partir de qual unidade".

## Decisões aplicadas

- **D-5** ordem do wizard; **D-6** rótulo "Pré-regulação"; **D-8** no Interno o "Enviar" inclui no SISREG com a senha do solicitante (o código desse envio é o plano 11; aqui fica o gancho); **D-9** NAR autentica pela unidade (plano 11); **D-2** modal de credencial como fallback.

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Página de nova solicitação em cascata, com aside de rascunhos | sim, por sistema | `SMSMais.front/src/features/ser/pages/SerNovaSolicitacaoPage.tsx` (~816 linhas) e clone `sernit/` |
| Renderização de campo dinâmico por tipo (textarea, checkbox múltiplo, radio/select com opções, date dd/MM/yyyy ↔ ISO, fallback texto) | sim, **duplicada** | funções locais `CampoDinamico` e `CampoPaciente` nas duas páginas acima → extrair |
| Campos dinâmicos por recurso, do catálogo espelhado | sim | `GET /regulacao/ser/rascunhos/campos` (`SerCatalogoService.ObterCamposAsync`), `ser_catalogo_campo`; idem SERNIT |
| Seletor de recurso com busca | sim | `features/ser/components/SeletorRecursoSer.tsx` → vira `BuscaProcedimento` do plano 01 |
| Seletor de CID por recurso | sim | `SeletorCidSer.tsx` (`GET .../nova-solicitacao/cids`) |
| Rascunho local + anexo (10 MB, `midia`, SHA-256) | sim, por sistema | `SMSMais.Core/Ser/SerRascunhoService.cs`, `SerSolicitacaoRascunho.CamposJson`, `SerRascunhoAnexo`; `POST /regulacao/ser/rascunhos/{id}/anexos` (`SerController.cs:547`, `RequestSizeLimit(12MB)`) |
| Validação "Pronto" contra obrigatórios do catálogo | sim | `SerRascunhoService.MarcarProntoAsync` |
| Desdobramento de checkbox múltiplo | sim | `SerValorMultiplo.cs` |
| Seletor de unidade ativa (header) e injeção de `X-Unidade-Id` | sim | `app/layout/Header.tsx`, `shared/api/httpClient.ts` (override por chamada em `sisreg-mapeamento/api/*`) |
| Busca de paciente reutilizável | sim | `shared/ui/BuscaPaciente` + `NomePacienteComResumo` |
| Mapa por GET da tela de inclusão do SISREG | sim, incompleto | `Automais.SISREG/docs/APRENDIZADOS.md:339-367` (`pa`, `cid10`, `cpfprofsol`/`nomeprofsol`, `ret`, `upsexec`) |
| Armazenamento de PDF em Spaces com read-back | sim | `SMSMais.Core/Armazenamento/ArmazenamentoSpaces.cs` (hoje `ContentType` fixo em PDF) |

O que **não** existe: entidade agnóstica de destino; unidade solicitante / autor / "em nome de" no rascunho; componente genérico de upload; formulário "união"; NAR.

## Desenho

### Ordem do wizard (D-5) e por quê

`[NAR: unidade em nome de] → 1 procedimento → 2 destino → 3 paciente → 4 regras → 5 formulário + anexos → 6 revisão`.

O procedimento é o passo mais barato de refazer e é o único que determina Interno/Externo e quais regras existem. O paciente entra logo antes do formulário porque as regras dedutíveis e documentais precisam dele, e o formulário é a parte cara (perguntas variáveis + anexos): o corte acontece antes do maior retrabalho. Paciente primeiro obrigaria a reavaliar tudo ao trocar o procedimento e deixaria o bloqueio para o fim. No NAR a unidade vem antes de tudo porque decide credencial e oferta. Ponto de reabertura: se a operação mostrar que a recepção sempre começa pelo paciente (cartão na mão), inverter 1 e 3 é mudança só de tela — o modelo não muda.

### Passos

**0 — Unidade de origem (R-18).** Não é passo visível: é o seletor de unidade ativa do header. Com "Todas as unidades" selecionado, o wizard exige escolher uma antes de continuar. `unidade_solicitante_id` = unidade ativa.

**NAR — "Em nome de".** Aparece só quando o usuário escolhe o fluxo NAR (botão na tela inicial do wizard). Dropdown com **todas** as unidades ativas do município (não só as vinculadas ao usuário). Fluxo forçado para Interno/SISREG. O restante é idêntico.

**1 — Procedimento.** `BuscaProcedimento` (plano 01). Mostra oferta interna (unidades com vagas vigentes) e existência externa (SER / SERNIT). Nada de escolher unidade.

**2 — Destino.** Botões Interno / Externo. Regras:
- Interno habilitado só se há oferta interna vigente.
- Externo habilitado se há origem SER ou SERNIT **e** (não há interno **ou** `regulacao_configuracao.permitir_externo_com_interno = true`).
- Os dois lados sempre aparecem, com o motivo do desabilitado ("há oferta interna; a configuração não permite Externo").
- NAR pula este passo (sempre Interno).
- Se há regra dedutível já avaliável sem paciente (não há: todas dependem do paciente), nada aqui. As ressalvas de destino (plano 03) aparecem no passo 4.

**3 — Paciente.** Plano 10: busca local por nome/CPF/CNS; não achou → consulta CADSUS pelo roteador (SISREG ou SER conforme configuração) e cria; CPF obrigatório para seguir (SERNIT não grava sem CPF). Telefone verificado sugerido no campo de contato.

**4 — Regras.** Plano 03: dedutíveis avaliadas na hora com o paciente; bloqueio total → o wizard para com os motivos; bloqueio parcial (só em um sistema externo) → segue com **ressalva de destino** gravada; não dedutíveis → questionário sim/não/não sei; documentais → geram as **caixinhas** do passo 5. No incremento 2 este passo existe vazio (sem regras cadastradas nada aparece); o motor entra no incremento 4.

**5 — Formulário + anexos.**
- *Externo*: campos = **união** SER ∪ SERNIT para o procedimento canônico (par definido pelo plano 08; enquanto não há par confirmado, união das origens que existirem). Cada campo canônico guarda em `regulacao_formulario_campo_mapa` o nome nativo em cada sistema. Rótulo, tipo, opções e obrigatoriedade vêm de `ser_catalogo_campo` / `sernit_catalogo_campo`; se os dois têm o campo com opções diferentes, a tela mostra a união das opções e o mapa registra a tradução. Bloco fixo do SER (classificação de risco, médico responsável, CID por recurso, telefone) entra como campos canônicos com origem.
- *Interno*: campos da tela `marcar` do SISREG (`pa` já fixado pelo procedimento; `cid10`; profissional solicitante `cpfprofsol`/`nomeprofsol`; retorno `ret`; `upsexec` **não** é escolhido pelo usuário comum — fica para o SISREG/regulação) + observação livre. Até o spike b, este formulário é o mapa por GET; depois do spike vira ipsis litteris (R-04).
- *Anexos*: uma caixinha por exigência documental (plano 03) + caixinha "Anexos gerais" sempre presente. Componente `<UploadAnexo>` (várias fotos/PDFs por caixinha, pré-visualização, remover antes de enviar).
- Tudo gravado em `formulario_json` = `{ canonico: {...}, sisreg: {...}, ser: {...}, sernit: {...} }`, com `formulario_versao_id` apontando para o snapshot do catálogo usado (`definicao_json` + hash). Nova versão só quando o hash muda.

**6 — Revisão e ações.**
- **Salvar rascunho** — sempre permitido; estado `Rascunho`.
- **Enviar** — só sem pendência (obrigatórios preenchidos, documentais obrigatórias atendidas ou dispensadas por exame interno, questionário respondido, CPF presente). Com pendência, o botão explica o que falta e oferece "salvar como rascunho".
  - *Externo e NAR*: estado → `PendenteRegulacao`, evento `EnvioFila`. Nenhum sistema externo é tocado.
  - *Interno* (D-8): chama o motor do plano 11 com a **credencial pessoal do solicitante** (D-2: sem credencial → modal → opção de salvar no perfil); sucesso → grava `numero_externo`, `sisreg_editavel_ate`, estado `PendenteRegulacao`; falha → fica `Rascunho` com o motivo no histórico. **Até o incremento 7 este caminho é o mesmo do Externo** (entra na fila sem número) e a tela avisa "inclusão no SISREG ainda é feita pelo agente".

### Tabelas deste plano (as demais estão no plano 04)

**`regulacao_formulario_versao`**: id, esquema (`sisreg.inclusao` | `externo.uniao`), procedimento_id, definicao_json (lista de campos: chave canônica, rótulo, tipo, obrigatório, opções, `origem[]`), hash unique, criado_em.

**`regulacao_formulario_campo_mapa`**: formulario_versao_id, chave_canonica, sistema, nome_nativo (`form0:dinamico_id_N`, campo do `marcar`), transformacao NULL (ex.: data dd/MM/yyyy; valor múltiplo `\n`).

**`regulacao_solicitacao_exigencia`** (caixinha) e **`regulacao_exigencia_arquivo`** (versões): definidas no plano 03; no incremento 2 só a caixinha "Anexos gerais" (`regra_id = null`) é usada.

### Anexos — onde guardar (questão aberta 4)

Recomendação: **Spaces** desde o início, via `IArmazenamentoArquivos`, generalizando `ArmazenamentoSpaces` para aceitar `ContentType` do arquivo (hoje fixo em PDF) e chave `Regulacao/{pacienteId}/{arquivoId}.{ext}`. Motivo: fotos de celular de exames somam dezenas de MB por solicitação; `midia` (bytea) foi feita para logos e prints. Limite 15 MB por arquivo, tipos `image/jpeg`, `image/png`, `application/pdf`. Se o Bernardo preferir `midia` para começar, o único ponto que muda é a implementação de `IArquivoExigenciaStore`.

### Deprecação dos rascunhos por sistema

1. Job único `MigrarRascunhosLegadosParaRegulacao` (endpoint 51, idempotente por `origem_legado_id`): copia `ser_solicitacao_rascunho`/`sernit_solicitacao_rascunho` em `Rascunho`/`Pronto` para `regulacao_solicitacao` (fluxo Externo, `formulario_json.ser|sernit` = `CamposJson`, caixinha "Anexos gerais" com os arquivos de `ser_rascunho_anexo`, `unidade_solicitante_id` = unidade principal do autor, `criado_por` = autor).
2. As páginas `SerNovaSolicitacaoPage`/`SernitNovaSolicitacaoPage` viram somente-leitura com link para o wizard novo; os endpoints `POST/PUT` de rascunho passam a devolver 410.
3. Uma release depois: migration `RemoveRascunhosPorSistema` com `DropTable` das quatro tabelas. **Nunca no mesmo deploy** da migração de dados.

## Tarefas

- [ ] **2.1** (plano 09) `regulacao_configuracao` mínima: `permitir_externo_com_interno`, `exigir_cpf`, `rotulo_fila`.
- [ ] **2.2** Entidades `RegulacaoSolicitacao` (colunas do plano 04 §Tabela), `RegulacaoFormularioVersao`, `RegulacaoFormularioCampoMapa`, `RegulacaoSolicitacaoExigencia`, `RegulacaoExigenciaArquivo`; enums `FluxoRegulacao`, `StatusRegulacao`; configurações; migration `SolicitacaoDeRegulacao`.
- [ ] **2.3** Extrair `CampoDinamico`, `CampoPaciente` e o conversor de data para `SMSMais.front/src/shared/regulacao/`; `features/ser` e `features/sernit` passam a importar de lá (sem mudança de comportamento; `npm run build` verde).
- [ ] **2.4** `<UploadAnexo>` em `shared/ui` (múltiplos arquivos, tipos, limite, progresso, remover) + `IArquivoExigenciaStore` (Spaces) + endpoints `POST /regulacao/solicitacoes/{id}/exigencias/{exigenciaId}/arquivos`, `DELETE .../arquivos/{arquivoId}`, `GET .../arquivos/{arquivoId}/conteudo`.
- [ ] **2.5** Passo do paciente (plano 10).
- [ ] **2.6** Gerador do formulário Externo (união) + `formulario_versao` + mapa; endpoint `GET /regulacao/procedimentos/{id}/formulario?fluxo=`.
- [ ] **2.7** Formulário Interno provisório (campos do mapa por GET) — marcado como provisório no código e no `PROGRESSO.md`.
- [ ] **2.8** Wizard em `features/regulacao/pages/NovaSolicitacaoPage.tsx` (+ componentes por passo), NAR, unidade de origem, Salvar rascunho, Enviar (fila) — `POST /regulacao/solicitacoes`, `PUT .../{id}`, `POST .../{id}/enviar-fila` (47).
- [ ] **2.9** Migração dos rascunhos legados + páginas antigas somente-leitura (o `DropTable` fica para depois).
- [ ] **2.10** Testes: geração da união com dois catálogos seedados; mapa de campos traduz para nativo; NAR exige `em_nome_de`; Externo bloqueado quando há interno e a config não permite; Enviar recusa com pendência e lista o que falta.

## Dependências

Plano 01 (busca), plano 10 (paciente), plano 09 (configuração mínima). O plano 04 define a entidade da solicitação e os estados — os dois planos são implementados juntos (incrementos 2 e 3 encadeados).

## Riscos e pontos a confirmar

- A união SER ∪ SERNIT depende de um par confirmado (plano 08). Sem par, o formulário pode pedir campo que só um sistema tem; o agente resolve no envio. Documentar na tela ("campo exigido pelo SER").
- O formulário Interno antes do spike b é aproximação; R-04 (ipsis litteris) só se cumpre no incremento 7.
- `EscopoUnidade` é fail-closed (ADR-0037): usuário sem vínculo não abre solicitação. Para o NAR, o "em nome de" **não** passa pelo escopo — é validação própria (unidade ativa do município).

## Testes

Tarefa 2.10 + verificação manual: abrir solicitação Externa com anexo; salvar rascunho; recarregar; enviar; ver na fila (plano 04). Abrir NAR; conferir `unidade_em_nome_de_id`. Trocar a unidade ativa no header e conferir `unidade_solicitante_id`.

## Fora de escopo

Motor de regras (03), fila e agente (04), inclusão real no SISREG (11), envio ao SER/SERNIT (12).

---

## Especificação para execução

> A entidade `RegulacaoSolicitacao`, o enum de estados e `RegulacaoEvento` estão especificados no **plano 04 §Especificação**; nascem na mesma migration deste plano (`SolicitacaoDeRegulacao`). Aqui: formulário, exigências/arquivos, wizard e a deprecação dos rascunhos.

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoFormularioVersao.cs`, `RegulacaoFormularioCampoMapa.cs`, `RegulacaoSolicitacaoExigencia.cs`, `RegulacaoExigenciaArquivo.cs` | criar |
| `SMSMais.Data/Entities/Enums/FluxoRegulacao.cs`, `SituacaoExigenciaRegulacao.cs`, `SituacaoArquivoExigencia.cs`, `OrigemArquivoExigencia.cs` | criar |
| `SMSMais.Data/Configurations/Regulacao/` (4 configurações) | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSets `RegulacaoFormularioVersoes`, `RegulacaoFormularioCamposMapa`, `RegulacaoSolicitacaoExigencias`, `RegulacaoExigenciaArquivos` |
| `SMSMais.Data/Migrations/<ts>_SolicitacaoDeRegulacao.cs` | gerar (com as entidades do plano 04) |
| `SMSMais.Core/Regulacao/Formularios/IRegulacaoFormularioService.cs`, `RegulacaoFormularioService.cs` | criar |
| `SMSMais.Core/Regulacao/Anexos/IArquivoExigenciaStore.cs`, `ArquivoExigenciaStoreSpaces.cs`, `IRegulacaoExigenciaService.cs`, `RegulacaoExigenciaService.cs` | criar |
| `SMSMais.Core/Armazenamento/IArmazenamentoArquivos.cs`, `ArmazenamentoSpaces.cs` | alterar: sobrecarga `SalvarAsync(string chave, byte[] conteudo, string contentType, CancellationToken ct)`; a existente delega com `"application/pdf"` |
| `SMSMais.Core/Regulacao/Legado/MigradorRascunhosLegadosService.cs` | criar |
| `SMSMais.Core/DependencyInjection.cs` | registrar `IRegulacaoFormularioService`, `IArquivoExigenciaStore`, `IRegulacaoExigenciaService`, `MigradorRascunhosLegadosService` |
| `SMSMais.Api/Controllers/RegulacaoSolicitacoesController.cs` | criar (plano 04); as rotas de formulário/exigência/arquivo/legado abaixo entram nele |
| `SMSMais.Api/Controllers/SerController.cs` (`SerRascunhoController`), `SernitController.cs` | tarefa 2.9: `POST/PUT/DELETE` de rascunho devolvem `410 Gone` com `ProblemDetails` apontando `/app/regulacao/solicitacoes/nova` |
| `SMSMais.front/src/shared/regulacao/CampoDinamico.tsx`, `CampoPaciente.tsx`, `datas.ts` | criar por **extração** literal de `features/ser/pages/SerNovaSolicitacaoPage.tsx`; a página do SER e a do SERNIT passam a importar daqui |
| `SMSMais.front/src/shared/ui/UploadAnexo.tsx` | criar |
| `SMSMais.front/src/features/regulacao/pages/NovaSolicitacaoPage.tsx`, `components/wizard/{EstadoWizard.ts,PassoUnidadeEmNomeDe,PassoProcedimento,PassoDestino,PassoPaciente,PassoRegras,PassoFormulario,PassoRevisao}.tsx` | criar |
| `SMSMais.front/src/features/ser/pages/SerNovaSolicitacaoPage.tsx`, `sernit/pages/SernitNovaSolicitacaoPage.tsx` | tarefa 2.9: banner "somente leitura" + botões de salvar desabilitados |

### B. Entidades

```csharp
public sealed class RegulacaoFormularioVersao
{
    public Guid Id { get; set; }
    public string Esquema { get; set; } = string.Empty;   // esquema: "sisreg.inclusao" | "externo.uniao" (max 40)
    public Guid ProcedimentoId { get; set; }              // procedimento_id (FK regulacao_procedimento)
    public string DefinicaoJson { get; set; } = "[]";     // definicao_json jsonb: CampoFormulario[]
    public string Hash { get; set; } = string.Empty;      // hash (max 64), UNIQUE ux_regulacao_form_versao_hash
    public DateTime CriadoEm { get; set; }
    public ICollection<RegulacaoFormularioCampoMapa> Mapa { get; set; } = [];
}
// CampoFormulario (JSON): { chave, rotulo, tipo: "texto"|"textarea"|"select"|"radio"|"checkbox"|"data"|"numero"|"cid",
//   obrigatorio, opcoes?: [{ valor, rotulo, origens: ["Ser","Sernit"] }], origens: ["Ser","Sernit"] | ["Sisreg"], ordem }

public sealed class RegulacaoFormularioCampoMapa
{
    public Guid Id { get; set; }
    public Guid FormularioVersaoId { get; set; }          // formulario_versao_id (Cascade)
    public string ChaveCanonica { get; set; } = string.Empty; // chave_canonica (max 120)
    public SistemaRegulacao Sistema { get; set; }
    public string NomeNativo { get; set; } = string.Empty;    // nome_nativo (max 200): "form0:dinamico_id_3", "cid10"…
    public string? Transformacao { get; set; }            // null | "data_ddMMyyyy" | "multiplo_quebra_linha" | "opcao:{json de-para}"
}
// unique ux_regulacao_form_mapa (formulario_versao_id, chave_canonica, sistema)

public sealed class RegulacaoSolicitacaoExigencia
{
    public Guid Id { get; set; }
    public Guid SolicitacaoId { get; set; }               // solicitacao_id (FK regulacao_solicitacao, Cascade)
    public Guid? RegraId { get; set; }                    // regra_id (FK regulacao_regra, SetNull); null = "Anexos gerais"
    public string Titulo { get; set; } = string.Empty;    // titulo (max 200)
    public bool Obrigatoria { get; set; }
    public SituacaoExigenciaRegulacao Situacao { get; set; } = SituacaoExigenciaRegulacao.Pendente;
    public Guid? ExameInternoExameImagemId { get; set; }  // exame_interno_exame_imagem_id (sem FK física)
    public Guid? ExameInternoLaudoId { get; set; }        // exame_interno_laudo_id (sem FK física)
    public Guid? ValidadoExamePor { get; set; }  public DateTime? ValidadoExameEm { get; set; }
    public string? CriticaTexto { get; set; }             // critica_texto (max 2000)
    public int Ordem { get; set; }
    public ICollection<RegulacaoExigenciaArquivo> Arquivos { get; set; } = [];
}
// unique parcial ux_regulacao_exigencia_regra (solicitacao_id, regra_id) WHERE regra_id IS NOT NULL

public sealed class RegulacaoExigenciaArquivo
{
    public Guid Id { get; set; }
    public Guid ExigenciaId { get; set; }                 // exigencia_id (Cascade)
    public string ChaveArmazenamento { get; set; } = string.Empty; // chave_armazenamento (max 300): "Regulacao/{pacienteId}/{id}.{ext}"
    public string Nome { get; set; } = string.Empty;      // nome (max 260)
    public string ContentType { get; set; } = string.Empty; // content_type (max 120)
    public long Tamanho { get; set; }
    public string Sha256 { get; set; } = string.Empty;    // sha256 (max 64)
    public int Versao { get; set; }                        // versao: 1..n dentro da caixinha
    public Guid? SubstituiArquivoId { get; set; }         // substitui_arquivo_id (self FK, NoAction)
    public SituacaoArquivoExigencia Situacao { get; set; } = SituacaoArquivoExigencia.Atual;
    public OrigemArquivoExigencia Origem { get; set; } = OrigemArquivoExigencia.Upload;
    public DateTime? EnviadoAoSistemaEm { get; set; }     // enviado_ao_sistema_em
    public DateTime CriadoEm { get; set; }  public Guid? CriadoPor { get; set; }
}
// índice ix_regulacao_exig_arquivo (exigencia_id, versao)
```

Enums: `FluxoRegulacao { Interno = 1, Externo = 2, Nar = 3 }` · `SituacaoExigenciaRegulacao { Pendente = 1, Atendida = 2, AtendidaPorExameInterno = 3, Dispensada = 4, Criticada = 5 }` · `SituacaoArquivoExigencia { Atual = 1, Substituido = 2, Criticado = 3, Removido = 4 }` · `OrigemArquivoExigencia { Upload = 1, ExameInterno = 2 }`.

### C. Serviços

```csharp
public interface IRegulacaoFormularioService
{
    Task<RegulacaoFormularioDto> ObterOuGerarAsync(Guid procedimentoId, FluxoRegulacao fluxo, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> TraduzirAsync(Guid formularioVersaoId, SistemaRegulacao sistema, JsonElement canonico, CancellationToken ct);
    IReadOnlyList<string> ObrigatoriosFaltando(RegulacaoFormularioDto formulario, JsonElement canonico);
}
public sealed record CampoFormularioDto(string Chave, string Rotulo, string Tipo, bool Obrigatorio, IReadOnlyList<OpcaoFormularioDto>? Opcoes, IReadOnlyList<SistemaRegulacao> Origens, int Ordem);
public sealed record OpcaoFormularioDto(string Valor, string Rotulo, IReadOnlyList<SistemaRegulacao> Origens);
public sealed record RegulacaoFormularioDto(Guid VersaoId, string Esquema, IReadOnlyList<CampoFormularioDto> Campos);
```
- **Externo** (`externo.uniao`): origem SER do canônico → `SerCatalogoService.ObterCamposAsync(tipo, valor, ramo)` + bloco fixo (`SerNovaSolicitacaoService.ObterFormularioAsync`: risco, médico, CID, telefone, natureza, unidade de origem); origem SERNIT → `SernitCatalogoService.ObterCamposAsync`. Chave canônica = `Slug(rotulo)` (sem acento, minúsculas, `_`). Mesmo slug nos dois → um campo, `Origens = [Ser, Sernit]`, `Obrigatorio = a || b`, opções unidas por rótulo (valores diferentes → `Transformacao = "opcao:{…}"` no mapa). Tipos diferentes → dois campos `x_ser`/`x_sernit`. Um só sistema com origem → campos só dele.
- **Interno** (`sisreg.inclusao`, provisório até o spike b): `cid10` (`cid`), `profissional_solicitante_cpf` (`texto`, obrigatório), `profissional_solicitante_nome` (`texto`, obrigatório), `retorno` (`radio` Sim/Não), `observacao` (`textarea`). Mapa: `cid10 → cid10`, `profissional_solicitante_cpf → cpfprofsol`, `profissional_solicitante_nome → nomeprofsol`, `retorno → ret`.
- **NAR** usa o esquema Interno.
- `Hash = SHA256(JSON canônico com campos ordenados por chave)`; hash já existente → reusa a versão.
- `TraduzirAsync`: aplica o mapa do sistema; `data_ddMMyyyy` converte ISO → dd/MM/yyyy; `multiplo_quebra_linha` junta lista com `\n` (o SER desdobra por `SerValorMultiplo`); `opcao:` traduz valor.

```csharp
public interface IRegulacaoExigenciaService
{
    Task<RegulacaoExigenciaDto> CriarAsync(Guid solicitacaoId, Guid? regraId, string titulo, bool obrigatoria, CancellationToken ct);
    Task<IReadOnlyList<RegulacaoExigenciaDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct);
    Task<RegulacaoArquivoDto> AnexarAsync(Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, Stream conteudo, CancellationToken ct);
    Task RemoverArquivoAsync(Guid solicitacaoId, Guid exigenciaId, Guid arquivoId, CancellationToken ct);
    Task<ArquivoConteudo> LerArquivoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct);
    Task DispensarAsync(Guid solicitacaoId, Guid exigenciaId, string motivo, CancellationToken ct); // agente (48)
}
public interface IArquivoExigenciaStore
{
    string MontarChave(Guid pacienteId, Guid arquivoId, string extensao);              // "Regulacao/{pacienteId}/{arquivoId}.{ext}"
    Task SalvarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct); // grava e relê para confirmar
    Task<Stream?> LerAsync(string chave, CancellationToken ct);
    Task ExcluirAsync(string chave, CancellationToken ct);
}
public sealed record RegulacaoArquivoDto(Guid Id, string Nome, string ContentType, long Tamanho, int Versao, SituacaoArquivoExigencia Situacao, OrigemArquivoExigencia Origem, DateTime? EnviadoAoSistemaEm, DateTime CriadoEm);
public sealed record RegulacaoExigenciaDto(Guid Id, Guid? RegraId, string Titulo, bool Obrigatoria, SituacaoExigenciaRegulacao Situacao, string? CriticaTexto, int Ordem, IReadOnlyList<RegulacaoArquivoDto> Arquivos);
public sealed record ArquivoConteudo(Stream Conteudo, string ContentType, string Nome);
```
Regras de `AnexarAsync`: tipo ∈ `config.AnexoTiposPermitidos` e tamanho ≤ `config.AnexoLimiteMb` (senão `ValidacaoException("arquivo", …)`); `Versao = max(versao da caixinha) + 1`; arquivo anterior `Atual` → `Substituido` **só** quando a caixinha é de exigência (regra_id ≠ null); em "Anexos gerais" cada arquivo é independente; caixinha volta a `Atendida` se estava `Pendente`/`Criticada`. `RemoverArquivoAsync` só se `EnviadoAoSistemaEm == null` e solicitação em `Rascunho`/`PendenteRegulacao`/`Devolvida`; marca `Removido` (não apaga do Spaces no ato — job de limpeza fica fora do escopo).

`MigradorRascunhosLegadosService.MigrarAsync(ct)`: para cada `SerSolicitacaoRascunho` com `Status ∈ {Rascunho, Pronto}` sem `RegulacaoSolicitacao.OrigemLegadoId == Id`: cria solicitação `Fluxo = Externo`, `Status = Rascunho`, `UnidadeSolicitanteId` = unidade `Principal` do `CriadoPor` em `usuario_unidade` (senão a primeira vinculada; senão registra em `StatusMotivo` "sem unidade — ajustar" e usa a unidade do admin global), `FormularioJson = {"canonico":{}, "ser": <CamposJson>}`, `ProcedimentoId` = origem SER por `(Tipo, RecursoValor, ramo)` → canônico (não achou → `null` + motivo), exigência "Anexos gerais" com os `SerRascunhoAnexo` copiados de `midia` para o Spaces (`Origem = Upload`). Idem `SernitSolicitacaoRascunho`. Idempotente; devolve `{ ser, sernit }`.

### D. Endpoints (no `RegulacaoSolicitacoesController`, `[Route("regulacao")]`)

| Verbo | Rota | `[RequerPermissao]` | Entrada | Saída |
|---|---|---|---|---|
| GET | `procedimentos/{id:guid}/formulario?fluxo=` | `Regulacao`, `Consulta` | — | `RegulacaoFormularioDto` |
| GET | `configuracao/fluxo` | `Regulacao`, `Consulta` | — | `{ permitirExternoComInterno, exigirCpf, rotuloFila, anexoLimiteMb, anexoTiposPermitidos }` |
| GET | `solicitacoes/{id:guid}/exigencias` | `Regulacao`, `Consulta` | — | `RegulacaoExigenciaDto[]` |
| POST | `solicitacoes/{id:guid}/exigencias/{exigenciaId:guid}/arquivos` — multipart `arquivo`, `[RequestSizeLimit(20 * 1024 * 1024)]` | `Regulacao`, `Edicao` | | `RegulacaoArquivoDto` |
| DELETE | `solicitacoes/{id:guid}/exigencias/{exigenciaId:guid}/arquivos/{arquivoId:guid}` | `Regulacao`, `Edicao` | | 204 |
| GET | `solicitacoes/{id:guid}/arquivos/{arquivoId:guid}/conteudo` | `Regulacao`, `Consulta` | | `File(stream, contentType, nome)` |
| POST | `solicitacoes/{id:guid}/exigencias/{exigenciaId:guid}/dispensar` | `RegulacaoTriagem`, `Edicao` | `{ motivo }` | 204 |
| POST | `legado/migrar-rascunhos` | `RegulacaoConfiguracao`, `Edicao` | — | `{ ser, sernit }` |

Escopo: todas as rotas de `solicitacoes/{id}` passam pelo `RegulacaoSolicitacaoService.ObterNoEscopoAsync(id)` do plano 04 (unidade do solicitante ∈ escopo, ou módulo 48).

### E. Front — wizard

- `EstadoWizard.ts` (`useReducer`): `{ passo: 0|1|2|3|4|5|6; fluxo: 'Interno'|'Externo'|'Nar'|null; unidadeEmNomeDeId?: string; procedimento?: RegulacaoProcedimentoItem; destino: { internoOk: boolean; externoOk: boolean; motivos: string[] }; paciente?: PacienteResumo; respostas: Record<string,'Sim'|'Nao'|'NaoSei'>; formulario?: RegulacaoFormulario; valores: Record<string,string>; solicitacaoId?: string; pendencias: string[] }`.
- `NovaSolicitacaoPage.tsx`: cabeçalho com a unidade ativa (do `authStore`; se `unidadeAtivaId` for `null` e o usuário tiver mais de uma, exibe aviso "escolha a unidade no topo" e bloqueia); botões "Interno/Externo" vs "NAR" na abertura (NAR só habilitado se a config permitir a qualquer usuário; a decisão do "quem pode NAR" é por módulo 47 mesmo — todos).
- `PassoUnidadeEmNomeDe` (só NAR): `Select` com `GET /unidades?ativo=true` (endpoint existente da feature `unidades`).
- `PassoProcedimento`: `<BuscaProcedimento>`; ao escolher, calcula `destino` com `existeExterno`, `executantesInternos.length` e `permitirExternoComInterno`.
- `PassoDestino`: dois cartões; desabilitado mostra o motivo; NAR pula (fluxo já é Interno).
- `PassoPaciente`: plano 10.
- `PassoRegras`: plano 03 (incremento 2: texto "sem regras cadastradas").
- `PassoFormulario`: ao entrar, se `solicitacaoId` não existe → `POST /regulacao/solicitacoes` (rascunho) para os anexos terem dono; `GET formulario` → `campos.map(c => <CampoDinamico … />)`; caixinhas via `GET exigencias` → `<UploadAnexo>` por caixinha (+ "Anexos gerais" sempre).
- `PassoRevisao`: resumo; "Salvar rascunho" (`PUT`), "Enviar" (`POST …/enviar-fila`); 400 → lista `pendencias`.
- `shared/ui/UploadAnexo.tsx`: props `{ multiple?: boolean; accept: string[]; limiteMb: number; arquivos: ArquivoResumo[]; onEnviar: (files: File[]) => Promise<void>; onRemover?: (id: string) => Promise<void>; disabled?: boolean; titulo?: string; situacao?: string }`; usa `formatarTamanhoBytes` de `features/anamnese/lib/anexos.ts`; mostra versão e "no sistema" quando `enviadoAoSistemaEm`.
- Rota `regulacao/solicitacoes/nova` em `AppRouter.tsx` (menu no plano 04).

### F. Testes

- `RegulacaoFormularioServiceTests`: `Uniao_junta_campos_com_mesmo_slug_e_marca_origens`; `Obrigatorio_se_obrigatorio_em_qualquer_sistema`; `Tipo_diferente_gera_dois_campos_sufixados`; `Hash_igual_reusa_a_versao`; `Traduzir_aplica_mapa_e_transformacoes`; `Obrigatorios_faltando_lista_as_chaves`.
- `RegulacaoExigenciaServiceTests` (store fake em memória): `Tipo_nao_permitido_e_recusado`; `Acima_do_limite_e_recusado`; `Nova_versao_incrementa_e_marca_a_anterior_substituida`; `Anexos_gerais_nao_substituem`; `Remover_depois_do_envio_e_recusado`.
- `MigradorRascunhosLegadosTests`: `Migra_cada_rascunho_uma_vez`; `Sem_unidade_vinculada_registra_motivo_e_nao_falha`.

### G. Passo a passo

1. Entidades/enums/configurações/DbSets deste plano **e** do plano 04 → build → `dotnet ef migrations add SolicitacaoDeRegulacao --project src/SMSMais.Data --startup-project src/SMSMais.Api` → build.
2. `IRegulacaoFormularioService` + DTOs + testes.
3. Sobrecarga no `ArmazenamentoSpaces` → `IArquivoExigenciaStore` → `IRegulacaoExigenciaService` → endpoints → testes.
4. **Extração** de `CampoDinamico`/`CampoPaciente`/conversor de data para `shared/regulacao/` → SER e SERNIT importam de lá → `npm run build` verde **antes** do wizard.
5. `UploadAnexo` → passos → `NovaSolicitacaoPage` → rota → `npm run build`.
6. Migrador de legado + `410` nas rotas antigas + banner → só depois de rodar o migrador em prod (com OK registrado em `PROGRESSO.md`).
7. `PROGRESSO.md`: 2.2–2.4, 2.6–2.9.

### H. Critério de pronto

Abrir solicitação Externa com dois arquivos numa caixinha, salvar rascunho, recarregar e recuperar tudo; enviar sem CPF → 400 com a lista; NAR sem unidade → 400; formulário de recurso que existe no SER e no SERNIT mostra os campos dos dois com a origem marcada; `features/ser` e `features/sernit` continuam funcionando com os componentes extraídos.
