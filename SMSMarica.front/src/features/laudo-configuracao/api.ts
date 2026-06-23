import { http } from '@/shared/api/httpClient';

export type LaudoConfiguracao = {
  cabecalhoHtml: string;
  cabecalhoJson: string;
  rodapeHtml: string;
  rodapeJson: string;
  atualizadoEm: string | null;
};

export type SalvarLaudoConfiguracaoPayload = {
  cabecalhoHtml: string;
  cabecalhoJson: string;
  rodapeHtml: string;
  rodapeJson: string;
};

export async function obterLaudoConfiguracao(): Promise<LaudoConfiguracao> {
  const { data } = await http.get<LaudoConfiguracao>('/laudos/configuracao');
  return data;
}

export async function salvarLaudoConfiguracao(
  payload: SalvarLaudoConfiguracaoPayload,
): Promise<LaudoConfiguracao> {
  const { data } = await http.put<LaudoConfiguracao>('/laudos/configuracao', payload);
  return data;
}
