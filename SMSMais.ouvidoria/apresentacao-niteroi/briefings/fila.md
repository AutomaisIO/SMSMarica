# Briefing da tela: Ouvidoria

Rota: /app/ouvidoria

## Propósito
Fila da ouvidoria central: abas por etapa do fluxo, filtros e tabela paginada no servidor. Cada linha abre /app/ouvidoria/{id}.

## Layout (descrição fiel do código)
Container `div.space-y-4`. (1) CABEÇALHO: `div.flex.flex-wrap.items-center.justify-between.gap-3` — à esquerda um bloco com `h1.flex.items-center.gap-2.text-xl.font-semibold.text-slate-800` contendo o ícone lucide `Megaphone` em `h-5 w-5 text-red-600` + o texto "Ouvidoria", e logo ao lado (gap-1.5) o botão "?" do manual (<AjudaManual artigo="ouvidoria" />); abaixo do h1 o subtítulo `p.text-sm.text-slate-500`. À direita, quando há permissão, o botão primário (gradiente vermelho, .btn .btn-primary) com ícone `Plus h-4 w-4` + "Registrar manifestação". (2) BARRA DE FILTROS: `div.flex.flex-wrap.items-end.gap-3` com 5 controles, todos com <label class="sr-only"> (rótulo invisível, só placeholder/opção vazia à vista), nesta ordem e largura: Input de busca `w-64`, Select tipo `w-44`, Select prioridade `w-40`, SeletorUnidade `w-56`, SeletorPontoResposta `w-64`. (3) ABAS: componente <Tabs> — nav com borda inferior `border-b border-gray-200`, botões `px-4 py-2 text-sm font-medium border-b-2`; a aba ativa fica `border-red-600 text-red-700`, as demais `border-transparent text-gray-500`; cada aba mostra, depois do rótulo, um badge contador `span.ml-2.rounded-full.bg-gray-100.px-2.py-0.5.text-xs.text-gray-700` (a aba "Concluídas" não tem contador). (4) CORPO DA ABA (`pt-4`): eventual faixa de erro vermelha (`rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700`), a <TabelaManifestacoes> (card branco `rounded-lg border border-gray-200 bg-white shadow-sm`, cabeçalho `bg-gray-50` com títulos `text-xs font-semibold uppercase tracking-wide text-gray-500`, células `px-4 py-3 text-sm text-gray-700`, linhas clicáveis com title "Abrir manifestação", linha atrasada e não respondida pintada `bg-red-50/40 hover:bg-red-50`, barra de rolagem horizontal flutuante) e, abaixo, a <Paginacao> ("Mostrar [25|50|100] por página" à esquerda, "1–25 de 1.234" ao centro, setas + números à direita; página atual em `bg-red-700 text-white`).

## Filtros
- Busca (Input, id="ouv-busca", label sr-only "Buscar", largura w-64) — placeholder: "Protocolo, nome ou CPF…"
- Tipo (Select, id="ouv-f-tipo", label sr-only "Tipo", largura w-44) — opções na ordem: "Todos os tipos" (valor vazio), "Solicitação", "Reclamação", "Denúncia", "Sugestão", "Elogio", "Informação"
- Prioridade (Select, id="ouv-f-prioridade", label sr-only "Prioridade", largura w-40) — opções: "Toda prioridade" (vazio), "Normal", "Alta", "Urgente"
- Unidade (SeletorUnidade, id="ouv-f-unidade", label sr-only "Unidade", largura w-56) — opção vazia "Todas as unidades" (ou "Carregando…" enquanto carrega); depois a lista de unidades ativas ordenada por nome pt-BR
- Ponto de resposta (SeletorPontoResposta, id="ouv-f-ponto", label sr-only "Ponto de resposta", largura w-64, incluirInativos) — opção vazia "Todos os pontos de resposta" (ou "Carregando…", ou "Nenhum ponto de resposta cadastrado" quando a lista é vazia); cada opção é "{nome} — {tipo do ponto}" + " ({unidade})" quando há unidade + " (inativo)" quando inativo

## Colunas da tabela
- 1. Protocolo — mono (`whitespace-nowrap font-mono text-sm text-slate-700`), o número + o badge de identificação (Sigilosa/Anônima) quando não for Identificada; ordenável
- 2. Tipo — <TipoManifestacaoBadge> (pílula com ícone); ordenável
- 3. Status — <StatusManifestacaoBadge> (pílula); ordenável
- 4. Prioridade — <PrioridadeManifestacaoBadge> (pílula); ordenável pela ordem Normal=1, Alta=2, Urgente=3
- 5. Assunto / resumo — bloco `max-w-xs`: 1ª linha o nome do assunto em `text-sm text-slate-800` ou, sem assunto, "Sem assunto" em `text-slate-400`; 2ª linha o resumo truncado em `text-xs text-slate-500` (title = resumo completo); ordenável
- 6. Manifestante — `text-sm text-slate-600`; o nome, ou "anônimo" (quando Anonima) / "restrito" (quando a identidade é restrita) em `text-xs text-slate-400`; ordenável. Esta coluna NÃO existe quando ocultarManifestante (tela Meu ponto)
- 7. Unidade — `text-sm text-slate-600`, nome da unidade ou "—"; ordenável
- 8. Ponto de resposta — `text-sm text-slate-600`, nome do ponto ou "—"; ordenável
- 9. Prazo — coluna em `flex flex-col gap-1`: sempre o <PrazoChip> com rótulo "Cidadão:" e, quando o status é Encaminhada e há prazo da área, um segundo chip com rótulo "Área:"; ordenável
- 10. Última atividade — `whitespace-nowrap text-sm text-slate-500`, instante formatado; ordenável

## Badges e chips
- Tipo: pílula com ícone — "Solicitação" (ícone FileQuestion), "Reclamação" (MessageSquareWarning), "Denúncia" (AlertOctagon), "Sugestão" (Lightbulb), "Elogio" (Sparkles), "Informação" (FileQuestion)
- Status: pílula com um dos 11 rótulos — "Registrada", "Em triagem", "Encaminhada à área", "Aguardando complementação", "Respondida pela área", "Em validação", "Respondida ao cidadão", "Em recurso", "Concluída", "Arquivada", "Encaminhada a outro órgão"
- Prioridade: pílula — "Normal", "Alta", "Urgente"
- Identificação (só quando NÃO é Identificada): pílula roxa `bg-purple-50 text-purple-700 ring-purple-600/20` com ícone EyeOff + texto "Sigilosa" (title "Identidade restrita à ouvidoria") ou ícone UserX + "Anônima" (title "Sem dados do manifestante e sem código de acesso")
- Prazo (<PrazoChip>): pílula com ícone CalendarClock, texto "{rótulo}: {data} · {situação}" — situação é "vence hoje" (0 dias), "vence amanhã" (1 dia), "{n} dias", "atrasada {n} dias" / "atrasada 1 dia", ou "encerrada" quando o status é final ou Respondida. title = "Prazo: {data}"
- Contadores das abas: badge cinza redondo com o número (Triagem = registradas+emTriagem, Em andamento = encaminhadas+aguardandoComplementacao, Aguardando validação, Atrasadas, Recurso)

## Ações
- Botão primário "Registrar manifestação" (ícone Plus) → navega para /app/ouvidoria/registrar
- Clique na linha da tabela → navega para /app/ouvidoria/{id} (tooltip "Abrir manifestação")
- Clique no cabeçalho de coluna → ordena a página carregada (crescente → decrescente → ordem original)
- Troca de aba → reseta a página para 1
- Paginação: "Mostrar" 25 / 50 / 100 por página; setas anterior/próxima; números de página
- Botão "?" ao lado do título → abre o artigo "ouvidoria" do Manual

## Textos literais do JSX
- Ouvidoria
- Manifestações dos cidadãos: triagem, encaminhamento, resposta e prazos.
- Registrar manifestação
- Buscar
- Protocolo, nome ou CPF…
- Tipo
- Todos os tipos
- Prioridade
- Toda prioridade
- Unidade
- Todas as unidades
- Ponto de resposta
- Todos os pontos de resposta
- Triagem
- Em andamento
- Aguardando validação
- Atrasadas
- Recurso
- Concluídas
- Nenhuma manifestação atrasada. Ótimo.
- Nenhuma manifestação encontrada.
- Protocolo
- Status
- Assunto / resumo
- Sem assunto
- Manifestante
- anônimo
- restrito
- Última atividade
- Cidadão
- Área
- Carregando…
- Mostrar
- por página
- Abrir manifestação
