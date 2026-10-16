/** Enums chegam como string (JsonStringEnumConverter global no backend). */
export type TipoOcorrencia = 'Acidente' | 'PaneVeiculo' | 'PacienteMalEstar' | 'Outro';
export type SituacaoOcorrencia = 'Aberta' | 'EmAtendimento' | 'Encerrada';

/** Ocorrência reportada pelo motorista no tablet do veículo (botão REPORT). */
export type Ocorrencia = {
  id: string;
  veiculoId: string;
  veiculoPlaca: string;
  veiculoModelo: string;
  tipo: TipoOcorrencia;
  descricao: string | null;
  /** Null quando o tablet ainda não tinha GPS no momento do REPORT. */
  latitude: number | null;
  longitude: number | null;
  precisaoM: number | null;
  rotaId: string | null;
  /** Relógio do tablet. */
  ocorridaEm: string;
  recebidaEm: string;
  situacao: SituacaoOcorrencia;
  atendidaEm: string | null;
  atendidaPorNome: string | null;
  encerradaEm: string | null;
  encerradaPorNome: string | null;
  observacaoCentral: string | null;
};

/** Evento `ocorrenciaRegistrada` do hub /hubs/rastreamento. */
export type OcorrenciaRegistradaEvento = {
  ocorrenciaId: string;
  veiculoId: string;
  veiculoPlaca: string;
  tipo: TipoOcorrencia;
};
