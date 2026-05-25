export type ModalidadeDicom = 'CR' | 'DX' | 'MG' | 'US' | 'CT' | 'MR' | 'NM' | 'PT' | 'OT';

export const MODALIDADES_DICOM: { valor: ModalidadeDicom; rotulo: string }[] = [
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
  nome: string;
  procedimentoSigtapId: string;
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
  criadoEm: string;
};

export type TipoExameListItem = {
  id: string;
  nome: string;
  modalidadeDicom: ModalidadeDicom;
  procedimentoSigtapCodigo: string;
  tempoEstimadoMinutos: number | null;
  ativo: boolean;
};

export type SalvarTipoExamePayload = {
  nome: string;
  procedimentoSigtapId: string;
  modalidadeDicom: ModalidadeDicom;
  requestedProcedureDescription: string;
  scheduledProcedureStepDescription: string;
  codigosProtocolo: string[];
  tempoEstimadoMinutos: number | null;
  unidadePadraoId: string | null;
  ativo?: boolean;
};
