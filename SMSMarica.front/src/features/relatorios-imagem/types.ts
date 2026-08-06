/**
 * Três eixos que NÃO são o mesmo número:
 * - `laudados`/`assinados` contam EXAMES (cobertura da fila);
 * - `laudosEmitidos`/`laudosAssinados` contam LAUDOS, retificação inclusa (produção médica).
 * Um exame retificado 3x é 1 laudado e 3 emitidos.
 */
export type ExamesImagemResumo = {
  totalExames: number;
  realizados: number;
  laudados: number;
  aguardandoLaudo: number;
  laudosEmitidos: number;
  laudosAssinados: number;
  assinados: number;
  aguardandoAssinatura: number;
  cancelados: number;
  medicosLaudando: number;
  diasNoPeriodo: number;
  mediaExamesDia: number;
  percentualLaudados: number;
  percentualAssinados: number;
  tempoMedioChegadaExecucaoHoras: number | null;
  tempoMedioExecucaoLaudoHoras: number | null;
  tempoMedioLaudoAssinaturaHoras: number | null;
  tempoMedioTotalHoras: number | null;
  amostraChegadaExecucao: number;
  amostraExecucaoLaudo: number;
  amostraLaudoAssinatura: number;
  amostraTotal: number;
};

/** Produção de um médico no período — trabalho feito, não só exame coberto. */
export type ProducaoMedico = {
  medico: string;
  crm: string | null;
  examesLaudados: number;
  laudosEmitidos: number;
  laudosAssinados: number;
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
  logradouro: string | null;
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
  porMedico: ProducaoMedico[];
};
