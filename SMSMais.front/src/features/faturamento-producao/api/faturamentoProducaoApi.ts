import { http } from '@/shared/api/httpClient';
import type { BpaPreview, ConfigUnidade, UnidadeHabilitada } from '@/features/faturamento-producao/types';

/** Unidades liberadas para o usuário gerar BPA (habilitadas ∩ escopo). */
export async function listarUnidadesHabilitadas(): Promise<UnidadeHabilitada[]> {
  const { data } = await http.get<UnidadeHabilitada[]>('/faturamento-producao/unidades-habilitadas');
  return data;
}

/** Preview da competência: linhas + estatísticas de qualidade. */
export async function obterPreviewBpa(unidadeId: string, competencia: number): Promise<BpaPreview> {
  const { data } = await http.get<BpaPreview>('/faturamento-producao/preview', {
    params: { unidadeId, competencia },
  });
  return data;
}

/** Baixa o arquivo BPA Magnético (só linhas válidas). O download já sai autenticado (via `http`). */
export async function baixarArquivoBpa(unidadeId: string, competencia: number): Promise<void> {
  const resp = await http.get('/faturamento-producao/arquivo', {
    params: { unidadeId, competencia },
    responseType: 'blob',
  });

  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const nome = match?.[1] ?? `bpa-${competencia}.txt`;

  const url = URL.createObjectURL(new Blob([resp.data], { type: 'text/plain;charset=iso-8859-1' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** Configurações: todas as unidades do escopo + flag de habilitada (exige ação Edição no backend). */
export async function listarConfiguracaoFaturamento(): Promise<ConfigUnidade[]> {
  const { data } = await http.get<ConfigUnidade[]>('/faturamento-producao/configuracao/unidades');
  return data;
}

export async function definirUnidadeHabilitada(unidadeId: string, habilitada: boolean): Promise<void> {
  await http.put(`/faturamento-producao/configuracao/unidades/${unidadeId}`, { habilitada });
}
