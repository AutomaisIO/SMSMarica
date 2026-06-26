/** Um elemento de tag no formato DICOM-JSON (QIDO-RS / WADO-RS metadata). */
export type ElementoDicom = {
  vr: string;
  Value?: unknown[];
};

/** Um dataset DICOM-JSON: mapa de tag (ex.: "00100010") para o elemento. */
export type DatasetDicom = Record<string, ElementoDicom>;

export type Estudo = {
  studyInstanceUID: string;
  accessionNumber: string;
  patientName: string;
  patientId: string;
  patientSex: string;
  patientAge: string;
  studyDate: string;
  studyDateFormatado: string;
  studyTime: string;
  modalidade: string;
  studyDescription: string;
  numeroSeries: string;
  numeroInstancias: string;
};

export type Serie = {
  seriesInstanceUID: string;
  seriesNumber: string;
  seriesDescription: string;
  modalidade: string;
  numeroInstancias: number;
};

/**
 * Vínculo de um estudo do PACS a uma solicitação/paciente. `explicita` = associação
 * manual/automática persistida (pode desassociar); senão é o casamento implícito do
 * exame de worklist. `origem` só vem nas explícitas.
 */
export type AssociacaoExame = {
  studyInstanceUID: string;
  solicitacaoExameId: string;
  accessionNumber: string;
  pacienteId: string;
  pacienteNome: string | null;
  explicita: boolean;
  origem: 'Manual' | 'Automatica' | null;
  prioridade: 'Eletiva' | 'Prioritaria' | 'Urgente';
  temAnamnese: boolean;
};

export type TipoBuscaNome = 'inicio' | 'qualquer';

export type FiltroBusca = {
  nome: string;
  tipoBuscaNome: TipoBuscaNome;
  dataInicial: string;
  dataFinal: string;
  limite: number;
  /** Offset para paginação (default 0). */
  offset?: number;
};

/**
 * Snapshot opaco do estado de annotations do Cornerstone (output de
 * `annotationManager.state.getAllAnnotations()`). Tratamos como JSON arbitrário
 * — quem entende o formato é o próprio Cornerstone.
 */
export type PayloadAnotacoes = unknown;

export type EstudoAnotacaoVersao = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  payload: PayloadAnotacoes;
  usuarioId: string;
  usuarioNome: string;
  criadoEm: string;
  comentario: string | null;
};

export type EstudoAnotacaoVersaoResumo = {
  id: string;
  versao: number;
  usuarioId: string;
  usuarioNome: string;
  criadoEm: string;
  comentario: string | null;
};
