import { http } from '@/shared/api/httpClient';
import type { EstatisticasExamesImagem } from '@/features/relatorios-imagem/types';

export async function obterEstatisticasExamesImagem(
  de?: string,
  ate?: string,
  unidadeId?: string,
): Promise<EstatisticasExamesImagem> {
  const { data } = await http.get<EstatisticasExamesImagem>('/estatisticas/exames-imagem', {
    params: { de: de || undefined, ate: ate || undefined, unidadeId: unidadeId || undefined },
  });
  return data;
}

/** Baixa o CSV analítico (uma linha por exame, com PII). O download já sai autenticado. */
export async function exportarExamesImagem(
  de?: string,
  ate?: string,
  unidadeId?: string,
): Promise<void> {
  const resp = await http.get('/estatisticas/exames-imagem/exportar', {
    params: { de: de || undefined, ate: ate || undefined, unidadeId: unidadeId || undefined },
    responseType: 'blob',
  });

  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const nome = match?.[1] ?? `exames-imagem_${de ?? ''}_a_${ate ?? ''}.csv`;

  const url = URL.createObjectURL(new Blob([resp.data], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
