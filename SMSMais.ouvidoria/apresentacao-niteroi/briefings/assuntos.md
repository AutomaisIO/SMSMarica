# Briefing da tela: Assuntos e marcadores

Rota: /app/ouvidoria/assuntos

## Propósito
Catálogo da ouvidoria: assuntos em dois níveis (com código OuvidorSUS e ordem) e marcadores livres — é o que aparece na triagem e no registro.

## Layout (descrição fiel do código)
Container `space-y-4`. (1) CABEÇALHO sem botão à direita: `h1` com ícone `Tags h-5 w-5 text-red-600` + "Assuntos e marcadores", "?" do manual (secao="gestao") e subtítulo. (2) <Tabs> com DUAS abas, sem contador: "Assuntos" e "Marcadores" (aba ativa em `border-red-600 text-red-700`). (3a) ABA ASSUNTOS: linha superior `flex flex-wrap items-center justify-between gap-3` com o checkbox "Mostrar inativos" à esquerda e o botão primário pequeno com ícone Plus "Novo assunto" à direita; depois uma LISTA (não é tabela): `ul.divide-y.divide-slate-100.rounded-xl.border.border-slate-200.bg-white` em que cada item `p-3` traz o assunto-pai em `font-medium text-slate-800`, ao lado o código em pílula mono `rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[11px] text-slate-600` prefixado por "OuvidorSUS ", o StatusBadge Ativo/Inativo, e à direita dois botões ghost pequenos: "Subassunto" (Plus, title "Adicionar subassunto") e o lápis (Pencil, aria-label "Editar {nome}"); abaixo, recuada em `pl-4`, a lista de subassuntos, cada um com o chevron `ChevronRight h-3.5 w-3.5 text-slate-300`, o nome em `text-slate-700`, o código em pílula mono (sem o prefixo "OuvidorSUS") e, quando inativo, o badge "Inativo"; à direita, o lápis. Vazio: "Nenhum assunto cadastrado.". (3b) ABA MARCADORES: linha com o texto explicativo à esquerda e o botão pequeno "Novo marcador" à direita; depois uma nuvem de pílulas `ul.flex.flex-wrap.gap-2` — cada marcador é um botão `rounded-full border px-3 py-1 text-sm` (ativo: `border-slate-300 bg-white text-slate-800`; inativo: `border-slate-200 bg-slate-50 text-slate-400 line-through`) com o nome e um lápis pequeno; ordenados com os ativos primeiro e depois por nome. Vazio: "Nenhum marcador.".

## Filtros
- Checkbox "Mostrar inativos" (só na aba Assuntos)
- Abas: "Assuntos" e "Marcadores"

## Campos / cartões
- MODAL DE ASSUNTO (largura sm): "Assunto pai" (Select id=as-pai; opção "— (primeiro nível)" + os assuntos raiz ordenados; fica disabled quando o assunto já tem subassuntos; dica "Tem subassuntos, então fica no primeiro nível." ou "Em branco = assunto de primeiro nível."); "Nome" * (Input id=as-nome, maxLength 200, autoFocus); grade 2 colunas com "Código OuvidorSUS" (Input id=as-codigo, maxLength 20; dica "Opcional; para exportação futura.") e "Ordem" (Input number min 0, id=as-ordem; dica "Menor aparece primeiro."); checkbox "Ativo (aparece no registro)"; botões "Cancelar" e "Salvar"
- MODAL DE MARCADOR (largura sm): "Nome" * (Input id=mk-nome, maxLength 80, autoFocus); checkbox "Ativo"; botões "Cancelar" e "Salvar"

## Badges e chips
- Código OuvidorSUS: pílula mono cinza — no primeiro nível vem como "OuvidorSUS {código}"; no subassunto, só o código
- StatusBadge "Ativo" / "Inativo"
- Marcador: pílula-botão arredondada; inativo fica riscado (line-through) e cinza

## Ações
- "Novo assunto" (Plus) → modal "Novo assunto"
- "Subassunto" (Plus, em cada assunto-pai) → modal "Novo subassunto" com o pai já escolhido
- Lápis (Pencil) em assunto ou subassunto → modal "Editar assunto"
- "Novo marcador" (Plus) → modal "Novo marcador"
- Clique numa pílula de marcador → modal "Editar marcador"
- Marcar "Mostrar inativos"
- Botão "?" → artigo "ouvidoria", seção "gestao"

## Modais
- "Novo assunto" / "Novo subassunto" / "Editar assunto" (sm) — notifica "Assunto criado." ou "Assunto atualizado."
- "Novo marcador" / "Editar marcador" (sm) — notifica "Marcador criado." ou "Marcador atualizado."

## Textos literais do JSX
- Assuntos e marcadores
- Como as manifestações são classificadas. Assunto tem dois níveis; marcador é etiqueta livre.
- Assuntos
- Marcadores
- Mostrar inativos
- Novo assunto
- Novo subassunto
- Editar assunto
- Adicionar subassunto
- Subassunto
- OuvidorSUS
- Ativo
- Inativo
- Nenhum assunto cadastrado.
- Carregando…
- Assunto pai
- — (primeiro nível)
- Tem subassuntos, então fica no primeiro nível.
- Em branco = assunto de primeiro nível.
- Nome
- Código OuvidorSUS
- Opcional; para exportação futura.
- Ordem
- Menor aparece primeiro.
- Ativo (aparece no registro)
- Cancelar
- Salvar
- Salvando…
- Informe o nome.
- Assunto criado.
- Assunto atualizado.
- Etiquetas livres para agrupar manifestações (ex.: "Imprensa", "Conselho de Saúde").
- Novo marcador
- Editar marcador
- Nenhum marcador.
- Marcador criado.
- Marcador atualizado.
