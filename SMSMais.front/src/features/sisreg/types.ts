export type TipoAutenticacaoSisreg = 'Basic' | 'Bearer' | 'ApiKey';
export type EscopoSisreg = 'Municipal' | 'Nacional';
/**
 * Onde a importação consulta o cadastro (CADSUS).
 *
 * Nome do enum, não número: a API serializa enum como string (JsonStringEnumConverter), como já
 * acontece com `EscopoSisreg` e `TipoAutenticacaoSisreg` aqui em cima. Tipar como número fazia o
 * `<select>` receber "Ser" e não casar com nenhuma `<option value={2}>`, então a tela voltava a
 * exibir a primeira opção — parecendo que o salvamento tinha sido descartado quando o banco já
 * estava com o valor certo.
 */
export type FonteCadastroPaciente = 'Sisreg' | 'Ser' | 'SerComFallbackSisreg';

export type SisregConfiguracao = {
  baseUrl: string;
  escopo: EscopoSisreg;
  uf: string;
  municipio: string;
  centraisReguladoras: string;
  tipoAutenticacao: TipoAutenticacaoSisreg;
  login: string | null;
  senhaDefinida: boolean;
  tokenDefinido: boolean;
  ativo: boolean;
  /**
   * Chave-mestra do sincronismo AUTOMÁTICO (varredura diária das unidades + lote de mapeamento).
   * Só de leitura aqui — quem altera é `alternarSincronismoAutomatico`, endpoint próprio.
   */
  sincronismoAutomaticoAtivo: boolean;
  fonteCadastroPaciente: FonteCadastroPaciente;
  /** Sessões paralelas do SER na consulta de cadastro (o backend limita entre 1 e 8). */
  consultasSimultaneasSer: number;
};

export type AtualizarSisregConfiguracaoPayload = {
  baseUrl: string;
  escopo: EscopoSisreg;
  uf: string;
  municipio: string;
  centraisReguladoras: string;
  tipoAutenticacao: TipoAutenticacaoSisreg;
  login?: string | null;
  senha?: string | null;
  token?: string | null;
  ativo: boolean;
  fonteCadastroPaciente: FonteCadastroPaciente;
  consultasSimultaneasSer: number;
};

export type TestarConexaoSisregResultado = { sucesso: boolean; mensagem: string };

/** Resultado de ligar/desligar o sincronismo automático com o SISREG. */
export type SincronismoAutomaticoSisreg = { ativo: boolean; mensagem: string };

/**
 * Resultado do backfill do profissional executante — relê a linha crua já guardada em
 * `raw_sisreg`. Não fala com o SISREG: nenhuma requisição, nenhum risco de CAPTCHA.
 */
export type BackfillExecutanteResultado = {
  examinadas: number;
  preenchidas: number;
  /** RAW existe mas não carrega o executante (JSON do caminho pontual do cons_agendas). */
  semDadoNoRaw: number;
  /** Seguem sem executante depois desta passada. */
  pendentes: number;
  mensagem: string;
};

/** Resposta ao disparo do lote "sincroniza tudo". */
export type MapeamentoLoteAceito = { unidadesTotal: number; mensagem: string };

/** Progresso do lote em curso (null = nenhum rodando). */
export type MapeamentoLoteStatus = {
  emExecucao: boolean;
  disparo: 'Manual' | 'Agendado';
  /** Descoberta ou mapeamento — a descoberta acontece antes de existir denominador. */
  fase: string;
  unidadesTotal: number;
  unidadesFeitas: number;
  unidadeAtual: string | null;
  /** Unidades que a credencial enxerga no SISREG. */
  unidadesNoSisreg: number;
  /** Criadas aqui nesta execução (existiam no SISREG e não no nosso cadastro). */
  unidadesCriadas: number;
  unidadesMapeadas: number;
  /** Puladas por TTL ou por orçamento — é o comportamento normal, não falha. */
  unidadesPuladas: number;
  requisicoesFeitas: number;
  profissionaisEncontrados: number;
  profissionaisNovos: number;
  procedimentosEncontrados: number;
  procedimentosNovos: number;
  practitionersCriados: number;
  practitionersVinculados: number;
  unidadesComErro: number;
  /** Requisições ainda disponíveis na janela de 60 min antes do teto anti-robô. */
  orcamentoRestante: number;
  iniciadoEm: string;
  ultimoErro: string | null;
};

/** Estado de uma sincronização — mesmos nomes do rastreio da varredura. */
export type StatusMapeamentoLote =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  | 'Parcial'
  | 'Erro'
  | 'Cancelada';

/** Uma sincronização já encerrada (o que sobra depois que o progresso vivo some). */
export type MapeamentoLoteExecucao = {
  id: string;
  disparo: 'Manual' | 'Agendado';
  status: StatusMapeamentoLote;
  unidadesNoSisreg: number;
  unidadesCriadas: number;
  unidadesComCnesPreenchido: number;
  unidadesTotal: number;
  unidadesMapeadas: number;
  unidadesPuladas: number;
  unidadesComErro: number;
  profissionaisEncontrados: number;
  profissionaisNovos: number;
  procedimentosEncontrados: number;
  procedimentosNovos: number;
  practitionersCriados: number;
  practitionersVinculados: number;
  requisicoes: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
};

/** Desfecho de uma unidade dentro da sincronização. */
export type ResultadoUnidadeLote =
  | 'Mapeada'
  | 'PuladaPorTtl'
  | 'PuladaPorOrcamento'
  | 'Erro'
  | 'SomenteDescoberta'
  /** Existe no SISREG e não tem executante (central de regulação). Não é erro. */
  | 'SemProfissionais';

/** Detalhe por unidade — o "quantos médicos vieram de cada uma". */
export type MapeamentoLoteExecucaoItem = {
  id: string;
  unidadeId: string;
  unidadeNome: string;
  cnes: string | null;
  unidadeCriada: boolean;
  resultado: ResultadoUnidadeLote;
  profissionaisEncontrados: number;
  profissionaisNovos: number;
  profissionaisAusentes: number;
  procedimentosEncontrados: number;
  procedimentosNovos: number;
  practitionersCriados: number;
  practitionersVinculados: number;
  requisicoes: number;
  observacao: string | null;
};

/** Configuração do disparo automático do lote. */
export type MapeamentoLoteAgendamento = {
  ativo: boolean;
  horaLocal: string;
  /** Carga inicial: rodadas em sequência até toda unidade ter primeiro mapeamento. */
  bootstrap: boolean;
  /** Unidades que ainda nunca foram mapeadas. */
  pendentesPrimeiroMapeamento: number;
  /** Requisições ainda disponíveis na janela de 60 min. */
  orcamentoRestante: number;
};

export type SalvarMapeamentoLoteAgendamento = {
  ativo: boolean;
  horaLocal: string;
  /** Omitido mantém o modo de carga inicial como está. */
  bootstrap?: boolean;
};

/** Programar a rede inteira para o sincronismo diário. */
export type PrepararRedePayload = {
  intervaloMinutos: number;
  horaInicialLocal: string;
  diasAFrente: number;
  habilitar: boolean;
};

export type PrepararRedeUnidade = {
  unidadeId: string;
  nome: string;
  horaLocal: string;
  profissionais: number;
  procedimentos: number;
};

export type PrepararRede = {
  unidadesPreparadas: number;
  profissionaisHabilitados: number;
  procedimentosHabilitados: number;
  primeiroHorario: string;
  ultimoHorario: string;
  unidades: PrepararRedeUnidade[];
  mensagem: string;
};

export type SisregBuscaResultado<T> = { total: number; itens: T[] };

/** Registro genérico do SISREG — os três índices compartilham muitos campos negociais. */
export type RegistroSisreg = {
  codigoSolicitacao?: number | null;
  status?: string | null;
  statusSolicitacao?: string | null;
  siglaSituacao?: string | null;
  nomeUsuario?: string | null;
  cnsUsuario?: string | null;
  dataSolicitacao?: string | null;
  dataMarcacao?: string | null;
  dataAprovacao?: string | null;
  dataConfirmacao?: string | null;
  dataInternacao?: string | null;
  descricaoInternaProcedimento?: string | null;
  descricaoProcedimento?: string | null;
  nomeUnidadeExecutante?: string | null;
  nomeUnidadeSolicitante?: string | null;
  codigoClassificacaoRisco?: number | null;
};

export type ConsultaSisreg =
  | 'novas-solicitacoes'
  | 'fila'
  | 'agendadas'
  | 'atendidas'
  | 'canceladas-devolvidas'
  | 'internacoes';

export const CONSULTAS_SISREG: { id: ConsultaSisreg; rotulo: string; usaIntervalo: boolean }[] = [
  { id: 'novas-solicitacoes', rotulo: 'Novas solicitações (ambulatorial)', usaIntervalo: true },
  { id: 'fila', rotulo: 'Fila de solicitações (ambulatorial)', usaIntervalo: false },
  { id: 'agendadas', rotulo: 'Solicitações agendadas (ambulatorial)', usaIntervalo: true },
  { id: 'atendidas', rotulo: 'Solicitações atendidas (ambulatorial)', usaIntervalo: true },
  { id: 'canceladas-devolvidas', rotulo: 'Canceladas/devolvidas (ambulatorial)', usaIntervalo: false },
  { id: 'internacoes', rotulo: 'Internações (hospitalar)', usaIntervalo: true },
];

/** Prévia da distribuição dos horários, sem gravar nada. */
export type PreverAgendamentoPayload = { intervaloMinutos: number; horaInicialLocal: string };

export type PreverAgendamento = {
  unidades: number;
  primeiroHorario: string;
  ultimoHorario: string;
  /** Quantas não cabem na madrugada e caem na tarde do dia seguinte. */
  foraDaMadrugada: number;
  resumo: string;
};

/** Resultado de ligar/desligar a importação diária de todas as unidades. */
export type AlternarAgendamentoRede = {
  unidadesAfetadas: number;
  unidadesAtivas: number;
  mensagem: string;
};

// ----------------------------------------------------------- escalas (a OFERTA de vagas)

/**
 * Sincronismo da grade de escalas do SISREG.
 *
 * Diferente do "sincroniza tudo" do mapeamento: aqui uma ÚNICA requisição traz a rede inteira e
 * todo o histórico (17.469 linhas na medição), então não há custo por unidade nem rodízio.
 */
export type EscalasSincronizacaoAceita = { execucaoId: string; mensagem: string };

/** Progresso da sincronização em curso (null = nenhuma rodando). */
export type EscalasSincronizacaoStatus = {
  emExecucao: boolean;
  disparo: 'Manual' | 'Agendado';
  /** Texto humano: a maior parte do tempo é o download, quando ainda não há denominador. */
  fase: string;
  escalasLidas: number;
  escalasGravadas: number;
  escalasNovas: number;
  escalasAtualizadas: number;
  linhasRejeitadas: number;
  unidadesNaoEncontradas: number;
  iniciadoEm: string;
  ultimoErro: string | null;
};

export type EscalasSincronizacaoExecucao = {
  id: string;
  disparo: 'Manual' | 'Agendado';
  status: StatusMapeamentoLote;
  escalasLidas: number;
  escalasNovas: number;
  escalasAtualizadas: number;
  /** Sumiram do arquivo do SISREG — marcadas ausentes, não apagadas. */
  escalasAusentes: number;
  /** Linhas que o SISREG mandou quebradas. Um número estável é o normal; o que importa é crescer. */
  linhasRejeitadas: number;
  /** CNES sem unidade no cadastro. Zero é o esperado — valor aqui é unidade nova no SISREG. */
  unidadesNaoEncontradas: number;
  requisicoes: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
};

export type EscalasAgendamento = { ativo: boolean; horaLocal: string; orcamentoRestante: number };

export type SalvarEscalasAgendamento = { ativo: boolean; horaLocal: string };
