# ADR-0056 — O que a unidade executa de imagem é cadastro por unidade; a flag de worklist e o aparelho de destino vão junto

**Status:** aceito · **Data:** 2026-09-08
**Relacionado:** [ADR-0013](./0013-agenda-multi-recurso.md) (equipamento é entidade `smsmarica`) ·
[ADR-0021](./0021-ecossistema-solicitacao-e-fulfillment.md) (espinha + satélite) ·
[ADR-0055](./0055-catalogo-canonico-e-embeddings.md) (catálogo canônico; oferta interna derivada) ·
[proposta](../pendencias/escopo-de-exame-por-unidade.md)

## Contexto

`TipoExame.EnviarParaWorklist` era um interruptor único do município. Quando o CDT ganhou um
raio-X em 09/2026, ficou claro que **não existia estado correto**: ligar o tipo servia o CDT e
fazia toda solicitação de Hospital Santo Antônio, Ernesto Che Guevara e DIMAGEM — que executam
radiografia e **não têm equipamento cadastrado** — falhar com `worklist.sem_equipamento`; deixar
desligado mantinha o aparelho novo sem worklist. O mesmo bit precisava valer `false` numa unidade
e `true` na outra.

O caso que expôs isso (`260908001`, 08/09): a recepção autorizou, o sistema detectou o impedimento,
carimbou o motivo e abriu o `ERRO-KF775R`; 15 minutos depois alguém clicou em *Reenviar worklist*,
o aviso sumiu da tela e o exame ficou parado com `tentativas_envio = 0`. Duas falhas — a regra
errada e um botão que apagava o diagnóstico sem resolver a causa.

### O que a medição mostrou (produção, 08/09/2026)

| | |
|---|---|
| Tipos de exame ativos | 204 (59 com a flag ligada) |
| Unidades cadastradas / que já executaram | 177 / **34** |
| **Pares (unidade × tipo) reais** | **298** — de 36.108 possíveis (**0,8%**) |
| Pares em unidades **com** equipamento | **62** (47 CDT, 15 CMI) |
| Equipamentos ativos | 6, em 2 unidades |
| Unidades com ambiguidade de destino | 1 (o CDT tem dois ultrassons) |

E o teste que decidiu entre cadastrar e derivar: o ADR-0055 §5 deriva a oferta interna de
`sisreg_escala`. Para imagem **não serve** — dos 298 pares, só **23 (7,7%)** aparecem na escala da
mesma unidade (no CDT, 11 de 47). Escala do SISREG é de profissional/consulta; exame de imagem
chega por demanda espontânea e solicitação manual.

## Decisão

### 1. `smsmarica.tipo_exame_unidade` é o nível que faltava

Par (tipo, unidade) com `enviar_para_worklist`, `equipamento_id` (nullable), `ativo` e auditoria
ADR-0006. Único por par, filtrado por `excluido_em IS NULL`.

Identidade e perfil técnico do procedimento (nome, código SISREG, SIGTAP, modalidade, descrições
DICOM) **continuam globais** — não mudam de unidade para unidade. O que muda é quem faz e em qual
máquina.

### 2. Alcance: destino DICOM, não oferta

Responde "quando um exame deste tipo for executado aqui, vai para qual máquina e entra na
worklist". **Não** é gate de solicitação nem de agendamento — quem oferece o quê ao cidadão é a
oferta derivada da escala (ADR-0055), e duas fontes de verdade sobre isso divergiriam, como já
aconteceu com o `unidades.csv` da Telefonia (ADR-0045).

### 3. Sem associação ⇒ não envia, com motivo

Fail-closed. O silêncio de antes vira mensagem que nomeia a unidade.

### 4. O `equipamento_id` tira a modalidade do caminho crítico

Ordem de resolução da estação: escolha da recepção → **destino configurado no escopo** → dedução
por unidade + modalidade → falha explícita.

Antes, o destino saía só do casamento `equipamento.modalidade == tipo.modalidade`. Foi esse fio
que, em 04/09/2026, fez cinco radiografias marcadas como `MG` apontarem para o mamógrafo do CDT —
165 exames a um clique da sala errada. Preenchido, o campo também mata a pergunta repetida à
recepção quando a resposta é fixa (os dois ultrassons do CDT).

### 5. A importação cria a associação; ligar continua manual

Procedimento novo entra no escopo da unidade que o importou **automaticamente**, `ativo` e
**desligado**, sem equipamento. Ligar e amarrar o aparelho são decisão de quem conhece a operação.

**Desligado não é pendência.** Corrigido em 08/09, no mesmo dia, depois de ver a tela com dados
reais: o CDT tem 48 exames no escopo e 40 desligados — ecocardiogramas, ecodopplers e afins que
**não devem** ir à worklist. A primeira versão marcava esses 40 como "a configurar" e trazia um
painel de pendências; quarenta alarmes falsos ensinam a ignorar a tela. Não há aviso de pendência,
não há coluna de situação e não existe painel de "exames a configurar". O que está desligado está
como tem de estar.

Pela mesma razão, **deixar o destino em branco é legítimo**: com mais de um aparelho na modalidade,
quem escolhe a sala é a recepção, na autorização — que é o comportamento correto e já existia.

### 6. Backfill fora da migration

A migration só cria a tabela. O backfill (298 pares, herdando a flag do tipo, o que torna o
cutover idêntico ao dia anterior) é rotina idempotente em `POST /escopo-exames/backfill`, porque
migration é imutável e roda igual em instância nova, onde não há histórico do qual derivar.

`TipoExame.EnviarParaWorklist` **fica** durante a transição, sem leitor, e sai numa migration
posterior.

## Consequências

- Uma pergunta ("esta unidade manda este exame?") passa a ter um ponto único —
  `IEscopoExameUnidade` —, usado pelos quatro lugares que antes liam a flag: autorização,
  impedimento, troca de equipamento e o filtro do worker. A expressão do worker é a **mesma** que a
  tela usa, para não haver duas definições de "está no escopo".
- A configuração ganha lugar próprio na **unidade** (aba "Exames de imagem"), que é como a operação
  pensa. O toggle "Integração PACS" sai do cadastro global — era decisão de unidade morando na tela
  do município.
- Uma tela só edita (a aba da unidade), seguindo o precedente do mapeamento SISREG, que foi
  aposentado como página standalone justamente para não ter dois lugares editando a mesma coisa.
- O select de aparelho oferece só os da unidade **e da modalidade do exame**. Lista vazia ali é
  sintoma útil: costuma ser modalidade errada no cadastro do tipo, que foi como cinco radiografias
  de tórax acabaram marcadas como `MG`.

## Alternativas consideradas

- **Derivar da escala do SISREG**, como o ADR-0055 faz para regulação. Rejeitada pela medição:
  cobre 7,7% dos pares de imagem.
- **Associação com N equipamentos.** Rejeitada: com dois aparelhos possíveis, deixar vazio já cai
  na dedução + escolha da recepção, que é o comportamento correto e já existe.
- **Unir `tipo_exame` (204) ao `regulacao_procedimento` (999) agora.** Adiada — são dois catálogos
  paralelos sem ponte, e juntá-los é projeto próprio.

## Verificação

Um mesmo tipo de exame ligado numa unidade e desligado noutra, ao mesmo tempo, com o worker
enviando só o da unidade certa; exame com destino configurado indo para o aparelho amarrado mesmo
quando a modalidade do tipo aponta para outro; unidade sem o exame no escopo recusando com motivo
que nomeia a unidade; e o backfill rodado duas vezes sem duplicar.
