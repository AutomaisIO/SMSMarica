# O que cada relatório Crystal do Klinikos entrega (medido, Conde 15–16/09/2026)

> Colunas medidas lendo os XLS gerados (instância do Conde, `unid_codigo` 0005, 1 dia).
> `lin/dia` = linhas de dado num dia. Tempo = cronometrado nesta sessão (vazio = não cronometrado
> isolado, mas todo Crystal fica na faixa de ~14–60 s). Nenhum dado de paciente aqui, só os
> nomes das colunas.

## A. Relatórios NOMINAIS (uma linha por boletim/paciente — é o que dá para ingerir)

| parrel | Relatório | lin/dia | tempo | Colunas de dado | Serve no hub para |
|---|---|---:|---:|---|---|
| **407** | Pacientes Registrados no Dia | 1.317 | 23–30 s | Nº Boletim, Prontuário, Paciente, Dt. Nascimento/Idade, Clínica | Encounter (chegada) + cadastro básico; **fonte da via rápida** |
| **667** | Nominal por Classificação de Risco | 623 | ~14 s | Nº Boletim, Paciente, Idade, **Data/Hora Entrada**, Clínica, **Origem** | chegada + **cor/risco** + como chegou |
| **526** | Atendimentos por Profissional | 831/dia | **~60 s (1,1 MB)** | Nº Boletim, Código (prontuário/atend.), **Hora Atendimento**, Nome, Idade, Sexo, Clínica, **+ sub-linha `CID:` e `PROCEDIMENTOS:` por boletim**; agrupado por **Profissional**. (coluna "Classificação" existe mas veio **vazia** — cor vem do 667) | início do atendimento + **CID** + procedimentos + profissional (o mais pesado) |
| **629** | Fila de Espera | 1.169 | ~10 s (PDF) | Nº Boletim, Nome, Início Atendimento, **Tempo**, Clínica | fila viva "agora" (mas melhor derivar do hub) |
| **630** | Pacientes em Observação | 210 | 10–13 s | Nº Boletim, Nome, Início Atendimento, Tempo, Especialidade, Profissional, Espec. Obs., Prof. Obs., **Leito** | quem está em observação + leito |
| **631** | Atendimentos em Andamento | 204 | 3 s (varia) | Nº Boletim, Nome, Início Atendimento, Tempo, Clínica, Profissional | em atendimento "agora" |
| **763** | Histórico de Eventos da Fila | 73 | ~13 s | Profissional Resp. Evento, Data Evento, **Evento Ocorrido**, Justificativa | trilha de eventos da fila |
| **100** | Internações Diárias | 60 | ~14 s | Prontuário-Nome, D.Nasc, Idade, Sexo, Hora In, Município/Bairro, **Local Internação** | Encounter de internação (Conde) |
| **802** | Óbito por Locais de Internação | — | ~14 s | Nº Boletim, Paciente, Idade, Data/Hora Entrada, **Data/Hora Óbito**, Local Internação | desfecho óbito |
| **21** | Prontuários Abertos (Cadastro) | 37 | ~14 s | Prontuário, Paciente, Clínica, Nascimento, Idade, Sexo, **CNS** | **sincronismo de cadastro** (novos + CNS) |
| **488** | Origem Criação Prontuário (Cadastro) | 7 | ~14 s | Data Abertura, Código, Paciente, Prontuário, Setor de Origem | origem do cadastro |

## B. Relatórios ESTATÍSTICOS (só totais — a origem faz a conta; **nós faríamos no hub**)

| parrel | Relatório | Colunas de dado |
|---|---|---|
| 56 | Registro Diário de Urgência por Clínica | Clínica, Total, e faixas etárias (< 1, 1 a 4, 5 a 9, 10 a 14, 15 a 19, 20 a 29, 30 a 39, …) |
| 57 | Registro Mensal de Urgência por Clínica | Mês, Total, mesmas faixas etárias |
| 66 | Urgência por Tempo de Permanência | Tempo de Permanência, Total, **Altas, Óbitos, Internações, Outros** |
| 65 | Tipo de Saída, Sexo e Faixa Etária | por faixa etária (< 1 ano … 60+), cruzado com tipo de saída e sexo |
| 711 | Classificação de Risco, Sexo e Faixa Etária | por faixa etária, cruzado com cor e sexo |
| 484 / 518 / 519 | Diagnósticos por Clínica / Faixa Etária / Registros por Clínica | totais (vieram 0 linhas no dia amostrado) |

> **Regra:** os do grupo B a gente **não puxa** — o hub calcula o mesmo agregado sobre os dados
> nominais do grupo A. Puxar estatística é pagar ~14 s de Crystal por uma conta que é nossa.

## C. Fora do Crystal (por tela / não em relatório)

| Dado | Onde | Custo |
|---|---|---|
| Narrativa médica (anamnese, exame físico, hipótese, conduta) | tela `AtendimentoMedico`; 791 PDF por paciente (Crystal, lento) | tela ~1 s/req, vários req/boletim |
| Prescrição (medicamento, dose, via) | tela `PrescricaoReceita` | idem |
| Sinais vitais | tela `Enfermagem` / `RegistrosEnfermagem` | idem |
| Histórico do paciente (leitura) | tela `ResumoProntuario` | tela ~1 s/req |

## D. CID (diagnóstico) — não sai em lote na web

Medido em 16/09: o **único relatório nominal de CID por boletim (815 "Atendimento Nominal por
CID") NÃO renderiza** — é da família "Consolidado" bugada no Crystal deles (vizinhas 831, 835,
818 deram COMException / FormulaException / 504). Os relatórios de CID que funcionam (484, 542,
543, 544, 546, 547) são **só totais** (por clínica/sexo/faixa), não amarram CID a boletim.
Nenhum dos nominais leves (407, 667, 526, 631) traz CID.

**Correção (medido no XLS do 526):** o CID **sai, sim, em lote** — o relatório **526** traz, sob
cada boletim, uma sub-linha `CID: <descrição>` e `PROCEDIMENTOS: <...>`. Ou seja, o CID **volta
para a espinha**, via 526, **sem precisar abrir tela**. O custo é o 526 ser o relatório mais
pesado (~60 s / 1,1 MB), mas é **1 requisição por unidade por hora** que traz CID + cor + hora do
atendimento + procedimentos de TODOS os boletins — muito mais barato que puxar CID de N boletins
um a um. O 815 (dedicado a CID) continua quebrado, mas o 526 o substitui.

**Só narrativa, prescrição e sinais vitais permanecem no deep** (esses de fato não saem em
nenhum relatório).

## E. Armadilhas de parsing (dry-run 16/09) — para o parser .NET ter teste

- **Chave do boletim precisa ser normalizada (zero-pad para 12).** No **407** o Nº Boletim vem
  como **número** e perde o zero à esquerda (`52609150001`, 11 díg.); no **667** vem como **texto**
  (`052609150001`, 12 díg.). Sem padronizar, o JOIN 407×667 dá **zero overlap** (medido). Regra:
  `spa_codigo` = string, `PadLeft(12,'0')`, sempre.
- **Cor no 667 é cabeçalho de GRUPO, não coluna.** As linhas de boletim ficam sob uma linha-título
  da cor (Verde/Amarelo/…); o parser tem de **rastrear a cor corrente** ao varrer, não procurar
  uma coluna "Classificação".
- **XLS é BIFF com células mescladas** — índice de coluna não alinha linha a linha; achar o
  cabeçalho pelo rótulo e ler por rótulo, não por índice fixo.

## Como ler a tabela

- **CID: ver seção D** — não é bulk na web; entra no deep.
- **A via rápida usa o 407** (mais leve e completo por boletim) e complementa cor/hora com 667/526
  quando o hub precisar.
- **631/629/630** são "agora" — úteis para conferência, mas o painel deriva do hub, não deles.
