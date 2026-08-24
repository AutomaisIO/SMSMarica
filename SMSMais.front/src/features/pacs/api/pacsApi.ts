import { http } from '@/shared/api/httpClient';
import type { DatasetDicom, Estudo, FiltroBusca, PaginaEstudos, Serie } from '@/features/pacs/types';
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
    accessionNumber: valorTexto(ds, Tag.AccessionNumber),
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

/**
 * Listagem de estudos. NÃO vai direto ao QIDO: passa por `/pacs/estudos`, que aplica o recorte
 * por unidade e o filtro por tipo — dois filtros que dependem do nosso banco, não do DICOM — e
 * pagina do lado do servidor. Os parâmetros abaixo são os mesmos do QIDO e seguem intactos.
 */
export async function buscarEstudos(filtro: FiltroBusca, signal?: AbortSignal): Promise<PaginaEstudos> {
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

  // Modalidade: chave de busca NATIVA do QIDO (0008,0061 ModalitiesInStudy). Multi-valor por
  // vírgula é união no dcm4chee (verificado: MG 1995 + OT 152 = 2147), então o recorte acontece
  // no PACS e a página continua vindo cheia — filtrar depois de receber quebraria a paginação.
  const modalidades = (filtro.modalidades ?? []).filter(Boolean);
  if (modalidades.length > 0) params[Tag.ModalitiesInStudy] = modalidades.join(',');

  const di = filtro.dataInicial ? semData(filtro.dataInicial) : '';
  const df = filtro.dataFinal ? semData(filtro.dataFinal) : '';
  if (di && df) params[Tag.StudyDate] = `${di}-${df}`;
  else if (di) params[Tag.StudyDate] = `${di}-`;
  else if (df) params[Tag.StudyDate] = `-${df}`;

  // Sempre ordenar do mais novo para o mais velho. dcm4chee aceita orderby
  // mesmo quando há filtros; sem filtros o resultado também vem ordenado.
  params.orderby = `-${Tag.StudyDate},-${Tag.StudyTime}`;

  const tipos = (filtro.tipoExameIds ?? []).filter(Boolean);
  if (tipos.length > 0) params.tipoExameIds = tipos.join(',');

  const { data } = await http.get<{ estudos: DatasetDicom[]; orfaosOcultos: number; truncado: boolean }>(
    '/pacs/estudos',
    { params, headers: HEADERS_DICOM, signal },
  );
  return {
    estudos: (Array.isArray(data?.estudos) ? data.estudos : []).map(mapearEstudo),
    orfaosOcultos: data?.orfaosOcultos ?? 0,
    truncado: data?.truncado ?? false,
  };
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
 * Pede ao backend que pré-aqueça (em background) o cache do estudo no proxy
 * WADO-RS. Responde 202 imediatamente; é best-effort — qualquer erro é ignorado
 * para não travar a UI. Disparar sem `await` bloqueante ao abrir o estudo, para
 * o servidor já esquentar o cache em paralelo com o prefetch do cliente.
 */
export async function aquecerEstudo(studyUID: string): Promise<void> {
  try {
    await http.post(`/pacs/aquecer/${encodeURIComponent(studyUID)}`);
  } catch {
    // Silencioso de propósito: o aquecimento é apenas uma otimização.
  }
}

/**
 * Descarta o cache local (proxy) de UMA imagem e o reconstrói no servidor.
 * Corrige o caso em que a entrada em cache de um frame ficou corrompida (imagem
 * abre em branco) enquanto a thumbnail está OK — como o conteúdo é servido como
 * imutável, não dá pra "re-pedir" pelo fluxo normal. Extrai study/series/sop do
 * imageId wadors. Retorna silenciosamente se o imageId não casar o padrão.
 */
export async function recriarImagensDaInstancia(imageId: string): Promise<void> {
  const m = imageId.match(/studies\/([^/]+)\/series\/([^/]+)\/instances\/([^/?]+)/);
  if (!m) return;
  const [, studyUID, seriesUID, sopUID] = m;
  await http.post(
    `/pacs/cache/recriar/${encodeURIComponent(studyUID)}/${encodeURIComponent(seriesUID)}/${encodeURIComponent(sopUID)}`,
  );
}

/**
 * Exclui um estudo do PACS. O backend trata o passo-a-passo do dcm4chee
 * (reject + delete permanente).
 */
export async function excluirEstudo(studyUID: string): Promise<void> {
  await http.delete(`/pacs/rs/studies/${encodeURIComponent(studyUID)}`);
}
