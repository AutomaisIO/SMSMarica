export const STATUS_ROTA = ['Planejada', 'EmAndamento', 'Concluida', 'Cancelada'] as const;
export type StatusRota = (typeof STATUS_ROTA)[number];

export const TIPOS_ALOCACAO = ['Paciente', 'Acompanhante'] as const;
export type TipoAlocacao = (typeof TIPOS_ALOCACAO)[number];

export type AlocacaoDto = {
  id: string;
  sessaoId: string;
  tratamentoId: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeId: string;
  unidadeNome: string;
  horaPrevistaBusca: string | null;
  assentoId: string;
  fileiraOrdem: number;
  numeroAssento: number;
  tipo: TipoAlocacao;
};

export type SessaoElegivel = {
  sessaoId: string;
  tratamentoId: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeId: string;
  unidadeNome: string;
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  status: 'Pendente' | 'Confirmada';
  vencida: boolean;
};

export type RotaDiariaListItem = {
  id: string;
  data: string;
  veiculoId: string;
  veiculoPlaca: string;
  motoristaId: string;
  motoristaNome: string;
  status: StatusRota;
  totalAlocacoes: number;
};

export type RotaDiaria = {
  id: string;
  data: string;
  veiculoId: string;
  veiculoPlaca: string;
  veiculoModelo: string;
  motoristaId: string;
  motoristaNome: string;
  status: StatusRota;
  criadoEm: string;
  iniciadaEm: string | null;
  concluidaEm: string | null;
  alocacoes: AlocacaoDto[];
};

export type CadastrarRotaPayload = {
  data: string;
  veiculoId: string;
  motoristaId: string;
};

export type AtualizarRotaPayload = CadastrarRotaPayload;

export type CriarAlocacaoPayload = {
  sessaoId: string;
  assentoId: string;
  tipo?: TipoAlocacao;
};

export type FiltrosListarRotas = {
  data?: string;
  motoristaId?: string;
  veiculoId?: string;
};
