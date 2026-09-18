import { http } from '@/shared/api/httpClient';
import type {
  ConfirmacaoConfiguracao,
  DisparoLote,
  FiltroLote,
  FiltroRespostas,
  MensageriaConfiguracao,
  NotificacaoDetalhe,
  NotificacaoFiltro,
  PaginaNotificacoes,
  PaginaRespostas,
  PreviaLote,
  RegraUnidade,
  ResumoDiarioMensageria,
  SalvarConfirmacaoConfiguracao,
  SalvarMensageriaConfiguracao,
} from '@/features/mensageria/types';

export function params(filtro: object): Record<string, string | number | boolean> {
  const p: Record<string, string | number | boolean> = {};
  for (const [k, v] of Object.entries(filtro)) {
    if (v !== undefined && v !== null && v !== '' && v !== false) p[k] = v as string | number | boolean;
  }
  return p;
}

// ---- Envios (comunicacoes-paciente) ----

export async function listarNotificacoes(filtro: NotificacaoFiltro): Promise<PaginaNotificacoes> {
  const { data } = await http.get<PaginaNotificacoes>('/comunicacoes-paciente', { params: params(filtro) });
  return data;
}

export async function obterNotificacao(id: string): Promise<NotificacaoDetalhe> {
  const { data } = await http.get<NotificacaoDetalhe>(`/comunicacoes-paciente/${id}`);
  return data;
}

export async function reenviarNotificacao(id: string): Promise<void> {
  await http.post(`/comunicacoes-paciente/${id}/reenviar`);
}

export async function obterResumoDiario(filtro: {
  de?: string;
  ate?: string;
  finalidade?: string;
  unidadeId?: string;
}): Promise<ResumoDiarioMensageria> {
  const { data } = await http.get<ResumoDiarioMensageria>('/comunicacoes-paciente/resumo-diario', {
    params: params(filtro),
  });
  return data;
}

export async function obterConfiguracaoMensageria(): Promise<MensageriaConfiguracao> {
  const { data } = await http.get<MensageriaConfiguracao>('/comunicacoes-paciente/configuracao-mensageria');
  return data;
}

export async function salvarConfiguracaoMensageria(
  dados: SalvarMensageriaConfiguracao,
): Promise<MensageriaConfiguracao> {
  const { data } = await http.put<MensageriaConfiguracao>('/comunicacoes-paciente/configuracao-mensageria', dados);
  return data;
}

// ---- Confirmações (respostas, lote, regras) — rotas antigas mantidas no backend ----

export async function listarRespostas(filtro: FiltroRespostas): Promise<PaginaRespostas> {
  const { data } = await http.get<PaginaRespostas>('/confirmacoes/respostas', { params: params(filtro) });
  return data;
}

export async function obterConfiguracaoConfirmacao(): Promise<ConfirmacaoConfiguracao> {
  const { data } = await http.get<ConfirmacaoConfiguracao>('/confirmacoes/configuracao');
  return data;
}

export async function salvarConfiguracaoConfirmacao(
  dados: SalvarConfirmacaoConfiguracao,
): Promise<ConfirmacaoConfiguracao> {
  const { data } = await http.put<ConfirmacaoConfiguracao>('/confirmacoes/configuracao', dados);
  return data;
}

export async function obterPreviaLote(filtro: FiltroLote): Promise<PreviaLote> {
  const { data } = await http.get<PreviaLote>('/confirmacoes/lote/previa', { params: params(filtro) });
  return data;
}

export async function dispararLote(dados: DisparoLote): Promise<PreviaLote> {
  const { data } = await http.post<PreviaLote>('/confirmacoes/lote', dados);
  return data;
}

export async function listarRegrasUnidades(): Promise<RegraUnidade[]> {
  const { data } = await http.get<RegraUnidade[]>('/confirmacoes/regras/unidades');
  return data;
}

export async function alterarRegraUnidade(unidadeId: string, enviar: boolean): Promise<void> {
  await http.put(`/confirmacoes/regras/unidades/${unidadeId}`, { enviar });
}
