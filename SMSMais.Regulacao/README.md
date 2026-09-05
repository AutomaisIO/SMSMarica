# Regulação → Solicitações — planejamento do módulo

> **Quem lê este documento:** quem for implementar o módulo (pessoa ou modelo), em sessões curtas e incrementais.
> **Fonte da verdade da intenção:** `descricao inicial.txt` (transcrição dos 14 áudios do Bernardo, 02–04/09/2026). Se um plano e a transcrição divergirem, a transcrição vence e o plano está com defeito — salvo onde uma **decisão D-n** (§5) registra explicitamente a escolha feita depois.
> **Fonte da verdade do andamento:** `PROGRESSO.md`. Antes de qualquer tarefa, leia-o; ao terminar, atualize-o.

Planejado em 04/09/2026 (Claude Fable 5.1, sessão de planejamento). Nada deste diretório está implementado.

## 1. O que é o módulo

Hoje o SMSMais **lê** os três sistemas de regulação (SISREG, SER, SERNIT): importa marcações, varre filas, lista notificações. Escreve pouco: no SER/SERNIT só follow-up e telefone, assinados pelo operador; no SISREG nada.

O módulo novo fecha o ciclo. A unidade solicitante abre a solicitação **dentro do SMSMais** — escolhe o procedimento, o sistema confere as regras de elegibilidade do manual, cobra os documentos exigidos, guarda os anexos — e a solicitação cai numa **fila pré-regulação**. Um **agente regulador** revisa, ajusta (com histórico) e a empurra para o sistema de regulação de verdade com a credencial certa. Dali em diante o número externo vira a chave, e o que voltar do sistema (follow-up, falha de contato, documento criticado) vira **pendência** visível para a ponta e para a regulação.

Três fluxos: **Interno** (SISREG, regulação do município), **Externo** (SER da SES-RJ, SERNIT de Niterói, futuramente eSUS) e **NAR** (agendamento indireto: sempre SISREG, em nome de outra unidade solicitante).

## 2. Glossário

| Termo | Significado neste módulo |
|---|---|
| **Interno** | Fluxo que termina no SISREG (regulação municipal). A oferta interna são as unidades de Maricá com escala no SISREG. |
| **Externo** | Fluxo que termina no SER, no SERNIT ou em sistema futuro. Não há unidade executante em Maricá: quem executa é o Estado/outro município. |
| **NAR** | Agendamento indireto. Sempre SISREG. O usuário abre a solicitação **em nome de** outra unidade solicitante; o agente regulador a inclui no SISREG autenticando com o CNES + senha daquela unidade. |
| **Pré-regulação** | Fila das solicitações abertas no SMSMais que ainda não foram triadas pelo agente regulador. Estado `PendenteRegulacao`. Nome provisório (D-6). |
| **Agente regulador** | Usuário com o módulo `RegulacaoTriagem` (48). Vê todas as unidades na mesma fila, ajusta, aprova pendências e envia ao sistema de regulação. Não é um perfil de acesso; é um módulo a mais no perfil. |
| **Solicitante** | Usuário com o módulo `Regulacao` (47), escopado pelas unidades a que está vinculado. Abre, edita, anexa, responde pendências. |
| **Regra dedutível** | Critério do manual que o sistema avalia sozinho a partir do cadastro (idade, sexo, CID). |
| **Regra não dedutível** | Critério clínico que vira pergunta ao solicitante: sim / não / não sei. |
| **Regra documental** | Exame ou documento exigido. Vira uma **caixinha** de anexo própria, com várias versões de arquivo. |
| **Ressalva de destino** | Quando o procedimento é bloqueado em um sistema externo e permitido em outro: a solicitação passa, marcada "só pode ir para X". |
| **Caixinha** | Exigência documental de uma solicitação. Aceita vários arquivos (fotos/PDFs) e guarda o histórico quando um arquivo é criticado e substituído. |
| **Número externo** | Código gerado pelo SISREG/SER/SERNIT ao incluir. Depois dele, o número interno vira só rastro. |
| **Credencial pessoal** | Login/senha do próprio usuário em cada sistema de regulação, guardados no perfil dele (D-1). |
| **Credencial de unidade** | Credencial SISREG que o agente regulador usa para incluir em nome de uma unidade: CNES + senha padrão (para todas) ou específica (para aquela unidade). |

## 3. Atores

| Ator | Módulo | O que faz |
|---|---|---|
| Solicitante (UBS, ambulatório, hospital) | 47 `Regulacao` | Abre a solicitação a partir da sua unidade; no Interno, inclui no SISREG com a própria senha (D-8); responde pendências; vê a fila e as notificações da própria unidade. |
| Agente regulador | 47 + 48 `RegulacaoTriagem` | Vê tudo; assume, ajusta, devolve, recusa; envia ao SER/SERNIT com a credencial pessoal; inclui NAR no SISREG com a credencial da unidade; aprova follow-up/telefone/anexo vindos da ponta e os submete. |
| Configurador da regulação | 51 `RegulacaoConfiguracao` | Cadastra regras de elegibilidade, cura o catálogo canônico de procedimentos, define as configurações do módulo. |
| Sistemas externos (SISREG, SER, SERNIT) | — | Geram número, situação, follow-up. Continuam sendo lidos pelas varreduras/importações que já existem. |

## 4. Fluxo macro

1. **Procedimento** — busca por texto livre (vetorial + lexical) sobre o catálogo canônico; a tela mostra as unidades internas que executam e a existência externa (SER/SERNIT).
2. **Destino** — Interno / Externo / NAR, conforme a oferta e a configuração "pode escolher Externo mesmo tendo Interno". No NAR, antes de tudo, a unidade "em nome de".
3. **Paciente** — busca local; se não existe, consulta CADSUS pelo roteador (SISREG ou SER); CPF obrigatório para seguir.
4. **Regras** — dedutíveis avaliadas na hora; não dedutíveis viram questionário; documentais viram caixinhas (com oferta de "usar exame interno já existente").
5. **Formulário + anexos** — Interno: campos da tela de inclusão do SISREG; Externo: união dos campos SER ∪ SERNIT. Tudo guardado como JSON com a versão do catálogo.
6. **Enviar** — sem pendência: Interno → inclui no SISREG com a senha do solicitante e entra na pré-regulação já com número; Externo/NAR → entra na pré-regulação sem número. Com pendência: fica **Rascunho**.
7. **Triagem** — o agente assume, confere, ajusta (histórico), e **envia ao sistema**: SER/SERNIT com a credencial pessoal dele; NAR no SISREG com a credencial da unidade. O número externo é gravado; o solicitante é notificado.
8. **Depois do envio** — varreduras casam o número externo com o espelho (`ser_solicitacao`, `sernit_solicitacao`, `solicitacao`); follow-ups viram pendências (falha de contato, documento criticado); a ponta responde; o agente aprova e submete (no SISREG, só baixa local).

## 5. Decisões tomadas com o Bernardo (04/09/2026)

| # | Decisão | Onde se aplica |
|---|---|---|
| **D-1** | Credenciais pessoais guardadas por **envelope pela senha do usuário**: uma chave derivada da senha de login do SMSMais embrulha a chave do usuário; ela é destravada no login e vive só em cache de sessão. O servidor não lê nada em repouso. Trocar a própria senha preserva tudo; **reset ou senha esquecida perde as credenciais** e o usuário recadastra. Depois de um restart da API, o usuário confirma a senha do SMSMais uma vez. Nenhum job em background usa credencial pessoal. | 07, ADR-0053 |
| **D-2** | O **modal de login continua como fallback**: só aparece se não houver credencial pessoal (nem de unidade, no NAR) configurada. Configurada, autentica direto. | 07, 04, 11, 12 |
| **D-3** | **Reaproveitar os módulos 47 (`Regulacao`, solicitante), 48 (`RegulacaoTriagem`, agente regulador) e 51 (`RegulacaoConfiguracao`)**; 49 e 50 ficam reservados. | 04, 09 |
| **D-4** | **Primeiro marco com envio automático = fila + agente + credenciais + envio ao SER/SERNIT.** A escrita no SISREG entra depois do spike da tela `marcar`. | §7 |
| **D-5** | Ordem do wizard: [NAR: unidade em nome de] → procedimento → destino → paciente → regras → formulário + anexos → revisão. Decisão delegada pelo áudio 6; justificativa no plano 02. | 02 |
| **D-6** | Nome provisório da fila: **"Pré-regulação"**; rótulo configurável. | 04, 09 |
| **D-7** | Texto fixo na tela de credenciais: *"As senhas colocadas aqui ficam criptografadas no banco, onde ninguém tem acesso sem a sua senha. Se você resetar a senha ou perder a senha, essas senhas deverão ser cadastradas novamente."* Repetido no modal de fallback ao oferecer "salvar" e na tela de reset de senha do admin. | 07 |
| **D-8** | **Interno (não-NAR): o solicitante inclui no SISREG com a senha pessoal dele, no momento do "Enviar".** A solicitação nasce no SISREG com número (editável por ~7 dias, a confirmar), com operador e unidade reais. A pré-regulação interna é "já no SISREG, aguardando OK do agente". | 02, 04, 11 |
| **D-9** | **No SISREG só se inclui com a senha do solicitante lotado na unidade.** Para o NAR, o agente autentica com o **CNES da unidade + senha de unidade** (padrão ou específica). Não existe inclusão "como regulador". | 07, 11 |
| **D-11** | **Construir tudo com a escrita externa desligada; validar as gravações depois, em bloco.** Instrução do Bernardo em 05/09/2026: *"vamos deixar tudo pronto MAS SEM GRAVAR nada nos sistemas de regulação… depois que estiver tudo pronto vamos validar especificamente as gravações"*. Consequências obrigatórias: (a) **toda** chamada que escreve em SISREG/SER/SERNIT passa por **um único ponto de estrangulamento**, `IEscritaExternaGate`, que consulta `regulacao_configuracao.escrita_externa_habilitada` (default **false**) e lança `EscritaExternaDesabilitadaException` — nenhum service fala com o provedor por fora; (b) com o gate fechado o fluxo **vai até a borda e registra o que enviaria** (payload, anexos, credencial que usaria) num evento `EnvioSimulado`, para a validação futura conferir contra o real; (c) o "Registrar envio" manual (número digitado pelo agente) **continua funcionando** — ele não escreve em sistema externo; (d) os spikes **a** e **b** seguem pendentes de OK, e a tarefa que depende de captura real deles fica explicitamente marcada como bloqueada em vez de implementada por adivinhação. | 04, 06, 07, 09, 11, 12 |
| **D-10** | **O catálogo canônico é plano, e quem desempata é o agente.** Pergunta levantada pelo spike c (05/09/2026): o SERNIT tem `Endocrinologia` como balde e o SER quebra em N subespecialidades — 17 casos. Decisão: **não há hierarquia**. O balde e os específicos são **entradas canônicas distintas**, e a busca por "endocrinologia" devolve **todas** ("todos de todos"). O solicitante escolhe qualquer uma e segue; **o agente regulador pode trocar o procedimento** na triagem, nos dois sentidos (do balde para o específico e do específico para outro). Consequência: só fundem em um canônico os recursos que casam pela **chave D3** (igualdade); **contenção nunca funde** — vira, no máximo, sugestão de curadoria. | 01, 04, 08 |

## 6. Planos (um arquivo por plano)

| # | Arquivo | Assunto | Incremento | Depende de |
|---|---|---|---|---|
| 01 | `01-catalogo-procedimentos-busca-semantica.md` | Catálogo canônico de procedimentos, embeddings, busca híbrida, oferta interna | 1 | — |
| 02 | `02-fluxo-abertura-solicitacao.md` | Wizard de abertura, NAR, formulário JSON, anexos, deprecação dos rascunhos por sistema | 2 | 01, 10 |
| 03 | `03-regras-elegibilidade-por-procedimento.md` | Regras dedutíveis / não dedutíveis / documentais, motor, tela de cadastro, exames internos | 4 | 01, 02, spike e |
| 04 | `04-fila-pre-regulacao-e-agente-regulador.md` | Entidade da solicitação, estados, histórico, permissões, telas da fila, envio assistido e automático | 3 | 02 |
| 05 | `05-vinculo-externo-e-notificacoes-por-unidade.md` | Casar número externo com os espelhos; notificações por unidade solicitante | 3 | 04 |
| 06 | `06-pendencias-pos-envio.md` | Follow-up → falha de contato / documento criticado; ponta responde, agente aprova e submete | 6 | 04, 05, 12, spike d |
| 07 | `07-credenciais-pessoais-por-usuario.md` | Cofre por usuário, credencial por unidade SISREG, tela "Minhas credenciais", sessão por operador | 5 | — |
| 08 | `08-paridade-ser-sernit.md` | Conferência SER × SERNIT, chave de pareamento, régua união, job recorrente | 8 | 01, spike c |
| 09 | `09-configuracoes-do-modulo.md` | `regulacao_configuracao` e a aba de configuração | 2/4 | — |
| 10 | `10-paciente-no-fluxo.md` | Resolução do paciente no wizard; CPF obrigatório na transição para a fila | 2 | — |
| 11 | `11-escrita-sisreg-inclusao.md` | Spike da tela `marcar`; inclusão pelo solicitante (D-8) e pelo agente por unidade (NAR) | 7 | 04, 07, spike b |
| 12 | `12-escrita-ser-sernit.md` | Spike do Gravar + anexo; envio automático; follow-up/telefone via agente | 5 | 04, 07, spike a |
| 13 | `13-spikes-de-laboratorio.md` | Os cinco spikes (a–e): ferramenta, critério de saída, custo, autorização | 0 | — |
| ADR | `adr/0052-…`, `adr/0053-…`, `adr/0054-…`, `adr/0055-…` | Rascunhos de ADR a promover para `docs/adr/` quando o incremento correspondente for para produção | — | — |

Todo plano tem o mesmo esqueleto: Objetivo · Requisitos cobertos · Decisões aplicadas · O que já existe e será reaproveitado · Desenho · Tarefas (checklist) · Dependências · Riscos e pontos a confirmar · Testes · Fora de escopo — e, no fim, a seção **"Especificação para execução"**: arquivos exatos a criar/alterar, entidades com colunas e tipos, enums, interfaces e DTOs, rotas com permissão, componentes e hooks do front, nomes de teste, passo a passo com comandos e critério de pronto. A parte de cima explica o porquê; a especificação é o que se digita.

## 7. Incrementos (cada um utilizável sozinho)

| Inc | Entrega | Planos | Risco externo |
|---|---|---|---|
| 0 | Spikes de laboratório a–e, com relatório | 13 | a e b escrevem em sistema real → OK explícito |
| 1 | Busca de procedimento com oferta interna e existência externa; job de embeddings; ADR-0055 | 01 | nenhum |
| 2 | Wizard grava a solicitação local (Rascunho / pré-regulação sem número), paciente resolvido, formulário JSON, anexos; configuração mínima | 02, 10, 09 | nenhum |
| 3 | Fila por unidade, fila global do agente, histórico, **"Registrar envio"** (o agente inclui no sistema como faz hoje e digita o número), vínculo por número, notificações por unidade; ADR-0052 | 04, 05 | nenhum |
| 4 | Motor de regras completo + tela de cadastro + conferência de exames internos | 03, 09 | nenhum |
| 5 | Cofre de credenciais + **envio automático ao SER/SERNIT** + follow-up/telefone via agente; ADR-0053, ADR-0054 | 07, 12 | alto |
| 6 | Pendências pós-envio (falha de contato, documento criticado) | 06 | médio |
| 7 | Escrita no SISREG: inclusão pelo solicitante (D-8) e NAR pelo agente | 11 | alto (CAPTCHA/orçamento) |
| 8 | Paridade SER × SERNIT recorrente com tela de divergências | 08 | nenhum |

Dependências: 1 → 2 → 3 → {4, 5} ; 5 → 6 ; {3, 5, spike b} → 7 ; {1, spike c} → 8. O marco D-4 é o fim do incremento 5. Os spikes c, d, e não escrevem em lugar nenhum e podem rodar a qualquer momento.

## 8. Matriz requisito → plano

| Req | Resumo | Planos |
|---|---|---|
| R-01 | Menu Solicitações; Interno/Externo/NAR | 02, 04 |
| R-02 | Busca vetorizada; unidades executantes | 01 |
| R-03 | Só Interno/Externo; config externo-com-interno | 02, 09 |
| R-04 | Formulário = tela do SISREG + anexos | 02, 11 |
| R-05 | Paridade SER × SERNIT; união de campos; JSON | 02, 08 |
| R-06 | Regras por procedimento × sistema; ressalva | 03 |
| R-07 | Questionário; caixinhas; pendência → rascunho | 03, 02 |
| R-08 | Ordem paciente × procedimento; CPF; fontes de cadastro | 02, 10 |
| R-09 | Regras no interno; exames internos | 03 |
| R-10 | Fila pré-regulação; visão da unidade | 04 |
| R-11 | Agente regulador; histórico; enviar; notificação; filtro por unidade | 04, 05, 07 |
| R-12 | Falha de contato; pendência de contato | 06 |
| R-13 | Ponta responde → agente aprova e submete | 06, 12 |
| R-14 | SISREG só baixa; documento criticado; pendência geral | 06 |
| R-15 | SISREG editável ~7 dias; operador e unidade | 11, 04 |
| R-16 | Credenciais pessoais; unidades SISREG do agente | 07 |
| R-17 | NAR em nome de; credencial da unidade | 02, 11, 07 |
| R-18 | Dropdown "a partir de qual unidade" | 02 |

Nenhum requisito ficou órfão. A rastreabilidade detalhada (número do áudio) está no início de cada plano.

## 9. Questões em aberto (respondem-se nos spikes ou com o Bernardo)

1. **Janela editável do SISREG** — existe mesmo? quantos dias? o que o agente consegue editar dentro dela e com qual credencial? (spike b)
2. **Follow-up no SISREG** — a transcrição diz nas duas direções (áudios 12 e 13). Existe ação de follow-up na tela do solicitante? (spike b)
3. **CAPTCHA do SISREG é por operador ou por IP?** Se for por IP, um agente pode travar todos os outros e o motor institucional. (spike b, medir)
4. **Onde guardar os anexos** — `midia` (bytea, como o rascunho SER hoje) ou Spaces (como os PDFs de exame). Recomendação nos planos 02 e 03: Spaces desde o início, com tipos além de PDF.
5. **CPF obrigatório** — ADR-0041 admite paciente sem CPF no cadastro; o módulo exige CPF só na transição para a fila (o SERNIT não grava sem CPF). Confirmar que isso basta para o SISREG e o SER.
6. **O que o OK do agente faz no SISREG** para uma solicitação interna já incluída pelo solicitante (D-8): nada, ou existe uma ação de confirmação? (spike b)
7. **Credencial padrão do agente enxerga todas as unidades?** Ou cada unidade tem login próprio? Define o modelo de "senha padrão" do plano 07. (spike b)

## 10. Como executar (para o modelo que for implementar)

1. Leia `PROGRESSO.md` e vá para a primeira tarefa não concluída do incremento em andamento. Não pule incrementos.
2. Leia o plano da tarefa **inteiro** antes de tocar em código. O plano cita os arquivos existentes que devem ser reaproveitados; não reinvente o que ele aponta.
3. Respeite as regras do `CLAUDE.md` da raiz: 3 projetos, schema `smsmarica`, migrations imutáveis, exceções tipadas, pt-BR no domínio, módulos do front isolados em `src/features/regulacao/`.
4. **Nada vai para produção sem OK explícito do Bernardo** — nem deploy, nem migration em prod, nem escrita em SISREG/SER/SERNIT reais. Os spikes a e b só rodam com esse OK, uma vez, capturando tudo na primeira tentativa.
5. Ao fechar uma tarefa: marque em `PROGRESSO.md`, registre o commit (se houver) e o que aprendeu que o plano não previa. Se descobrir que o plano está errado, corrija o plano e anote a mudança no `PROGRESSO.md` — nunca implemente em silêncio algo diferente do que está escrito.
6. Para revisar os planos contra a transcrição, existe o workflow `/regulacao-revisar-planos` (`.claude/workflows/`). Só rode com autorização: consome vários agentes.
7. Opcional: `/regulacao-executar-plano` com `{ plano: "04", tarefa: "3.1", data: "aaaa-mm-dd" }` executa **uma** tarefa com agentes (ler → implementar → verificar → revisar) e grava o relatório em `revisoes/`; ele recusa tarefas que exigem OK de produção e não marca nada no `PROGRESSO.md` — isso continua sendo de quem lê o relatório. O caminho padrão segue sendo a execução manual pelo `PROGRESSO.md`.

## 11. Fontes usadas no planejamento

- `descricao inicial.txt` — a especificação.
- `docs/ser-criar-solicitacao.md`, `docs/ser.md`, `docs/ser-continuacao.md`, `docs/adr/0042-ser-segunda-fonte-de-regulacao.md`.
- `Automais.SISREG/docs/APRENDIZADOS.md` (§ "Subsídio para escrita"), `Automais.SER/` (sondas), `Automais.SERNIT/docs/APRENDIZADOS.md`.
- `Automais.SER/documentacao/CRECE_MANUAL DO SOLICITANTE_Versão1 30.11.2022.pdf` e `REUNI_MANUAL DO SOLICITANTE_V3 29.12.2022.pdf` — os manuais de elegibilidade.
- Código: `SMSMais.Core/Ser/`, `SMSMais.Core/Sernit/`, `SMSMais.Core/Integracoes/{SerWeb,SernitWeb,SisregWeb,Cadastro,Credenciais}/`, `SMSMais.Core/Inteligencia/Provedores/VoyageEmbeddings.cs`, `SMSMais.Core/Common/Unidades/EscopoUnidade.cs`, `SMSMais.Data/Entities/{Ser,Sernit,Sisreg}/`, `SMSMais.Data/Entities/Enums/ModuloPermissao.cs`, `SMSMais.front/src/features/{ser,sernit,sisreg}/`, `SMSMais.front/src/app/layout/menuConfig.ts`.
