/** Enum do backend — viaja como STRING no JSON. */
export type SituacaoAlertaEnvio = 'Enviado' | 'Parcial' | 'Falhou' | 'SemDestinatario' | 'TetoDiario';

export type AlertaDestinatario = {
  id: string;
  telefone: string;
  nome: string | null;
  ativo: boolean;
  criadoEm: string;
};

export type AlertaOrigem = {
  chave: string;
  rotulo: string;
  grupo: string;
  descricao: string;
  /** Fonte do catálogo (explícita) × descoberta pela captura do log. */
  catalogada: boolean;
  silenciada: boolean;
  ocorrencias: number;
  ultimaOcorrenciaEm: string | null;
  ultimoAvisoEm: string | null;
  /** Ocorrências que o freio segurou desde o último aviso. */
  ocorrenciasSemAviso: number;
  ultimoTitulo: string | null;
  ultimoDetalhe: string | null;
};

export type AlertaEnvio = {
  id: string;
  origemChave: string;
  origemRotulo: string | null;
  titulo: string;
  detalhe: string;
  criadoEm: string;
  ocorrencias: number;
  situacao: SituacaoAlertaEnvio;
  resultado: string | null;
};

export type AlertaTemplate = {
  nome: string;
  templateAprovado: boolean;
  templateCorpo: string | null;
  parametrosAprovados: number | null;
  parametrosConfigurados: string[];
  tetoDiario: number;
};

export type AlertaPainel = {
  destinatarios: AlertaDestinatario[];
  telefonesPorIntegracao: { provedor: string; telefones: string[] }[];
  origens: AlertaOrigem[];
  envios: AlertaEnvio[];
  template: AlertaTemplate;
};

export type SalvarDestinatario = { telefone: string; nome?: string | null; ativo: boolean };
