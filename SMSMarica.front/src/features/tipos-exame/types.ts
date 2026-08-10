export type ModalidadeDicom =
  | 'Indefinida'
  | 'CR' | 'DX' | 'MG' | 'US' | 'CT' | 'MR' | 'NM' | 'PT' | 'OT';

export const MODALIDADES_DICOM: { valor: ModalidadeDicom; rotulo: string }[] = [
  // Primeiro da lista de propósito: é o estado dos tipos que o SISREG criou e ninguém configurou.
  { valor: 'Indefinida', rotulo: 'Não configurada' },
  { valor: 'MG', rotulo: 'Mamografia (MG)' },
  { valor: 'CR', rotulo: 'Radiografia computadorizada (CR)' },
  { valor: 'DX', rotulo: 'Radiografia digital (DX)' },
  { valor: 'US', rotulo: 'Ultrassom (US)' },
  { valor: 'CT', rotulo: 'Tomografia (CT)' },
  { valor: 'MR', rotulo: 'Ressonância magnética (MR)' },
  { valor: 'NM', rotulo: 'Medicina nuclear (NM)' },
  { valor: 'PT', rotulo: 'PET (PT)' },
  { valor: 'OT', rotulo: 'Outro (OT)' },
];

export type TipoExame = {
  id: string;
  /** Nome do procedimento no SISREG, em MAIÚSCULAS. */
  nome: string;
  /** O `pa` do SISREG. Null quando o SISREG não informou — acontece em ~1/3 das linhas. */
  codigoSisreg: string | null;
  /** Criado pela importação, sem ninguém ter configurado. */
  autoCriado: boolean;
  /** Correlação de faturamento. Opcional — não identifica nem libera a execução. */
  procedimentoSigtapId: string | null;
  procedimentoSigtapCodigo: string;
  procedimentoSigtapNome: string;
  modalidadeDicom: ModalidadeDicom;
  requestedProcedureDescription: string;
  scheduledProcedureStepDescription: string;
  codigosProtocolo: string[];
  tempoEstimadoMinutos: number | null;
  unidadePadraoId: string | null;
  unidadePadraoNome: string | null;
  ativo: boolean;
  enviarParaWorklist: boolean;
  criadoEm: string;
};

export type TipoExameListItem = {
  id: string;
  nome: string;
  codigoSisreg: string | null;
  autoCriado: boolean;
  /** Nasceu da importação e ainda não tem configuração DICOM — não vai ao PACS. */
  aguardandoConfiguracaoDicom: boolean;
  modalidadeDicom: ModalidadeDicom;
  procedimentoSigtapCodigo: string;
  tempoEstimadoMinutos: number | null;
  ativo: boolean;
  enviarParaWorklist: boolean;
};

export type SalvarTipoExamePayload = {
  nome: string;
  codigoSisreg: string | null;
  procedimentoSigtapId: string | null;
  modalidadeDicom: ModalidadeDicom;
  requestedProcedureDescription: string;
  scheduledProcedureStepDescription: string;
  codigosProtocolo: string[];
  tempoEstimadoMinutos: number | null;
  unidadePadraoId: string | null;
  ativo?: boolean;
  enviarParaWorklist?: boolean;
};
