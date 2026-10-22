import { http } from '@/shared/api/httpClient';

/** Uma imagem retirada do que vai ao paciente (continua no PACS e aqui no visualizador). */
export type ImagemInvalidada = {
  sopInstanceUID: string;
  motivo: string | null;
  invalidadaEm: string;
  invalidadaPorId: string;
  invalidadaPorNome: string;
  /** Quem retirou ou quem tem acesso global — decidido no backend. */
  podeRestaurar: boolean;
};

export type ImagensInvalidadasDoEstudo = {
  /** Quem lauda ou quem tem acesso global — decidido no backend. */
  podeInvalidar: boolean;
  imagens: ImagemInvalidada[];
};

function rotaBase(studyUID: string): string {
  return `/estudos/${encodeURIComponent(studyUID)}/imagens-invalidadas`;
}

export async function listarImagensInvalidadas(studyUID: string): Promise<ImagensInvalidadasDoEstudo> {
  const { data } = await http.get<ImagensInvalidadasDoEstudo>(rotaBase(studyUID));
  return data;
}

/** Retira a imagem do PDF do paciente; o backend refaz o arquivo dele na hora. */
export async function invalidarImagem(
  studyUID: string,
  sopUID: string,
  motivo?: string | null,
): Promise<ImagensInvalidadasDoEstudo> {
  const { data } = await http.put<ImagensInvalidadasDoEstudo>(
    `${rotaBase(studyUID)}/${encodeURIComponent(sopUID)}`,
    { motivo: motivo ?? null },
  );
  return data;
}

/** Devolve a imagem ao PDF do paciente; o backend refaz o arquivo dele na hora. */
export async function restaurarImagem(studyUID: string, sopUID: string): Promise<ImagensInvalidadasDoEstudo> {
  const { data } = await http.delete<ImagensInvalidadasDoEstudo>(
    `${rotaBase(studyUID)}/${encodeURIComponent(sopUID)}`,
  );
  return data;
}
