# Painel da Saúde — front (secretario.smsmarica.online)

Painel executivo público (sem autenticação) do Secretário de Saúde: números ao vivo do
Hospital Municipal Conde Modesto Leal, vindos do Oracle do Salux HIS via `GET /api/painel`.

Vite + React 18 + TypeScript · Tailwind 3.4 · recharts 2.13 · vite-plugin-pwa (autoUpdate)
· fontes self-hosted (@fontsource: Archivo para display, Instrument Sans para corpo).

## Rodar

```bash
npm install
npm run dev        # http://localhost:5175 — proxy /api → http://localhost:5090 (sem rewrite)
npm run build      # tsc -b && vite build (deve passar limpo)
npm run preview    # serve o dist/
```

Em produção o fetch é same-origin (`/api/painel` via nginx).

## Mock / demonstração

O contrato real (`docs/contrato-painel.json`) vive em `src/mock/painel.json` e entra:

- automaticamente em **DEV** quando o fetch falha (back fora do ar) — nunca por cima de
  dados reais já recebidos;
- em qualquer ambiente com **`?mock=1`** na URL.

Com o mock ativo os carimbos de atualização são rebatidos para "agora" (para o layout
demonstrar coerente) e a UI exibe o selo **"dados de exemplo"** ao lado do pill de
frescor — o painel nunca finge frescor de dado real.

## Frescor (nunca fingido)

`geradoEm` < 3 min e `oracle.ok` → pill "ao vivo" com ponto pulsante; entre 3 e 15 min →
pill âmbar "dados de HH:mm"; acima disso ou `oracle.ok=false` → "reconectando ao Salux…".
Falha de rede com dados antigos na tela → faixa "Sem conexão com o Salux — mostrando
dados de HH:mm". Poll a cada 60 s + refetch em `visibilitychange`.

## Decisões

- **Assinatura visual**: a seção "Tempo até o atendimento médico" renderiza cada cor de
  classificação como uma **pulseira de hospital** (banda de raio alto, ponta na cor com 3
  furinhos de snap, tempo médio grande, barra fina tempo × meta com tick na meta; o
  trecho que excede a meta usa o tom forte da cor). Complemento de identidade: filete de
  **ECG** (1.5px, vermelho-marica) atravessa o header — anima contínuo, pausa quando os
  dados não estão "ao vivo" e vira estático com `prefers-reduced-motion`.
- **Internações empilhadas**: o contrato atual só traz o total por dia na `serieDiaria`
  (sem split diário urgência/eletiva) — inventar a divisão está fora de questão. O
  gráfico empilha automaticamente (vinho × neutro-serie, com legenda) se o back passar a
  mandar `urgencia`/`eletiva` por ponto; até lá o split aparece nos cartões de mês e no
  tile "Internações hoje", com barra proporcional + legenda.
- **Regras de gráfico**: um eixo y por gráfico, baseline 0, grid horizontal recessivo
  (#EDF0F3), texto sempre em tinta/grafite, tooltip custom em papel, rótulo direto só em
  hoje e no máximo, legenda só com ≥ 2 séries, números `Intl.NumberFormat('pt-BR')`.
- **Micro-interações**: count-up ~600 ms uma vez por mudança real, hover de cartão com
  borda vermelho-marica suave, skeleton shimmer na primeira carga — tudo desligado com
  `prefers-reduced-motion`.
- **PWA**: precache só de estáticos (`js/css/html/png/woff2`); `/api` está no
  `navigateFallbackDenylist` e **não tem runtime caching** — dado clínico-operacional é
  sempre rede.

## Validação da paleta (dataviz `validate_palette.js`, modo light)

Cores de triagem Manchester (entidade = cor, **sempre com o nome escrito ao lado —
nunca cor sozinha**):

- `#D62828` vermelho · `#E9A400` amarelo · `#2E9E5B` verde · `#2F6FDE` azul →
  **ALL CHECKS PASS** (pior par adjacente verde↔amarelo ΔE CVD 10.1 protan / 6.5 tritan;
  visão normal 23.3). O tritan 6.5 fica na banda 6–8, aceitável aqui porque toda cor
  carrega o nome escrito. WARN de contraste do amarelo vs superfície (2.09:1) —
  mitigado pelos rótulos de texto permanentes.
- Cinza "sem classificação": `#8B94A0` do brief reprovou o chroma floor (0.021 < 0.10) e
  beirava o contraste (2.99:1). Ajuste mínimo para `#8494A8` (chroma 0.035, contraste
  ≥ 3:1, ΔE ≥ 17.7 vs azul; demais checagens PASS). O chroma floor (0.10) fica
  **dispensado por design** nesse slot: o cinza codifica a *ausência* de cor de
  classificação — forçá-lo a chroma ≥ 0.10 o tornaria um azul de verdade e mentiria o
  dado. Nunca aparece sem o rótulo "Sem classificação".
- Séries de gráfico `#9E1B32` (vinho, principal) × `#C9CED6` (neutro, comparação):
  separação excelente (ΔE CVD 38.7 / normal 42.9). O neutro claro fica fora da banda de
  lightness/chroma por design — é série de comparação recessiva, sempre com legenda e
  valores escritos.

## Estrutura

```
src/
  types/painel.ts        # types do contrato (docs/contrato-painel.json)
  mock/painel.json       # cópia do contrato p/ demo
  lib/                   # usePainel (poll/fallback), formatos pt-BR, triagem, relógio
  components/            # Header, FileteEcg, PillFrescor, Pulseira, gráficos, tiles…
  sections/              # Agora, Emergência (pulseiras), Atendimentos, Internações, rodapé
```
