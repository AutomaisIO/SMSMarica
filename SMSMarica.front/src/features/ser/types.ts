/**
 * Fila do SER (Sistema Estadual de Regulação, SES-RJ) espelhada na nossa base — ADR-0042.
 *
 * A tela lê o NOSSO banco, não o SER: quem fala com o SER é o motor de varredura, em background.
 * Os campos espelham as colunas da grade do SER de propósito, para o operador reconhecer a tela.
 */

/** Situações do SER. A ordem aqui é a da fila (do que espera para o que terminou). */
export const SITUACOES_SER = [
  'EmFila',
  'Pendente',
  'Agendada',
  'ChegadaNaoConfirmada',
  'ChegadaConfirmada',
  'Cancelada',
  'Alta',
] as const;

export type SituacaoSer = (typeof SITUACOES_SER)[number];

export const ROTULO_SITUACAO: Record<SituacaoSer, string> = {
  EmFila: 'Em fila',
  Pendente: 'Pendente',
  Agendada: 'Agendada',
  ChegadaNaoConfirmada: 'Chegada não confirmada',
  ChegadaConfirmada: 'Chegada confirmada',
  Cancelada: 'Cancelada',
  Alta: 'Alta',
};

export type TipoRecursoSer = 'Consulta' | 'Exame';

export type ModoVarreduraSer = 'CargaInicial' | 'Diaria' | 'SomenteGrade';

export type StatusVarreduraSer =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  /** Terminou, mas alguma fatia estourou o teto do SER — há registros NÃO lidos. */
  | 'Parcial'
  | 'Erro'
  | 'Cancelada'
  /** Parada por queda/deploy do serviço e RETOMÁVEL — o runner continua do ponteiro. */
  | 'Interrompida';

/** Em que ponto a rodada está. Grade = espelho da fila; Historico = trilha de eventos. */
export type FaseVarreduraSer = 'Grade' | 'Historico' | 'Finalizada';

export type SolicitacaoSerLista = {
  id: string;
  /** "ID Solicitação" do SER — é a chave que o operador usa para falar com a central. */
  idSer: string;
  tipo: TipoRecursoSer;
  recurso: string;
  dataSolicitacao: string | null;
  pacienteNome: string;
  idadeTexto: string | null;
  cpf: string | null;
  cns: string | null;
  cid: string | null;
  solicitanteNome: string | null;
  municipioSolicitante: string | null;
  agendadoParaTexto: string | null;
  situacao: SituacaoSer;
  situacaoAnterior: SituacaoSer | null;
  situacaoMudouEm: string | null;
  sincronizadoEm: string;
  historicoLidoEm: string | null;
  eventosCount: number;
  /** Solicitações em Alta não têm histórico no SER — o menu não oferece o item. */
  historicoIndisponivel: boolean;
  diasNaFila: number | null;
};

export type EventoSer = {
  id: string;
  dataEvento: string;
  /** Verbo do SER: Solicitar, FollowUP, Pendenciar, Cancelar. */
  evento: string;
  estadoAnterior: string | null;
  estadoAtual: string | null;
  centralRegulacao: string | null;
  unidadeExecutora: string | null;
  usuario: string | null;
  lotacaoEvento: string | null;
  ip: string | null;
  observacao: string | null;
};

export type SolicitacaoSerDetalhe = {
  resumo: SolicitacaoSerLista;
  nomeMae: string | null;
  sexo: string | null;
  dataNascimento: string | null;
  etnia: string | null;
  cep: string | null;
  uf: string | null;
  municipioPaciente: string | null;
  bairro: string | null;
  tipoLogradouro: string | null;
  logradouro: string | null;
  numero: string | null;
  complemento: string | null;
  telefoneResidencial: string | null;
  telefoneWhatsapp: string | null;
  telefoneContato: string | null;
  eventos: EventoSer[];
};

export type BuscaSerFiltro = {
  situacao?: SituacaoSer;
  tipo?: TipoRecursoSer;
  /** Busca livre: nome, CPF, CNS, ID do SER e recurso. */
  termo?: string;
  dataSolicitacaoInicio?: string;
  dataSolicitacaoFim?: string;
  mudouDesde?: string;
  pagina?: number;
  tamanho?: number;
};

export type BuscaSerResultado = {
  itens: SolicitacaoSerLista[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ResumoSituacaoSer = { situacao: SituacaoSer; quantidade: number };

export type ExecucaoSer = {
  id: string;
  modo: ModoVarreduraSer;
  disparo: 'Manual' | 'Agendado';
  status: StatusVarreduraSer;
  janelaInicio: string;
  janelaFim: string;
  situacoesVarridas: string;
  buscas: number;
  paginas: number;
  solicitacoesEncontradas: number;
  solicitacoesNovas: number;
  solicitacoesAtualizadas: number;
  mudancasSituacao: number;
  historicosLidos: number;
  eventosNovos: number;
  followUpsNovos: number;
  historicosIndisponiveis: number;
  gatilhosGerados: number;
  /** > 0 significa cobertura incompleta: existem registros que NÃO foram lidos. */
  fatiasTruncadas: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
  /** Ponteiro de retomada: onde a rodada está e de onde ela continua se o serviço cair. */
  fase: FaseVarreduraSer;
  cursorSituacao: SituacaoSer | null;
  cursorData: string | null;
  cursorIdSer: string | null;
  historicosPendentes: number;
  retomadas: number;
  retomadaEm: string | null;
  /**
   * Sinal de vida: quando o motor gravou progresso pela última vez. É o que distingue
   * "trabalhando numa fatia grande" de "pendurada" — contador parado, sozinho, não diz qual.
   */
  ultimoSinalEm: string | null;
};

export type StatusMotorSer = {
  credencialConfigurada: boolean;
  varreduraEmAndamento: boolean;
  totalSolicitacoes: number;
  totalEventos: number;
  gatilhosPendentes: number;
  ultimaExecucao: ExecucaoSer | null;
};

export type DispararVarreduraPayload = {
  modo: ModoVarreduraSer;
  inicio?: string;
  fim?: string;
  situacoes?: SituacaoSer[];
};

// ---- Consulta DIRETA (tela de testes: vai ao SER ao vivo, nada é gravado) ----

export type ConsultaDiretaFiltro = {
  situacao: SituacaoSer;
  tipo?: TipoRecursoSer;
  dataSolicitacaoInicio?: string;
  dataSolicitacaoFim?: string;
  cpf?: string;
  nome?: string;
  cns?: string;
  idSolicitacao?: string;
  pagina?: number;
  /**
   * Consultar pela tela de Histórico (export de 500) em vez da tela de Solicitação (teto de 100).
   * É o caminho que a varredura usa de verdade — e o único em que a contagem significa algo.
   */
  porExport?: boolean;
  /**
   * No export, mandar o filtro de unidade solicitante. Existe para testar a suspeita de que esse
   * campo (autocomplete com hidden vazio) zera a consulta em silêncio.
   */
  filtrarPorSolicitante?: boolean;
};

/** Qual tela do SER respondeu. Muda o teto e o que a resposta prova. */
export type FonteConsultaSer = 'TelaSolicitacao' | 'ExportHistorico';

/** Linha crua do SER — tudo string, exatamente como o parser leu da grade. */
export type LinhaDiretaSer = {
  idSer: string;
  tipo: string | null;
  recurso: string | null;
  dataSolicitacao: string | null;
  paciente: string | null;
  idade: string | null;
  cpf: string | null;
  cns: string | null;
  cid: string | null;
  solicitante: string | null;
  municipioSolicitante: string | null;
  agendadoPara: string | null;
  situacao: string | null;
};

export type ConsultaDiretaResultado = {
  linhas: LinhaDiretaSer[];
  /** Páginas do datascroller. Sempre 0 no export: aquela tela não pagina. */
  paginas: number;
  /** Na tela de Solicitação, 5 páginas = corte de 100. No export, é o aviso do próprio SER. */
  bateuNoTeto: boolean;
  duracaoMs: number;
  fonte: FonteConsultaSer;
  /** O aviso de corte nas palavras do SER, ou null quando o lote veio inteiro. */
  avisoDoSer: string | null;
};

export type EventoDiretoSer = {
  data: string | null;
  evento: string | null;
  estadoAnterior: string | null;
  estadoAtual: string | null;
  centralRegulacao: string | null;
  unidadeExecutora: string | null;
  usuario: string | null;
  lotacaoEvento: string | null;
  ip: string | null;
  observacao: string | null;
};

export type HistoricoDiretoResultado = {
  idSer: string;
  paciente: Record<string, string>;
  eventos: EventoDiretoSer[];
  duracaoMs: number;
};

// ---------------------------------------------------------------- notificações
// A fila de gatilhos do motor vista pela regulação: o que mudou no SER e ninguém olhou ainda.

export type TipoGatilhoSer =
  | 'MudancaSituacao'
  | 'NovoFollowUp'
  | 'NovaSolicitacao'
  | 'MudancaAgendamento';

export type NotificacaoSer = {
  id: string;
  /** Id interno da solicitação — o modal de detalhe busca por ele. */
  solicitacaoId: string;
  idSer: string;
  tipo: TipoGatilhoSer;
  situacaoAnterior: SituacaoSer | null;
  situacaoAtual: SituacaoSer | null;
  criadoEm: string;
  tipoRecurso: TipoRecursoSer | null;
  pacienteNome: string | null;
  recurso: string | null;
  dataSolicitacao: string | null;
  agendadoParaTexto: string | null;
  unidadeExecutora: string | null;
};

export type NotificacoesPagina = {
  itens: NotificacaoSer[];
  total: number;
  pagina: number;
  tamanho: number;
};

/** Quantos movimentos por ler existem em cada (tipo de recurso, situação). */
export type NotificacaoContador = {
  tipo: TipoRecursoSer | null;
  situacao: SituacaoSer;
  quantidade: number;
};

export type NotificacoesResumo = {
  total: number;
  contadores: NotificacaoContador[];
};

export type NotificacoesFiltro = {
  tipo?: TipoRecursoSer;
  situacao?: SituacaoSer;
  tipoGatilho?: TipoGatilhoSer;
  pagina?: number;
  tamanho?: number;
};
