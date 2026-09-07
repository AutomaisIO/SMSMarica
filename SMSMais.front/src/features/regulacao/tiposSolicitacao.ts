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
  /** Quem assumiu o caso na regulação. `null` = ainda na fila. */
  agenteResponsavelId: string | null;
  enviadoEm: string | null;
  /** O número foi digitado pelo agente (envio assistido), não gerado pelo nosso envio. */
  envioAssistido: boolean;
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

// ---------------------------------------------------------------- fila (plano 04)

/**
 * De que lado veio a ação registrada na linha do tempo. `Sistema` é varredura/importação/job —
 * sem pessoa por trás.
 */
export type PapelEventoRegulacao = 'Solicitante' | 'Agente' | 'Sistema';

export type TipoEventoRegulacao =
  | 'Criacao'
  | 'Edicao'
  | 'Anexo'
  | 'RespostaRegra'
  | 'EnvioFila'
  | 'Assumida'
  | 'Ajuste'
  | 'Devolucao'
  | 'EnvioSistema'
  | 'FalhaEnvio'
  | 'NumeroExterno'
  | 'RessalvaDestino'
  | 'PendenciaAberta'
  | 'PendenciaRespondida'
  | 'PendenciaSubmetida'
  | 'PendenciaBaixada'
  | 'SituacaoExterna'
  | 'Cancelamento'
  | 'Recusa'
  | 'OkInterno'
  | 'TrocaProcedimento';

export type EventoRegulacao = {
  id: string;
  tipo: TipoEventoRegulacao;
  de: StatusRegulacao | null;
  para: StatusRegulacao | null;
  usuarioNome: string | null;
  papel: PapelEventoRegulacao;
  /** `{campo: {de, para}}` — só nos eventos de edição/ajuste. */
  diff: Record<string, { de: string | null; para: string | null }> | null;
  detalhe: Record<string, unknown> | null;
  criadoEm: string;
};

export type SolicitacaoRegulacaoLista = {
  id: string;
  numeroLocal: number;
  numeroExterno: string | null;
  sistemaDestino: SistemaRegulacao | null;
  fluxo: FluxoRegulacao;
  status: StatusRegulacao;
  pacienteNome: string;
  pacienteCpf: string | null;
  procedimento: string;
  unidadeSolicitanteId: string;
  unidadeSolicitante: string;
  unidadeEmNomeDe: string | null;
  agenteNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
};

export type PaginaSolicitacoesRegulacao = {
  total: number;
  itens: SolicitacaoRegulacaoLista[];
};

export type FiltroSolicitacoesRegulacao = {
  status?: StatusRegulacao[];
  fluxo?: FluxoRegulacao;
  sistema?: SistemaRegulacao;
  procedimentoId?: string;
  unidadeSolicitanteId?: string;
  agenteId?: string;
  busca?: string;
  /** Só as que eu abri — vale sobretudo para o agente, que enxerga tudo. */
  soMinhas?: boolean;
  pagina?: number;
  tamanho?: number;
};

export type ResumoFilaRegulacao = {
  porStatus: Partial<Record<StatusRegulacao, number>>;
  /** `true` = está vendo o município inteiro (módulo 48), não só a própria unidade. */
  veTodasUnidades: boolean;
};

// ---------------------------------------------------------------- notificações (plano 05)

export type NotificacaoRegulacao = {
  eventoId: string;
  solicitacaoId: string;
  numeroLocal: number;
  numeroExterno: string | null;
  sistema: SistemaRegulacao | null;
  tipo: TipoEventoRegulacao;
  de: StatusRegulacao | null;
  para: StatusRegulacao | null;
  pacienteNome: string;
  procedimento: string;
  unidadeSolicitanteId: string;
  unidadeSolicitante: string;
  criadoEm: string;
  vista: boolean;
};

export type PaginaNotificacoesRegulacao = {
  total: number;
  itens: NotificacaoRegulacao[];
};

/** `minha` = as unidades do usuário; `todas` = o município (exige módulo 48 ou config aberta). */
export type EscopoNotificacao = 'minha' | 'todas';

// ---------------------------------------------------------------- regras (plano 03)

export type TipoRegraRegulacao = 'Dedutivel' | 'NaoDedutivel' | 'Documental' | 'Informativa';
export type SeveridadeRegraRegulacao = 'Bloqueia' | 'Ressalva' | 'Aviso';
export type RespostaRegraRegulacao = 'Sim' | 'Nao' | 'NaoSei' | 'Deduzido';
export type ResultadoRegraRegulacao = 'Atende' | 'Bloqueia' | 'Ressalva' | 'Indefinido';

export type RegraAvaliada = {
  regraId: string;
  versao: number;
  tipo: TipoRegraRegulacao;
  severidade: SeveridadeRegraRegulacao;
  sistema: SistemaRegulacao | null;
  descricao: string;
  resultado: ResultadoRegraRegulacao;
  /** Por que deu isso — a tela mostra ao lado da regra. */
  motivo: string | null;
};

export type PerguntaPendente = {
  regraId: string;
  pergunta: string;
  sistema: SistemaRegulacao | null;
  severidade: SeveridadeRegraRegulacao;
};

export type ExameParaRegras = {
  id: string;
  tipoExameId: string | null;
  realizadoEm: string;
  laudado: boolean;
  descricao: string;
  laudoId: string | null;
};

export type DocumentoPendente = {
  regraId: string;
  rotulo: string;
  obrigatorio: boolean;
  tipoExameId: string | null;
  validadeDias: number | null;
  examesInternosCandidatos: ExameParaRegras[];
};

export type AvaliacaoElegibilidade = {
  regras: RegraAvaliada[];
  perguntasPendentes: PerguntaPendente[];
  documentosPendentes: DocumentoPendente[];
  destinosPermitidos: SistemaRegulacao[];
  destinosComRessalva: SistemaRegulacao[];
  /** Por que cada destino saiu da lista. */
  motivosDeBloqueio: Partial<Record<SistemaRegulacao, string>>;
  bloqueiaEnvio: boolean;
};
