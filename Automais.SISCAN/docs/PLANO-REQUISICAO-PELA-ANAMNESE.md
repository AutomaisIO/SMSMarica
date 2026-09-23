# Gerar a requisição do SISCAN a partir da nossa anamnese

Objetivo (Bernardo, 22/09/2026): de dentro da nossa **Anamnese**, preencher a etapa de requisição
do SISCAN, **gerar o número** e **carimbá-lo na anamnese**, para a médica seguir com o laudo.

O protocolo da tela está medido em [`FLUXO-NOVA-REQUISICAO.md`](./FLUXO-NOVA-REQUISICAO.md).
Este arquivo é o plano — o que foi decidido, o que falta e em que ordem.

## 1. Decisões tomadas (22/09/2026)

| Tema | Decisão |
|------|---------|
| Em nome de quem nasce | **Da USF que pediu no SISREG** — Unidade Requisitante = a unidade da ficha; Responsável = o profissional dela |
| Gatilho | **Botão "Gerar Requisição SISCAN"** na anamnese. Ao **sair** da anamnese, perguntar se quer gerar. Ao **salvar**, avisar que ainda não gerou |
| Credencial | **Login e senha do SISCAN do próprio usuário**, pedidos num modal com a cara do card de login do SISCAN. **Não gravamos** — a sessão do SISCAN vive presa ao ciclo de autenticação do SMSMais e morre com ele |
| Risco elevado: "Moderado" | vira **Sim** no SISCAN (conservador: qualquer coisa acima de Baixo entra como risco elevado) |
| Tipo de mamografia | pela idade: **>= 36 rastreamento, < 36 diagnóstica** |

A decisão de credencial **não é nova**: é exatamente o modelo do `SerSessaoOperadorStore`
(ADR-0059, fase 2; decidido em 18/08/2026 para o SER). Motivo escrito lá e que vale igual aqui:
o sistema federal **carimba quem fez**, e escrever com uma credencial de sincronismo faria toda
ação do município sair no nome da mesma pessoa — a trilha de auditoria passaria a mentir sobre a
autoria. Clonar o padrão, não inventar outro.

## 2. ~~O bloqueio~~ RESOLVIDO em 22/09/2026 — o Salvar foi medido

Uma criação real, autorizada por ação em 22/09/2026, fechou a pergunta. Detalhe completo em
[`FLUXO-NOVA-REQUISICAO.md`](./FLUXO-NOVA-REQUISICAO.md) §8. O que o desenho precisa saber:

- o modal devolve **só o protocolo**, e **com zeros à esquerda em 14 posições**
  (`00000141043026`) — a grade mostra o mesmo número sem eles (`141043026`);
- o **Nº do Exame** (`141108550`) **não vem no modal**: só relendo a grade. Como a tela deles
  pesquisa pelos dois, **a releitura entra no fluxo**, não é opcional;
- a releitura pelo **Nº do Prontuário = nosso AccessionNumber** achou a requisição de primeira:
  a ponte de idempotência funciona;
- a requisição criada por nós **abre editável** — evidência para "quem cria, altera";
- **data retroativa é aceita** (gravamos a data da ficha, de três meses antes).

Passo 1 da ordem de execução, portanto, está **cumprido**.

## 3. São DOIS números, e um terceiro campo que serve de ponte

Medido na grade do Gerenciar Exame (22/09/2026):

| O quê | Exemplo | Onde aparece |
|-------|---------|--------------|
| **Protocolo** | `140739031` | coluna *Protocolo* da grade; pesquisável em `Nº Protocolo` |
| **Nº do Exame** | `140804778` | embutido no id das ações (`frm:listaExamePaginada:<nº>:j_idNNN`); pesquisável em `Nº Exame` |
| **Nº do Prontuário** | livre | campo da requisição **e** filtro da pesquisa |

São números **diferentes** para a mesma linha. Carimbar os dois.

E o Nº do Prontuário resolve a **idempotência**: gravando ali o nosso `AccessionNumber`, o
vínculo fica nos dois sentidos — nós guardamos o protocolo deles, eles guardam a nossa chave,
pesquisável na tela deles. Antes de criar, procurar por esse prontuário: se já existe, não cria
de novo. Sem isso, um POST que caia depois de gravar gera requisição duplicada para a mesma
paciente, e o SISCAN não tem nada nosso para barrar.

## 4. O casamento do Responsável não tem chave — precisa de gente

O SISCAN identifica o profissional por **CNS** (`NOME - CNS` no combo). Nós não temos esse CNS:
`fhir.practitioner` guarda CPF, conselho e nome; `smsmarica.solicitacao` guarda nome do
solicitante, CPF às vezes, conselho nunca. Medido em 22/09/2026 sobre as mamografias desde
01/07/2026:

| | |
|---|---|
| solicitações | 2.294 |
| com nome do solicitante | 100% |
| com CPF | 33% |
| com nº de conselho | **0%** |
| solicitantes distintos | 344, em 33 unidades |

E o nome não bate na forma: a ficha traz `FERNANDA SOUZA`, o SISCAN traz `FERNANDA SOUZA LEITE`.

**Desenho:** a máquina **sugere** pelo melhor casamento de nome dentro da unidade (o combo tem
8–15 nomes), a **pessoa confirma** no modal, e o par (solicitante da ficha ↔ CNS do SISCAN) fica
**aprendido** numa tabela de pareamento — não se pergunta de novo para o mesmo profissional. É o
mesmo padrão do ADR-0055 (pareamento entre sistemas sugerido pela máquina, confirmado por pessoa).

Caso de borda que o código tem de nomear em vez de esconder: **o solicitante da ficha não existe
no SISCAN daquela unidade**. Aí não há o que adivinhar — a tela diz isso e pede uma escolha.

## 5. O que falta na anamnese (v2)

Três campos e uma conta, já levantados em `FLUXO-NOVA-REQUISICAO.md` §6d:

1. "Antes desta consulta, teve as mamas examinadas por um profissional de saúde?" (Sim / Nunca / Não sabe)
2. "Fez radioterapia na mama ou no plastrão?" (Sim / Não / Não sabe → lado → **ano por lado**)
3. **Ano** da última mamografia e da cirurgia (hoje é texto livre na observação)

O resto já sai do que colhemos: nódulo por mama, risco elevado (com "Moderado" → Sim),
população-alvo do rastreamento (derivada dos critérios de risco), cirurgia Sim/Não e prótese.

## 6. Ordem de execução

1. **Spike do Salvar** — uma criação real, com OK por ação, para saber o que o modal devolve e se
   o prontuário é exigido. Bloqueia todo o resto.
2. ✅ **Anamnese v2** — FEITO em 22/09/2026 (front apenas; o backend guarda JSON opaco).
   Seção 7 "REQUISIÇÃO DO SISCAN" na tela, mesmo bloco na leitura do médico, `versao: 2`.
   **Pendente:** a tela de Anamnese não tem artigo no Manual — dívida a propor/registrar.
3. ✅ **`Integracoes/SiscanWeb/`** — FEITO em 22/09/2026. `SiscanHtml` (campos como o navegador
   monta, `AplicarA4J`, item de menu por rótulo, parâmetros A4J lidos do JS da página) e
   `SiscanWebSessao` (login SHA-256, navegação por menu, trava de leitura, **`SubmeterEscritaAsync`
   separado** com `operacao` obrigatória no log). 7 testes de unidade sobre HTML fixo.
4. ✅ **`SiscanSessaoOperadorStore`** — FEITO. Chaveado pelo `jti`, validade de 8 h por
   inatividade, valida a credencial contra o SISCAN na hora, `DELETE /siscan/sessao` no logout do
   painel (ligado em `authStore.sair()`). Endpoints em `SiscanSessaoOperadorController`
   (`GET/POST/DELETE /siscan/sessao`), permissão `SolicitacoesExame`.
5. ✅ **Colunas** em `exame_imagem` — FEITO. Migration `SiscanRequisicaoNoExameImagem`
   (`siscan_protocolo`, `siscan_numero_exame`, `siscan_requisicao_em`, `siscan_requisicao_por`,
   `siscan_erro` + índice filtrado no protocolo). O contexto da anamnese passou a devolver os
   dois números.
6. ✅ **Front** — FEITO. Botão *Gerar Requisição SISCAN*, modal de login do SISCAN, modal de
   confirmação com o que será enviado + combo de responsável, carimbo no cabeçalho, e o aviso ao
   salvar e ao sair quando ainda não há requisição. **Artigo do Manual escrito**
   (`manual/conteudo/anamnese.tsx`) e o `?` posto no título da tela.
7. **Pareamento de profissional** — PENDENTE. Hoje a sugestão por nome é recalculada a cada
   geração e não fica aprendida; quando o mesmo profissional for confirmado muitas vezes, vale a
   tabela.

### O serviço, como ficou

`SiscanRequisicaoService` tem dois caminhos. `Preparar` percorre o assistente **sem gravar** e
devolve o que será enviado, quem pode assinar e o que falta — é o que a tela mostra antes do
"confirma?". `Gerar` é o único que escreve.

Três decisões que valem registro:

- **Nenhum `j_idNN` fixo.** Cada pergunta é resolvida pela **legenda do fieldset** (o texto que a
  paciente lê, que é estável) e traduzida na hora do envio. Se uma legenda não for encontrada, a
  geração **falha dizendo que a tela mudou** — postar num id chutado gravaria a resposta na
  pergunta errada, e ninguém veria.
- **As condicionais são abertas antes.** Ano da última mamografia, lado e ano da radioterapia e os
  26 anos de cirurgia só existem depois do A4J do "Sim". Mandar o ano sem abrir a região é mandar
  campo que o servidor não conhece — ele ignora, sem erro.
- **A releitura está dentro do fluxo**, duas vezes: antes de criar (idempotência pelo prontuário)
  e depois de criar (para pegar o Nº do Exame, que o modal não devolve).

**Onde o Responsável é escolhido, e por quê ali.** Medido em 22/09/2026: o combo do SISCAN só
existe **depois** de percorrer o assistente inteiro para aquela paciente (CNS → tipo → unidade →
Avançar → tipo de mamografia). Não há como listar os profissionais de uma unidade sem começar uma
requisição. E não existe lista local que sirva: `sisreg_profissional_unidade` tem 686 nomes em 42
unidades, mas é quem tem **agenda** no SISREG — a profissional que assinou a ficha do nosso caso
não está lá (a INOÁ II tem 15 no espelho, nenhuma delas). Logo, o seletor de Responsável **vive no
modal de "Gerar Requisição"**, com a lista viva, e o escolhido é carimbado de volta em
`siscan.responsavel` (nome + CNS). A anamnese mostra o que foi carimbado.

## 7. Riscos a medir antes de prometer

- ~~**Sessão única.**~~ **MEDIDO em 22/09/2026: o SISCAN NÃO tem sessão única.** Duas sessões
  com a mesma credencial coexistiram, e a primeira continuou lendo depois que a segunda entrou
  (`probe_sessao_unica.py`). Entrar pelo painel **não derruba** quem está no navegador — ao
  contrário do SISREG e do SER. Mais uma vez a analogia entre os três sistemas estava errada, como
  já tinha acontecido no SER.
- **A conta do operador enxerga todas as unidades?** A conta do laboratório (prestador CDT) vê as
  37 do município. Contas de USF podem ver menos — o código precisa falhar dizendo *"a unidade da
  ficha não está na lista deste operador"*, e não em silêncio.
- **Duplicata.** Ver §3: o Nº do Prontuário é a âncora.
