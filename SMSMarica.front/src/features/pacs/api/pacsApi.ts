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
  // fuzzymatching=true quebra wildcard PN (testado contra esse dcm4chee — sempre
  // retorna 204 com *NOME*), então deixamos desligado. A normalização do nome
  // (NFD + uppercase + espaços→*) já cobre acentos e variações.
  const params: Record<string, string | number> = {
    limit: filtro.limite,
    offset: filtro.offset ?? 0,
    includefield: 'all',
  };

  // DICOM armazena PatientName como "Familia^Nome^Meio" (ou variações com
  // "^" no começo quando o equipamento joga tudo em "Given"). Para a busca
  // pegar nas duas formas sem o usuário se preocupar:
  //   - tira acentos (NFD + corta combining marks)
  //   - sobe pra MAIÚSCULAS (alguns dcm4chee são case-sensitive em PN)
  //   - troca espaços por "*" — assim "Silva Joao" vira "*SILVA*JOAO*",
  //     que casa contra "Silva^João^Santos" mesmo com o "^" no meio.
  const nomeNormalizado = filtro.nome
    .normalize('NFD')
    // Faixa U+0300–U+036F = "Combining Diacritical Marks" (acentos
    // soltos após NFD). Removidos para "João" virar "Joao".
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .toUpperCase()
    .replace(/\s+/g, '*');
  if (nomeNormalizado) {
    params[Tag.PatientName] =
      filtro.tipoBuscaNome === 'inicio' ? `${nomeNormalizado}*` : `*${nomeNormalizado}*`;
  }

  const di = filtro.dataInicial ? semData(filtro.dataInicial) : '';
  const df = filtro.dataFinal ? semData(filtro.dataFinal) : '';
  if (di && df) params[Tag.StudyDate] = `${di}-${df}`;
  else if (di) params[Tag.StudyDate] = `${di}-`;
  else if (df) params[Tag.StudyDate] = `-${df}`;

  // Sempre ordenar do mais novo para o mais velho. dcm4chee aceita orderby
  // mesmo quando há filtros; sem filtros o resultado também vem ordenado.
  params.orderby = `-${Tag.StudyDate},-${Tag.StudyTime}`;

  const { data } = await http.get<DatasetDicom[]>('/pacs/rs/studies', {
    params,
    headers: HEADERS_DICOM,
  });
  // QIDO-RS responde 204 No Content quando não há matches — axios entrega "" em vez de array.
  return (Array.isArray(data) ? data : []).map(mapearEstudo);
}

export async function listarSeries(studyUID: string): Promise<Serie[]> {
  const { data } = await http.get<DatasetDicom[]>(`/pacs/rs/studies/${studyUID}/series`, {
    params: { includefield: 'all' },
    headers: HEADERS_DICOM,
  });
  return (Array.isArray(data) ? data : [])
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
  return Array.isArray(data) ? data : [];
}

/**
 * Exclui um estudo do PACS. O backend trata o passo-a-passo do dcm4chee
 * (reject + delete permanente).
 */
export async function excluirEstudo(studyUID: string): Promise<void> {
  await http.delete(`/pacs/rs/studies/${encodeURIComponent(studyUID)}`);
}
