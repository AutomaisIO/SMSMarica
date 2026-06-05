import { http } from '@/shared/api/httpClient';
import type {
  AtualizarConfiguracaoPayload,
  ConfiguracaoIa,
  FonteConfig,
  FonteIa,
  PerguntarPayload,
  PerguntarResposta,
  ResultadoTesteConexao,
  SalvarFonteConfigPayload,
} from '@/features/ia/types';

// ── Perguntar ───────────────────────────────────────────────────────────────

export async function perguntar(payload: PerguntarPayload): Promise<PerguntarResposta> {
  const { data } = await http.post<PerguntarResposta>('/ia/perguntar', payload);
  return data;
}

export async function listarFontes(): Promise<FonteIa[]> {
  const { data } = await http.get<FonteIa[]>('/ia/fontes');
  return data;
}

/** Feedback de "resposta errada" sobre uma consulta gerada. */
export async function reportarRespostaErrada(
  consultaId: string,
  comentario?: string,
): Promise<void> {
  await http.post(`/ia/consultas/${consultaId}/feedback`, {
    correta: false,
    comentario: comentario ?? null,
  });
}

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
