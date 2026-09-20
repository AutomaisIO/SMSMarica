---
name: confrontar-manual
description: Confronta qualquer mudança feita no SMSMarica com o Manual do usuário (SMSMais.front/src/features/manual) — atualiza o artigo da tela afetada no MESMO commit, ou PROPÕE criar o artigo quando a tela ainda não está documentada. Use SEMPRE que terminar uma mudança que altere o que o usuário vê ou faz (tela nova, campo novo, botão, aba, fluxo, regra de negócio visível, permissão, rótulo), quando o usuário disser "atualiza o manual", "isso está no manual?", "documenta essa tela", e antes de commitar/deployar mudança de front ou de regra visível do back. Aplicação automática — não espere ser chamada.
---

# Confrontar a mudança com o Manual

O manual do usuário vive **dentro do código** (`SMSMais.front/src/features/manual/`) por um motivo
só: para mudar no mesmo commit que muda a tela. Documentação que mora longe descola — e manual
descolado é pior que manual nenhum, porque ensina errado com ar de autoridade.

Por isso a regra do Bernardo: **de SMSMarica para dentro, toda mudança é confrontada com o manual.**

## Quando isto vale

Vale quando a mudança altera **o que a pessoa vê ou faz**:

- tela nova, aba nova, card, botão, modal, filtro, coluna;
- rótulo, texto de ajuda, mensagem de erro que a pessoa lê;
- regra de negócio visível (o que entra em cada fila, o que um desfecho provoca, prazo, limite);
- permissão/módulo novo (muda quem enxerga o quê);
- integração cujo comportamento a pessoa precisa entender (ex.: "cancelar aqui não cancela no SISREG").

**Não** vale para refactor puro, renome interno, performance, teste, migration sem efeito visível.
Nesses casos, diga em uma linha "sem efeito no manual" e siga.

## O procedimento

1. **Descubra a(s) tela(s) afetadas.** Pela rota (`/app/...`) ou pelo item de menu em
   `SMSMais.front/src/app/layout/menuConfig.ts`.

2. **Veja se já existe artigo.** Abra `SMSMais.front/src/features/manual/registro.ts` (lista
   `ARTIGOS`) e procure a `rota` correspondente — é o mesmo casamento que o "?" da tela usa
   (`artigoDaRota`).

3. **Se existe artigo:**
   - edite a(s) seção(ões) afetadas em `src/features/manual/conteudo/<slug>.tsx`;
   - se a mudança cria conceito novo, acrescente uma **seção** (id novo, estável) em vez de enfiar
     num parágrafo existente;
   - atualize `palavrasChave` e o `busca` da seção se entraram termos novos — o índice de busca só
     acha o que está **declarado**, o texto do JSX não é indexável;
   - **suba `atualizadoEm`** para a data de hoje;
   - se a simulação da tela ficou mentindo (botão que não existe mais, fila que mudou de regra),
     **corrija a simulação** — simulação errada é pior que texto errado;
   - tudo no **mesmo commit** da mudança de código.

4. **Se NÃO existe artigo:** **proponha criar**, sem pedir licença para propor. Diga qual tela está
   sem documentação e ofereça escrever o artigo agora, com o esqueleto padrão (ver abaixo). Se o
   Bernardo não quiser agora, registre a dívida — um ticket pela skill `criar-ticket`, tipo
   Sugestão, com o título `Manual: documentar <tela>` — para não virar pendência invisível.

5. **Verifique o gate:** `npm run build` no `SMSMais.front` (é `tsc -b`; ver
   `reference_front_build_gate_tsc_b`). O manual é código e quebra o build como qualquer outro.

## Esqueleto de um artigo novo

Crie `src/features/manual/conteudo/<slug>.tsx` exportando um `Artigo` e registre em `ARTIGOS`:

```tsx
export const artigoX: Artigo = {
  slug: 'x',
  titulo: '...',
  resumo: '...',              // uma frase — aparece no índice e na busca
  grupo: 'atendimento',       // ver GRUPOS em registro.ts
  icone: IconeDoMenu,         // o MESMO ícone do item de menu
  rota: '/app/x',             // é o que liga o "?" da tela ao artigo
  publico: 'Quem ...',
  atualizadoEm: 'AAAA-MM-DD',
  palavrasChave: ['...'],
  secoes: () => [ { id: 'para-que-serve', titulo: '...', busca: '...', conteudo: <>...</> } ],
};
```

Depois, ponha o `?` no título da tela:

```tsx
import { AjudaManual } from '@/shared/ui/AjudaManual';
<h1 ...>Título</h1>
<AjudaManual artigo="x" />
```

O `AjudaManual` **some sozinho** quando o slug não existe no registro — um "?" que leva a lugar
nenhum ensina a pessoa a não clicar no "?".

## Como escrever (o padrão do artigo de Confirmações)

- **Ordem pela dúvida de quem aprende**, não pela arrumação da tela: de onde vem isso → o que são as
  partes → como se lê → o que fazer → os casos chatos → quem pode o quê → dúvidas frequentes.
- **Diga o porquê**, não só o passo. "Motivo obrigatório" é regra; "é o que responde, semanas depois,
  por que a vaga foi cancelada" é o que faz a pessoa preencher direito.
- **Simulação quando houver sequência de decisões** (`components/TelaSimulada` + um componente em
  `simulacoes/`): dados inventados, CPF inválido, nome com "(exemplo)", e nada que chame API.
- **Identidade visual do tenant**: só `primary-*`/`secondary-*` e variáveis `--theme-*`. Nunca
  `red-*` fixo, nunca o nome de um município no texto — use `instituicao()` (ADR-0043).
- **Sem dado de paciente real**, em hipótese alguma — nem em print, nem em exemplo.

## O que NÃO fazer

- Não escrever o manual num `.md` solto, num Word ou no banco. Ele é código.
- Não documentar o que você ainda não conferiu no código (o manual é lido como verdade).
- Não deixar `atualizadoEm` velho depois de mexer no conteúdo — é o que diz ao leitor se pode confiar.
