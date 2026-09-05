# 10 — Paciente no fluxo (incremento 2)

## Objetivo

O passo 3 do wizard: achar o paciente na base local; se não existir, buscar no CADSUS pela porta configurada (SISREG ou SER) e criar sem sobrescrever nada; exigir CPF antes de a solicitação entrar na fila; sugerir o telefone verificado. Nada aqui é novo em mecânica — é reaproveitar o que a importação e o cadastro já fazem, no lugar certo.

## Requisitos cobertos

- **R-08 (A6)** dados do paciente (CPF/CNS); sem CPF → solicitar; não existe local → SER / outro cadastro; eSUS no futuro.

## Decisões aplicadas

D-5 (paciente depois do procedimento e do destino, antes das regras).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Busca local por nome/CPF; por CPF; por telefone | sim | `IPacientesService.BuscarAsync`, `ObterPorCpfAsync` (`GET /pacientes/por-cpf/{cpf}`), `ObterPorCnsAsync` (interno, sem endpoint) |
| Consulta CADSUS com fonte configurável e cache tri-estado | sim | `ICadastroPacienteService` (`Core/Integracoes/Cadastro/`: `CadastroPacienteRoteador`, `SerCadastroPacienteService`, `CacheCadastroSer`); `sisreg_configuracao.fonte_cadastro_paciente` (Sisreg / Ser / SerComFallbackSisreg) |
| Ordem obrigatória de resolução e regra "nunca sobrescrever nome" | sim | memória do projeto (importação SISREG): CNS → CADSUS → CPF → reusar sem tocar nome; só cria no miss total |
| Validação de CPF | sim | `CpfBr` no Core |
| Cadastro | sim | `IPacientesService.CadastrarAsync` (`POST /pacientes`); CPF opcional no validador |
| Componente de busca | sim | `shared/ui/BuscaPaciente`, `NomePacienteComResumo` |
| Telefone verificado por OTP | sim | telecom marcado no FHIR (`telefoneVerificadoNosso` já é devolvido na pesquisa de paciente do SER) |
| Pesquisa de paciente no SER pela tela de nova solicitação | sim | `GET /regulacao/ser/nova-solicitacao/paciente?documento=` (`SerNovaSolicitacaoService.PesquisarPacienteAsync`) |

## Desenho

### Passo do wizard

1. Campo único **CPF / CNS / nome** → `BuscarAsync`. Achou → seleciona; mostra CPF, CNS, nascimento, telefones (o verificado destacado).
2. Não achou por CPF/CNS → botão **"Buscar no CADSUS"** → `ICadastroPacienteService.ConsultarPorCpfAsync/ConsultarPorCnsAsync` (roteador decide a porta; não injetar `IConsultaCnsService` direto — regra da casa). Resultado em cartão de confirmação; ao confirmar, **antes de criar**, reconsultar a base pelo CPF retornado (pode existir com outro CNS) → reusa sem tocar no nome; miss total → `CadastrarAsync` com `meta.source` da fonte.
3. **CPF ausente**: a solicitação pode ser salva como Rascunho, mas a transição para a fila (`enviar-fila`) recusa com "CPF obrigatório para regulação" quando `regulacao_configuracao.exigir_cpf = true`. Botão "Informar CPF" chama o endpoint de atualização do paciente (`PUT /pacientes/{id}`) com validação `CpfBr`; se o CPF já pertence a outro cadastro, seguir a regra da casa (repontar, nunca fundir nem apagar).
4. **Telefone**: o verificado por OTP é sugerido no campo de contato do formulário (bloco fixo do SER pede telefone); se não há verificado, o operador digita e a tela avisa que não está verificado.
5. NAR e Interno: nada muda; o paciente é o mesmo passo.

### Por que CPF na transição e não no cadastro

ADR-0041 decidiu que paciente sem CPF entra marcado, não fica de fora. O módulo respeita isso: cadastra e salva rascunho sem CPF; só não **regula** sem CPF, porque o SERNIT não grava sem CPF e o SISREG/SER precisam dele para casar identidade. Registrar no ADR-0052 como consequência.

### Extensões previstas

eSUS como nova porta do roteador (novo valor em `FonteCadastroPaciente`), sem mudança no wizard.

## Tarefas

- [ ] **2.5** Componente `PassoPaciente` (busca local, cartão, "Buscar no CADSUS", confirmação, "Informar CPF") usando os endpoints existentes; adicionar endpoint `GET /pacientes/por-cns/{cns}` (hoje só interno) com `Pacientes/Consulta`.
- [ ] Regra no service da solicitação: `enviar-fila` exige `paciente_cpf` quando `exigir_cpf`; snapshots `paciente_cpf/cns/nome` gravados na solicitação.
- [ ] Testes: paciente existente por CPF com CNS diferente → reusa e não muda nome; miss total → cria com `meta.source`; sem CPF → rascunho ok, fila recusa; CPF inválido recusado por `CpfBr`.

## Dependências

Nenhuma além do que já existe.

## Riscos e pontos a confirmar

- Cada consulta CADSUS pelo SISREG gasta o orçamento do operador institucional; a fonte configurada em prod deve ser SER (memória do projeto: modo 3 só é seguro com o SER saudável). O wizard mostra qual porta foi usada.
- Não sobrescrever telefone principal (contato validado por OTP é intocável, ADR-0020).

## Testes

Listados nas tarefas. Manual: CPF inexistente local + existente no CADSUS → cria; CNS de paciente já cadastrado por CPF → reusa.

## Fora de escopo

Fusão de cadastros, verificação de telefone, eSUS.

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Api/Controllers/PacientesController.cs` | acrescentar `GET por-cns/{cns}` (`Pacientes`, Consulta) chamando `IPacientesService.ObterPorCnsAsync` |
| `SMSMais.Core/Regulacao/Pacientes/IRegulacaoPacienteService.cs`, `RegulacaoPacienteService.cs`, `Dtos/RegulacaoPacienteDtos.cs` | criar (resolução local → CADSUS → criar/reusar; sem duplicar regra do cadastro) |
| `SMSMais.Core/DependencyInjection.cs` | registrar |
| `SMSMais.Api/Controllers/RegulacaoSolicitacoesController.cs` | rotas `regulacao/pacientes/*` abaixo (ou controller próprio `RegulacaoPacientesController`) |
| `SMSMais.front/src/features/regulacao/components/wizard/PassoPaciente.tsx`, `components/CartaoPacienteCadsus.tsx`, `components/InformarCpfModal.tsx` | criar |
| `tests/SMSMais.Tests/Regulacao/Pacientes/RegulacaoPacienteServiceTests.cs` | criar |

### B. Serviço

```csharp
public interface IRegulacaoPacienteService
{
    Task<IReadOnlyList<PacienteResumoRegulacaoDto>> BuscarLocalAsync(string termo, CancellationToken ct);            // IPacientesService.BuscarAsync + ObterPorCnsAsync quando termo é CNS de 15 dígitos
    Task<PacienteCadsusDto> ConsultarCadsusAsync(string cpfOuCns, CancellationToken ct);                          // ICadastroPacienteService (roteador); NaoEncontradoException = não existe; outra = fonte falhou → ValidacaoException("cadsus.indisponivel", "…porta {FonteAtual}")
    Task<PacienteResumoRegulacaoDto> ConfirmarCadsusAsync(PacienteCadsusDto dto, CancellationToken ct);            // reconsulta local por CPF (e CNS); existe → reusa SEM alterar nome; não existe → IPacientesService.CadastrarAsync com meta.source da fonte
    Task<PacienteResumoRegulacaoDto> InformarCpfAsync(Guid pacienteId, string cpf, CancellationToken ct);           // CpfBr.Valido; CPF de outro cadastro → ConflitoException("paciente.cpf_de_outro_cadastro") com o id do outro (o front oferece "usar este cadastro")
}
public sealed record PacienteResumoRegulacaoDto(Guid Id, string Nome, string? Cpf, string? Cns, DateOnly? Nascimento, string? Sexo, string? TelefoneVerificado, IReadOnlyList<string> OutrosTelefones, bool CpfPendente);
public sealed record PacienteCadsusDto(string? Cpf, string? Cns, string Nome, DateOnly? Nascimento, string? Sexo, string? NomeMae, string Fonte /* "Sisreg" | "Ser" */);
```
Regras: `ConsultarCadsusAsync` nunca chama `IConsultaCnsService` direto (sempre o roteador). `ConfirmarCadsusAsync` segue a ordem obrigatória: local por CNS → local por CPF → criar; nunca sobrescreve nome/nascimento/sexo de cadastro existente; telefone só é sugerido, nunca gravado aqui. `InformarCpfAsync` usa o mesmo endpoint/serviço que a recepção já usa para o gate de CPF (`PUT /pacientes/{id}` ou o método específico existente — reusar, não duplicar).

### C. Endpoints

`GET regulacao/pacientes/buscar?termo=` (`Regulacao`, Consulta) · `GET regulacao/pacientes/cadsus?documento=` (`Regulacao`, Consulta) · `POST regulacao/pacientes/cadsus/confirmar` body `PacienteCadsusDto` (`Regulacao`, Inclusao) · `POST regulacao/pacientes/{id:guid}/cpf {cpf}` (`Regulacao`, Edicao). E `GET pacientes/por-cns/{cns}` (`Pacientes`, Consulta).

### D. Front

- `PassoPaciente.tsx`: campo único "CPF, CNS ou nome" → `useBuscarPacienteLocal(termo)` (debounce 300 ms); lista com `NomePacienteComResumo`; selecionado → cartão (CPF, CNS, nascimento, telefone verificado destacado; chip "CPF pendente" quando falta). Sem resultado por CPF/CNS → botão "Buscar no CADSUS" → `CartaoPacienteCadsus` (dados + "fonte: SER/SISREG") → "Confirmar e usar" → `POST confirmar`. "Informar CPF" → `InformarCpfModal` (máscara + validação de DV no cliente antes de enviar); 409 `paciente.cpf_de_outro_cadastro` → oferece "usar o cadastro existente" (troca o paciente selecionado).
- Aviso quando `configFluxo.exigirCpf && cpfPendente`: "sem CPF a solicitação pode ser salva como rascunho, mas não enviada".
- Hooks em `features/regulacao/api/queries.ts`: `useBuscarPacienteLocal`, `useConsultarCadsus` (mutation, para não disparar sozinho), `useConfirmarCadsus`, `useInformarCpf`.

### E. Testes

`RegulacaoPacienteServiceTests` (fixture + `ICadastroPacienteService` substituído por NSubstitute): `Existente_por_cpf_com_cns_diferente_reusa_e_nao_muda_nome`; `Miss_total_cria_com_meta_source`; `Cadsus_nao_encontrado_vira_nao_encontrado`; `Cadsus_indisponivel_cita_a_porta`; `Cpf_invalido_recusado`; `Cpf_de_outro_cadastro_conflita_com_id`.

### F. Passo a passo

1. `GET pacientes/por-cns/{cns}` → build.
2. `IRegulacaoPacienteService` + endpoints + testes.
3. `PassoPaciente` + modais → `npm run build`.
4. `PROGRESSO.md` 2.5.

### G. Critério de pronto

CPF que não existe local e existe no CADSUS → cadastro criado com a fonte; CNS de paciente já cadastrado por CPF → reusa sem alterar o nome; paciente sem CPF → rascunho salva, `enviar-fila` devolve pendência "CPF do paciente"; CPF de outro cadastro → oferta de troca.
