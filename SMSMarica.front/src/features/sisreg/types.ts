export type TipoAutenticacaoSisreg = 'Basic' | 'Bearer' | 'ApiKey';
export type EscopoSisreg = 'Municipal' | 'Nacional';

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
};

export type TestarConexaoSisregResultado = { sucesso: boolean; mensagem: string };

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
