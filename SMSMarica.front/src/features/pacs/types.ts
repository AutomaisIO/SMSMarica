/** Um elemento de tag no formato DICOM-JSON (QIDO-RS / WADO-RS metadata). */
export type ElementoDicom = {
  vr: string;
  Value?: unknown[];
};

/** Um dataset DICOM-JSON: mapa de tag (ex.: "00100010") para o elemento. */
export type DatasetDicom = Record<string, ElementoDicom>;

export type Estudo = {
  studyInstanceUID: string;
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

export type TipoBuscaNome = 'inicio' | 'qualquer';

export type FiltroBusca = {
  nome: string;
  tipoBuscaNome: TipoBuscaNome;
  dataInicial: string;
  dataFinal: string;
  limite: number;
};
