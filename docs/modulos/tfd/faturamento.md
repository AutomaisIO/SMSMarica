# Faturamento SUS / BPA — Módulo TFD (FT10)

> Como o transporte do TFD vira **receita** para o município: cada paciente transportado é
> contabilizado em **unidades de faturamento** e exportado em **BPA (Boletim de Produção
> Ambulatorial)** para o SUS. Ver [`escopo.md`](./escopo.md) e [ADR-0017](../../adr/0017-otimizacao-rota-tfd.md).

## Regra de negócio

- **1 unidade de faturamento a cada 50 km rodados com o paciente a bordo.**
- Conta **apenas trajetos com paciente dentro do carro** (ida e volta).
- Cada paciente é contabilizado individualmente (mesmo viajando no mesmo carro).

> Exemplo: paciente levado de Maricá a Niterói (≈ 60 km) e trazido de volta (≈ 60 km) =
> 120 km a bordo → **2–3 unidades** conforme a regra de arredondamento adotada (ver
> "Decisões em aberto").

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

## Faseamento

- **Fase 1:** capturar **registros/unidades** já com a operação (o dado nasce com cada
  sessão concluída).
- **Geração do arquivo BPA:** depende de confirmar o **código SIGTAP de transporte**, o
  **layout (BPA-C vs BPA-I)** e o **CNES** do estabelecimento — fecha no fim da Fase 1 ou
  início da Fase 2.

## Decisões em aberto (confirmar com a SMS / regulação)

1. **Arredondamento** das unidades: cada 50 km completos = 1 (`floor`) vs `ceil` vs
   proporcional.
2. **Base da distância:** km **a bordo por trecho** (rota compartilhada) vs **origem→destino**
   ponto-a-ponto; distância **planejada** (Google) vs **real do GPS**.
3. **Código SIGTAP** de transporte e **valor unitário**.
4. **Layout BPA:** consolidado (BPA-C) ou individualizado (BPA-I); dados do estabelecimento
   (CNES) e do profissional, quando exigidos.
5. **Ida + volta** conta dobrado? (premissa atual: sim — toda quilometragem com paciente a
   bordo).
