export const meta = {
  name: 'regulacao-revisar-planos',
  description: 'Revisa os planos de SMSMais.Regulacao/ contra a transcricao (cobertura, contradicoes, consistencia) e escreve o relatorio em revisoes/',
  phases: [
    { title: 'Cobertura', detail: 'um agente por plano: requisitos cobertos, contradicoes com a transcricao, suposicoes nao marcadas' },
    { title: 'Contra-leitura', detail: 'tres agentes leem so a transcricao e listam requisitos; cruzam com o README' },
    { title: 'Consistencia', detail: 'nomes de tabelas, estados e modulos iguais entre os planos e os ADRs' },
    { title: 'Sintese', detail: 'consolida e grava revisoes/<data>-revisao-planos.md' },
  ],
}

// args esperado: { data: 'aaaa-mm-dd' } (Date.now() nao e permitido no script)
const data = (args && args.data) || 'sem-data'
const PASTA = 'C:/Projetos GIT/SMSMarica/SMSMais.Regulacao'
const TRANSCRICAO = `${PASTA}/descricao inicial.txt`
const README = `${PASTA}/README.md`

const PLANOS = [
  '01-catalogo-procedimentos-busca-semantica.md',
  '02-fluxo-abertura-solicitacao.md',
  '03-regras-elegibilidade-por-procedimento.md',
  '04-fila-pre-regulacao-e-agente-regulador.md',
  '05-vinculo-externo-e-notificacoes-por-unidade.md',
  '06-pendencias-pos-envio.md',
  '07-credenciais-pessoais-por-usuario.md',
  '08-paridade-ser-sernit.md',
  '09-configuracoes-do-modulo.md',
  '10-paciente-no-fluxo.md',
  '11-escrita-sisreg-inclusao.md',
  '12-escrita-ser-sernit.md',
  '13-spikes-de-laboratorio.md',
  'adr/0052-fila-pre-regulacao-e-agente-regulador.md',
  'adr/0053-cofre-de-credenciais-pessoais.md',
  'adr/0054-escrita-por-robo-nos-sistemas-de-regulacao.md',
  'adr/0055-catalogo-canonico-e-embeddings.md',
]

const ACHADOS = {
  type: 'object',
  properties: {
    plano: { type: 'string' },
    requisitosCobertos: { type: 'array', items: { type: 'string' } },
    contradicoes: { type: 'array', items: { type: 'object', properties: { trecho: { type: 'string' }, audio: { type: 'string' }, gravidade: { type: 'string' } }, required: ['trecho', 'audio', 'gravidade'] } },
    suposicoesNaoMarcadas: { type: 'array', items: { type: 'string' } },
    lacunas: { type: 'array', items: { type: 'string' } },
  },
  required: ['plano', 'requisitosCobertos', 'contradicoes', 'suposicoesNaoMarcadas', 'lacunas'],
}

const REQUISITOS = {
  type: 'object',
  properties: {
    requisitos: { type: 'array', items: { type: 'object', properties: { texto: { type: 'string' }, audio: { type: 'string' }, coberto: { type: 'boolean' }, onde: { type: 'string' } }, required: ['texto', 'audio', 'coberto'] } },
  },
  required: ['requisitos'],
}

const CONSISTENCIA = {
  type: 'object',
  properties: {
    divergencias: { type: 'array', items: { type: 'object', properties: { termo: { type: 'string' }, variantes: { type: 'array', items: { type: 'string' } }, arquivos: { type: 'array', items: { type: 'string' } } }, required: ['termo', 'variantes', 'arquivos'] } },
  },
  required: ['divergencias'],
}

phase('Cobertura')
log(`Revisando ${PLANOS.length} documentos contra a transcricao`)
const cobertura = await pipeline(
  PLANOS,
  (p) => agent(
    `Leia a transcricao em "${TRANSCRICAO}" e o documento "${PASTA}/${p}". Leia tambem a secao 5 (decisoes D-1..D-9) de "${README}": uma decisao registrada la NAO e contradicao, mesmo que difira do audio.
Responda em pt-BR. Liste: (1) os requisitos R-nn que o documento cobre de fato (nao so cita); (2) contradicoes reais entre o documento e a transcricao, com o trecho do audio e a gravidade (alta = muda o que sera construido; media = muda tela/regra; baixa = redacao); (3) suposicoes que o documento faz sem marcar como "a confirmar"; (4) lacunas: coisas que a transcricao pede no escopo desse documento e ele nao trata. Seja concreto; nada de elogio.`,
    { label: `cobertura:${p}`, phase: 'Cobertura', schema: ACHADOS },
  ),
)

phase('Contra-leitura')
const lentes = ['fluxo do solicitante na ponta', 'trabalho do agente regulador e das integracoes', 'seguranca, credenciais e configuracoes']
const contra = await parallel(lentes.map((lente, i) => () => agent(
  `Leia SOMENTE a transcricao em "${TRANSCRICAO}" com a lente "${lente}" e extraia todos os requisitos que ela contem nessa lente, um por linha, com o numero do audio. Depois abra "${README}" e, para cada requisito, diga se a matriz da secao 8 e os planos listados o cobrem (coberto=true/false, onde). Nao invente requisitos que o audio nao pede. Responda em pt-BR.`,
  { label: `contra-leitura:${i + 1}`, phase: 'Contra-leitura', schema: REQUISITOS },
)))

phase('Consistencia')
const consistencia = await agent(
  `Abra todos estes arquivos: ${PLANOS.map((p) => `"${PASTA}/${p}"`).join(', ')} e "${README}". Procure o mesmo conceito escrito de formas diferentes: nomes de tabela (prefixo regulacao_), nomes de estado (ex.: PendenteRegulacao), nomes de modulo/permissao (47/48/51 e seus nomes), nomes de servico e endpoint, numeros de tarefa (ex.: 3.7) entre PROGRESSO.md ("${PASTA}/PROGRESSO.md") e os planos. Liste cada divergencia com as variantes e os arquivos. Responda em pt-BR.`,
  { label: 'consistencia', phase: 'Consistencia', schema: CONSISTENCIA },
)

phase('Sintese')
const orfaos = contra.filter(Boolean).flatMap((r) => r.requisitos).filter((r) => !r.coberto)
const contradicoesAltas = cobertura.filter(Boolean).flatMap((c) => c.contradicoes.map((x) => ({ plano: c.plano, ...x }))).filter((x) => /alta/i.test(x.gravidade))
log(`${orfaos.length} requisitos sem cobertura; ${contradicoesAltas.length} contradicoes de gravidade alta; ${(consistencia && consistencia.divergencias.length) || 0} divergencias de nome`)

const relatorio = await agent(
  `Escreva o arquivo "${PASTA}/revisoes/${data}-revisao-planos.md" em pt-BR, formato markdown, com as secoes: 1. Resumo (numeros); 2. Requisitos sem cobertura (tabela: requisito, audio, sugestao de plano); 3. Contradicoes por gravidade (tabela: plano, trecho, audio, gravidade, correcao sugerida); 4. Suposicoes nao marcadas (por plano); 5. Lacunas (por plano); 6. Divergencias de nome (tabela); 7. Lista de correcoes a aplicar, em ordem, cada uma apontando arquivo e secao. Nao aplique nenhuma correcao nos planos: so escreva o relatorio. Dados:
COBERTURA=${JSON.stringify(cobertura.filter(Boolean))}
ORFAOS=${JSON.stringify(orfaos)}
CONSISTENCIA=${JSON.stringify(consistencia)}`,
  { label: 'relatorio', phase: 'Sintese' },
)

return { orfaos: orfaos.length, contradicoesAltas: contradicoesAltas.length, divergencias: (consistencia && consistencia.divergencias.length) || 0, relatorio: `${PASTA}/revisoes/${data}-revisao-planos.md`, resumo: relatorio }
