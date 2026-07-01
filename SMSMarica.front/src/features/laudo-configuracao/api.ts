import { http } from '@/shared/api/httpClient';

export type LaudoConfiguracao = {
  cabecalhoHtml: string;
  cabecalhoJson: string;
  rodapeHtml: string;
  rodapeJson: string;
  permitirLaudarSemAssociacao: boolean;
  permitirLaudarSemAnamnese: boolean;
  downloadLinkValidadeDias: number;
  magicLinkValidadeDias: number;
  atualizadoEm: string | null;
};

export type SalvarLaudoConfiguracaoPayload = {
  cabecalhoHtml: string;
  cabecalhoJson: string;
  rodapeHtml: string;
  rodapeJson: string;
  permitirLaudarSemAssociacao: boolean;
  permitirLaudarSemAnamnese: boolean;
  downloadLinkValidadeDias: number;
  magicLinkValidadeDias: number;
};

export type RegrasIniciarLaudo = {
  permitirLaudarSemAssociacao: boolean;
  permitirLaudarSemAnamnese: boolean;
};

export async function obterLaudoConfiguracao(): Promise<LaudoConfiguracao> {
  const { data } = await http.get<LaudoConfiguracao>('/laudos/configuracao');
  return data;
}

/** Só as regras de iniciar o laudo — liberado a qualquer autenticado (para o botão "Laudar"). */
export async function obterRegrasIniciarLaudo(): Promise<RegrasIniciarLaudo> {
  const { data } = await http.get<RegrasIniciarLaudo>('/laudos/configuracao/regras');
  return data;
}

export async function salvarLaudoConfiguracao(
  payload: SalvarLaudoConfiguracaoPayload,
): Promise<LaudoConfiguracao> {
  const { data } = await http.put<LaudoConfiguracao>('/laudos/configuracao', payload);
  return data;
}
