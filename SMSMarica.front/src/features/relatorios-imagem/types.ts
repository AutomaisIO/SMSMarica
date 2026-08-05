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

/** Linha da exportação de FATURAMENTO (com PII, ticket #74). Uma por exame realizado. */
export type ExameFaturamento = {
  paciente: string;
  cpf: string | null;
  cns: string | null;
  nascimento: string | null; // yyyy-MM-dd
  cep: string | null;
  celular: string | null;
  exame: string | null;
  realizacao: string | null; // ISO
};

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
