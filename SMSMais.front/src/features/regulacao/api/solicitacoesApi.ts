import { http } from '@/shared/api/httpClient';

import type { MedicoParecido, SituacaoMedicoPendente } from './medicosApi';

import type {
  AvaliacaoElegibilidade,
  EscopoNotificacao,
  EventoRegulacao,
  Exigencia,
  FiltroSolicitacoesRegulacao,
  FluxoRegulacao,
  FormularioRegulacao,
  PaginaNotificacoesRegulacao,
  PaginaSolicitacoesRegulacao,
  ExameParaRegras,
  PendenciaEnvio,
  RespostaRegraRegulacao,
  RespostaRegraRegistrada,
  ResumoFilaRegulacao,
  SolicitacaoRegulacao,
} from '../tiposSolicitacao';

const base = '/regulacao/solicitacoes';

export type CriarSolicitacaoPayload = {
  fluxo: FluxoRegulacao;
  procedimentoId: string;
  pacienteId: string;
  /** Obrigatória no NAR (D-9). */
  unidadeEmNomeDeId?: string | null;
  sistemaDestino?: string | null;
  observacoes?: string | null;
};

export type AtualizarSolicitacaoPayload = {
  sistemaDestino?: string | null;
  formulario?: Record<string, unknown> | null;
  observacoes?: string | null;
  /** Fluxo, procedimento e paciente: só a unidade, com a solicitação em rascunho ou devolvida. */
  fluxo?: FluxoRegulacao | null;
  procedimentoId?: string | null;
  /** Mandar o MESMO paciente relê a identidade — é assim que o CPF informado depois chega aqui. */
  pacienteId?: string | null;
};

export async function criarSolicitacao(p: CriarSolicitacaoPayload): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(base, p);
  return data;
}

export async function obterSolicitacao(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.get<SolicitacaoRegulacao>(`${base}/${id}`);
  return data;
}

export async function atualizarSolicitacao(
  id: string,
  p: AtualizarSolicitacaoPayload,
): Promise<SolicitacaoRegulacao> {
  const { data } = await http.put<SolicitacaoRegulacao>(`${base}/${id}`, p);
  return data;
}

export async function obterFormularioRegulacao(
  procedimentoId: string,
  fluxo: FluxoRegulacao,
): Promise<FormularioRegulacao> {
  const { data } = await http.get<FormularioRegulacao>(`${base}/formulario`, {
    params: { procedimentoId, fluxo },
  });
  return data;
}

export async function obterPendencias(id: string): Promise<PendenciaEnvio[]> {
  const { data } = await http.get<PendenciaEnvio[]>(`${base}/${id}/pendencias`);
  return data;
}

export async function enviarParaFila(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/enviar-fila`);
  return data;
}

export async function listarExigencias(solicitacaoId: string): Promise<Exigencia[]> {
  const { data } = await http.get<Exigencia[]>(`${base}/${solicitacaoId}/exigencias`);
  return data;
}

export async function anexarArquivo(
  solicitacaoId: string,
  exigenciaId: string,
  arquivo: File,
): Promise<void> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  await http.post(`${base}/${solicitacaoId}/exigencias/${exigenciaId}/arquivos`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
}

export async function removerArquivo(solicitacaoId: string, arquivoId: string): Promise<void> {
  await http.delete(`${base}/${solicitacaoId}/exigencias/arquivos/${arquivoId}`);
}

// ---------------------------------------------------------------- fila (plano 04)

/**
 * A fila. Por padrão é a da unidade escolhida no topo, para todos. `filaDoMunicipio` pede o
 * município inteiro — e o backend só atende quem tem o 48. Rascunho, em qualquer fila, só de quem
 * o abriu.
 */
export async function listarSolicitacoes(
  filtro: FiltroSolicitacoesRegulacao,
): Promise<PaginaSolicitacoesRegulacao> {
  const { data } = await http.get<PaginaSolicitacoesRegulacao>(base, {
    params: filtro,
    // `status` é uma lista: sem isto o axios manda `status[]=` e o binder do ASP.NET ignora.
    paramsSerializer: { indexes: null },
  });
  return data;
}

export async function obterResumoFila(filaDoMunicipio: boolean): Promise<ResumoFilaRegulacao> {
  const { data } = await http.get<ResumoFilaRegulacao>(`${base}/resumo`, {
    params: { filaDoMunicipio },
  });
  return data;
}

export async function listarEventos(id: string): Promise<EventoRegulacao[]> {
  const { data } = await http.get<EventoRegulacao[]>(`${base}/${id}/eventos`);
  return data;
}

// ---------------------------------------------------------------- ações do agente (módulo 48)

export async function assumirSolicitacao(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/assumir`);
  return data;
}

export async function devolverSolicitacao(id: string, motivo: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/devolver`, { motivo });
  return data;
}

export async function recusarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`${base}/${id}/recusar`, { motivo });
}

export async function cancelarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`${base}/${id}/cancelar`, { motivo });
}

export async function registrarEnvioSolicitacao(
  id: string,
  payload: { sistema: string; numeroExterno: string; enviadoEm?: string | null },
): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/registrar-envio`, payload);
  return data;
}

export async function confirmarOkInterno(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/ok-interno`);
  return data;
}

// ---------------------------------------------------------------- notificações (plano 05)

export async function listarNotificacoesRegulacao(
  escopo: EscopoNotificacao,
  soNaoVistas: boolean,
): Promise<PaginaNotificacoesRegulacao> {
  const { data } = await http.get<PaginaNotificacoesRegulacao>('/regulacao/notificacoes', {
    params: { escopo, soNaoVistas, tamanho: 50 },
  });
  return data;
}

export async function obterResumoNotificacoesRegulacao(
  escopo: EscopoNotificacao,
): Promise<{ naoVistas: number }> {
  const { data } = await http.get<{ naoVistas: number }>('/regulacao/notificacoes/resumo', {
    params: { escopo },
  });
  return data;
}

export async function marcarNotificacaoVista(eventoId: string): Promise<void> {
  await http.post(`/regulacao/notificacoes/${eventoId}/vista`);
}

export async function marcarNotificacoesDaSolicitacaoVistas(solicitacaoId: string): Promise<void> {
  await http.post(`/regulacao/notificacoes/solicitacao/${solicitacaoId}/vistas`);
}

// ---------------------------------------------------------------- elegibilidade (plano 03)

export async function obterElegibilidade(id: string): Promise<AvaliacaoElegibilidade> {
  const { data } = await http.get<AvaliacaoElegibilidade>(`${base}/${id}/elegibilidade`);
  return data;
}

/** `opcoes`: nas perguntas de lista respondidas "Sim", os ids das opções marcadas, por regra. */
export async function responderRegras(
  id: string,
  respostas: Record<string, RespostaRegraRegulacao>,
  opcoes?: Record<string, string[]>,
): Promise<AvaliacaoElegibilidade> {
  const { data } = await http.put<AvaliacaoElegibilidade>(`${base}/${id}/respostas`, {
    respostas,
    opcoes: opcoes ?? null,
  });
  return data;
}

/** O que foi respondido e deduzido, como ficou gravado — não reavalia (o GET de elegibilidade reavalia). */
export async function listarRespostasRegras(id: string): Promise<RespostaRegraRegistrada[]> {
  const { data } = await http.get<RespostaRegraRegistrada[]>(`${base}/${id}/respostas`);
  return data;
}

export async function listarExamesInternos(
  solicitacaoId: string,
  exigenciaId: string,
): Promise<ExameParaRegras[]> {
  const { data } = await http.get<ExameParaRegras[]>(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/exames-internos`,
  );
  return data;
}

export async function usarExameInterno(
  solicitacaoId: string,
  exigenciaId: string,
  exameId: string,
  laudoId?: string | null,
): Promise<Exigencia> {
  const { data } = await http.post<Exigencia>(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/usar-exame-interno`,
    { exameId, laudoId },
  );
  return data;
}

// ---------------------------------------------------------------- envio automático ao SER

/** O que foi posto em um campo da tela do SER, e se o SER aceitou. */
export type PassoEnvioSer = { campo: string; valor: string | null; ok: boolean; observacao: string | null };

export type AnexoEnvioSer = { nome: string; tamanho: number; arquivosJuntados: number };

export type PedidoParecidoSer = {
  idSer: string;
  recurso: string | null;
  dataSolicitacao: string | null;
  situacao: string | null;
};

/** Os sistemas com envio automático (ADR-0069): a mesma aplicação em duas instâncias. */
export type SistemaEnvioAutomatico = 'Ser' | 'Sernit';

/** A prévia: a tela do SER/SERNIT preenchida inteira, sem anexar nem gravar. */
export type PreparoEnvioSer = {
  /** "SER" ou "SERNIT" — o nome que o servidor usa nas mensagens. */
  sistema: string;
  /** Usuário do operador no sistema (o nome do campo é do tempo em que só havia o SER). */
  operadorSer: string;
  recurso: string;
  passos: PassoEnvioSer[];
  anexos: AnexoEnvioSer[];
  possiveisDuplicados: PedidoParecidoSer[];
  /** O médico pedido pela unidade não está na lista do sistema: a pergunta ao regulador. */
  medicoNovo: MedicoNovoNoSistema | null;
};

/** O médico pedido na abertura que o combo "Médico responsável" do sistema não tem HOJE. */
export type MedicoNovoNoSistema = {
  pendenteId: string;
  nome: string;
  tipoDocumento: string | null;
  numeroDocumento: string | null;
  /** Texto livre da unidade ("ONCOLOGISTA") — quase nunca bate com a lista do sistema. */
  especialidadePedida: string | null;
  situacao: SituacaoMedicoPendente;
  /** Nomes do combo que podem ser o mesmo médico (abreviado, sobrenome a mais) e o CRM igual. */
  parecidos: MedicoParecido[];
  /** As especialidades do modal "Adicionar Médico" do sistema. */
  especialidades: string[];
  especialidadeSugerida: string | null;
  /** O modal existe e o médico está pendente (não houve tentativa). */
  podeCadastrar: boolean;
};

/**
 * "Autorizo cadastrar" — o regulador conferiu os nomes parecidos e autoriza. Quem cadastra é o PRÓPRIO
 * envio, no "Adicionar Médico" da tela de nova solicitação do SER/SERNIT, antes de preencher o pedido.
 */
export type AutorizoCadastroMedico = {
  especialidade: string;
  nome: string | null;
  tipoDocumento: string | null;
  numeroDocumento: string | null;
};

export type ResultadoEnvioSer = {
  numeroExterno: string;
  /** O pedido foi relido do SER com este paciente e este recurso. */
  conferido: boolean;
  mensagemDoSer: string | null;
  operadorSer: string;
  passos: PassoEnvioSer[];
  solicitacao: SolicitacaoRegulacao;
  sistema: string;
};

/** O sistema é o destino da solicitação — o servidor escolhe entre SER e SERNIT. */
export async function prepararEnvioAutomatico(id: string): Promise<PreparoEnvioSer> {
  const { data } = await http.post<PreparoEnvioSer>(`${base}/${id}/envio-automatico/preparar`);
  return data;
}

/** ESCREVE no SER ou no SERNIT, assinado pelo regulador logado no sistema. */
export async function enviarAutomatico(
  id: string,
  enviarMesmoComPedidoParecido: boolean,
  medicoNovo: AutorizoCadastroMedico | null = null,
): Promise<ResultadoEnvioSer> {
  const { data } = await http.post<ResultadoEnvioSer>(`${base}/${id}/envio-automatico/enviar`, {
    enviarMesmoComPedidoParecido,
    // Só vai quando o médico pedido não está na lista: o envio o cadastra antes (ESCREVE no cadastro do Estado).
    medicoNovo: medicoNovo ? { autorizo: true, ...medicoNovo } : null,
  });
  return data;
}
