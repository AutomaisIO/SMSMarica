# Faturamento SUS / BPA — Módulo TFD (FT10)

> Como o transporte do TFD vira **receita** para o município: cada paciente transportado é
> contabilizado em **unidades de faturamento** e exportado em **BPA (Boletim de Produção
> Ambulatorial)** para o SUS. Ver [`escopo.md`](./escopo.md) e [ADR-0017](../../adr/0017-otimizacao-rota-tfd.md).

## Regra de negócio

- A cada **50 km** rodados com o paciente a bordo = **1 unidade**, com **valor** e **código
  SIGTAP configuráveis** (tela de configuração do faturamento).
- O cálculo é **proporcional** (não arredonda para blocos inteiros): **`unidades = km ÷ 50`**;
  **`valor = unidades × valor_por_unidade`**.
  - Ex.: **170 km → 3,4 unidades** (3 blocos de 50 km + 20 km = 20/50 = 0,4) → `3,4 × valor`.
- Conta **apenas trajetos com paciente dentro do carro** (ida e volta). Cada paciente é
  contabilizado individualmente (mesmo viajando no mesmo carro).

## Modelo de dados (a implementar)

- **`RegistroFaturamento`** (um por paciente por sessão realizada): `SessaoId`, `PacienteId`
  (hub FHIR), competência `AnoMes`, `KmComPaciente`, `Unidades`, `ProcedimentoSigtapId`,
  `Status` (`Pendente`/`EmBpa`/`Faturado`/`Cancelado`).
- **`ArquivoBpa`** (um por competência/geração): competência, conteúdo do arquivo, contagem,
  status, `GeradoEm`.
- Reusa a entidade existente **`ProcedimentoSigtap`** para o código SIGTAP de transporte.

## Fluxo

1. **Conclusão da sessão** (`SessaoDeTratamento.RealizadaEm`) dispara
   `IFaturamentoService.ContabilizarAsync(sessao)`.
2. Calcula **km a bordo** (do `PlanoRotaJson`/trechos da `Alocacao`; opção de reconciliar com
   o GPS real) → **unidades = km ÷ 50** → cria/atualiza o `RegistroFaturamento` (`Pendente`).
3. **Geração do BPA por competência:** `IGeradorBpaService.GerarAsync(competencia)` agrega as
   unidades por procedimento e produz o **arquivo no layout DATASUS** (largura fixa); cria o
   `ArquivoBpa` e marca os registros como `EmBpa`.
4. Endpoints: `GET /faturamento/registros?competencia=YYYY-MM`, `POST /faturamento/bpa/gerar`
   (download), `GET /faturamento/bpa`. RBAC `Faturamento`.

## Relatórios (visões)

`GET /faturamento/resumo?dimensao=&competencia=AAAAMM&de=&ate=` agrega os registros por
**dimensão** num **período** (competência ou intervalo `de`/`ate`), retornando, por item,
**qtd de registros, total de km, total de unidades e total em R$**. Dimensões:

- **Paciente** · **Motorista** · **Veículo** · **Tipo de tratamento** · **Unidade (destino)**.

`GET /faturamento/registros?competencia=&de=&ate=` lista os registros individuais.
`POST /faturamento/contabilizar/{sessaoId}` (re)contabiliza uma sessão.
`GET/PUT /faturamento/config` define **valor por unidade** e **código SIGTAP**.

> As dimensões (motorista, veículo, tipo de tratamento, unidade) ficam em *snapshot* no
> registro no momento da contabilização — os relatórios são fiéis ao que aconteceu, mesmo
> que cadastros mudem depois.

## Faseamento

- **Fase 1:** capturar **registros/unidades** já com a operação (o dado nasce com cada
  sessão concluída).
- **Geração do arquivo BPA:** depende de confirmar o **código SIGTAP de transporte**, o
  **layout (BPA-C vs BPA-I)** e o **CNES** do estabelecimento — fecha no fim da Fase 1 ou
  início da Fase 2.

## Decisões em aberto (confirmar com a SMS / regulação)

1. ~~Arredondamento das unidades~~ — **DEFINIDO: proporcional** (`km ÷ 50`, 2 casas).
2. **Base da distância:** km **a bordo por trecho** (rota compartilhada) vs **origem→destino**
   ponto-a-ponto; distância **planejada** (Google) vs **real do GPS**.
3. **Código SIGTAP** de transporte e **valor unitário**.
4. **Layout BPA:** consolidado (BPA-C) ou individualizado (BPA-I); dados do estabelecimento
   (CNES) e do profissional, quando exigidos.
5. **Ida + volta** conta dobrado? (premissa atual: sim — toda quilometragem com paciente a
   bordo).
