import type { SistemaRegulacao } from './types';

/** Tipos do fluxo de abertura de solicitação (planos 02 e 04). */

export type FluxoRegulacao = 'Interno' | 'Externo' | 'Nar';

export type StatusRegulacao =
  | 'Rascunho'
  | 'PendenteRegulacao'
  | 'EmAnalise'
  | 'Devolvida'
  | 'EnviandoAoSistema'
  | 'EnviadaAoSistema'
  | 'EmFilaExterna'
  | 'Agendada'
  | 'Concluida'
  | 'Cancelada'
  | 'Recusada'
  | 'FalhaEnvio';

export type OpcaoCampoFormulario = {
  valor: string;
  rotulo: string;
  origens: SistemaRegulacao[];
};

export type CampoFormulario = {
  chave: string;
  rotulo: string;
  /** `text`, `textarea`, `select`, `radio`, `checkbox`, `date`, `cid`. */
  tipo: string;
  obrigatorio: boolean;
  opcoes: OpcaoCampoFormulario[] | null;
  /** Em quais sistemas o campo existe — a tela avisa quando é exigência de um só. */
  origens: SistemaRegulacao[];
  ordem: number;
};

export type FormularioRegulacao = {
  /** A versão que o envio vai usar para traduzir. Viaja junto de propósito. */
  versaoId: string;
  esquema: string;
  campos: CampoFormulario[];
};

export type SolicitacaoRegulacao = {
  id: string;
  numeroLocal: number;
  fluxo: FluxoRegulacao;
  status: StatusRegulacao;
  statusMotivo: string | null;
  unidadeSolicitanteId: string;
  unidadeEmNomeDeId: string | null;
  pacienteId: string;
  pacienteNome: string;
  pacienteCpf: string | null;
  procedimentoId: string;
  procedimentoNome: string;
  sistemaDestino: SistemaRegulacao | null;
  formularioVersaoId: string | null;
  formulario: Record<string, unknown>;
  numeroExterno: string | null;
  observacoes: string | null;
  criadoEm: string;
};

/** Por que a solicitação ainda não pode ir para a fila. Lista vazia = pode enviar. */
export type PendenciaEnvio = { codigo: string; descricao: string };

export type ArquivoExigencia = {
  id: string;
  nome: string;
  contentType: string;
  tamanho: number;
  versao: number;
  situacao: string;
  origem: string;
  enviadoAoSistemaEm: string | null;
  criadoEm: string;
};

export type Exigencia = {
  id: string;
  /** Nulo = a caixinha "Anexos gerais", que toda solicitação tem. */
  regraId: string | null;
  titulo: string;
  obrigatoria: boolean;
  situacao: string;
  criticaTexto: string | null;
  ordem: number;
  arquivos: ArquivoExigencia[];
};
