# Anamnese do Holter (CDT) — altura, peso e fumante na worklist do CardioNet

Branch `ANAMNESE-CDT-HOLTER`, criada da `main` `14597d05`. **Não foi compilado nem testado**:
a máquina do Rafael não tem .NET nem Node. **Não tem migration**: o conteúdo vai no
`anamnese.conteudo_json`, que já existe.

## Problema

No CardioNet Client DICOM, ao importar o paciente da worklist WORK-CARDIO-CDT, o aparelho vai direto
para a tela de gravar o equipamento. Altura (0010,1020), peso (0010,1030) e fumante (0010,21A0) são
pedidos no C-FIND, mas o item volta com esses campos vazios, e a técnica precisa digitá-los no
aparelho.

## Decisão (Rafael, 21/10/2026)

- O Holter ganha uma **anamnese própria** (altura em cm, peso em kg e fumante: sim, não ou parou),
  aberta pelo mesmo botão da lista de Solicitações que a mamografia já usa.
- **O exame só vai à worklist depois que a técnica salvar a anamnese.** Hoje ele vai na
  confirmação de presença.
- **Correção:** a técnica reabre, corrige e salva de novo, e o sistema **sobrescreve o paciente na
  worklist**.

## O que muda

| Onde | O quê |
|---|---|
| `Core/Anamneses/AnamneseHolter.cs` (novo) | Questionário pela modalidade (ECG = Holter, o resto = mamografia), validação do conteúdo e tradução para a worklist. "Parou" vira `NO`, porque o Smoking Status só tem YES/NO/UNKNOWN e o aparelho pergunta se o paciente fuma hoje. |
| `Core/Worklist/DadosClinicosWorklist.cs` (novo) | Altura em metros, peso em kg e smoking status. |
| `ConstrutorMwlItem.Item(..., clinicos)` | Põe (0010,1020) DS, (0010,1030) DS e (0010,21A0) CS no item. Sem anamnese, o item sai igual ao de antes. |
| `IDcm4cheeMwlClient` | Ganha uma sobrecarga `CriarOuAtualizarMwlItemAsync(s, clinicos, ct)`. A antiga delega para ela com `null`, então os mocks dos testes não mudam. |
| `TagDicomWorklist.Reservadas` | As três tags entram na lista, para que nenhum "campo opcional" do equipamento as sobrescreva. |
| `SolicitacoesService` | `AguardaAnamneseHolterAsync` é a trava, e vale quando o exame é ECG, o aparelho tem `UsaAnamnese` ligado e o pedido não tem anamnese `holter`. Ela é aplicada em quatro pontos: **autorização** (não enfileira e carimba "Aguardando a anamnese…" em `ErroIntegracaoPacs`, sem abrir erro em Sistema → Erros), **reenvio** (recusa), **worker** (última barreira para qualquer outro caminho que enfileire) e **troca de equipamento** (leva os dados clínicos). |
| `SolicitacoesService.AnamneseSalvaAsync` (novo) | Roda depois de salvar a anamnese do Holter. Antes da recepção, não faz nada (a autorização já encontra a anamnese). Em `Solicitada`, enfileira. Em `Enviada`/`Recebida`, volta para `Solicitada` e o worker refaz o `POST /mwlitems`, que é upsert por StudyInstanceUID + SPS ID, então o item é **sobrescrito** e não duplicado. Depois disso, apenas avisa que a correção não chega mais ao aparelho. |
| `AnamnesesService` | O contexto passa a dizer qual `questionario` abrir. O `Salvar` recusa questionário trocado, valida o conteúdo do Holter e chama `AnamneseSalvaAsync`. O DTO devolve `avisoWorklist`. |
| Front `AnamnesePage` | Escolhe o questionário pelo `questionario` do contexto. `components/AnamneseHolter.tsx` (novo) tem os três campos, o aviso de que os dados vão ao aparelho e os botões "Salvar e enviar à worklist" e "Salvar correção". |
| Testes | `Worklist/AnamneseHolterWorklistTests.cs`: tags e formato DS, "parou" vira NO, validação, questionário por modalidade e tags reservadas. |

## Roteiro para quem for aplicar

1. `dotnet build` e `dotnet test`. A única mudança de assinatura é a sobrecarga do
   `IDcm4cheeMwlClient`.
2. `npm run build` / `tsc` no `SMSMais.front`.
3. **Configurar pela tela:** o equipamento **CARDIO-CDT** precisa de **"Usa anamnese" ligado**
   (ADR-0075). Com ele desligado, a trava não age, de propósito: senão o exame ficaria preso para
   sempre.
4. **Testar no dcm4chee** com `findscu` no AE WORKLIST, pedindo 0010,1020, 0010,1030 e 0010,21A0.
   Confirmar que o arquivo guarda as três tags no item ou no paciente. Smoking Status pode estar
   no filtro de atributos de **paciente** do dcm4chee, e aí ele só aparece se o paciente for
   atualizado. Se não vier, incluir as três tags também em `ConstrutorMwlItem.Paciente`.
5. Testar no CardioNet: importar da worklist, ver os campos preenchidos, corrigir a anamnese e
   reimportar.
6. Manual: incluir a anamnese do Holter em `manual/conteudo/solicitacoes.tsx` e
   `equipamentos.tsx`.

## Pontos para o Bernardo decidir

- **O questionário sai da modalidade (ECG).** Hoje só o Holter é ECG com aparelho, já que o
  eletrocardiograma ficou "Não configurada" em 20/10. Se aparecer outro ECG, a escolha passa para o
  cadastro do equipamento (algo como `QuestionarioAnamnese`).
- **"Aguardando anamnese" usa `ErroIntegracaoPacs`.** Funciona, mas a tela pode pintar de erro.
  Se incomodar, a alternativa é um selo próprio na lista.
- **Holter já autorizado antes do deploy** e ainda em `Solicitada`/`Enviada`: o worker vai
  pará-lo esperando a anamnese. Os que já estão `Recebida` continuam na worklist sem os dados.
- **"Parou" → `NO`.** Se o CardioNet tiver outro campo para ex-fumante, rever.

## Aplicação (22/10/2026, branch `feat/anamnese-holter`)

O commit do Rafael entrou sem conflito em cima da `main` `7385763` (nenhum arquivo dele tinha mudado
desde a base `14597d05`). Na aplicação:

- **Build quebrava num teste:** `ExamesPorCodigoInternoTests` monta o `AnamnesesService` à mão e
  não passava o `ISolicitacoesService` novo. Passa um substituto (o teste é de RX, não chama o
  Holter).
- **Pedido com mais de um procedimento:** o contexto decidia o questionário pelo exame clicado e o
  salvar, pelo exame principal (onde a anamnese mora). Num pedido de MAPA (principal) + Holter
  (extra), a tela abria o do Holter e o salvar o recusava com "questionário errado". Os dois agora
  decidem pelo **pedido**: é Holter quando algum exame dele é ECG (`PedidoTemHolterAsync`).
- **Aviso de exame já fora da worklist** dizia o enum cru (`EmExecucao`); agora diz "em execução",
  "realizado", "laudado" ou "cancelado".
- **Testes com banco** (`Worklist/AnamneseHolterFluxoTests.cs`, 8): a autorização segura o Holter
  sem anamnese; salvar a anamnese de um Holter autorizado o enfileira; o worker leva altura, peso e
  fumante ao item; o worker não envia Holter sem anamnese; corrigir um `Recebida` volta a
  `Solicitada`; aparelho ECG com "Usa anamnese" desligado não segura; pedido MAPA + Holter abre e
  salva o questionário do Holter; fora do Holter o questionário do Holter é recusado.
- **Manual:** seção nova "Anamnese do Holter" no artigo Anamnese (com o "?" da tela apontando para
  ela) e correção de Solicitações e Equipamentos, que diziam que o Holter não usa anamnese.
- Suíte completa verde; `npm run build` verde.

**Continua pendente (roteiro, passos 3 a 5):** ligar "Usa anamnese" no CARDIO-CDT, conferir com
`findscu` no dcm4chee se as três tags voltam no C-FIND (Smoking Status pode cair no filtro de
paciente) e testar a importação no CardioNet. Com "Usa anamnese" desligado, o deploy não muda nada
no Holter.
