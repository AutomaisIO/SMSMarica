export type EstatisticasResumo = {
  totalMensagens: number;
  enviadas: number;
  recebidas: number;
  templatesSistema: number;
  templatesAtendente: number;
  mensagensSessao: number;
  conversasNovas: number;
  diasNoPeriodo: number;
  mediaDiaria: number;
  taxaEntrega: number;
  taxaLeitura: number;
  atendentes: number;
};

export type SerieDia = { dia: string; enviadas: number; recebidas: number };

export type RotuloContagem = { rotulo: string; total: number };

export type RoboConsumoAssunto = {
  assunto: string;
  turnos: number;
  tokensEntrada: number;
  tokensSaida: number;
  tokensTotal: number;
  custoUsd: number;
};

export type RoboConsumo = {
  turnos: number;
  tokensEntrada: number;
  tokensSaida: number;
  tokensTotal: number;
  custoUsd: number;
  porAssunto: RoboConsumoAssunto[];
};

export type EstatisticasWhatsApp = {
  de: string;
  ate: string;
  resumo: EstatisticasResumo;
  porDia: SerieDia[];
  porCategoria: RotuloContagem[];
  porTemplate: RotuloContagem[];
  porStatus: RotuloContagem[];
  porAtendente: RotuloContagem[];
  robo: RoboConsumo;
};
