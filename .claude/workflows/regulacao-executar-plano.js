export const meta = {
  name: 'regulacao-executar-plano',
  description: 'Executa UMA tarefa de um plano de SMSMais.Regulacao/ com agentes (ler -> implementar -> verificar -> revisar) e escreve o relatorio em revisoes/. Nunca commita nem deploya.',
  phases: [
    { title: 'Ler', detail: 'extrai a tarefa do plano e do PROGRESSO.md' },
    { title: 'Implementar', detail: 'um agente implementa a tarefa seguindo a Especificacao para execucao' },
    { title: 'Verificar', detail: 'build do server e do front, testes do modulo' },
    { title: 'Revisar', detail: 'dois revisores: correcao e aderencia ao plano/CLAUDE.md' },
    { title: 'Relatar', detail: 'grava revisoes/<data>-execucao-<plano>-<tarefa>.md' },
  ],
}

// args esperado: { plano: '04', tarefa: '3.1', data: 'aaaa-mm-dd' }
// Uso previsto: sessoes curtas, uma tarefa por vez, com o PROGRESSO.md como memoria.
// Este workflow e opcional: a execucao manual (um modelo lendo PROGRESSO.md) e o caminho padrao.
if (!args || !args.plano || !args.tarefa) {
  throw new Error('Informe args = { plano: "NN", tarefa: "x.y", data: "aaaa-mm-dd" }')
}
const PASTA = 'C:/Projetos GIT/SMSMarica/SMSMais.Regulacao'
const RAIZ = 'C:/Projetos GIT/SMSMarica'
const data = args.data || 'sem-data'

const TAREFA = {
  type: 'object',
  properties: {
    arquivoPlano: { type: 'string' },
    titulo: { type: 'string' },
    descricao: { type: 'string' },
    arquivosAlvo: { type: 'array', items: { type: 'string' } },
    criteriosDePronto: { type: 'array', items: { type: 'string' } },
    dependenciasNaoAtendidas: { type: 'array', items: { type: 'string' } },
    exigeOkProducao: { type: 'boolean' },
  },
  required: ['arquivoPlano', 'titulo', 'descricao', 'arquivosAlvo', 'criteriosDePronto', 'dependenciasNaoAtendidas', 'exigeOkProducao'],
}
const IMPLEMENTACAO = {
  type: 'object',
  properties: {
    arquivosCriados: { type: 'array', items: { type: 'string' } },
    arquivosAlterados: { type: 'array', items: { type: 'string' } },
    migrationsGeradas: { type: 'array', items: { type: 'string' } },
    resumo: { type: 'string' },
    desviosDoPlano: { type: 'array', items: { type: 'string' } },
  },
  required: ['arquivosCriados', 'arquivosAlterados', 'migrationsGeradas', 'resumo', 'desviosDoPlano'],
}
const VERIFICACAO = {
  type: 'object',
  properties: {
    buildServerOk: { type: 'boolean' },
    buildFrontOk: { type: 'boolean' },
    testesRodados: { type: 'string' },
    testesOk: { type: 'boolean' },
    saidaResumida: { type: 'string' },
  },
  required: ['buildServerOk', 'buildFrontOk', 'testesRodados', 'testesOk', 'saidaResumida'],
}
const REVISAO = {
  type: 'object',
  properties: {
    aprovado: { type: 'boolean' },
    achados: { type: 'array', items: { type: 'object', properties: { arquivo: { type: 'string' }, gravidade: { type: 'string' }, texto: { type: 'string' } }, required: ['arquivo', 'gravidade', 'texto'] } },
  },
  required: ['aprovado', 'achados'],
}

phase('Ler')
const tarefa = await agent(
  `Leia "${PASTA}/PROGRESSO.md" e o plano cujo nome comeca com "${args.plano}-" em "${PASTA}/" (incluindo a secao "Especificacao para execucao"). Localize a tarefa "${args.tarefa}". Responda em pt-BR: arquivo do plano, titulo e descricao completa da tarefa (copie as instrucoes relevantes da especificacao), arquivos-alvo, criterios de pronto, dependencias do PROGRESSO.md que ainda estao "[ ]" e que esta tarefa exige, e se a tarefa exige OK de producao (deploy, migration em prod, escrita em SISREG/SER/SERNIT reais, spikes a/b).`,
  { label: `ler:${args.plano}/${args.tarefa}`, phase: 'Ler', schema: TAREFA },
)
if (!tarefa) throw new Error('Nao foi possivel ler a tarefa')
if (tarefa.exigeOkProducao) {
  log('Tarefa exige OK de producao: este workflow nao a executa. Registre o OK em PROGRESSO.md e execute manualmente.')
  return { bloqueada: true, motivo: 'exige OK de producao', tarefa }
}
if (tarefa.dependenciasNaoAtendidas.length) {
  log(`Dependencias pendentes: ${tarefa.dependenciasNaoAtendidas.join('; ')}`)
  return { bloqueada: true, motivo: 'dependencias pendentes', tarefa }
}

phase('Implementar')
const impl = await agent(
  `Voce esta em "${RAIZ}". Leia "${RAIZ}/CLAUDE.md" e "${PASTA}/CLAUDE.md" e siga-os. Implemente EXATAMENTE a tarefa abaixo, seguindo a "Especificacao para execucao" do plano "${PASTA}/${tarefa.arquivoPlano}" (entidades, colunas, assinaturas, rotas, componentes, nomes de teste). Nao implemente outras tarefas. Nao faca commit, nao rode deploy, nao toque em producao. Se precisar gerar migration, use: dotnet ef migrations add <Nome> --project src/SMSMais.Data --startup-project src/SMSMais.Api (a partir de SMSMais.server). Ao terminar, rode "dotnet build" em SMSMais.server e, se tocou no front, "npm run build" em SMSMais.front; corrija ate 0 erros / 0 warnings. Se algo do plano nao puder ser feito como escrito, faca a menor adaptacao possivel e liste em desviosDoPlano.
TAREFA: ${tarefa.titulo}
${tarefa.descricao}
ARQUIVOS-ALVO: ${tarefa.arquivosAlvo.join(', ')}
CRITERIOS DE PRONTO: ${tarefa.criteriosDePronto.join(' | ')}`,
  { label: `implementar:${args.tarefa}`, phase: 'Implementar', schema: IMPLEMENTACAO },
)
if (!impl) throw new Error('Implementacao nao retornou')

phase('Verificar')
const verif = await agent(
  `Em "${RAIZ}": rode "cd SMSMais.server && dotnet build" e informe se terminou com 0 erros e 0 warnings. Se algum arquivo em ${JSON.stringify(impl.arquivosAlterados.concat(impl.arquivosCriados).filter((a) => a.includes('SMSMais.front')))} foi tocado, rode "cd SMSMais.front && npm run build". Rode "dotnet test --filter Regulacao" (se a variavel SMSMARICA_TESTS_CONNECTION nao estiver definida e o Docker nao existir, informe que os testes de banco nao rodaram, sem inventar resultado). Nao altere codigo. Responda em pt-BR com a saida resumida.`,
  { label: 'verificar', phase: 'Verificar', schema: VERIFICACAO, effort: 'low' },
)

phase('Revisar')
const arquivos = impl.arquivosCriados.concat(impl.arquivosAlterados)
const revisoes = await parallel([
  () => agent(
    `Revise os arquivos ${JSON.stringify(arquivos)} em "${RAIZ}" procurando DEFEITOS DE CORRECAO: bugs, transicao de estado sem validacao, escopo de unidade ignorado, segredo em log/DTO, migration que apaga dado, excecao engolida, N+1, falta de indice unico onde o plano pede. Tente refutar cada achado antes de reportar. Responda em pt-BR.`,
    { label: 'revisar:correcao', phase: 'Revisar', schema: REVISAO },
  ),
  () => agent(
    `Compare os arquivos ${JSON.stringify(arquivos)} em "${RAIZ}" com a "Especificacao para execucao" do plano "${PASTA}/${tarefa.arquivoPlano}" e com "${RAIZ}/CLAUDE.md": nomes de tabela/coluna/enum/rota/servico/componente iguais aos especificados? pt-BR no dominio? feature isolada em src/features/regulacao? nada de TenantId? nada institucional em migration? Liste cada divergencia com arquivo e gravidade (alta = muda contrato; media = nome; baixa = estilo). Responda em pt-BR.`,
    { label: 'revisar:aderencia', phase: 'Revisar', schema: REVISAO },
  ),
])

phase('Relatar')
const relatorioPath = `${PASTA}/revisoes/${data}-execucao-${args.plano}-${String(args.tarefa).replace('.', '_')}.md`
await agent(
  `Escreva o arquivo "${relatorioPath}" em pt-BR (markdown) com: 1. Tarefa (plano, numero, titulo); 2. O que foi feito (resumo, arquivos criados/alterados, migrations); 3. Verificacao (builds, testes, saida resumida); 4. Revisao de correcao (achados por gravidade); 5. Revisao de aderencia (divergencias); 6. Desvios do plano declarados pela implementacao; 7. Proximos passos: o que o humano precisa fazer (marcar a tarefa em PROGRESSO.md se tudo verde; corrigir achados de gravidade alta antes). NAO edite PROGRESSO.md nem os planos: quem marca e o humano/modelo da sessao apos ler o relatorio. Dados:
IMPLEMENTACAO=${JSON.stringify(impl)}
VERIFICACAO=${JSON.stringify(verif)}
REVISOES=${JSON.stringify(revisoes.filter(Boolean))}`,
  { label: 'relatorio', phase: 'Relatar', effort: 'low' },
)

const altos = revisoes.filter(Boolean).flatMap((r) => r.achados).filter((a) => /alta/i.test(a.gravidade))
return {
  tarefa: `${args.plano}/${args.tarefa}`,
  buildServerOk: verif ? verif.buildServerOk : null,
  buildFrontOk: verif ? verif.buildFrontOk : null,
  testesOk: verif ? verif.testesOk : null,
  achadosAltos: altos.length,
  desvios: impl.desviosDoPlano.length,
  relatorio: relatorioPath,
}
