export const TIPOS_PERIODICIDADE = ['Diaria', 'IntervaloDias', 'SemanaDiasFixos', 'Manual'] as const;
export type TipoPeriodicidade = (typeof TIPOS_PERIODICIDADE)[number];

// Números do enum no backend (persistido como int).
export const TIPO_PERIODICIDADE_VALOR: Record<TipoPeriodicidade, number> = {
  Diaria: 1,
  IntervaloDias: 2,
  SemanaDiasFixos: 3,
  Manual: 4,
};

export const STATUS_SESSAO = ['Pendente', 'Confirmada', 'Realizada', 'Cancelada', 'NaoRealizada'] as const;
export type StatusSessao = (typeof STATUS_SESSAO)[number];

export const STATUS_SESSAO_VALOR: Record<StatusSessao, number> = {
  Pendente: 1,
  Confirmada: 2,
  Realizada: 3,
  Cancelada: 4,
  NaoRealizada: 5,
};

export function statusSessaoDeNumero(n: number | StatusSessao): StatusSessao {
  if (typeof n === 'string') return n;
  const entry = Object.entries(STATUS_SESSAO_VALOR).find(([, v]) => v === n);
  return (entry?.[0] as StatusSessao) ?? 'Pendente';
}

export type TipoTratamento = {
  id: string;
  nome: string;
  codigo: string;
  ativo: boolean;
};

export type TratamentoListItem = {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeId: string;
  unidadeNome: string;
  tipoTratamentoNome: string | null;
  descricao: string;
  proximaSessao: string | null;
  totalSessoes: number;
  sessoesRealizadas: number;
  ativo: boolean;
};

export type Periodicidade = {
  id: string;
  tipo: TipoPeriodicidade | number;
  intervaloDias: number | null;
  diasSemanaMascara: number | null;
  dataInicio: string;
  quantidadeSessoes: number;
};

export type Sessao = {
  id: string;
  tratamentoId: string;
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
  status: StatusSessao | number;
  realizadaEm: string | null;
  nomeAcompanhante: string | null;
  parentescoAcompanhante: string | null;
  motoristaIdaId: string | null;
  veiculoIdaId: string | null;
  horaSaidaResidencia: string | null;
  horaChegadaUnidade: string | null;
  motoristaVoltaId: string | null;
  veiculoVoltaId: string | null;
  horaSaidaUnidade: string | null;
  horaChegadaResidencia: string | null;
  motivoNaoRealizacao: string | null;
  observacoes: string | null;
  alocadaEmRotaId: string | null;
  alocadaNaData: string | null;
  fileiraAssentoAlocado: number | null;
  numeroAssentoAlocado: number | null;
};

export type Tratamento = {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeId: string;
  unidadeNome: string;
  tipoTratamentoId: string | null;
  tipoTratamentoNome: string | null;
  descricao: string;
  codigoSusLiberacao: string | null;
  observacoes: string | null;
  horaPrevistaBusca: string | null;
  ativo: boolean;
  criadoEm: string;
  encerradoEm: string | null;
  periodicidade: Periodicidade | null;
  sessoes: Sessao[];
};

export type ExpandirPeriodicidadePayload = {
  tipo: number;
  intervaloDias: number | null;
  diasSemanaMascara: number | null;
  dataInicio: string;
  quantidadeSessoes: number;
};

export type CadastrarTratamentoPayload = {
  pacienteId: string;
  unidadeId: string;
  tipoTratamentoId: string | null;
  descricao: string;
  codigoSusLiberacao: string | null;
  observacoes: string | null;
  horaPrevistaBusca: string | null;
  periodicidade: {
    tipo: number;
    intervaloDias: number | null;
    diasSemanaMascara: number | null;
    dataInicio: string;
    quantidadeSessoes: number;
  };
  datas: string[];
};

export type AtualizarTratamentoPayload = {
  descricao: string;
  tipoTratamentoId: string | null;
  codigoSusLiberacao: string | null;
  observacoes: string | null;
  horaPrevistaBusca: string | null;
};

export type AdicionarSessaoPayload = {
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
};

export type AtualizarSessaoPayload = {
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
  observacoes: string | null;
};

export type ConfirmarSessaoPayload = {
  realizada: boolean;
  nomeAcompanhante: string | null;
  parentescoAcompanhante: string | null;
  motoristaIdaId: string | null;
  veiculoIdaId: string | null;
  horaSaidaResidencia: string | null;
  horaChegadaUnidade: string | null;
  motoristaVoltaId: string | null;
  veiculoVoltaId: string | null;
  horaSaidaUnidade: string | null;
  horaChegadaResidencia: string | null;
  motivoNaoRealizacao: string | null;
  observacoes: string | null;
};
