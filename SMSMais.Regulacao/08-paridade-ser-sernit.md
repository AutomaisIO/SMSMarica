# 08 — Paridade SER × SERNIT (spike c agora; job recorrente no incremento 8)

## Objetivo

Saber, recurso a recurso, o que existe no SER e no SERNIT, com que nome e com que formulário — porque o fluxo Externo pede um formulário único cuja régua é "o que tiver mais pergunta". Primeiro como verificação única (spike c), depois como rotina que roda a cada sincronização dos catálogos e aponta divergências para a curadoria.

## Requisitos cobertos

- **R-05 (A4)** verificar se os procedimentos/códigos do SER e do SERNIT são os mesmos; nivelar pelo que tem mais pergunta.

## Decisões aplicadas

Escolha própria: o `valor` do combo é interno de cada instância e não bate — a chave tem de sair do rótulo.

**Corrigido pelo spike c (04/09/2026).** A chave planejada era *rótulo normalizado por tipo*
(`unaccent(upper(trim(rotulo)))`). Medida contra os catálogos reais, ela encontra **3 pares
acidentais**: os três catálogos usam convenções de nome disjuntas (`Ambulatório 1ª vez - Cardiologia`
× `CONSULTA EM CARDIOLOGIA` × `Cardiologia`), e o prefixo de modalidade mata toda igualdade.

A chave é **D3 = (tipo, conjunto de tokens do rótulo sem prefixo de modalidade nem ruído, dimensão
público)** — 23 pares, zero colisão dos dois lados. Especificação passo a passo em
[`revisoes/spike-c-paridade.md` §1](revisoes/spike-c-paridade.md). Pontos que não podem ser
simplificados na hora de portar para C#:

- o conteúdo dos **parênteses entra na chave** (`Cardiologia (Oncologia)` é outro recurso);
- **`COM`/`SEM` não são ruído** (`com Sedação` × `sem Sedação` são recursos opostos);
- **público (pediátrico × geral) é dimensão, não token** — mas "adulto" e "sem marcador" são o mesmo
  balde, porque o SERNIT nunca escreve "adulto";
- se a normalização esvaziar a chave, refazer sem descartar público (`Pediátria` é uma especialidade).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Catálogo SER (tipo, valor, rótulo, ramo) e campos por recurso | sim | `ser_catalogo_recurso`, `ser_catalogo_campo` (Numero, Campo, Rotulo, Tipo, Obrigatorio, OpcoesJson); `SerCatalogoSyncService` |
| Catálogo SERNIT | sim | `sernit_catalogo_recurso`, `sernit_catalogo_campo`; `SernitCatalogoSyncService` |
| Medições | sim | **Medido em 04/09/2026 pelo spike c** (o número antigo do `APRENDIZADOS.md` estava defasado): SER 482 recursos / 1.833 campos / 20 moldes — 136 CONSULTA + 98 EXAME no ramo "Não", 167 + 81 no "Sim"; SERNIT 78 recursos / 417 campos / 6 moldes — 43 + 35. Todos com `campos_lidos = true`. |
| `unaccent` | sim | Postgres |
| Origens e pareamento sugerido/confirmado | plano 01 | `regulacao_procedimento_origem` (`sugerido_procedimento_id`, `sugerido_score`, `vinculo`) |
| Acesso de leitura ao banco de prod | sim | skill `acessar-banco-no-servidor` |

Não existe hoje nenhuma tabela de equivalência entre os dois; e o SERNIT não tem testes.

## Desenho

### Spike c — **FEITO em 04/09/2026**, relatório em [`revisoes/spike-c-paridade.md`](revisoes/spike-c-paridade.md)

Resultado, para quem for implementar sem reler o relatório inteiro:

- **23 pares** pela chave D3 (19 consultas + 4 exames), **399 chaves só-SER**, **55 só-SERNIT**. Cobertura do SERNIT: 29%.
- **38 recursos do SERNIT não existem no SER**, nem por contenção — sobretudo a linha de reabilitação/órtese/prótese e 22 exames (`Eletrocardiograma`, `Retossigmoidoscopia`, `Urofluxometria`…). Para esses o Externo tem destino único; é ressalva de destino (plano 03) por **ausência de oferta**, não por regra clínica.
- Os **17 casos de contenção não são pares, são hierarquia**: o SER quebra a especialidade em subespecialidades e o SERNIT tem um balde só (`Endocrinologia` × `ENDOCRINOLOGIA - ANDROLOGIA` / `- DIABETES`). Contenção vira **sugestão para curadoria**, nunca par automático — dois dos candidatos são falsos e só a dimensão público os barrou. **Isto abriu uma questão de modelagem para o plano 01** (o canônico não pode ser 1:1 com origem): ver §7 do relatório, pendente com o Bernardo.
- **Formulários**: 11 dos 23 pares são idênticos. Nos outros 12 o SER traz sempre o mesmo molde de 3 campos (`Queixa Principal`, `Resultado de Exames`, `Observações`) e o SERNIT traz 7 (clínico) ou 12 (cirúrgico). **Zero** conflito de tipo e **zero** de opções; a única divergência de obrigatoriedade, nas 11 ocorrências, é `Observações`.
- O vocabulário inteiro tem **76 rótulos de campo distintos** (SER 67, SERNIT 23, 14 comuns) para 26 moldes. A união é um **dicionário canônico de campos**, não engenharia por par. Pior caso: 14 campos.

**Régua união decidida:** campos = união dos dois lados; chave do campo = rótulo normalizado **mais uma tabela de sinônimos** (sem ela o par `VIDEOLARINGOSCOPIA` fica com 0 campo em comum, porque o SER escreve `Observações` e o SERNIT `Observação`); obrigatoriedade = **OR**; tipo e opções por igualdade, e **conflito vira divergência para curadoria em vez de regra automática** — não há conflito hoje, e inventar a regra agora seria adivinhação; ordem = SER primeiro, depois os só-SERNIT.

### Rotina recorrente (incremento 8)

- `RegulacaoParidadeService.RecalcularAsync()` ao fim de `SerCatalogoSyncService` e `SernitCatalogoSyncService`: para cada origem `Ser`/`Sernit` do plano 01 sem par confirmado, procura no outro sistema pelo rótulo normalizado (igualdade) e, na falta, por cosine ≥ 0,85 (embedding do plano 01) → grava `sugerido_procedimento_id`/`score`.
- Tabela **`regulacao_paridade_divergencia`**: procedimento_id, campo_canonico, sistema_a, sistema_b, tipo_divergencia (`SoEmUm`, `TipoDiferente`, `ObrigatoriedadeDiferente`, `OpcoesDiferentes`), detalhe_json, detectada_em, resolvida_em/por, resolucao (texto).
- Tela (51) "Paridade SER × SERNIT": sugestões de par (confirmar/rejeitar — a mesma curadoria do plano 01) e divergências de formulário com "resolvido" (a resolução é o que o gerador da união do plano 02 aplica: manter os dois, escolher um, traduzir opções).
- A união do plano 02 lê as divergências resolvidas; não resolvidas → mantém os dois campos e marca na tela "exigido por SER" / "exigido por SERNIT".

## Tarefas

- [x] **c** (spike) SQL + relatório `revisoes/spike-c-paridade.md` com a régua união. **Feito 04/09/2026.**
- [ ] **8.1** `RegulacaoParidadeService` + `regulacao_paridade_divergencia` + migration `ParidadeSerSernit` + disparo pós-sync.
- [ ] **8.2** Tela de divergências (51) e leitura das resoluções pelo gerador da união (plano 02, tarefa 2.6).
- [ ] Testes do SERNIT: criar `tests/SMSMais.Tests/Integracoes/Sernit*` cobrindo o parser de catálogo (hoje não há nenhum) — pré-requisito para confiar na comparação.

## Dependências

Plano 01 (origens e embeddings). Spike c não depende de nada.

## Riscos e pontos a confirmar

- Rótulos parecidos com sentido diferente ("1ª VEZ" × "RETORNO"): igualdade exata não confunde; a sugestão por cosine pode — por isso confirma-se na tela.
- ~~o par SERNIT é com **um** dos ramos (provavelmente "Não")~~ — **errado, medido no spike c**: o ramo que pareia é o **"Sim"** (ambulatório estadual), 23 pares contra **0** do ramo "Não". Mesmo assim o pareamento roda contra o **SER inteiro**: há recurso do SERNIT cujo único candidato está no ramo "Não" (`Genetica`), e o ramo é atributo de *formulário*, não de identidade do recurso.
- **O rótulo não é único nem dentro do SER**: 30 rótulos por ramo têm dois `valor` (chave natural é `(tipo, ramo, valor)`). O de-para canônico↔origem é 1:N do lado SER, e "qual `valor` enviar" precisa de desempate explícito. Os formulários dos irmãos são idênticos — conferido nos 23 pares.
- Os catálogos mudam quando a SES recompila; a rotina roda a cada sync e a divergência resolvida pode reabrir (nova linha).

## Testes

Seeds com dois catálogos pequenos: par igual, par com campo só no SER, par com opções diferentes → três divergências certas; resolver uma → união reflete.

## Fora de escopo

Escrita em qualquer sistema; regras de elegibilidade (as regras por recurso continuam presas à origem, plano 03).

---

## Especificação para execução

### A. Spike c — **executado**; resultado em [`revisoes/spike-c-paridade.md`](revisoes/spike-c-paridade.md)

O SQL que estava aqui **não roda e não serve**, por dois motivos, e fica registrado só como aviso:

1. `unaccent` está instalado no schema **`smsmarica`**, não no `public` — chamar sem qualificar dá
   `function unaccent(text) does not exist`. O mesmo vale para `vector` (plano 01).
2. Mesmo corrigido, o pareamento por igualdade de rótulo devolve **3 pares acidentais**. A chave certa
   (D3) não é expressável em SQL puro de forma legível — tem passo de remoção de prefixo, tabela de
   sinônimos e uma dimensão separada. Foi apurada em Python sobre um dump das quatro tabelas e é
   **essa lógica que vai para C#** no `RegulacaoParidadeService` (tarefa 8.1), não uma query.

Colunas conferidas contra `SerCatalogoConfiguration`/`SernitCatalogoConfiguration` e válidas para a
tarefa 8.1: `{ser,sernit}_catalogo_recurso(id, tipo, valor, rotulo, campos_lidos[, ambulatorio_estadual])`
e `{ser,sernit}_catalogo_campo(recurso_id, numero, campo, rotulo, tipo, obrigatorio, opcoes_json, ordem)`.
`TipoRecursoSer` e `TipoRecursoSernit` têm os **mesmos valores** (Consulta=1, Exame=2), então comparar
`tipo` entre sistemas é válido. **`numero` e `campo` são ids internos de cada instância** — a única
chave estável de campo é o rótulo normalizado.

### B. Arquivos (incremento 8)

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoParidadeDivergencia.cs` + `Enums/TipoDivergenciaParidade.cs` + configuração + DbSet | criar |
| `SMSMais.Data/Migrations/<ts>_ParidadeSerSernit.cs` | gerar |
| `SMSMais.Core/Regulacao/Paridade/IRegulacaoParidadeService.cs`, `RegulacaoParidadeService.cs`, `Dtos/RegulacaoParidadeDtos.cs` | criar |
| `SMSMais.Core/Ser/SerCatalogoSyncService.cs`, `Sernit/SernitCatalogoSyncService.cs` | ao fim: `await paridade.RecalcularAsync(ct)` (try/catch, só loga) — depois da chamada ao catálogo canônico |
| `SMSMais.Core/Regulacao/Formularios/RegulacaoFormularioService.cs` | ler divergências resolvidas ao gerar a união (`Resolucao`: `ManterAmbos` | `UsarSer` | `UsarSernit` | `TraduzirOpcoes`) |
| `SMSMais.Api/Controllers/RegulacaoParidadeController.cs` | criar |
| `SMSMais.front/src/features/regulacao/pages/ParidadePage.tsx` (aba em `RegulacaoConfiguracaoPage`) | criar |
| `tests/SMSMais.Tests/Regulacao/Paridade/*.cs`, `tests/SMSMais.Tests/Integracoes/Sernit/SernitCatalogoParserTests.cs` | criar |

### C. Entidade

```csharp
public sealed class RegulacaoParidadeDivergencia
{
    public Guid Id { get; set; }
    public Guid ProcedimentoId { get; set; }                 // canônico com origens Ser e Sernit
    public string ChaveCanonica { get; set; } = string.Empty; // max 120 (slug do campo) ou "" para divergência de recurso
    public TipoDivergenciaParidade Tipo { get; set; }        // SoEmUm=1, TipoDiferente=2, ObrigatoriedadeDiferente=3, OpcoesDiferentes=4
    public string DetalheJson { get; set; } = "{}";          // {ser: {...}, sernit: {...}}
    public DateTime DetectadaEm { get; set; }
    public DateTime? ResolvidaEm { get; set; }  public Guid? ResolvidaPor { get; set; }
    public string? Resolucao { get; set; }                   // max 40: ManterAmbos | UsarSer | UsarSernit | TraduzirOpcoes
    public string? ResolucaoJson { get; set; }               // de-para de opções quando TraduzirOpcoes
    public bool Reaberta { get; set; }                       // detectada de novo depois de resolvida
}
// unique ux_regulacao_paridade (procedimento_id, chave_canonica, tipo)
```

**Ajuste exigido pelo spike c:** `ProcedimentoId` sozinho pressupõe **uma** origem por sistema, e não é
o caso — o SER tem 30 rótulos por ramo com dois `valor`, e a hierarquia SERNIT-balde × SER-folhas cria
1:N do outro lado. A divergência precisa apontar para o **par de origens** que a gerou
(`ser_origem_id` / `sernit_origem_id`, ambos para `regulacao_procedimento_origem`), com
`ProcedimentoId` mantido só para agrupar na tela. O unique passa a
`(ser_origem_id, sernit_origem_id, chave_canonica, tipo)`. Sem isso, dois `valor` do SER com formulários
que divirjam no futuro brigam pela mesma linha e o último a rodar vence em silêncio.

Falta ainda a tabela de **sinônimos de rótulo de campo** (semente: `OBSERVACAO` ↔ `OBSERVACOES`), lida
pelo comparador antes de classificar `SoEmUm`. Sem ela o par `VIDEOLARINGOSCOPIA` gera 10 divergências
falsas.

### D. Serviço e endpoints

```csharp
public interface IRegulacaoParidadeService
{
    Task<RegulacaoParidadeResumoDto> RecalcularAsync(CancellationToken ct);   // para cada canônico com origem Ser E Sernit: compara campos; cria/reabre divergências; fecha as que sumiram
    Task<PaginaDto<RegulacaoParidadeDivergenciaDto>> ListarAsync(bool soAbertas, Guid? procedimentoId, int pagina, int tamanho, CancellationToken ct);
    Task ResolverAsync(Guid id, string resolucao, JsonElement? resolucaoJson, CancellationToken ct);
    Task<IReadOnlyDictionary<string, ResolucaoParidade>> ResolucoesAsync(Guid procedimentoId, CancellationToken ct);  // consumido pelo gerador da união
}
```
`RegulacaoParidadeController`, `[Route("regulacao/paridade")]` (`RegulacaoConfiguracao`): `POST recalcular` (Edicao), `GET ?soAbertas=&procedimentoId=` (Consulta), `POST {id}/resolver {resolucao, resolucaoJson?}` (Edicao), `GET resumo` (Consulta).

### E. Front

`ParidadePage.tsx`: cartões de resumo (canônicos pareados, divergências abertas, reabertas); tabela (procedimento, campo, tipo de divergência, SER, SERNIT, ação); ação "Resolver" com as quatro opções e, para `TraduzirOpcoes`, um de-para; link para o procedimento na curadoria. Pareamento de recursos (sugestões) continua na aba do plano 01.

### F. Testes

`RegulacaoParidadeServiceTests` (fixture com dois catálogos seedados): `Par_igual_nao_gera_divergencia`; `Campo_so_no_ser_gera_SoEmUm`; `Opcoes_diferentes_gera_divergencia`; `Resolver_e_recalcular_nao_reabre_se_nada_mudou`; `Mudanca_no_catalogo_reabre`. `RegulacaoFormularioServiceTests` (+): `Uniao_aplica_resolucao_UsarSer`. `SernitCatalogoParserTests`: parser do catálogo SERNIT com HTML de fixture (hoje sem cobertura). Caso concreto achado no spike c: o recurso `Eletrocardiograma` tem um campo cujo rótulo visível é o nome técnico da coluna deles (`campo='form0:dinamico_id_1009'`, `rotulo='OBS_CONSULTA_EXAME_REDE'`) — o parser tem de preservá-lo como veio, não "consertar".

Acrescentar, a partir do spike c: `Chave_ignora_prefixo_de_modalidade` (`CONSULTA EM CARDIOLOGIA` ≡ `Cardiologia`); `Chave_separa_pediatrico_de_geral`; `Chave_nao_funde_com_e_sem_sedacao`; `Chave_nao_funde_cardiologia_com_cardio_oncologia`; `Chave_de_Pediatria_nao_fica_vazia`; `Sinonimo_observacao_singular_e_plural_nao_gera_divergencia`. Os cinco primeiros são regressões de bugs reais do normalizador, encontrados durante o spike.

### G. Passo a passo

1. ~~Spike c (SQL) → relatório.~~ **Feito 04/09/2026** — [`revisoes/spike-c-paridade.md`](revisoes/spike-c-paridade.md). Antes da tarefa 8.1, resolver com o Bernardo a questão §7 do relatório (canônico = folha do SER ou balde do SERNIT), porque ela decide o modelo do plano 01.
2. Entidade/enum/config/DbSet → build → migration `ParidadeSerSernit` → build.
3. Service + disparo pós-sync + controller → testes.
4. Gerador da união lê resoluções → teste.
5. Front → `npm run build`.
6. `PROGRESSO.md` c, 8.1, 8.2.

### H. Critério de pronto

Dois catálogos seedados com um par igual, um campo só no SER e opções diferentes → três divergências corretas; resolver "UsarSer" → a união do plano 02 mostra só o campo do SER; sync do SERNIT que muda o campo → divergência reaberta.
