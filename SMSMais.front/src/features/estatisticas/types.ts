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

/** Tokens e custo vêm null para quem não tem o módulo EstatisticaCustos. */
export type RoboConsumoAssunto = {
  assunto: string;
  turnos: number;
  tokensEntrada: number | null;
  tokensSaida: number | null;
  tokensTotal: number | null;
  custoUsd: number | null;
};

export type RoboConsumo = {
  turnos: number;
  tokensEntrada: number | null;
  tokensSaida: number | null;
  tokensTotal: number | null;
  custoUsd: number | null;
  porAssunto: RoboConsumoAssunto[];
};

export type CustoMetaTemplate = {
  template: string;
  categoria: string;
  enviadas: number;
  cobradas: number;
  tarifaUsd: number | null;
  totalUsd: number;
};

export type CustoMetaDia = { dia: string; enviadas: number; cobradas: number; totalUsd: number };

/** Estimativa de custo Meta (templates × tarifa da categoria). Só para quem vê custos. */
export type CustosMeta = {
  tarifaCadastrada: boolean;
  totalUsd: number;
  templatesEnviados: number;
  templatesCobrados: number;
  templatesGratis: number;
  porTemplate: CustoMetaTemplate[];
  porDia: CustoMetaDia[];
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
  custosMeta: CustosMeta | null;
  veCustos: boolean;
};
