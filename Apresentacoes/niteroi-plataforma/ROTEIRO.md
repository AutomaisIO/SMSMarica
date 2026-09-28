# Roteiro — SMSMais, a plataforma · Niterói (FMS)

Segundo material para a **Fundação Municipal de Saúde de Niterói**, depois do deck do módulo de
Ouvidoria. O cliente pediu para conhecer **a plataforma inteira**.

**Entrega:** dois decks, mesma identidade visual de Niterói e mesmo pipeline
(`../../SMSMais.ouvidoria/apresentacao-niteroi/`).

| | Deck | Slides | Arquivo |
|---|---|---|---|
| **A** | Executivo — a reunião, 25–30 min | **22** | `SMSMais - A Plataforma (executivo).pdf` |
| **B** | Anexo — deixar com a equipe | **54** | `SMSMais - A Plataforma (anexo tecnico).pdf` |

O deck B **contém** o A: os 22 slides do executivo são os mesmos arquivos. `ORDEM-a.txt` e
`ORDEM-b.txt` montam os dois cortes a partir de `slides/` — nenhum slide escrito duas vezes, e uma
correção conserta os dois.

```bash
python montar.py ORDEM-a.txt apresentacao.html && node render.mjs apresentacao.html "SMSMais - A Plataforma (executivo).pdf"
python montar.py ORDEM-b.txt anexo.html        && node render.mjs anexo.html        "SMSMais - A Plataforma (anexo tecnico).pdf"
```

**Os 32 slides do anexo são compostos à mão, cada um com a gramática que o conteúdo pede** — não
por template. A primeira versão (24/09) era gerada por template e saiu como 32 cartões iguais de
texto; o cliente rejeitou ("um monte de quadradinho, chato"). A segunda versão usa um repertório:

| Gramática | Classe | Onde |
|---|---|---|
| Trilho com nós numerados, bonecos nas estações e **seta de retorno** | `.trilho-nos` + `.retorno` | ciclo da solicitação, entrega do laudo, prontuários, ouvidoria, erros |
| Convergência (N origens → 1 resultado, feixe em SVG) | `.converge` | de onde vem o pedido, três filas, cinco registros → uma pessoa |
| Barra proporcional "de cada 100" com consequências | `.proporcao` | confirmações, qualidade da importação |
| Diagrama de decisão (losango, SIM/NÃO) | `.decisao` | robô, alterações de agenda, destinatário LGPD |
| Série em área (SVG) | `.serie` | mensagens por dia, simulação da fila |
| Barras duplas oferta × uso | `.duplas` | vagas por especialidade |
| Ranking horizontal / rosca / KPI | `.ranking` `.rosca` `.painel-kpi` | estatísticas, mensageria, satisfação, acervo, operadores |
| Mini-tela (recorte de interface) | `.mini-tela` | lista de trabalho, central de atendimento, trilha, manual, instituição |
| Matriz perfil × módulo | `.matriz` | permissões |
| Antes/depois · linha do tempo · número hero · checks | `.antes-depois` `.linha-tempo` `.numerao` `.checks` | modelos de laudo, correção de identidade, catálogo, roteiro |
| **Boneco** com balão de fala, no conteúdo | `.com-boneco` `.boneco` `.balao-fala` | em ~12 slides |

Helpers em `gramatica.py`; conteúdo em `anexo_v1.py` (exames, regulação) e `anexo_v2.py` (cidadão,
dado, gestão, casa). Regenerar:

```bash
python anexo_v1.py && python anexo_v2.py
```

**Bonecos** (`assets/bonecos/`, ~25 KB cada): personagem 3D estilo Pixar, corpo inteiro, **fundo
branco puro** — como o slide é branco, integra sem recorte. Cinco no elenco: recepcionista, cidadã,
médico, gestora, atendente. Faltam quatro (técnica de imagem, motorista, paciente homem, mãe com
criança): o Artlist desconectou na troca de conta em 24/09 e não voltou; ao reconectar (`/mcp`),
gerar com o prompt-base *"Pixar-style 3D animated character, full body, isolated on a pure flat
white background with no floor and no shadow… clean vector-like 3D render"*, 3:4, modelo 2251.
Um prompt com "elderly woman… relief" foi bloqueado pelo filtro do modelo; reescrito sem o
adjetivo, passou.

**Armadilhas desta rodada:** `span` não aceita `width` sem `display:block` (barras do ranking
saíram invisíveis); `.trilho` colidiu com a barrinha do ranking (renomeado para `.trilho-nos`);
SVG com viewBox alto define a altura da linha do grid (feixe da convergência usa viewBox 70×100 e
`preserveAspectRatio="none"`); balão de fala a `top:-92px` de um boneco encostado no rodapé cai
sobre a logo — o boneco fica mais baixo que a linha (`height` menor + `align-self:flex-end`).

**Numeração das partes difere entre os decks** — o B tem uma parte a mais ("O dado por baixo"), então
usa divisores próprios: `b39-secao-dado` (Parte 4) e `b70-secao-casa` (Parte 5), enquanto o A usa
`18-secao-casa` (Parte 4). Conferir com:

```bash
for f in $(grep -v '^#' ORDEM-b.txt | grep secao); do grep -o '<p class="numero">[^<]*' "slides/$f"; done
```

---

## O tom — a regra deste deck

**Mostrar o que o sistema faz. Não como ele faz.**

O cliente é a secretaria, não a área de TI. O deck fala em serviço prestado, não em arquitetura:

| Não escrever | Escrever |
|---|---|
| Hub FHIR R4 com identidade por CPF/CNS | Um cadastro só de cidadão |
| Worklist DICOM por AE Title | O aparelho recebe a lista de quem vai atender |
| Conciliação determinística por accession | O exame já sai vinculado ao pedido |
| Assinatura PAdES / ICP-Brasil | Assinado com o certificado do médico, com validade jurídica |
| Estratégia de importação plugável por PEP | O histórico dos sistemas de hoje é lido e reunido |
| Extensão CDP que observa a sessão | *(não entra — vira uma linha em "a integração é parte da entrega")* |

**A integração é uma afirmação, não uma explicação.** Um slide diz com o que funciona, e pronto.
Ninguém na sala quer saber se é API, banco ou tela — quer saber que funciona e que não vai virar
uma conta à parte.

Os padrões técnicos (FHIR, DICOM, SIGTAP, ICP-Brasil) aparecem **só na capa**, como credencial.

**Sem números operacionais** — nem de Maricá, nem de Niterói. Número de outra prefeitura não prova
nada para esta, e de Niterói não temos levantamento. As telas levam **cenário fictício coerente**,
com nomes de unidade reais da rede da FMS e nenhum dado de paciente real.

**Sobre o SERNIT:** é da casa deles. Aparece na lista do slide de integração como qualquer outro
sistema, sem alarde e sem insinuar substituição.

---

## Deck A — a reunião (~21 slides)

| # | Slide | O que diz | Estado |
|---|---|---|---|
| 1 | **Capa** | "A plataforma SMSMais" | ✅ pronto |
| 2 | **O que o SMSMais faz** | As seis frentes: regulação e fila · agenda · exames e laudos · contato com o cidadão · transporte de pacientes · gestão da secretaria | ✅ pronto |
| 3 | **Funciona com o que vocês já têm** | Com o que integra, em seis caixas. Faixa: *a integração é parte da entrega* | ✅ pronto |
| 4 | *Parte 1 — Atender e resolver* | Divisor | |
| 5 | **Do pedido ao laudo no celular do paciente** | Os cinco passos do exame de imagem | ✅ pronto |
| 6 | **O médico lauda aqui** | 🖥️ TELA — laudo com a imagem ao lado | |
| 7 | **A fila num lugar só** | 🖥️ TELA — regulação: quem pediu, para quem foi, há quanto tempo espera | |
| 8 | **Onde sobra e onde falta** | Agenda: vagas oferecidas × ocupadas, por unidade e por especialidade | |
| 9 | **Transporte de pacientes** | Tratamento, rota do dia, assento, frota no mapa, custo para faturar | |
| 10 | *Parte 2 — Falar com o cidadão* | Divisor | |
| 11 | **O app do cidadão** | O que ele vê: consultas, exames, resultados, transporte | |
| 12 | **Avisar, confirmar, atender** | 🖥️ TELA — WhatsApp oficial: confirmação antes do dia, atendimento humano e automático | ✅ pronto |
| 12b | **Cada login é um ramal** | 🖥️ TELA — telefonia: ramal por login, chamada gravada e transcrita no cadastro. **Marcado "No roteiro"** | ✅ pronto |
| 13 | **Ouvidoria** | O módulo que já conhecem, no lugar dele dentro da plataforma | |
| 14 | *Parte 3 — Enxergar a secretaria* | Divisor | |
| 15 | **Pergunte em português** | 🖥️ TELA — pergunta em linguagem natural, resposta com número e gráfico | |
| 16 | **Indicadores e metas** | Apurados sozinhos, por unidade e por período; relatório pronto | |
| 17 | **Quem fez o quê** | Registro de cada ação, erros do sistema com código, canal de suporte | |
| 18 | *Parte 4 — Feito para a sua casa* | Divisor | |
| 19 | **A sua marca, as suas regras** | Instância própria; marca, cores, catálogos, prazos e permissões vêm do cadastro — não do código | |
| 20 | **Implantação** | As etapas, e o que é decisão da casa em vez de desenvolvimento | |
| 21 | **Fecho** | Assinatura e contato | |

---

## Deck B — o anexo (~55 slides)

Os slides do A, mais uma tela e um detalhamento por frente. Mesma ordem, mesmo tom — o anexo é
**mais telas**, não mais jargão.

### Exames e laudos (+9)
Abrir exame · visualizador · modelos de laudo · equipamentos · relatórios de imagem ·
o acervo antigo · cada unidade vê o que é dela · entrega e conferência do laudo · correção quando
o exame foi para o paciente errado

### Regulação e agenda (+9)
Fila da regulação · SISREG · SERNIT · nova solicitação · notificações · análise de vagas ·
alterações de agenda · simulação da oferta · produção por operador

### Cidadão (+8)
App (telas) · confirmação de consulta · central de atendimento · atendimento automático ·
mensagens enviadas e falhas · pendências de cadastro · pesquisa de satisfação · ouvidoria

### Cadastros e prontuário (+7)
Pacientes · unidades · profissionais · o cadastro único do cidadão · o histórico vindo dos
sistemas de hoje · qualidade do que entra · o que fazer com divergência

### Gestão (+7)
Indicadores · estatísticas de atendimento · produção por sistema · auditoria · erros · suporte ·
manual dentro do produto

### Customização e fecho (+6)
Perfis e permissões · identidade da instituição · manual · segurança e LGPD · o que está no
roteiro de evolução · ressalva honesta (não houve levantamento de campo em Niterói; os números das
telas são cenário)

---

## Telefonia — o único slide de roteiro (23/09/2026)

O deck tem **um** slide sobre recurso que ainda não existe, e ele carrega selo **"No roteiro"**.
Pedido do cliente.

**O que sustenta o slide, e é real:** a telefonia IP das unidades já opera — 31 unidades no
`Telefonia/registro/unidades.csv`, VPN WireGuard por unidade sem NAT (o IP `10.200.<id>.x` diz de
qual unidade é cada aparelho), Asterisk central e o **Automais.Pabx**, serviço .NET com CRUD de
ramais, provisionamento dos telefones, status por AMI e leitura do CDR. O próprio
`Telefonia/PABX/README.md` já chama o CDR de *"alicerce do futuro histórico de chamadas por
paciente"* — ou seja, o recurso estava previsto antes de ser pedido.

**O que é roteiro:** o ramal amarrado ao login do SMSMais, e a gravação com transcrição dentro do
cadastro do paciente.

**Se o cliente preferir sem o selo**, é uma linha no `slides/12b-telefonia.html` — mas aí o slide
passa a afirmar algo que não roda, e isso contradiz a régua que o resto do deck segue. A
recomendação é manter: dizer "já operamos a telefonia de 31 unidades, falta amarrar ao cadastro" é
mais forte do que fingir que está pronto, e é verificável.

**Coerência de cenário:** a ligação da Terezinha cita a consulta de cardiologia de 12/10 às 14h20
na Policlínica do Centro, e menciona que ela confirmou pelo WhatsApp — os mesmos dados do slide de
WhatsApp e da fila da regulação. Se algum desses mudar, os três mudam juntos.

---

## Direção visual (23/09/2026)

O deck deixou de ser texto em caixas. Três camadas:

**1. Cenas geradas no Artlist** — personagens 3D estilo Pixar, paleta quente casando com o laranja
de Niterói. Em `assets/cenas/`, otimizadas de ~6 MB para ~200 KB cada:

| Arquivo | Cena | Onde |
|---|---|---|
| `capa.jpg` | Fachada de unidade, morros e baía | Capa e divisor da Parte 4 |
| `tecnica.jpg` | Técnica operando o aparelho | Divisor da Parte 1 |
| `medico.jpg` | Radiologista laudando | Slide do laudo |
| `transporte.jpg` | Motorista ajudando idosa na van | Slide de transporte |
| `cidada.jpg` | Cidadã com o celular na sala de espera | Slide do app |
| `atendente.jpg` | Atendente de headset | Divisor da Parte 2 e slide do WhatsApp |
| `gestora.jpg` | Gestora diante dos gráficos | Divisor da Parte 3 |
| `rx.jpg` | Radiografia de tórax PA | Dentro da tela de laudo |

Modelo **Nano Banana 2** (`modelId: 2251`), 130 créditos por imagem. Prefixo de estilo comum a
todos os prompts: *Pixar-style 3D animated film still, warm orange and cream palette, soft global
illumination, cinematic shallow depth of field, friendly rounded character design*.

**2. Simulações em HTML/CSS** — fiéis ao produto, porque imagem gerada de interface mente:
mockup de celular com o app, chat da Consulta Inteligente (com o passo de raciocínio, o bloco
`numero` e o gráfico, como o motor devolve), conversa de WhatsApp, faixa de indicadores e a tela
de laudo com assinatura.

**3. Composição** — quatro arranjos, alternados para o ritmo não cansar: divisor com foto de
página inteira e véu escuro; foto sangrando à direita com mockup sobreposto; foto à esquerda com
dois painéis ao lado; e slide de cartões, sem foto.

**Armadilhas que custaram tempo, anotadas para a próxima:**
- A foto entra **abaixo** da fita de marca e do rodapé (`z-index`), senão os dois somem.
- Com foto sangrando à esquerda, o crédito do rodapé cai sobre a imagem: usar `.rodape.so-num`.
- Coluna ao lado de foto é estreita — título longo quebra e o verificador acusa cabeçalho sobre o
  corpo. Encurtar o título, não empurrar o corpo.
- Radiografia em espaço vertical estreito fica irreconhecível: a tela de laudo **empilha** (imagem
  larga em cima, texto embaixo), e a imagem gerada foi recortada para tirar as tarjas pretas.
- Cuidado com contradição entre texto e layout: o card dizia "imagem ao lado do texto" depois que
  a imagem passou para cima.

---

## Produção

Pipeline herdado da Ouvidoria: `estilo.css` (design system + réplica do painel), `telas/_shell.html`,
`render.mjs` (falha com código 1 em transbordo, corte, cabeçalho sobre o corpo e imagem quebrada),
`assets/` (marca de Niterói, Automais, assinatura, fontes embutidas).

**Acréscimos já feitos ao `estilo.css`:** `.corpo.centro` (centraliza conjunto curto em vez de
esticar cada card). Cuidado com **colisão de nome de classe** — `.tag` já existe como badge das
telas do painel.

**Ainda por fazer:**

1. Reescrever `telas/_shell.html` — o menu lateral hoje é o da Ouvidoria e precisa mostrar as áreas
   da plataforma.
2. Levantar cada tela a partir do código real, como nos `briefings/` do deck anterior.
   Simulação desatualizada mente com ar de autoridade.
3. Definir o cenário fictício, coerente entre telas.
4. Revisão adversarial no fim.
