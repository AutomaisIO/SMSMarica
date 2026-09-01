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
