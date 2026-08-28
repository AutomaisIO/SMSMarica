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
  unidadesTotal: number;
  unidadesFeitas: number;
  unidadeAtual: string | null;
  requisicoesFeitas: number;
  profissionaisEncontrados: number;
  profissionaisNovos: number;
  practitionersCriados: number;
  practitionersVinculados: number;
  unidadesComErro: number;
  iniciadoEm: string;
  ultimoErro: string | null;
};

/** Configuração do disparo diário automático do lote. */
export type MapeamentoLoteAgendamento = { ativo: boolean; horaLocal: string };

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
