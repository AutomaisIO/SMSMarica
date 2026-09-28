# Briefing da tela: Meu ponto de resposta

Rota: /app/ouvidoria/meu-ponto

## Propósito
Fila do membro de ponto de resposta: o que foi encaminhado à sua unidade ou área, para responder. Abre o detalhe em /app/ouvidoria/meu-ponto/{id} (sem card de manifestante).

## Layout (descrição fiel do código)
Container `space-y-4`. (1) CABEÇALHO sem botão à direita: `h1` com ícone `Inbox h-5 w-5 text-red-600` + "Meu ponto de resposta", o "?" do manual (artigo="ouvidoria", secao="meu-ponto") ao lado, e subtítulo `p.text-sm.text-slate-500` em duas frases. (2) BARRA DE FILTROS enxuta: `flex flex-wrap items-end gap-3` com apenas dois controles — Input de busca `w-64` e Select de recorte `w-60`, ambos com label sr-only. (3) Eventual faixa de erro vermelha. (4) <TabelaManifestacoes> com ocultarManifestante (a tabela tem 9 colunas, sem a coluna Manifestante) e vazio "Nada encaminhado ao seu ponto neste recorte.". (5) <Paginacao> com tamanhos 25/50/100. Não há abas nesta tela.

## Filtros
- Busca (Input, id="mp-busca", label sr-only "Buscar por protocolo", largura w-64) — placeholder "Protocolo…"
- Recorte (Select, id="mp-recorte", label sr-only "Recorte", largura w-60) — opções na ordem: "Aguardando minha área" (status Encaminhada, padrão), "Respondidas pela área" (RespondidaPelaArea + EmValidacao), "Todas do meu ponto" (sem filtro de status)

## Colunas da tabela
- 1. Protocolo (mono, com badge de identificação quando não identificada)
- 2. Tipo
- 3. Status
- 4. Prioridade
- 5. Assunto / resumo
- 6. Unidade
- 7. Ponto de resposta
- 8. Prazo (chips Cidadão / Área)
- 9. Última atividade
- (a coluna "Manifestante" NÃO aparece nesta tela)

## Badges e chips
- Tipo, Status, Prioridade, Identificação e PrazoChip — idênticos aos da fila central

## Ações
- Clique na linha → /app/ouvidoria/meu-ponto/{id}
- Trocar recorte ou digitar na busca → volta para a página 1
- Paginação 25/50/100
- Botão "?" → artigo "ouvidoria", seção "meu-ponto"

## Textos literais do JSX
- Meu ponto de resposta
- Manifestações encaminhadas à sua unidade ou área. Você vê o relato e responde; quem manifestou fica com a ouvidoria.
- Buscar por protocolo
- Protocolo…
- Recorte
- Aguardando minha área
- Respondidas pela área
- Todas do meu ponto
- Nada encaminhado ao seu ponto neste recorte.
- Protocolo
- Tipo
- Status
- Prioridade
- Assunto / resumo
- Unidade
- Ponto de resposta
- Prazo
- Última atividade
- Mostrar
- por página
