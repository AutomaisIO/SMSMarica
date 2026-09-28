# Briefing da tela: Pontos de resposta

Rota: /app/ouvidoria/pontos-resposta

## Propósito
Cadastro dos pontos de resposta — quem responde pela unidade, pela área central ou pela apuração — e seus membros (com titular).

## Layout (descrição fiel do código)
Container `space-y-4`. (1) CABEÇALHO `flex flex-wrap items-center justify-between gap-3`: à esquerda `h1` com ícone `Network h-5 w-5 text-red-600` + "Pontos de resposta", "?" do manual (secao="gestao") e subtítulo `text-sm text-slate-500`; à direita, botão primário com ícone Plus "Novo ponto". (2) Um checkbox solto acima da tabela: `label.flex.items-center.gap-2.text-sm.text-slate-600` com "Mostrar inativos". (3) Eventual faixa de erro vermelha. (4) <Tabela> padrão (cartão branco, cabeçalho cinza com títulos maiúsculos pequenos) com 7 colunas; lista ordenada por nome (pt-BR), filtrada pelos ativos salvo se "Mostrar inativos"; linha clicável com tooltip "Editar ponto"; vazio: "Nenhum ponto de resposta. Sem ele, não há para onde encaminhar.". (5) Modal de criação/edição quando alguma linha é aberta.

## Filtros
- Checkbox "Mostrar inativos" (fora da tabela, acima dela)

## Colunas da tabela
- 1. Nome — `font-medium text-slate-800`; ordenável
- 2. Tipo — texto: "Unidade de saúde", "Área central (secretaria)" ou "Unidade apuratória (denúncias)"; ordenável
- 3. Unidade — `text-sm text-slate-600`, nome ou "—"; ordenável
- 4. Prazo próprio — `text-sm text-slate-600`, "{n} dias" ou "Padrão"
- 5. Membros — `text-sm text-slate-600`, "{n} membro" / "{n} membros"; acrescenta " · sem titular" quando há membros mas nenhum titular; o title traz a lista "Nome (titular), Nome, …"
- 6. Em aberto — quando > 0, pílula âmbar `bg-amber-50 text-amber-800 ring-amber-600/20` com o número; quando 0, "0" em `text-xs text-slate-400`; ordenável
- 7. Situação — <StatusBadge>: pílula "Ativo" (`badge badge-success` = bg-emerald-100 text-emerald-700) ou "Inativo" (`badge badge-gray` = bg-gray-100 text-gray-700)

## Campos / cartões
- MODAL (largura lg) — grade 2 colunas:
- "Nome" * (Input id=pr-nome, maxLength 200, autoFocus)
- "Tipo" * (Select id=pr-tipo) — "Unidade de saúde", "Área central (secretaria)", "Unidade apuratória (denúncias)"; quando Apuração, dica "Recebe denúncias habilitadas, na versão pseudonimizada."
- "Unidade" (SeletorUnidade id=pr-unidade) — obrigatória quando o tipo é Unidade; dica "Uma unidade só pode ter um ponto." (tipo Unidade) ou "Opcional."; opção vazia "Selecione" (tipo Unidade) ou "Nenhuma"
- "Prazo próprio (dias)" (Input number 1–90, id=pr-prazo) — dica "Em branco: usa a configuração geral por prioridade."
- Checkbox "Ativo (aparece para encaminhamento)"
- FIELDSET "Membros": texto explicativo "Quem vê e responde o que for encaminhado a este ponto (precisa do módulo \"Ouvidoria — ponto de resposta\" no perfil). O titular é a referência para cobranças."; campo de busca com ícone Search e placeholder "Adicionar membro: nome ou CPF do usuário (mín. 2 caracteres)" (aria-label "Buscar usuário para adicionar como membro"); dropdown com "Buscando…" / "Nenhum usuário ativo encontrado." / a lista de usuários (nome à esquerda, e-mail em cinza à direita); lista de membros com divisórias, cada um com o nome, o botão Titular/Membro e o botão de remover; quando vazia: "Nenhum membro. Sem membros, ninguém recebe o encaminhamento."

## Badges e chips
- "Ativo" / "Inativo" (StatusBadge)
- Contador "Em aberto" em pílula âmbar
- Dentro do modal, cada membro tem um botão-pílula alternável: "Titular" (`bg-amber-50 text-amber-800 ring-amber-600/30`, ícone Star) ou "Membro" (`bg-white text-slate-500 ring-slate-300`)

## Ações
- "Novo ponto" (ícone Plus) → abre o modal vazio
- Clique na linha → abre o modal já preenchido (tooltip "Editar ponto")
- Marcar/desmarcar "Mostrar inativos"
- Dentro do modal: buscar e adicionar membros, alternar Titular/Membro, remover membro (ícone Trash2, aria-label "Remover {nome}"), "Cancelar", "Salvar"
- Botão "?" → artigo "ouvidoria", seção "gestao"

## Modais
- "Novo ponto de resposta" / "Editar ponto de resposta" (largura lg) — o título muda conforme seja criação ou edição; ao salvar notifica "Ponto criado." ou "Ponto atualizado."

## Textos literais do JSX
- Pontos de resposta
- Unidades, áreas centrais e unidades apuratórias que recebem e respondem manifestações.
- Novo ponto
- Mostrar inativos
- Nome
- Tipo
- Unidade
- Prazo próprio
- Padrão
- Membros
- membro
- membros
- · sem titular
- Em aberto
- Situação
- Ativo
- Inativo
- Editar ponto
- Nenhum ponto de resposta. Sem ele, não há para onde encaminhar.
- Novo ponto de resposta
- Editar ponto de resposta
- Unidade de saúde
- Área central (secretaria)
- Unidade apuratória (denúncias)
- Recebe denúncias habilitadas, na versão pseudonimizada.
- Uma unidade só pode ter um ponto.
- Opcional.
- Selecione
- Nenhuma
- Prazo próprio (dias)
- Em branco: usa a configuração geral por prioridade.
- Ativo (aparece para encaminhamento)
- Quem vê e responde o que for encaminhado a este ponto (precisa do módulo "Ouvidoria — ponto de resposta" no perfil). O titular é a referência para cobranças.
- Adicionar membro: nome ou CPF do usuário (mín. 2 caracteres)
- Buscar usuário para adicionar como membro
- Buscando…
- Nenhum usuário ativo encontrado.
- Nenhum membro. Sem membros, ninguém recebe o encaminhamento.
- Titular
- Membro
- Titular — clique para tornar membro comum
- Clique para tornar titular
- Cancelar
- Salvar
- Salvando…
- Informe o nome do ponto.
- Ponto do tipo Unidade precisa da unidade.
- Ponto criado.
- Ponto atualizado.
