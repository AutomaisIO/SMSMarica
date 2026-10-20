# Holter e MAPA com exame (accession + Study), sem deixar de ser Gráfico/funcional

Preparado em 20/10/2026 pelo Rafael com o Claude, para auditoria do Bernardo. **Nada foi commitado
no código, nem executado em produção.** As mudanças estão em
[`holter-mapa-satelite.patch`](./holter-mapa-satelite.patch), gerado contra a `main` em `a165763b`.

> ⚠ **Não compilado e não testado.** A máquina onde isto foi escrito não tem .NET SDK nem Node.

## Para o Claude de quem for aplicar

1. Ler este README inteiro e o adendo do ADR-0021, que está dentro do patch.
2. Aplicar a partir da raiz do repositório: `git apply colaborativo-bernardo-rafael-auditar/2026-10-20-holter-mapa-satelite/holter-mapa-satelite.patch`.
   Se a `main` já tiver andado e der conflito, usar `git apply --3way`.
3. Compilar e testar:
   - `SMSMais.server`: `dotnet build` (0 warnings) e `dotnet test`, que usa Docker/Testcontainers.
     Testes novos: `CategoriaSigtapCriaSateliteTests` e `SateliteForaDaImagemTests`.
   - `SMSMais.front`: `npm run build` e `npm run lint`.
4. **Confrontar o Manual** (skill `confrontar-manual`): a tela de Solicitações muda para Holter/MAPA
   (card de exame, autorização, equipamento, laudo). Isso **não foi feito** e entra no mesmo commit.
5. Revisar, ajustar o que for preciso e commitar pelo fluxo normal. Depois do deploy, seguir o
   roteiro de produção abaixo.
6. Atualizar a tabela de itens em `../README.md` (ou remover esta pasta).

## Auditoria (Bernardo, 20/10/2026)

Aplicado na branch `feat/holter-mapa-satelite`. `dotnet build` 0 erros/0 avisos, `npm run build`
limpo, testes na bancada (incluindo os dois novos). Acrescentado na aplicação: o artigo **Exames e
consultas** do Manual, o alerta da prévia de importação (não diz mais "sem exame" para Holter/MAPA)
e as decisões abaixo no adendo do ADR-0021.

**Decisões do Bernardo:**

1. **ELETROCARDIOGRAMA fica fora.** A máquina imprime direto e entrega ao paciente; não gera nada.
   O tipo volta para modalidade "Não configurada" **antes** do deploy.
2. **Os 631 retroativos: desconsiderar por ora.** Só viram `Realizada` quando o PDF for associado.
   O passo 5 do roteiro (`incluirAnteriores=true`) **não roda** agora.
3. **A regra fica a do Rafael (modalidade do tipo), não a da PR #47 (escopo da unidade com
   worklist ligada).** Medido em produção em 20/10: Holter e MAPA só são executados no CDT, então as
   duas dão o mesmo resultado; o ELETROCARDIOGRAMA já estava com modalidade Indefinida, e pela
   regra da #47 ele ganharia exame no CDT (o escopo dele lá está com worklist ligada). A #47 é
   fechada; a documentação dela (MAPA em aparelho próprio, `WORK-MAPAS-CDT`) vem para esta branch.

**Conferido em produção (20/10, só leitura):** ELETROCARDIOGRAMA `Indefinida`; HOLTER 24 HORAS
`ECG`; M.A.P.A `OT`; no CDT, HOLTER → (deduz CARDIO-CDT) e M.A.P.A → `MAPAS-CDT`, ambos com
worklist ligada. Pedidos não cancelados: Holter Gráfico/funcional 392 passados + 309 de hoje em
diante; MAPA 240 + 146; com exame (Imagem) Holter 189 e MAPA 81.

## O problema

- Holter (`1703204`, tipo ECG → equipamento CARDIO-CDT / `WORK-CARDIO-CDT`) e MAPA (`9290002`, tipo
  OT → equipamento MAPAS-CDT / `WORK-MAPA-CDT`) são SIGTAP `0211`. Por isso a importação os grava
  como **Gráfico/funcional, sem satélite**. Sem accession nem Study, eles não vão à worklist, e o
  laudo em PDF que volta pelo PACS não tem onde casar.
- Os que têm exame só o têm porque a carga de 08/09 os gravou como **Imagem**, e o reparo de 05/10
  (`bc5d594d`) criou o satélite. O MAPA de teste de 16/10 (pedido 2610053375) é um deles.
- Contagem pela tela em 20/10/2026: Holter 918 = 194 Imagem + 724 Gráfico/funcional; MAPA 476 = 81
  Imagem + 395 Gráfico/funcional. Dos 1.119 sem exame (tirando os cancelados): **631 retroativos**
  e **454 de hoje em diante**.

## A decisão (Rafael)

Holter e MAPA **continuam Gráfico/funcional**, que é o que eles são, mas **com exame**. A regra
"satélite só para a categoria Imagem" vira **"satélite para Imagem, ou quando o tipo de exame tem
modalidade DICOM"**. Os 275 que hoje estão como Imagem **voltam para Gráfico/funcional, mantendo o
Study**. Detalhe no adendo do ADR-0021.

## O que o patch muda

| Onde | Mudança |
|---|---|
| `CategoriaSigtap.CriaSatelite(categoria, modalidadeDoTipo)` | Regra única. Substitui o `CriaSateliteImagem`, que não tinha chamador. |
| `ResolvedorTipoExameSisreg.ResolverParaExecucaoAsync` | Imagem: resolve ou cria o tipo, como antes. Fora dela: só um tipo **existente** com modalidade, **nunca cria**. Senão cada audiometria ou EEG importado viraria um tipo novo. |
| `ImportacaoSisregService` | Cria o satélite quando `CriaSatelite`. A releitura diária (`GarantirAsync` no pedido existente) passa a completar o Holter/MAPA antigo que o SISREG reler. |
| `SateliteImagemSisreg` | `GarantirAsync` sem barreira de categoria. O principal e os extras (ADR-0074) usam o resolvedor novo. `RepararAsync(…, incluirAnterioresForaDaImagem, …)`: o retroativo vale **só fora da imagem**. Na imagem continua o corte de hoje em diante, e as ~512 mil antigas da carga de 08/09 ficam intactas. |
| `SisregImportacaoController` | `reparar-exames-sem-satelite` ganha `?incluirAnteriores=true`. |
| `DownloadTokenService`, `DeclaracaoComparecimentoService` | Passam a checar "tem accession", não "é Imagem". |
| `EstatisticasService` | Tira o filtro de categoria dos exames: Holter e MAPA passam a contar (o filtro de modalidade separa). |
| Front (`types.ts` → `temExame()`; lista, detalhe, modal, situação) | Os gates `categoria === 'Imagem'` que queriam dizer "tem exame" usam `temExame()`. O selo de categoria no cabeçalho continua pela categoria, então o Holter aparece como Gráfico/funcional. |
| Testes | `CategoriaSigtapCriaSateliteTests` (unidade) e `SateliteForaDaImagemTests` (Postgres): Holter ganha exame com o tipo existente; audiometria e EEG não ganham nem criam tipo; o reparo retroativo pega o MAPA passado e deixa a imagem passada. |
| `docs/adr/0021` | Adendo de 20/10/2026. |

**Não mexido:** o alerta da prévia de importação ("Categoria … sem exame de imagem/PACS",
`ImportacaoSisregService` ~l.221-230) continua aparecendo para Holter e MAPA. É só texto.

## ⚠ Antes do backfill

1. **ELETROCARDIOGRAMA não tem equipamento nem worklist** (Rafael). Mas o tipo dele está com
   modalidade **ECG**, e por isso a unidade CDT o mostra como "Único da modalidade → CARDIO-CDT ·
   Envia". Com a regra nova, todo ECG ganharia exame e, ao ser autorizado, iria para a worklist do
   Holter. **O tipo ELETROCARDIOGRAMA tem de ficar com modalidade "Não configurada" antes de
   publicar** (Tipos de Exame → ELETROCARDIOGRAMA → Modalidade DICOM).
2. **Status dos retroativos.** O satélite nasce `Solicitada`, sem fila de envio. Para os 631
   passados (que a tela mostra como "Falta"), falta decidir se ficam assim ou viram `Realizada`
   para receber o PDF do acervo. Pendente com o Rafael.

## Roteiro em produção (depois do deploy)

Banco: `smsmais` no PSQL-01, schema `smsmarica`. Os valores do enum são `categoria` (2 = Imagem,
4 = Gráfico/funcional) e `status` (4 = Cancelada).

**1. Diagnóstico (só leitura):**

```sql
SELECT s.procedimento_texto, s.categoria,
       (s.data_agendada < now()) AS passado,
       EXISTS (SELECT 1 FROM smsmarica.exame_imagem e WHERE e.solicitacao_id = s.id) AS tem_exame,
       count(*)
FROM smsmarica.solicitacao s
WHERE s.procedimento_texto IN ('HOLTER 24 HORAS',
                               'MONITORIZACAO AMBULATORIAL DE PRESSAO ARTERIAL (M.A.P.A)')
  AND s.excluido_em IS NULL AND s.status <> 4
GROUP BY 1,2,3,4 ORDER BY 1,2,3,4;
```

**2. Os 275 de Imagem voltam para Gráfico/funcional** (o exame e o Study ficam):

> ⚠ **Corrigido na auditoria (20/10).** Sem o `EXISTS`, o UPDATE pegava **~9.800** linhas: além
> dos 275 com exame, as 4.015 Holter e 5.499 MAPA antigas que a carga de 08/09 gravou como Imagem
> **sem** exame (medido em produção em 20/10). Essas ficam como estão: hoje a tela as esconde
> (imagem sem exame não aparece) e, viradas Gráfico/funcional, apareceriam de uma vez na lista e
> entrariam no retroativo.

```sql
BEGIN;
UPDATE smsmarica.solicitacao s
   SET categoria = 4, atualizado_em = now()
 WHERE s.categoria = 2
   AND s.procedimento_texto IN ('HOLTER 24 HORAS',
                                'MONITORIZACAO AMBULATORIAL DE PRESSAO ARTERIAL (M.A.P.A)')
   AND s.excluido_em IS NULL
   AND EXISTS (SELECT 1 FROM smsmarica.exame_imagem e WHERE e.solicitacao_id = s.id);
-- conferir a contagem (~275: 270 não cancelados em 20/10 + cancelados) antes de COMMIT;
-- se não bater, ROLLBACK
COMMIT;
```

**3. Conferir a configuração:** HOLTER 24 HORAS com modalidade ECG; M.A.P.A com OT;
ELETROCARDIOGRAMA "Não configurada" (item 1 acima). Na unidade CDT: M.A.P.A → MAPAS-CDT (fixo) e
HOLTER → CARDIO-CDT (conferido pela tela em 20/10).

**4. Futuros** (hoje em diante): `POST /sisreg/importacao/reparar-exames-sem-satelite?limite=500`,
repetindo com `&cursor=<cursor devolvido>` até `restantes = 0`.

**5. Retroativos:** o mesmo endpoint com `&incluirAnteriores=true`, em lotes, até `restantes = 0`.

**6. Verificar:** repetir o SQL do passo 1. Esperado: Holter e MAPA todos com `categoria = 4` e
`tem_exame = true` (fora os cancelados).

Nada disso dispara mensagem a paciente: o reparo não enfileira comunicação e não manda nada à
worklist (`ProximaTentativaEm` nulo; só a recepção, ao autorizar, envia).
