# Backfill: achar as requisições que já foram lançadas à mão

**23/09/2026.** Medido contra o SISCAN real (V2.19.1-RC08) e contra o banco de produção.

## O problema

Até hoje, a requisição de mamografia era digitada **direto no SISCAN** pela unidade, enquanto a
anamnese era preenchida **aqui**. Os dois lados nunca se conheceram:

- a anamnese não sabe o número da requisição, então a médica não tem por onde seguir para o laudo;
- o botão novo de "Gerar Requisição SISCAN", sem saber, criaria uma **segunda** requisição para a
  mesma paciente e o mesmo exame.

São 873 anamneses sem protocolo, de 26/06 a 23/09/2026.

## A ponte: a grade mostra o Cartão SUS

A descoberta que torna tudo possível: a grade de **EXAME → GERENCIAR EXAME** traz o **Cartão SUS**
como coluna. Ou seja, dá para varrer o período inteiro e cruzar com as nossas anamneses **sem abrir
requisição nenhuma**.

Colunas da grade: `Paciente · Cartão SUS · Datas · Protocolo · Exame · Unidade Requisitante ·
Profissional Responsável - Resultado · Status`. O **Nº do Exame não é coluna** — ele mora dentro do
id das ações da linha (`frm:listaExamePaginada:139257485:j_id173`), e é ele que abre o
"Incluir Resultado do Exame".

## Três medidas que fazem a varredura ser barata

| Medida | Consequência |
|---|---|
| `frm:tamanhoPagina` aceita **300** (padrão 10) | 1.329 linhas em ~10 pesquisas |
| O rodapé traz o total real ("de 544 registro(s)") | quando estoura a página, **parte-se a janela de datas ao meio** em vez de paginar — o datascroller RichFaces exigiria engenharia reversa, e aritmética de datas não mente |
| `frm:botaoVoltar` devolve a grade **com os resultados** | abrir a requisição custa **0,2 s** e voltar **0,2 s**, contra 14–32 s de um clique de menu |

Sem a terceira, ler 551 requisições levaria horas. Com ela, minutos.

⚠️ Na **primeira** pesquisa depois de abrir a tela, o `frm:tamanhoPagina` ainda não pegou (o bean
guarda o 10). Não é problema quando se parte a janela, porque o total do rodapé denuncia o corte —
mas quem confiar na contagem de linhas da primeira página vai achar que existem 10 registros.

## O que a varredura achou

01/06 a 30/09/2026, só mamografia: **1.329 requisições** — 752 Liberado, 566 Requisitado,
11 Com Resultado.

## A regra de pareamento

```
par = mesma paciente (CNS, inclusive os secundários)
    + requisição com Data da Solicitação no MESMO DIA em que a anamnese foi preenchida
    + única dos dois lados (uma candidata; e o protocolo não disputado por outro exame nosso)
```

**Por que "mesmo dia".** Das 498 candidatas que estavam na mesma unidade, **483 caíam no mesmo
dia**. Quem preenche a anamnese lança a requisição na sequência. Ampliar a janela para ±45 dias não
trouxe quase nada e trouxe ambiguidade.

**Por que a unidade NÃO é chave.** Quem digita no SISCAN usa, com frequência, "SECRETARIA MUNICIPAL
DE SAUDE DE MARICA" ou outra USF que não a do pedido — 14 casos de "CENTRO MATERNO INFANTIL" contra
"UNIDADE DE SAUDE DA FAMILIA CENTRAL", por exemplo. A unidade vira **sinal para mostrar**, não
critério para casar.

### Resultado

| | |
|---|---|
| **551 pares** | zero colisão de protocolo · todos com Nº do Exame |
| **241 sem requisição nenhuma** | 233 são de setembro — é a fila viva, que o botão novo atende |
| 70 com requisição em outro dia | olho humano |
| 11 com duas ou mais no mesmo dia | olho humano |

## Duas provas independentes, lidas dentro da requisição

Abrir a requisição confirma o par sem depender do cruzamento:

- **`frm:cartaoSUS`** — o CNS da requisição, conferido contra todos os CNS da paciente (o par pode
  ter casado por um secundário: comparar só com o principal dá falso alarme);
- **`frm:prontuario`** — **vazio significa digitada à mão**. Quando traz o nosso accession, a
  requisição saiu daqui. Qualquer outro número é caso para humano.

A tela ainda entrega `frm:cnesUnidade` (o CNES, melhor que o nome truncado da grade) e
`frm:dataSolicitacaoInputDate`.

## O que se traz de volta para a anamnese

**Só as lacunas.** Nunca se sobrescreve o que a enfermeira respondeu aqui — trocar uma verdade por
outra sem ninguém ver é pior que ficar sem a resposta.

A lacuna é bem definida: **863 das 873 anamneses são v1 e nenhuma tem o bloco `siscan`**, que é
justamente onde moram as perguntas que só o SISCAN fazia. O que volta:

| Vem do SISCAN | Vai para |
|---|---|
| "Antes desta consulta, teve as mamas examinadas?" (01/02; 03 = Não Sabe é descartado) | `siscan.mamasExaminadasAntes` |
| `frm:anoUltimaMamografia` | `siscan.anoUltimaMamografia` |
| "Fez radioterapia?" + local + ano de cada lado | `siscan.radioterapia` |
| `frm:ano{Tipo}{Lado}` (13 tipos × 2 lados) | `siscan.cirurgias` |

O que **nós já respondemos** — fez mamografia, fez cirurgia, nódulo — é **conferido, não escrito**:
a diferença vira linha de relatório.

`versao` **não** sobe para 2: o bloco passa a existir, mas o resto do questionário v2 não foi
respondido, e dizer que foi seria mentir. A tela de leitura renderiza o bloco por presença, não por
versão.

## O que se grava, e como desfazer

```
smsmarica.exame_imagem   siscan_protocolo, siscan_numero_exame, siscan_requisicao_em
smsmarica.anamnese       conteudo_json (só as lacunas) + _origemSiscan
```

`siscan_requisicao_por` fica **NULL de propósito**: ninguém daqui criou essa requisição. Protocolo
preenchido + autor nulo é a assinatura do backfill, e o carimbo `_origemSiscan` no JSON diz de onde
veio e quando.

O `UPDATE` tem `and siscan_protocolo is null`: se alguém gerar pela tela enquanto o script roda, o
dela vale.

**Gravar o protocolo congela a anamnese** (`AnamnesesService` recusa alteração de anamnese enviada
ao SISCAN). Para quem já tem requisição isso é o comportamento certo — e é exatamente por isso que
um par errado custa caro. O arquivo de desfazer nasce **antes** da escrita, com o estado anterior de
cada linha.

## As ferramentas

```bash
# 1. o espelho (somente leitura, ~2 min)
python espelho_requisicoes.py --de 01/06/2026 --ate 30/09/2026 --saida <fora-do-repo>.json

# 2. cruzar + ler cada requisição (somente leitura)
python backfill_siscan.py --espelho <espelho>.json --saida <relatorio>.json [--limite 5]

# 3. aplicar — o ÚNICO que escreve, e só com --confirmar
python aplicar_backfill.py --relatorio <relatorio>.json --desfazer <desfazer>.json [--confirmar]
```

A saída do passo 1 tem nome e CNS; a do passo 2 tem dado clínico. **Nunca no repositório** —
scratchpad ou fora da árvore.
