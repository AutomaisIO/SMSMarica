import type { DatasetDicom } from '@/features/pacs/types';

/** Tags DICOM usadas no módulo PACS (hex string como vem no DICOM-JSON). */
export const Tag = {
  StudyDate: '00080020',
  StudyTime: '00080030',
  ModalitiesInStudy: '00080061',
  Modality: '00080060',
  StudyDescription: '00081030',
  SeriesDescription: '0008103E',
  SOPInstanceUID: '00080018',
  PatientName: '00100010',
  PatientID: '00100020',
  PatientBirthDate: '00100030',
  PatientSex: '00100040',
  PatientAge: '00101010',
  StudyInstanceUID: '0020000D',
  SeriesInstanceUID: '0020000E',
  SeriesNumber: '00200011',
  InstanceNumber: '00200013',
  NumberOfStudyRelatedSeries: '00201206',
  NumberOfStudyRelatedInstances: '00201208',
  NumberOfSeriesRelatedInstances: '00201209',
  PixelSpacing: '00280030',
  ImagerPixelSpacing: '00181164',
  ImageOrientationPatient: '00200037',
  ImagePositionPatient: '00200032',
} as const;

/** Primeiro valor textual de uma tag, ou string vazia. */
export function valorTexto(ds: DatasetDicom, tag: string): string {
  const valor = ds[tag]?.Value?.[0];
  if (valor == null) return '';
  if (typeof valor === 'string') return valor;
  return String(valor);
}

/** Nome de paciente (VR PN): pega o componente Alphabetic e remove o "^". */
export function valorNomePaciente(ds: DatasetDicom, tag: string): string {
  const valor = ds[tag]?.Value?.[0] as { Alphabetic?: string } | string | undefined;
  if (valor == null) return '';
  if (typeof valor === 'string') return valor.replace(/\^/g, ' ').trim();
  return (valor.Alphabetic ?? '').replace(/\^/g, ' ').trim();
}

/** Primeiro valor numérico de uma tag, ou null. */
export function valorNumero(ds: DatasetDicom, tag: string): number | null {
  const valor = ds[tag]?.Value?.[0];
  if (valor == null) return null;
  const n = Number(valor);
  return Number.isFinite(n) ? n : null;
}

/** AAAAMMDD -> DD/MM/AAAA. */
export function formatarDataDicom(da: string): string {
  if (!da || da.length < 8) return da ?? '';
  return `${da.slice(6, 8)}/${da.slice(4, 6)}/${da.slice(0, 4)}`;
}

/** Idade DICOM (ex.: "062Y") -> "62". */
export function formatarIdadeDicom(ageStr: string): string {
  if (!ageStr) return '';
  const m = ageStr.match(/^0*(\d+)([YMWD])$/i);
  return m ? m[1] : ageStr;
}

/**
 * Faz o Cornerstone3D mostrar a régua em mm para imagens 2D (MG, DX, CR, MMG).
 *
 * Dois truques:
 *  1. Equipamentos como Oehm und Rehbein às vezes só preenchem
 *     `ImagerPixelSpacing` (00181164) sem `PixelSpacing` (00280030). Para MG
 *     digital os dois são equivalentes (placa colada na mama).
 *  2. O provider do Cornerstone marca `usingDefaultValues=true` se faltarem
 *     `ImageOrientationPatient` (00200037) ou `ImagePositionPatient` (00200032),
 *     e o `StackViewport` aí seta `hasPixelSpacing=false` → régua em px.
 *     Essas tags são para corte tomográfico (CT/MR); radiografia/MG não traz.
 *     Injetamos valores neutros (orientação padrão, origem no canto) para
 *     destravar a régua em mm sem afetar nada mais (não há MPR no Stack).
 */
export function garantirPixelSpacing(ds: DatasetDicom): DatasetDicom {
  const temPS = (ds[Tag.PixelSpacing]?.Value?.length ?? 0) > 0;
  const ips = ds[Tag.ImagerPixelSpacing];
  if (!temPS && (ips?.Value?.length ?? 0) > 0) {
    ds[Tag.PixelSpacing] = { vr: 'DS', Value: ips!.Value };
  }
  const aindaTemPS = (ds[Tag.PixelSpacing]?.Value?.length ?? 0) > 0;
  if (aindaTemPS) {
    if (!ds[Tag.ImageOrientationPatient]?.Value?.length) {
      ds[Tag.ImageOrientationPatient] = { vr: 'DS', Value: ['1', '0', '0', '0', '1', '0'] };
    }
    if (!ds[Tag.ImagePositionPatient]?.Value?.length) {
      ds[Tag.ImagePositionPatient] = { vr: 'DS', Value: ['0', '0', '0'] };
    }
  }
  return ds;
}
