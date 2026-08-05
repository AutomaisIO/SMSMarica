export type ExamesImagemResumo = {
  totalExames: number;
  realizados: number;
  laudados: number;
  aguardandoLaudo: number;
  laudosEmitidos: number;
  cancelados: number;
  medicosLaudando: number;
  diasNoPeriodo: number;
  mediaExamesDia: number;
  percentualLaudados: number;
  tempoMedioChegadaExecucaoHoras: number | null;
  tempoMedioExecucaoLaudoHoras: number | null;
  tempoMedioTotalHoras: number | null;
  amostraChegadaExecucao: number;
  amostraExecucaoLaudo: number;
  amostraTotal: number;
};

export type SerieExamesDia = {
  dia: string;
  registrados: number;
  realizados: number;
  laudados: number;
};

export type RotuloContagem = { rotulo: string; total: number };

export type EstatisticasExamesImagem = {
  de: string;
  ate: string;
  unidadeId: string | null;
  resumo: ExamesImagemResumo;
  porDia: SerieExamesDia[];
  porModalidade: RotuloContagem[];
  porUnidade: RotuloContagem[];
  porStatus: RotuloContagem[];
  porTipoExame: RotuloContagem[];
  porMedico: RotuloContagem[];
};
