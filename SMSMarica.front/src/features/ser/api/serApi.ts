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
  FormularioNovaSer,
  OpcaoSer,
  CampoDinamicoSer,
  CatalogoFormularioSer,
  AnexoRascunhoSer,
  RascunhoSerDetalhe,
  RascunhoSerLista,
  RascunhoSerRequest,
  StatusRascunhoSer,
  TipoRecursoSer,
  PacienteEncontradoSer,
  SessaoOperadorSer,
  FollowUpResultadoSer,
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

// ---------------------------------------------------------------- nova solicitação

export async function obterFormularioNovaSer(): Promise<FormularioNovaSer> {
  const { data } = await http.get<FormularioNovaSer>(
    '/regulacao/ser/configuracao/nova-solicitacao/formulario',
  );
  return data;
}

export async function listarRecursosNovaSer(tipo: string): Promise<OpcaoSer[]> {
  const { data } = await http.get<OpcaoSer[]>(
    '/regulacao/ser/configuracao/nova-solicitacao/recursos',
    { params: { tipo } },
  );
  return data;
}

export async function pesquisarPacienteSer(documento: string): Promise<PacienteEncontradoSer> {
  const { data } = await http.get<PacienteEncontradoSer>(
    '/regulacao/ser/configuracao/nova-solicitacao/paciente',
    { params: { documento } },
  );
  return data;
}

export async function obterCamposNovaSer(tipo: string, recurso: string): Promise<CampoDinamicoSer[]> {
  const { data } = await http.get<CampoDinamicoSer[]>(
    '/regulacao/ser/configuracao/nova-solicitacao/campos',
    { params: { tipo, recurso } },
  );
  return data;
}


// ---------------------------------------------------------------- catálogo local + rascunhos
// Nenhuma destas chamadas toca o SER — leem e escrevem só na nossa base.

export async function obterFormularioCatalogoSer(): Promise<CatalogoFormularioSer> {
  const { data } = await http.get<CatalogoFormularioSer>('/regulacao/ser/rascunhos/formulario');
  return data;
}

export async function obterCamposCatalogoSer(
  tipo: TipoRecursoSer,
  recurso: string,
  ambulatorioEstadual: boolean,
): Promise<CampoDinamicoSer[]> {
  const { data } = await http.get<CampoDinamicoSer[]>('/regulacao/ser/rascunhos/campos', {
    params: { tipo, recurso, ambulatorioEstadual },
  });
  return data;
}

export async function listarRascunhosSer(status?: StatusRascunhoSer): Promise<RascunhoSerLista[]> {
  const { data } = await http.get<RascunhoSerLista[]>('/regulacao/ser/rascunhos', {
    params: status ? { status } : undefined,
  });
  return data;
}

export async function obterRascunhoSer(id: string): Promise<RascunhoSerDetalhe> {
  const { data } = await http.get<RascunhoSerDetalhe>(`/regulacao/ser/rascunhos/${id}`);
  return data;
}

export async function salvarRascunhoSer(
  id: string | null,
  corpo: RascunhoSerRequest,
): Promise<RascunhoSerDetalhe> {
  const { data } = id
    ? await http.put<RascunhoSerDetalhe>(`/regulacao/ser/rascunhos/${id}`, corpo)
    : await http.post<RascunhoSerDetalhe>('/regulacao/ser/rascunhos', corpo);
  return data;
}

export async function excluirRascunhoSer(id: string): Promise<void> {
  await http.delete(`/regulacao/ser/rascunhos/${id}`);
}

export async function marcarRascunhoProntoSer(id: string): Promise<RascunhoSerDetalhe> {
  const { data } = await http.post<RascunhoSerDetalhe>(`/regulacao/ser/rascunhos/${id}/pronto`);
  return data;
}

export async function anexarRascunhoSer(id: string, arquivo: File): Promise<AnexoRascunhoSer> {
  const fd = new FormData();
  fd.append('arquivo', arquivo);
  const { data } = await http.post<AnexoRascunhoSer>(`/regulacao/ser/rascunhos/${id}/anexos`, fd);
  return data;
}

export async function removerAnexoRascunhoSer(id: string, anexoId: string): Promise<void> {
  await http.delete(`/regulacao/ser/rascunhos/${id}/anexos/${anexoId}`);
}

/**
 * Dispara a cópia do catálogo. Responde 202 na hora — quem trabalha é um job em segundo plano.
 *
 * Antes esta chamada esperava os ~10 minutos da cópia: o proxy desistia, o navegador mostrava
 * "Network Error" e o cancelamento da conexão abortada MATAVA a cópia no meio.
 */
export async function sincronizarCatalogoSer(refazerTudo = false): Promise<void> {
  await http.post('/regulacao/ser/configuracao/catalogo/sincronizar', null, {
    params: { refazerTudo },
  });
}

// ---------------------------------------------------------------- escrita no SER

export async function obterSessaoOperadorSer(): Promise<SessaoOperadorSer> {
  const { data } = await http.get<SessaoOperadorSer>('/regulacao/ser/sessao');
  return data;
}

/**
 * Entra no SER com a credencial do próprio operador. O backend valida contra o SER na hora e
 * guarda só em memória — a senha não é persistida em lugar nenhum.
 */
export async function entrarNoSer(usuario: string, senha: string): Promise<SessaoOperadorSer> {
  const { data } = await http.post<SessaoOperadorSer>('/regulacao/ser/sessao', { usuario, senha });
  return data;
}

export async function sairDoSer(): Promise<void> {
  await http.delete('/regulacao/ser/sessao');
}

/** Registra FollowUP no SER. Só volta OK depois de o backend RELER o histórico e achar o evento. */
export async function registrarFollowUpSer(
  id: string,
  texto: string,
): Promise<FollowUpResultadoSer> {
  const { data } = await http.post<FollowUpResultadoSer>(`/regulacao/ser/${id}/followup`, { texto });
  return data;
}
