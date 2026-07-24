import { http } from '@/shared/api/httpClient';
import type {
  AbaIndicador,
  FiltroIndicador,
  FonteIndicador,
  IndicadorDetalhe,
  IndicadorResumo,
  IndicadorVersao,
  ResultadoIndicador,
  SalvarIndicadorPayload,
  UnidadeIndicador,
} from '@/features/indicadores/types';

export async function listarUnidades(): Promise<UnidadeIndicador[]> {
  const { data } = await http.get<UnidadeIndicador[]>('/indicadores/unidades');
  return data;
}

export async function listarIndicadores(
  aba: AbaIndicador,
  filtro: FiltroIndicador,
): Promise<IndicadorResumo[]> {
  const { data } = await http.get<IndicadorResumo[]>(`/indicadores/abas/${aba}`, {
    params: { hospital: filtro.hospital, inicio: filtro.inicio, fim: filtro.fim },
  });
  return data;
}

/** Apura todos os indicadores com motor da aba. Roda em sequência no Oracle — pode demorar. */
export async function apurarAba(
  aba: AbaIndicador,
  filtro: FiltroIndicador,
): Promise<IndicadorResumo[]> {
  const { data } = await http.post<IndicadorResumo[]>(`/indicadores/abas/${aba}/apurar`, filtro);
  return data;
}

export async function obterIndicador(id: string): Promise<IndicadorDetalhe> {
  const { data } = await http.get<IndicadorDetalhe>(`/indicadores/${id}`);
  return data;
}

export async function listarVersoes(id: string): Promise<IndicadorVersao[]> {
  const { data } = await http.get<IndicadorVersao[]>(`/indicadores/${id}/versoes`);
  return data;
}

/** `previa` não grava histórico — é o botão "Executar prévia" da tela de edição. */
export async function apurarIndicador(
  id: string,
  filtro: FiltroIndicador,
  previa = false,
): Promise<ResultadoIndicador> {
  const { data } = await http.post<ResultadoIndicador>(`/indicadores/${id}/apurar`, filtro, {
    params: { previa: previa ? 'true' : undefined },
  });
  return data;
}

export async function listarFontes(): Promise<FonteIndicador[]> {
  const { data } = await http.get<FonteIndicador[]>('/indicadores/fontes');
  return data;
}

export async function criarIndicador(payload: SalvarIndicadorPayload): Promise<IndicadorDetalhe> {
  const { data } = await http.post<IndicadorDetalhe>('/indicadores', payload);
  return data;
}

export async function atualizarIndicador(
  id: string,
  payload: SalvarIndicadorPayload,
): Promise<IndicadorDetalhe> {
  const { data } = await http.put<IndicadorDetalhe>(`/indicadores/${id}`, payload);
  return data;
}

export async function excluirIndicador(id: string): Promise<void> {
  await http.delete(`/indicadores/${id}`);
}
