import type { Necessidades } from '@/features/tratamentos/types';

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
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  horaPrevistaBusca: string | null;
  assentoId: string;
  fileiraOrdem: number;
  numeroAssento: number;
  tipo: TipoAlocacao;
  /** Situação da viagem: o tablet marca realizada/não realizada; a central corrige. */
  statusSessao: string;
  /** A central confirmou/corrigiu a viagem no painel — vale isso, não o tablet. */
  corrigidaNoPainel: boolean;
};

export type SessaoElegivel = {
  sessaoId: string;
  tratamentoId: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  status: 'Pendente' | 'Confirmada';
  vencida: boolean;
  /** Condição do paciente registrada no atendimento — só leitura; o gerador ainda não usa. */
  necessidades: Necessidades;
  acompanhantesPrevistos: number;
  limiteAcompanhantes: number;
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

// ---- Geração automática do translado (FT3) ----

export type GerarTransladoPayload = {
  data: string;
  confirmar: boolean;
  usarIa: boolean;
};

export type ParadaGerada = {
  ordem: number;
  sessaoId: string;
  pacienteId: string;
  pacienteNome: string;
  comAcompanhante: boolean;
  necessidades: Necessidades | null;
  acompanhantesPrevistos: number;
  limiteAcompanhantes: number;
};

export type RotaGerada = {
  rotaId: string | null;
  veiculoId: string;
  veiculoPlaca: string;
  motoristaId: string;
  motoristaNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  qtdPacientes: number;
  distanciaTotalMetros: number;
  duracaoEstimadaSegundos: number;
  paradas: ParadaGerada[];
};

export type SessaoNaoAlocada = {
  sessaoId: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  motivo: string;
};

export type ResultadoGeracao = {
  data: string;
  confirmado: boolean;
  usouIa: boolean;
  aproximado: boolean;
  totalSessoes: number;
  totalAlocadas: number;
  rotas: RotaGerada[];
  naoAlocadas: SessaoNaoAlocada[];
};

// ---- Viagem registrada pelo tablet do veículo (etapa 2) ----

export const TIPOS_EVENTO_VIAGEM = ['Inicio', 'Embarque', 'Ausencia', 'Desembarque', 'Conclusao'] as const;
export type TipoEventoViagem = (typeof TIPOS_EVENTO_VIAGEM)[number];

/** Uma marcação do tablet. `aplicado=false`: não mudou a viagem (a central já tinha corrigido
 * no painel, sessão ou rota cancelada) — o motivo vem ao lado. */
export type EventoViagem = {
  id: string;
  tipo: TipoEventoViagem;
  ocorridoEm: string;
  recebidoEm: string;
  sessaoId: string | null;
  pacienteNome: string | null;
  pessoa: 'Paciente' | 'Acompanhante' | null;
  acompanhanteNome: string | null;
  motoristaNome: string | null;
  latitude: number | null;
  longitude: number | null;
  aplicado: boolean;
  motivoNaoAplicado: string | null;
  /** Marcação da volta (embarque na unidade, chegada em casa). */
  volta: boolean;
};

// ---- Endereços que o Google não localizou (revisão de geocódigos) ----

/** Endereço do paciente sem ponto confiável no mapa. `enderecoNormalizado` vem sem acento e em
 * minúsculas (é a chave do cache). */
export type EnderecoParaRevisar = {
  id: string;
  enderecoNormalizado: string;
  latitude: number;
  longitude: number;
  fonte: string;
  precisao: string | null;
  revisaoPendente: boolean;
  geocodificadoEm: string;
};

// ---- Volta: quem aguarda retorno e "puxar" para uma rota (etapa 3) ----

/** Paciente que terminou (ou deve estar terminando) o atendimento e espera a volta. Em Maricá a van
 * não espera: entra quem desceu hoje; fora do município, só quem ficou na unidade quando ela saiu. */
export type PacienteAguardando = {
  sessaoId: string;
  tratamentoId: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  latitude: number | null;
  longitude: number | null;
  distanciaMetros: number | null;
  acompanhanteEsperado: boolean;
  dataPrevista: string;
  foraDoMunicipio: boolean;
  /** "HH:mm:ss": o marcado na sessão, ou chegada + tempo médio do tratamento. */
  prontoPrevisto: string | null;
};
