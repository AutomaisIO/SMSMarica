import { http } from '@/shared/api/httpClient';
import type {
  BuscaSerFiltro,
  ConsultaDiretaFiltro,
  ConsultaDiretaResultado,
  HistoricoDiretoResultado,
  SituacaoSer,
  BuscaSerResultado,
  DispararVarreduraPayload,
  ExecucaoSer,
  ResumoSituacaoSer,
  SolicitacaoSerDetalhe,
  StatusMotorSer,
  NotificacoesFiltro,
  NotificacoesPagina,
  NotificacoesResumo,
  VarreduraAutomaticaSer,
} from '@/features/ser/types';

/** Busca na NOSSA base espelhada — não vai ao SER. */
export async function buscarSolicitacoesSer(filtro: BuscaSerFiltro): Promise<BuscaSerResultado> {
  const { data } = await http.get<BuscaSerResultado>('/regulacao/ser', { params: filtro });
  return data;
}

export async function obterResumoSer(): Promise<ResumoSituacaoSer[]> {
  const { data } = await http.get<ResumoSituacaoSer[]>('/regulacao/ser/resumo');
  return data;
}

export async function obterSolicitacaoSer(id: string): Promise<SolicitacaoSerDetalhe> {
  const { data } = await http.get<SolicitacaoSerDetalhe>(`/regulacao/ser/${id}`);
  return data;
}

export async function obterStatusMotorSer(): Promise<StatusMotorSer> {
  const { data } = await http.get<StatusMotorSer>('/regulacao/ser/configuracao/status');
  return data;
}

export async function listarExecucoesSer(limite = 20): Promise<ExecucaoSer[]> {
  const { data } = await http.get<ExecucaoSer[]>('/regulacao/ser/configuracao/execucoes', {
    params: { limite },
  });
  return data;
}

/** Enfileira a varredura. Responde 202 — a rodada leva de 15 min a ~1 h e roda em background. */
export async function dispararVarreduraSer(payload: DispararVarreduraPayload): Promise<void> {
  await http.post('/regulacao/ser/configuracao/varreduras', payload);
}

/** Testa a credencial contra o SER sem gravá-la (nenhuma escrita no SER). */
export async function testarCredencialSer(usuario: string, senha: string): Promise<void> {
  await http.post('/regulacao/ser/configuracao/testar-credencial', { usuario, senha });
}

/** Grava a credencial (cifrada). O backend autentica antes de persistir. */
export async function salvarCredencialSer(usuario: string, senha: string): Promise<void> {
  await http.put('/regulacao/ser/configuracao/credencial', { usuario, senha });
}

/** Consulta AO VIVO no SER (tela de testes). Nada é gravado na nossa base. */
export async function consultaDiretaSer(
  filtro: ConsultaDiretaFiltro,
): Promise<ConsultaDiretaResultado> {
  const { data } = await http.post<ConsultaDiretaResultado>(
    '/regulacao/ser/configuracao/consulta-direta',
    filtro,
  );
  return data;
}

/** Histórico lido ao vivo no SER, para conferir contra a tela de lá. */
export async function historicoDiretoSer(
  idSer: string,
  situacao: SituacaoSer,
): Promise<HistoricoDiretoResultado> {
  const { data } = await http.get<HistoricoDiretoResultado>(
    `/regulacao/ser/configuracao/consulta-direta/${idSer}/historico`,
    { params: { situacao } },
  );
  return data;
}

// ---------------------------------------------------------------- notificações

export async function obterResumoNotificacoesSer(): Promise<NotificacoesResumo> {
  const { data } = await http.get<NotificacoesResumo>('/regulacao/ser/notificacoes/resumo');
  return data;
}

export async function listarNotificacoesSer(filtro: NotificacoesFiltro): Promise<NotificacoesPagina> {
  const { data } = await http.get<NotificacoesPagina>('/regulacao/ser/notificacoes', { params: filtro });
  return data;
}

/** Marca UM movimento como visto — some da tela e sai da fila de gatilhos. */
export async function marcarNotificacaoVista(id: string): Promise<void> {
  await http.post(`/regulacao/ser/notificacoes/${id}/vista`);
}

/** Marca tudo que está pendente de uma solicitação: quem abriu, viu tudo dela. */
export async function marcarNotificacoesDaSolicitacaoVistas(idSer: string): Promise<number> {
  const { data } = await http.post<number>(`/regulacao/ser/notificacoes/solicitacao/${idSer}/vistas`);
  return data;
}

// ---------------------------------------------------------------- varredura automática

export async function obterVarreduraAutomaticaSer(): Promise<VarreduraAutomaticaSer> {
  const { data } = await http.get<VarreduraAutomaticaSer>(
    '/regulacao/ser/configuracao/varredura-automatica',
  );
  return data;
}

export async function salvarVarreduraAutomaticaSer(
  config: VarreduraAutomaticaSer,
): Promise<VarreduraAutomaticaSer> {
  const { data } = await http.put<VarreduraAutomaticaSer>(
    '/regulacao/ser/configuracao/varredura-automatica',
    config,
  );
  return data;
}
