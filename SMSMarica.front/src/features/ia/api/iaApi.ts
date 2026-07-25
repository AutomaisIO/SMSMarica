import { http } from '@/shared/api/httpClient';
import type {
  AprendizadoIa,
  AtualizarConfiguracaoPayload,
  ConfiguracaoIa,
  CorrecaoIa,
  FeedbackIa,
  FonteConfig,
  ResultadoTesteConexao,
  SalvarFonteConfigPayload,
} from '@/features/ia/types';

// ── Configuração ────────────────────────────────────────────────────────────

export async function obterConfiguracao(): Promise<ConfiguracaoIa> {
  const { data } = await http.get<ConfiguracaoIa>('/ia/configuracao');
  return data;
}

export async function atualizarConfiguracao(
  payload: AtualizarConfiguracaoPayload,
): Promise<ConfiguracaoIa> {
  const { data } = await http.put<ConfiguracaoIa>('/ia/configuracao', payload);
  return data;
}

export async function listarFontesConfig(): Promise<FonteConfig[]> {
  const { data } = await http.get<FonteConfig[]>('/ia/configuracao/fontes');
  return data;
}

export async function criarFonteConfig(payload: SalvarFonteConfigPayload): Promise<string> {
  const { data } = await http.post<string>('/ia/configuracao/fontes', payload);
  return data;
}

export async function atualizarFonteConfig(
  id: string,
  payload: SalvarFonteConfigPayload,
): Promise<void> {
  await http.put(`/ia/configuracao/fontes/${id}`, payload);
}

export async function removerFonteConfig(id: string): Promise<void> {
  await http.delete(`/ia/configuracao/fontes/${id}`);
}

export async function testarConexaoFonte(id: string): Promise<ResultadoTesteConexao> {
  const { data } = await http.post<ResultadoTesteConexao>(
    `/ia/configuracao/fontes/${id}/testar-conexao`,
  );
  return data;
}

// ── Governança / Melhorias (aprendizado) ─────────────────────────────────────

export async function listarAprendizados(fonteId?: string): Promise<AprendizadoIa[]> {
  const { data } = await http.get<AprendizadoIa[]>('/ia/aprendizados', {
    params: fonteId ? { fonteId } : undefined,
  });
  return data;
}

export async function desativarAprendizado(id: string): Promise<void> {
  await http.delete(`/ia/aprendizados/${id}`);
}

export async function listarCorrecoes(fonteId?: string): Promise<CorrecaoIa[]> {
  const { data } = await http.get<CorrecaoIa[]>('/ia/correcoes', {
    params: fonteId ? { fonteId } : undefined,
  });
  return data;
}

export async function listarFeedbacks(apenasPendentes = true): Promise<FeedbackIa[]> {
  const { data } = await http.get<FeedbackIa[]>('/ia/feedbacks', { params: { apenasPendentes } });
  return data;
}

export async function tratarFeedback(
  id: string,
  descartar: boolean,
  resolucao?: string,
): Promise<void> {
  await http.post(`/ia/feedbacks/${id}/tratar`, { descartar, resolucao: resolucao ?? null });
}
