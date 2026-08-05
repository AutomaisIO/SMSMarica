import { http } from '@/shared/api/httpClient';
import type { EstatisticasExamesImagem } from '@/features/relatorios-imagem/types';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

export async function obterEstatisticasExamesImagem(
  de?: string,
  ate?: string,
  unidadeId?: string,
  modalidade?: ModalidadeDicom,
  tipoExameId?: string,
): Promise<EstatisticasExamesImagem> {
  const { data } = await http.get<EstatisticasExamesImagem>('/estatisticas/exames-imagem', {
    params: {
      de: de || undefined,
      ate: ate || undefined,
      unidadeId: unidadeId || undefined,
      modalidade: modalidade || undefined,
      tipoExameId: tipoExameId || undefined,
    },
  });
  return data;
}

export type ConteudoExportacao = 'Exames' | 'Laudos' | 'ExamesLaudos';

/** Baixa o CSV analítico (sem PII), no conteúdo escolhido. O download já sai autenticado. */
export async function exportarExamesImagem(
  de?: string,
  ate?: string,
  unidadeId?: string,
  modalidade?: ModalidadeDicom,
  tipoExameId?: string,
  conteudo: ConteudoExportacao = 'ExamesLaudos',
): Promise<void> {
  const resp = await http.get('/estatisticas/exames-imagem/exportar', {
    params: {
      de: de || undefined,
      ate: ate || undefined,
      unidadeId: unidadeId || undefined,
      modalidade: modalidade || undefined,
      tipoExameId: tipoExameId || undefined,
      conteudo,
    },
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
