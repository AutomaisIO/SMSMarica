# ADR-0022 — Indicadores contratuais com motor SQL cadastrável

**Status:** aceito · **Data:** 2026-07-23

## Contexto

A SMS Maricá cobra do HMCML uma planilha contratual de indicadores (5 abas, ~94 linhas)
apurada por mês de competência e fechada por trimestre. Cada linha tem numerador,
denominador, resultado, meta e pontuação; o total da aba vira "percentual alcançado da
pontuação". Hoje isso é preenchido à mão.

Os dados de origem estão no **Oracle do Salux HIS** (schema `INFOSAUDE`), na rede interna do
hospital. O backend já alcança essa base pelo túnel WireGuard e já executa consulta read-only
com guard (`IFonteDados` / `SaluxOracleFonte` / `SqlReadOnlyGuard`), criados para o módulo IA.

Duas perguntas precisavam de decisão:

1. **Onde mora o cálculo de cada indicador?** Em código (deploy a cada ajuste) ou em cadastro?
2. **Qual é a linguagem do cálculo?** SQL, Python sandboxado, ou C# plugável?

## Decisão

**O motor de cada indicador é um SQL guardado no cadastro, versionado a cada gravação, e
executado read-only contra a base de origem.** Meta, peso, memória de cálculo e forma de
cálculo também são cadastro — não código.

### Por que SQL e não Python

Python daria flexibilidade, mas custaria caro: é um runtime que não existe no servidor .NET, e
código arbitrário vindo de um cadastro de tela é execução remota por desenho — exigiria sandbox
e isolamento para ser aceitável. Em troca de quê? Praticamente todo indicador da planilha é uma
razão entre duas contagens. O SQL já é a linguagem natural disso, já tem guard read-only
testado, já tem conta com permissão mínima, e é auditável linha a linha por quem entende do
domínio.

Onde o SQL não basta (soma de subindicadores), o cadastro resolve com o tipo `Agrupador` — que
é exatamente o `=SUM(G6:G10)` da planilha.

### Contrato do SQL

Parâmetros nomeados: `:ini`, `:fim`, `:hospital`. Substituídos antes da execução por literais
tipados (`DateOnly`/`int`) — nunca por texto do usuário — e o resultado ainda passa pelo guard.

| Tipo | O SQL devolve |
|---|---|
| `Razao` | 1 linha: `numerador`, `denominador` |
| `Densidade` | 1 linha: `numerador`, `denominador` (× `FatorDensidade`) |
| `Absoluto` | 1 linha: `numerador` |
| `Distribuicao` | N linhas: `rotulo`, `quantidade` |
| `Agrupador` | nada — soma os filhos |

### O cálculo espelha as fórmulas da planilha, não uma reinterpretação

Lendo as fórmulas do arquivo original:

- `=IFERROR(H3/I3,"-")` → resultado é **sempre** numerador ÷ denominador. Não existe "média"
  como cálculo separado: tempo médio é Σ minutos ÷ nº de pacientes.
- `=IFERROR((H22/I22)*1000,"-")` → densidade é a mesma razão com multiplicador.
- `=IF(J3="-",0,IF(J3<=5,$G3,0))` → pontuação é **tudo-ou-nada**: bateu a meta leva o peso
  cheio, não bateu leva zero, sem denominador leva zero.
- `=IF(J31>=30%,...)` → percentual é guardado como **fração** (0,30), não como 30. A conversão
  para "%" é formatação de tela.
- Trimestre é `SUM(numeradores) / SUM(denominadores)` — **não** a média dos três meses. Como o
  filtro da tela é um período livre, apurar direto no trimestre já produz isso.

Por isso a meta é modelada como operador + valor (`MetaOperador`, `MetaValor`,
`MetaValorMaximo`), e não só como texto: sem isso não há como pontuar.

### Tudo é editável

Meta, peso, memória de cálculo, fonte declarada, unidade, tipo e SQL são campos de cadastro,
com CRUD completo. A planilha é cláusula de contrato: muda de versão, ganha linha, muda meta.
Nada disso pode exigir deploy.

O SQL é versionado (`indicador_versao`) a cada mudança real, com nota do autor. Editar meta ou
peso não gera versão — só o motor.

### Resultado é gravado, com a versão que o produziu

`indicador_execucao` guarda numerador, denominador, valor, se atingiu a meta, pontuação apurada,
duração e erro — junto com o id da versão do SQL. Sem isso o número não é reproduzível, e um
indicador que mudou de fórmula no meio do trimestre viraria discussão sem prova.

Falha também é gravada: execução que quebrou é informação.

## Consequências

- **Escopo da base:** só o hospital 1 (Conde Modesto Leal). UPA Inoã (2) e PA Santa Rita (3)
  existem na mesma base Salux mas estão fora desta planilha — o serviço recusa outra unidade.
- **Apuração é sob demanda e sequencial.** A tela não dispara consulta ao abrir, e a apuração de
  uma aba roda um indicador por vez: o Oracle é produção viva de hospital, não é lugar de abrir
  dezenas de sessões para desenhar tela.
- **Ressalva anda junto do número.** Vários campos do Salux têm preenchimento parcial (a hora do
  atendimento médico, por exemplo, está em 48,7% dos boletins). O campo `Ressalva` é exibido
  colado no resultado — limitação escondida vira decisão errada.
- **Situação explícita:** `Validado` (conferido contra dado real), `NaoValidado`, `SemMotor`,
  `ForaDoBanco`. Indicador sem número não some da tela; aparece com o motivo.
- **Permissão:** módulo `Indicadores` (44). Consulta vê e apura; Edição altera o SQL — que é
  consulta read-only, mas ainda assim contra produção do hospital.
- **Não substitui o hub FHIR.** A decisão do usuário foi apurar tudo direto do Oracle. Quando
  o hub carregar internação e questionários (ver relatório de projeção FHIR), parte dos motores
  pode migrar para lá trocando só o SQL e a fonte — o cadastro não muda.

## Alternativas descartadas

- **Python sandboxado no cadastro** — runtime novo + superfície de execução remota, sem ganho
  proporcional (ver acima).
- **Motor em C# plugável** — tipado e testável, mas aprimorar um indicador passaria a exigir
  deploy, que é justamente o que se quer evitar num artefato que muda por cláusula contratual.
- **Calcular sobre o hub FHIR** — hoje o hub só tem urgência (nenhuma internação), o que
  deixaria a maior parte da planilha sem origem.
