import { http } from '@/shared/api/httpClient';
import type { DatasetDicom, Estudo, FiltroBusca, Serie } from '@/features/pacs/types';
import {
  Tag,
  formatarDataDicom,
  formatarIdadeDicom,
  valorNomePaciente,
  valorNumero,
  valorTexto,
} from '@/features/pacs/lib/dicomJson';

const HEADERS_DICOM = { Accept: 'application/dicom+json' };

function semData(yyyymmdd: string): string {
  return yyyymmdd.replace(/-/g, '');
}

function mapearEstudo(ds: DatasetDicom): Estudo {
  const studyDate = valorTexto(ds, Tag.StudyDate);
  return {
    studyInstanceUID: valorTexto(ds, Tag.StudyInstanceUID),
    patientName: valorNomePaciente(ds, Tag.PatientName),
    patientId: valorTexto(ds, Tag.PatientID),
    patientSex: valorTexto(ds, Tag.PatientSex),
    patientAge: formatarIdadeDicom(valorTexto(ds, Tag.PatientAge)),
    studyDate,
    studyDateFormatado: formatarDataDicom(studyDate),
    studyTime: valorTexto(ds, Tag.StudyTime),
    modalidade: valorTexto(ds, Tag.ModalitiesInStudy) || valorTexto(ds, Tag.Modality),
    studyDescription: valorTexto(ds, Tag.StudyDescription),
    numeroSeries: valorTexto(ds, Tag.NumberOfStudyRelatedSeries),
    numeroInstancias: valorTexto(ds, Tag.NumberOfStudyRelatedInstances),
  };
}

function mapearSerie(ds: DatasetDicom): Serie {
  return {
    seriesInstanceUID: valorTexto(ds, Tag.SeriesInstanceUID),
    seriesNumber: valorTexto(ds, Tag.SeriesNumber),
    seriesDescription: valorTexto(ds, Tag.SeriesDescription),
    modalidade: valorTexto(ds, Tag.Modality),
    numeroInstancias: valorNumero(ds, Tag.NumberOfSeriesRelatedInstances) ?? 0,
  };
}

export async function buscarEstudos(filtro: FiltroBusca): Promise<Estudo[]> {
  const params: Record<string, string | number> = {
    limit: filtro.limite,
    includefield: 'all',
    fuzzymatching: 'true',
  };

  const nome = filtro.nome.trim();
  if (nome) params[Tag.PatientName] = `*${nome}*`;

  const di = filtro.dataInicial ? semData(filtro.dataInicial) : '';
  const df = filtro.dataFinal ? semData(filtro.dataFinal) : '';
  if (di && df) params[Tag.StudyDate] = `${di}-${df}`;
  else if (di) params[Tag.StudyDate] = `${di}-`;
  else if (df) params[Tag.StudyDate] = `-${df}`;

  // Sem filtro algum: mostrar os mais recentes.
  if (!nome && !di && !df) params.orderby = `-${Tag.StudyDate}`;

  const { data } = await http.get<DatasetDicom[]>('/pacs/rs/studies', {
    params,
    headers: HEADERS_DICOM,
  });
  return (data ?? []).map(mapearEstudo);
}

export async function listarSeries(studyUID: string): Promise<Serie[]> {
  const { data } = await http.get<DatasetDicom[]>(`/pacs/rs/studies/${studyUID}/series`, {
    params: { includefield: 'all' },
    headers: HEADERS_DICOM,
  });
  return (data ?? [])
    .map(mapearSerie)
    .sort((a, b) => Number(a.seriesNumber) - Number(b.seriesNumber));
}

/** Metadados completos (por instância) de uma série — base para montar os imageIds. */
export async function obterMetadadosSerie(
  studyUID: string,
  seriesUID: string,
): Promise<DatasetDicom[]> {
  const { data } = await http.get<DatasetDicom[]>(
    `/pacs/rs/studies/${studyUID}/series/${seriesUID}/metadata`,
    { headers: HEADERS_DICOM },
  );
  return data ?? [];
}
