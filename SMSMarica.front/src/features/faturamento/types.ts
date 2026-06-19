// Enums serializados como string pelo backend (JsonStringEnumConverter global).
export type StatusFaturamento = 'Pendente' | 'EmBpa' | 'Faturado' | 'Cancelado';

export type DimensaoFaturamento =
  | 'Paciente'
  | 'Motorista'
  | 'Veiculo'
  | 'TipoTratamento'
  | 'Unidade';

export type RegistroFaturamento = {
  id: string;
  sessaoId: string;
  pacienteId: string;
  pacienteNome: string;
  motoristaId: string | null;
  veiculoId: string | null;
  tipoTratamentoId: string | null;
  unidadeId: string;
  unidadeNome: string;
  competencia: number;
  data: string;
  kmComPaciente: number;
  unidades: number;
  valorUnitario: number;
  valorTotal: number;
  codigoSigtap: string | null;
  status: StatusFaturamento;
};

export type ResumoFaturamentoItem = {
  chaveId: string;
  descricao: string;
  qtdRegistros: number;
  totalKm: number;
  totalUnidades: number;
  totalValor: number;
};

export type ResumoFaturamento = {
  dimensao: DimensaoFaturamento;
  competencia: number | null;
  de: string | null;
  ate: string | null;
  itens: ResumoFaturamentoItem[];
  totalGeralUnidades: number;
  totalGeralValor: number;
};

export type ConfigFaturamento = {
  valorPor50Km: number;
  kmPorUnidade: number;
  codigoSigtap: string | null;
  ativo: boolean;
};
