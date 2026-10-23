// Contrato com o backend (FaturamentoProducaoController). Enums viajam como string e DateOnly como
// "yyyy-MM-dd" (JsonStringEnumConverter + camelCase globais).

export type UnidadeHabilitada = { id: string; nome: string; cnes: string | null };

export type ConfigUnidade = { id: string; nome: string; cnes: string | null; habilitada: boolean };

export type MotivoContagem = { motivo: string; quantidade: number };

export type BpaEstatisticas = {
  total: number;
  validas: number;
  pendencias: number;
  comSigtap: number;
  comCid: number;
  comCnsProfissional: number;
  comCnsPaciente: number;
  comCpfPaciente: number;
  comSexo: number;
  motivosPendencia: MotivoContagem[];
};

export type BpaLinha = {
  folha: number | null;
  sequencia: number | null;
  procedimentoSigtap: string | null;
  procedimentoTexto: string | null;
  cid: string | null;
  cnsProfissional: string | null;
  cbo: string | null;
  profissionalNome: string | null;
  pacienteNome: string;
  cns: string | null;
  cpf: string | null;
  nascimento: string | null;
  sexo: string;
  dataAtendimento: string | null;
  valida: boolean;
  motivos: string[];
};

export type BpaPreview = {
  unidadeId: string;
  unidadeNome: string;
  cnes: string | null;
  competencia: number;
  folhas: number;
  controle: number;
  estatisticas: BpaEstatisticas;
  linhas: BpaLinha[];
};
