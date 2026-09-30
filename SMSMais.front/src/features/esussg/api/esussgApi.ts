import { http } from '@/shared/api/httpClient';
import type { TecnicoNotificacao } from '@/shared/regulacao/tecnicos';
import type {
  BuscaEsusSgFiltro,
  BuscaEsusSgResultado,
  CatalogoSyncEsusSgResultado,
  CredencialEsusSg,
  DispararVarreduraEsusSgPayload,
  ExecucaoEsusSg,
  NotificacoesEsusSgFiltro,
  NotificacoesEsusSgPagina,
  NotificacoesEsusSgResumo,
  ResumoEsusSg,
  SolicitacaoEsusSgDetalhe,
  StatusMotorEsusSg,
  VarreduraAutomaticaEsusSg,
} from '@/features/esussg/types';

/*
 * Rotas de `EsusSgController.cs`. Tudo aqui lê o NOSSO banco — nenhuma chamada vai ao ESUS ao
 * vivo, exceto testar/salvar credencial e sincronizar catálogo (que o backend faz por nós).
 */

/** Busca na NOSSA base espelhada — não vai ao ESUS. */
export async function buscarSolicitacoesEsusSg(filtro: BuscaEsusSgFiltro): Promise<BuscaEsusSgResultado> {
  const { data } = await http.get<BuscaEsusSgResultado>('/regulacao/esussg', { params: filtro });
  return data;
}

export async function obterResumoEsusSg(): Promise<ResumoEsusSg> {
  const { data } = await http.get<ResumoEsusSg>('/regulacao/esussg/resumo');
  return data;
}

export async function obterSolicitacaoEsusSg(id: string): Promise<SolicitacaoEsusSgDetalhe> {
  const { data } = await http.get<SolicitacaoEsusSgDetalhe>(`/regulacao/esussg/${id}`);
  return data;
}

// ---------------------------------------------------------------- motor (configuração)

export async function obterStatusMotorEsusSg(): Promise<StatusMotorEsusSg> {
  const { data } = await http.get<StatusMotorEsusSg>('/regulacao/esussg/configuracao/status');
  return data;
}

export async function listarExecucoesEsusSg(limite = 20): Promise<ExecucaoEsusSg[]> {
  const { data } = await http.get<ExecucaoEsusSg[]>('/regulacao/esussg/configuracao/execucoes', {
    params: { limite },
  });
  return data;
}

/** Enfileira a rodada. Responde 202 — ela roda em segundo plano; 409 = já há uma em andamento. */
export async function dispararVarreduraEsusSg(payload: DispararVarreduraEsusSgPayload): Promise<void> {
  await http.post('/regulacao/esussg/configuracao/varreduras', payload);
}

/** Testa o login sem salvar. Devolve o nome do usuário no ESUS — a prova de que entrou. */
export async function testarCredencialEsusSg(credencial: CredencialEsusSg): Promise<string> {
  const { data } = await http.post<unknown>('/regulacao/esussg/configuracao/testar-credencial', credencial);
  // O ASP.NET pode responder a string como text/plain ou como JSON — o axios entrega um ou outro.
  return typeof data === 'string' ? data : String(data ?? '');
}

/** Valida no ESUS primeiro e só então grava (cifrada). */
export async function salvarCredencialEsusSg(credencial: CredencialEsusSg): Promise<void> {
  await http.put('/regulacao/esussg/configuracao/credencial', credencial);
}

export async function obterVarreduraAutomaticaEsusSg(): Promise<VarreduraAutomaticaEsusSg> {
  const { data } = await http.get<VarreduraAutomaticaEsusSg>(
    '/regulacao/esussg/configuracao/varredura-automatica',
  );
  return data;
}

export async function salvarVarreduraAutomaticaEsusSg(
  config: VarreduraAutomaticaEsusSg,
): Promise<VarreduraAutomaticaEsusSg> {
  const { data } = await http.put<VarreduraAutomaticaEsusSg>(
    '/regulacao/esussg/configuracao/varredura-automatica',
    config,
  );
  return data;
}

/** Relê o catálogo do ESUS agora (uma requisição) e leva as mudanças ao catálogo canônico. */
export async function sincronizarCatalogoEsusSg(): Promise<CatalogoSyncEsusSgResultado> {
  const { data } = await http.post<CatalogoSyncEsusSgResultado>(
    '/regulacao/esussg/configuracao/catalogo/sincronizar',
  );
  return data;
}

// ---------------------------------------------------------------- notificações

// `indexes: null` manda a lista como `tecnicos=A&tecnicos=B` — o formato que o ASP.NET liga.
export async function obterResumoNotificacoesEsusSg(tecnicos: string[]): Promise<NotificacoesEsusSgResumo> {
  const { data } = await http.get<NotificacoesEsusSgResumo>('/regulacao/esussg/notificacoes/resumo', {
    params: { tecnicos },
    paramsSerializer: { indexes: null },
  });
  return data;
}

export async function listarTecnicosNotificacoesEsusSg(): Promise<TecnicoNotificacao[]> {
  const { data } = await http.get<TecnicoNotificacao[]>('/regulacao/esussg/notificacoes/tecnicos');
  return data;
}

export async function listarNotificacoesEsusSg(
  filtro: NotificacoesEsusSgFiltro,
): Promise<NotificacoesEsusSgPagina> {
  const { data } = await http.get<NotificacoesEsusSgPagina>('/regulacao/esussg/notificacoes', {
    params: filtro,
    paramsSerializer: { indexes: null },
  });
  return data;
}

/** "Visto" é marca NOSSA (tira da fila de notificações) — não escreve no ESUS. */
export async function marcarNotificacaoEsusSgVista(id: string): Promise<void> {
  await http.post(`/regulacao/esussg/notificacoes/${id}/vista`);
}

export async function marcarNotificacoesDaSolicitacaoEsusSgVistas(idEsusSg: string): Promise<number> {
  const { data } = await http.post<number>(
    `/regulacao/esussg/notificacoes/solicitacao/${encodeURIComponent(idEsusSg)}/vistas`,
  );
  return data;
}
