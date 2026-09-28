# Briefing da tela: Painel da ouvidoria

Rota: /app/ouvidoria/painel

## Propósito
Indicadores da ouvidoria no período escolhido: seis cartões numéricos e quatro gráficos de barras (por tipo, por status, assuntos mais frequentes e faixas de tempo até a resposta).

## Layout (descrição fiel do código)
Container `space-y-5`. (1) CABEÇALHO: `h1` com ícone `BarChart3 h-5 w-5 text-red-600` + "Painel da ouvidoria", "?" do manual (secao="gestao"), subtítulo `text-sm text-slate-500`. (2) FILTROS: `flex flex-wrap items-end gap-3` com três <Campo> rotulados (aqui o rótulo é VISÍVEL, diferente da fila): "De" (date, padrão = primeiro dia do mês corrente), "Até" (date, padrão = hoje) e "Unidade" (`w-64`, opção vazia "Todas as unidades"). (3) CARTÕES: grade responsiva `sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6` — seis cartões brancos `rounded-xl border border-slate-200 bg-white p-4 shadow-sm`, cada um com o rótulo em `text-xs font-medium uppercase tracking-wide text-slate-500`, o valor em `text-2xl font-semibold tabular-nums` (cinza-900 por padrão, `text-green-700` quando destaque bom, `text-red-700` quando ruim) e um detalhe opcional em `text-xs text-slate-500`. (4) GRÁFICOS: grade `lg:grid-cols-2` com quatro seções-cartão; cada uma com título `text-sm font-semibold text-slate-700` e um BarChart do Recharts com barras vermelhas `#b91c1c` de raio 3, grade tracejada `#e5e7eb`, eixos em fonte 11px, tooltip formatando o valor como "Manifestações" e cursor `#f8fafc`. "Por tipo" e "Por status" são verticais (altura 240px, rótulos do eixo X inclinados -20° quando há mais de 6 itens); "Assuntos mais frequentes (top 10)" é horizontal (layout vertical, eixo Y categórico com 160px de largura, altura = máx(200, n*32+40)); "Tempo até a resposta (faixas)" é vertical. Gráfico sem dados mostra "Sem dados no período." em `py-8 text-center text-sm text-slate-400`. Cada gráfico ainda carrega uma lista `sr-only` com "{rótulo}: {quantidade}" para leitores de tela.

## Filtros
- "De" (Input type=date, id="pn-de", max = valor de Até) — padrão: primeiro dia do mês corrente
- "Até" (Input type=date, id="pn-ate", min = De, max = hoje) — padrão: hoje
- "Unidade" (SeletorUnidade, id="pn-unidade", largura w-64) — opção vazia "Todas as unidades"

## Campos / cartões
- CARTÃO 1 "Registradas" — total do período
- CARTÃO 2 "Respondidas" — número; detalhe "{percentual} do total"
- CARTÃO 3 "No prazo" — percentual; detalhe "{n} no prazo · {n} fora"; cor verde/vermelha conforme o resultado
- CARTÃO 4 "Tempo médio de resposta" — "{n} d" (uma casa decimal) ou "—"; detalhe "área: {n} d"
- CARTÃO 5 "Estoque (em aberto)" — número
- CARTÃO 6 "Resolutividade" — percentual; detalhe "{n} resolvidas · {n} não"
- GRÁFICO 1 "Por tipo"
- GRÁFICO 2 "Por status"
- GRÁFICO 3 "Assuntos mais frequentes (top 10)" — horizontal, ordenado por quantidade decrescente
- GRÁFICO 4 "Tempo até a resposta (faixas)" — categorias "Até 30 dias", "31 a 60 dias", "Mais de 60 dias"

## Badges e chips
- Cartão "No prazo": o valor fica verde quando há mais no prazo do que fora, e vermelho quando o contrário

## Ações
- Mudar De / Até / Unidade recarrega os números e os gráficos
- Botão "?" → artigo "ouvidoria", seção "gestao"

## Textos literais do JSX
- Painel da ouvidoria
- Manifestações registradas no período, resposta, prazo e resolutividade.
- De
- Até
- Unidade
- Todas as unidades
- Registradas
- Respondidas
- do total
- No prazo
- no prazo ·
- fora
- Tempo médio de resposta
- área:
- Estoque (em aberto)
- Resolutividade
- resolvidas ·
- não
- Por tipo
- Por status
- Assuntos mais frequentes (top 10)
- Tempo até a resposta (faixas)
- Até 30 dias
- 31 a 60 dias
- Mais de 60 dias
- Sem dados no período.
- Manifestações
- Carregando…
