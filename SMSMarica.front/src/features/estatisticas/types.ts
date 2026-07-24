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

export type EstatisticasWhatsApp = {
  de: string;
  ate: string;
  resumo: EstatisticasResumo;
  porDia: SerieDia[];
  porCategoria: RotuloContagem[];
  porTemplate: RotuloContagem[];
  porStatus: RotuloContagem[];
  porAtendente: RotuloContagem[];
};
