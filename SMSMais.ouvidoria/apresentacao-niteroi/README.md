# Apresentação — Módulo Ouvidoria · Niterói

Apresentação comercial/institucional do **módulo de Ouvidoria do SMSMais** ([ADR-0060](../../docs/adr/0060-modulo-ouvidoria.md))
para a **Fundação Municipal de Saúde de Niterói**, com a identidade visual oficial da Prefeitura de
Niterói.

**Entrega:** [`apresentacao.pdf`](./apresentacao.pdf) — 29 slides, 16:9 (960 × 540 pt = 1280 × 720 px),
texto vetorial (selecionável), ~5 MB.

> As telas do painel são **simulações fiéis ao código real** do módulo, re-tematizadas com a marca de
> Niterói. Os dados (protocolos, nomes de cidadão, números do painel) são **fictícios e coerentes entre
> si** — ver a seção *O CENÁRIO* em [`CONTRATO.md`](./CONTRATO.md). Os **nomes de unidade são reais**
> (rede publicada pela FMS). Nenhum dado de paciente real entrou aqui.

## Roteiro

| # | Slide | O que faz |
|---|---|---|
| 1 | Capa | |
| 2 | A Ouvidoria da FMS já existe | Os canais que a FMS publica hoje × o que um canal sozinho não entrega |
| 3 | O módulo em uma página | O ciclo em quatro passos + trilha, relógio e ligação |
| 4 | *Parte 1 — o que a lei já exige* | Divisor |
| 5 | **Em que isto foi baseado** | A norma, os sistemas de referência (OuvidorSUS, PN CGU 116/Fala.BR, ISO 10002) e as ouvidorias estudadas |
| 6 | Doze obrigações legais | Obrigação × norma × comportamento do sistema |
| 7 | Seis tipos oficiais | Tipologia + providência + peso real de cada tipo |
| 8 | Evidência | Números públicos de OuvSUS, Rio, BH, Recife, SP, ES |
| 9 | *Parte 2 — o caminho de uma manifestação* | Divisor |
| 10 | O ciclo de vida | 7 estados do caminho feliz + desvios + o que o sistema faz sozinho |
| 11 | Dois relógios | 30+30 do cidadão × 20/10/2 da área |
| 12 | Sigilo e LGPD | Três níveis de identidade + o trilho da denúncia + as 4 permissões |
| 13 | *Parte 3 — o sistema, tela a tela* | Divisor |
| 14–23 | **As telas** | Fila · Registrar · Detalhe · Responder · Meu ponto · **Painel** · Pontos de resposta · Assuntos · Configuração · Página do cidadão |
| 24 | *Parte 4 — por que um módulo da saúde* | Divisor |
| 25 | Seis diferenciais | O que um sistema genérico não tem como fazer |
| 26 | Indicadores | Fórmula, meta e referência de cada um + para quem vai o relatório |
| 27 | Instância própria | ADR-0043: isolamento físico, marca vinda do cadastro |
| 28 | Implantação | Seis semanas, e o que precisa ser **decidido** (não programado) |
| 29 | Fecho | Marca **Automais**, assinatura e contato de Bernardo Almeida |

## Como regenerar o PDF

Requer Node 22+, Python 3.11+ e Chrome instalado (`C:/Program Files/Google/Chrome/Application/chrome.exe`).

```bash
cd SMSMais.ouvidoria/apresentacao-niteroi
npm install                 # puppeteer-core (usa o Chrome já instalado; não baixa Chromium)

python gerar_slides.py      # slides narrativos, parte 1
python gerar_slides2.py     # parte 2
python gerar_slides3.py     # parte 4 + fecho
python gerar_fundamentos.py # slide "Em que isto foi baseado"
python gerar_telas.py       # as molduras dos slides de tela

python montar.py            # injeta o shell do painel nas telas e monta apresentacao.html
node render.mjs             # -> apresentacao.pdf + slides-png/slide-NN.png
```

`render.mjs` **falha com código 1** se encontrar problema. Ele verifica, slide a slide:

- página com dimensão diferente de 1280 × 720;
- qualquer elemento que ultrapasse a borda do slide (acima, abaixo, à esquerda ou à direita);
- conteúdo cortado por `overflow: hidden` — exceto truncagem proposital com reticências;
- **cabeçalho invadindo o corpo** (os dois são posicionados de forma absoluta e se sobrepõem em silêncio);
- imagem que não carregou.

Decorações que sangram de propósito pela borda levam a classe `.sangria` e são ignoradas.

### Trabalhando numa tela só

```bash
python provar.py painel     # monta um slide só com essa tela, verifica e mede
```

Imprime o relatório de transbordo e `usado` × `util`. A área útil do conteúdo do painel é
**1048 × 558 px** (o `.app` tem 1400 × 666 e é escalado por `0,83428` dentro da moldura).

## Estrutura

```
apresentacao.pdf          entrega
apresentacao.html         deck montado (intermediário)
base.html                 casca do HTML
estilo.css                design system do deck + réplica fiel do painel SMSMais
CONTRATO.md               geometria, biblioteca de ícones e O CENÁRIO (dados simulados)
ORDEM.txt                 ordem dos slides
slides/*.html             um arquivo por slide; {{TELA:nome}} injeta um painel
telas/_shell.html         sidebar + topbar do painel (injetado em toda tela)
telas/conteudo/*.html     o conteúdo de cada tela; *.meta define o item de menu ativo e o usuário
briefings/*.md            levantamento fiel de cada tela, extraído do código do front
assets/                   logos oficiais extraídos do manual da marca + fontes embutidas
dados/                    levantamento bruto do módulo (JSON) e o manual da marca em PDF
gerar_*.py, montar.py     geradores
render.mjs, medir*.mjs    renderização e verificação
```

## A identidade visual

Extraída do **manual da marca oficial** da Prefeitura de Niterói (`assets/marca-niteroi.pdf`, obtido
em `niteroi.rj.gov.br/manualdamarca`):

| | |
|---|---|
| Assinatura | `assets/niteroi-saude.png` — lockup **“Prefeitura de Niterói \| SAÚDE”**, recortado do manual em 400 dpi com fundo transparente |
| Laranja | `#EE7219` — cor dominante da marca (R238 G114 B25) |
| Apoio | azul `#1F7ABF` · verde `#0A9647` · amarelo `#F8C500` · vermelho `#C7191A` · preto `#1D1D1B` |
| Tipografia | manual especifica **DIN 2014 Narrow**; aqui usa-se **Barlow Condensed** (títulos) e **Inter** (corpo e UI, a fonte real do painel), ambas embutidas em base64 |
| Marca da casa | `assets/automais-offwhite.png` e `assets/assinatura-offwhite.png` — logo Automais e assinatura convertidas para off-white `#FBF7F2` com alfa preservado, para assentar sobre o laranja. Geradas a partir da skill `marcas-e-assinatura`. |

O painel aparece laranja porque o SMSMais é **whitelabel por configuração** (ADR-0043): a escala de
cor é derivada em runtime da `corPrimaria` cadastrada na instituição, tomando-a como passo 600 —
exatamente o que `shared/tema/paleta.ts` faz. Nenhuma linha de código muda para trocar de município.

## Achado para o produto — ticket #130

Montar as telas em laranja revelou um vazamento do whitelabel que **não é só da Ouvidoria**: o
vermelho de Maricá está escrito direto no código em `shared/ui/Tabs.tsx` (aba ativa) e
`shared/ui/Paginacao.tsx` (página atual), componentes usados por **21 arquivos em 14 módulos** —
Confirmações, Estratégias de fila, IA, Médicos, Mensageria, Motoristas, Ouvidoria, Pacientes,
Estatísticas da regulação, SER, SERNIT, Estatísticas do SISREG, Unidades e Usuários. Somam-se a
isso o ícone do título de cada página da Ouvidoria (`text-red-600`), as barras do gráfico do Painel
(`#b91c1c`) e o marcador selecionado na triagem: **123 ocorrências** de vermelho fixo em
`features/`.

Em Maricá ninguém percebe, porque a marca já é vermelha. Em qualquer outro município esses detalhes
aparecem em vermelho no meio da cor da prefeitura. Nesta apresentação foram tematizados à mão;
**no produto continuam como estão**. Registrado no módulo Suporte como **ticket #130**.


## Duas decisões de discurso

1. **A apresentação não diz "fase 1", "fase 2" nem cita a data de entrega do módulo.** O que está
   pronto aparece como *"o que está entregue e rodando"* e o que falta como *"no roteiro de
   evolução"* — o módulo não deve soar recém-nascido diante do cliente.
2. **Os números das telas são simulação coerente**, e isso está escrito no slide de implantação,
   junto com a ressalva de que não houve levantamento de campo em Niterói. Os **nomes de unidade
   são reais** (rede publicada pela FMS); os nomes de cidadão são fictícios.

## Verificação

Além do `render.mjs` (que falha se achar transbordo, corte, sobreposição de cabeçalho ou imagem
quebrada), a apresentação passou por uma revisão visual adversarial: seis agentes leram os 29 PNGs
procurando defeito, um sétimo procurou contradição entre slides, e cada achado foi submetido a um
verificador cético instruído a refutar. Dos 71 achados brutos, 27 foram confirmados e corrigidos —
entre eles: a vigência da Lei 13.460 em Niterói (art. 25, II → dezembro de 2018, não 2019), a
citação do art. 16 (o 30+30 é o *caput*; o § único é o 20+20 da área), o universo dos percentuais
do OuvSUS (436.004 sob gestão municipal, não os 618.727 do total do SUS) e a mesma manifestação
aparecendo em estados incompatíveis entre a fila e a página do cidadão.
