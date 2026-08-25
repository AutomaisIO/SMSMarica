import { http } from '@/shared/api/httpClient';
import type {
  BuscaSernitFiltro,
  SituacaoSernit,
  BuscaSernitResultado,
  DispararVarreduraSernitPayload,
  ExecucaoSernit,
  ResumoSituacaoSernit,
  SolicitacaoSernitDetalhe,
  StatusMotorSernit,
  NotificacoesSernitFiltro,
  NotificacoesSernitPagina,
  NotificacoesSernitResumo,
  VarreduraAutomaticaSernit,
  FormularioNovaSernit,
  OpcaoSernit,
  CampoDinamicoSernit,
  CatalogoFormularioSernit,
  SugestoesCidSernit,
  AnexoRascunhoSernit,
  RascunhoSernitDetalhe,
  RascunhoSernitLista,
  RascunhoSernitRequest,
  StatusRascunhoSernit,
  TipoRecursoSernit,
  PacienteEncontradoSernit,
  SessaoOperadorSernit,
  FollowUpResultadoSernit,
  ContatosSernit,
  AlterarContatosSernit,
} from '@/features/sernit/types';

/** Busca na NOSSA base espelhada — não vai ao SERNIT. */
export async function buscarSolicitacoesSernit(filtro: BuscaSernitFiltro): Promise<BuscaSernitResultado> {
  const { data } = await http.get<BuscaSernitResultado>('/regulacao/sernit', { params: filtro });
  return data;
}

export async function obterResumoSernit(): Promise<ResumoSituacaoSernit[]> {
  const { data } = await http.get<ResumoSituacaoSernit[]>('/regulacao/sernit/resumo');
  return data;
}

export async function obterSolicitacaoSernit(id: string): Promise<SolicitacaoSernitDetalhe> {
  const { data } = await http.get<SolicitacaoSernitDetalhe>(`/regulacao/sernit/${id}`);
  return data;
}

export async function obterStatusMotorSernit(): Promise<StatusMotorSernit> {
  const { data } = await http.get<StatusMotorSernit>('/regulacao/sernit/configuracao/status');
  return data;
}

export async function listarExecucoesSernit(limite = 20): Promise<ExecucaoSernit[]> {
  const { data } = await http.get<ExecucaoSernit[]>('/regulacao/sernit/configuracao/execucoes', {
    params: { limite },
  });
  return data;
}

/** Enfileira a varredura. Responde 202 — a rodada roda em background. */
export async function dispararVarreduraSernit(payload: DispararVarreduraSernitPayload): Promise<void> {
  await http.post('/regulacao/sernit/configuracao/varreduras', payload);
}

export async function testarCredencialSernit(usuario: string, senha: string): Promise<void> {
  await http.post('/regulacao/sernit/configuracao/testar-credencial', { usuario, senha });
}

export async function salvarCredencialSernit(usuario: string, senha: string): Promise<void> {
  await http.put('/regulacao/sernit/configuracao/credencial', { usuario, senha });
}

// ---------------------------------------------------------------- notificações

export async function obterResumoNotificacoesSernit(): Promise<NotificacoesSernitResumo> {
  const { data } = await http.get<NotificacoesSernitResumo>('/regulacao/sernit/notificacoes/resumo');
  return data;
}

export async function listarNotificacoesSernit(
  filtro: NotificacoesSernitFiltro,
): Promise<NotificacoesSernitPagina> {
  const { data } = await http.get<NotificacoesSernitPagina>('/regulacao/sernit/notificacoes', {
    params: filtro,
  });
  return data;
}

export async function marcarNotificacaoSernitVista(id: string): Promise<void> {
  await http.post(`/regulacao/sernit/notificacoes/${id}/vista`);
}

export async function marcarNotificacoesDaSolicitacaoSernitVistas(idSernit: string): Promise<number> {
  const { data } = await http.post<number>(
    `/regulacao/sernit/notificacoes/solicitacao/${idSernit}/vistas`,
  );
  return data;
}

// ---------------------------------------------------------------- varredura automática

export async function obterVarreduraAutomaticaSernit(): Promise<VarreduraAutomaticaSernit> {
  const { data } = await http.get<VarreduraAutomaticaSernit>(
    '/regulacao/sernit/configuracao/varredura-automatica',
  );
  return data;
}

export async function salvarVarreduraAutomaticaSernit(
  config: VarreduraAutomaticaSernit,
): Promise<VarreduraAutomaticaSernit> {
  const { data } = await http.put<VarreduraAutomaticaSernit>(
    '/regulacao/sernit/configuracao/varredura-automatica',
    config,
  );
  return data;
}

// ---------------------------------------------------------------- nova solicitação (ao vivo)

export async function obterFormularioNovaSernit(): Promise<FormularioNovaSernit> {
  const { data } = await http.get<FormularioNovaSernit>(
    '/regulacao/sernit/configuracao/nova-solicitacao/formulario',
  );
  return data;
}

export async function listarRecursosNovaSernit(tipo: string): Promise<OpcaoSernit[]> {
  const { data } = await http.get<OpcaoSernit[]>(
    '/regulacao/sernit/configuracao/nova-solicitacao/recursos',
    { params: { tipo } },
  );
  return data;
}

export async function pesquisarPacienteSernit(documento: string): Promise<PacienteEncontradoSernit> {
  const { data } = await http.get<PacienteEncontradoSernit>(
    '/regulacao/sernit/configuracao/nova-solicitacao/paciente',
    { params: { documento } },
  );
  return data;
}

export async function obterCamposNovaSernit(tipo: string, recurso: string): Promise<CampoDinamicoSernit[]> {
  const { data } = await http.get<CampoDinamicoSernit[]>(
    '/regulacao/sernit/configuracao/nova-solicitacao/campos',
    { params: { tipo, recurso } },
  );
  return data;
}

/** Os CID que o SERNIT aceita como Hipótese para AQUELE recurso (a lista muda por recurso). */
export async function sugerirCidsSernit(
  tipo: string,
  recurso: string,
  termo: string,
): Promise<SugestoesCidSernit> {
  const { data } = await http.get<SugestoesCidSernit>(
    '/regulacao/sernit/configuracao/nova-solicitacao/cids',
    { params: { tipo, recurso, termo } },
  );
  return data;
}

// ---------------------------------------------------------------- catálogo local + rascunhos

export async function obterFormularioCatalogoSernit(): Promise<CatalogoFormularioSernit> {
  const { data } = await http.get<CatalogoFormularioSernit>('/regulacao/sernit/rascunhos/formulario');
  return data;
}

export async function obterCamposCatalogoSernit(
  tipo: TipoRecursoSernit,
  recurso: string,
): Promise<CampoDinamicoSernit[]> {
  const { data } = await http.get<CampoDinamicoSernit[]>('/regulacao/sernit/rascunhos/campos', {
    params: { tipo, recurso },
  });
  return data;
}

export async function listarRascunhosSernit(
  status?: StatusRascunhoSernit,
): Promise<RascunhoSernitLista[]> {
  const { data } = await http.get<RascunhoSernitLista[]>('/regulacao/sernit/rascunhos', {
    params: status ? { status } : undefined,
  });
  return data;
}

export async function obterRascunhoSernit(id: string): Promise<RascunhoSernitDetalhe> {
  const { data } = await http.get<RascunhoSernitDetalhe>(`/regulacao/sernit/rascunhos/${id}`);
  return data;
}

export async function salvarRascunhoSernit(
  id: string | null,
  corpo: RascunhoSernitRequest,
): Promise<RascunhoSernitDetalhe> {
  const { data } = id
    ? await http.put<RascunhoSernitDetalhe>(`/regulacao/sernit/rascunhos/${id}`, corpo)
    : await http.post<RascunhoSernitDetalhe>('/regulacao/sernit/rascunhos', corpo);
  return data;
}

export async function excluirRascunhoSernit(id: string): Promise<void> {
  await http.delete(`/regulacao/sernit/rascunhos/${id}`);
}

export async function marcarRascunhoProntoSernit(id: string): Promise<RascunhoSernitDetalhe> {
  const { data } = await http.post<RascunhoSernitDetalhe>(`/regulacao/sernit/rascunhos/${id}/pronto`);
  return data;
}

export async function anexarRascunhoSernit(id: string, arquivo: File): Promise<AnexoRascunhoSernit> {
  const fd = new FormData();
  fd.append('arquivo', arquivo);
  const { data } = await http.post<AnexoRascunhoSernit>(`/regulacao/sernit/rascunhos/${id}/anexos`, fd);
  return data;
}

export async function removerAnexoRascunhoSernit(id: string, anexoId: string): Promise<void> {
  await http.delete(`/regulacao/sernit/rascunhos/${id}/anexos/${anexoId}`);
}

/** Dispara a cópia do catálogo. Responde 202 — quem trabalha é um job em segundo plano. */
export async function sincronizarCatalogoSernit(refazerTudo = false): Promise<void> {
  await http.post('/regulacao/sernit/configuracao/catalogo/sincronizar', null, {
    params: { refazerTudo },
  });
}

// ---------------------------------------------------------------- escrita no SERNIT

export async function obterSessaoOperadorSernit(): Promise<SessaoOperadorSernit> {
  const { data } = await http.get<SessaoOperadorSernit>('/regulacao/sernit/sessao');
  return data;
}

export async function entrarNoSernit(usuario: string, senha: string): Promise<SessaoOperadorSernit> {
  const { data } = await http.post<SessaoOperadorSernit>('/regulacao/sernit/sessao', { usuario, senha });
  return data;
}

export async function sairDoSernit(): Promise<void> {
  await http.delete('/regulacao/sernit/sessao');
}

/** Registra FollowUP no SERNIT. Só volta OK depois de o backend RELER o histórico e achar o evento. */
export async function registrarFollowUpSernit(
  id: string,
  texto: string,
): Promise<FollowUpResultadoSernit> {
  const { data } = await http.post<FollowUpResultadoSernit>(`/regulacao/sernit/${id}/followup`, {
    texto,
  });
  return data;
}

/** Lê os telefones AO VIVO da tela de edição do SERNIT — não do nosso espelho. */
export async function obterContatosSernit(id: string): Promise<ContatosSernit> {
  const { data } = await http.get<ContatosSernit>(`/regulacao/sernit/${id}/contatos`);
  return data;
}

/** Altera os telefones no SERNIT. Só volta OK depois de o backend reabrir a tela e conferir. */
export async function alterarContatosSernit(
  id: string,
  corpo: AlterarContatosSernit,
): Promise<ContatosSernit> {
  const { data } = await http.put<ContatosSernit>(`/regulacao/sernit/${id}/contatos`, corpo);
  return data;
}
