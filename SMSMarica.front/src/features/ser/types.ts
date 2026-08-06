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
  /** Terminou, mas alguma fatia estourou o teto de 100 do SER — há registros NÃO lidos. */
  | 'Parcial'
  | 'Erro'
  | 'Cancelada';

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
};

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
  paginas: number;
  /** 5 páginas = o corte de 100 registros da tela do SER. */
  bateuNoTeto: boolean;
  duracaoMs: number;
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
