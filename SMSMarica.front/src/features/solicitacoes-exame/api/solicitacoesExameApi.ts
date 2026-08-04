import { http } from '@/shared/api/httpClient';
import type {
  AtualizarSolicitacaoPayload,
  CadastrarSolicitacaoPayload,
  EquipamentoExame,
  FiltroSolicitacoes,
  HistoricoSolicitacao,
  SolicitacaoExame,
  SolicitacaoExameListItem,
} from '@/features/solicitacoes-exame/types';

export async function listarSolicitacoes(filtro: FiltroSolicitacoes): Promise<SolicitacaoExameListItem[]> {
  const { data } = await http.get<SolicitacaoExameListItem[]>('/solicitacoes-exame', {
    params: {
      status: filtro.status,
      pacienteId: filtro.pacienteId,
      unidadeId: filtro.unidadeId,
      tipoExameId: filtro.tipoExameId,
      dataInicial: filtro.dataInicial,
      dataFinal: filtro.dataFinal,
      accessionNumber: filtro.accessionNumber,
      busca: filtro.busca,
      painel: filtro.painel,
      visaoSolicitante: filtro.visaoSolicitante ? true : undefined,
      limite: filtro.limite ?? 50,
    },
  });
  return data;
}

export async function obterSolicitacao(id: string): Promise<SolicitacaoExame> {
  const { data } = await http.get<SolicitacaoExame>(`/solicitacoes-exame/${id}`);
  return data;
}

export async function obterSolicitacaoPorStudy(studyInstanceUID: string): Promise<SolicitacaoExame | null> {
  const resp = await http.get<SolicitacaoExame>(`/solicitacoes-exame/por-study/${encodeURIComponent(studyInstanceUID)}`, {
    validateStatus: (s) => s === 200 || s === 204,
  });
  return resp.status === 204 ? null : resp.data;
}

export async function cadastrarSolicitacao(payload: CadastrarSolicitacaoPayload): Promise<string> {
  const { data } = await http.post<string>('/solicitacoes-exame', payload);
  return data;
}

export async function atualizarSolicitacao(id: string, payload: AtualizarSolicitacaoPayload): Promise<void> {
  await http.put(`/solicitacoes-exame/${id}`, payload);
}

export async function cancelarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/cancelar`, { motivo });
}

/**
 * Autorização presencial (recepção): grava a chave e libera o envio ao PACS.
 * `equipamentoId` é obrigatório quando a unidade tem mais de um equipamento na
 * modalidade — o servidor recusa com `autorizacao.equipamento_obrigatorio` sem ele.
 */
export async function autorizarSolicitacao(
  id: string,
  chaveConfirmacao: string,
  equipamentoId?: string | null,
): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/autorizar`, { chaveConfirmacao, equipamentoId: equipamentoId ?? null });
}

/** Equipamentos (estações) elegíveis para executar o exame — unidade executante + modalidade. */
export async function listarEquipamentosDoExame(id: string): Promise<EquipamentoExame[]> {
  const { data } = await http.get<EquipamentoExame[]>(`/solicitacoes-exame/${id}/equipamentos`);
  return data;
}

export async function reenviarWorklist(id: string): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/reenviar-worklist`);
}

export async function excluirSolicitacao(id: string, force = false): Promise<void> {
  await http.delete(`/solicitacoes-exame/${id}`, { params: force ? { force: true } : undefined });
}

/** Histórico do processo de comunicação (comunicações WhatsApp + contatos manuais). */
export async function obterHistorico(id: string): Promise<HistoricoSolicitacao> {
  const { data } = await http.get<HistoricoSolicitacao>(`/solicitacoes-exame/${id}/historico`);
  return data;
}

/**
 * Reenvia uma comunicação: REVOGA os links de acesso anteriores (e sessões abertas por
 * eles) e reconstrói o envio com os dados ATUAIS do paciente (telefone certo, link novo).
 */
export async function reenviarComunicacao(id: string, comunicacaoId: string): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/comunicacoes/${comunicacaoId}/reenviar`);
}

/** Finalidades de envio manual disponíveis ao operador. */
export type FinalidadeEnvioManual = 'ExameLiberado' | 'LaudoPronto';

/**
 * Envio MANUAL do resultado ao paciente (botão "Enviar exame"/"Enviar laudo"): cria/reconstrói
 * a comunicação e dispara na hora. `assumirRisco` envia mesmo sem telefone verificado.
 */
export async function enviarComunicacaoManual(
  id: string,
  finalidade: FinalidadeEnvioManual,
  assumirRisco: boolean,
): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/enviar-comunicacao`, { finalidade, assumirRisco });
}

/** Registra um contato MANUAL com o paciente ("liguei, não atendeu"...). */
export async function registrarContato(
  id: string,
  body: { meio: string; resultado: string; observacao: string | null },
): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/contatos`, body);
}

/** Campos editáveis (atendente) impressos na declaração de comparecimento. */
export type ParametrosDeclaracaoComparecimento = {
  /** Hora de entrada (ISO local, ex.: 2026-07-01T08:30). */
  horaEntrada: string;
  /** Hora de saída (ISO local). */
  horaSaida: string;
  /** Motivo do comparecimento (texto livre). */
  motivo: string;
};

/**
 * Abre, em nova aba, o PDF da declaração de comparecimento da solicitação. O
 * endpoint exige bearer (popups não levam o token do interceptor), então baixamos
 * como blob e abrimos uma blob URL. Os parâmetros (entrada/saída/motivo) vêm do
 * modal preenchido pela atendente.
 */
export async function abrirDeclaracaoComparecimento(
  id: string,
  parametros?: ParametrosDeclaracaoComparecimento,
): Promise<void> {
  const resp = await http.get(`/solicitacoes-exame/${id}/declaracao-comparecimento`, {
    responseType: 'blob',
    params: parametros
      ? {
          horaEntrada: parametros.horaEntrada,
          horaSaida: parametros.horaSaida,
          motivo: parametros.motivo || undefined,
        }
      : undefined,
  });
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const janela = window.open(url, `declaracao-${id}`);
  if (!janela) {
    alert('A janela do documento foi bloqueada pelo navegador. Libere os popups para este site.');
    URL.revokeObjectURL(url);
    return;
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export type LinkDownload = { token: string; url: string; expiraEm: string };

/** Gera um link público de download (uso único) do exame completo, para enviar ao paciente. */
export async function gerarLinkDownload(id: string): Promise<LinkDownload> {
  const { data } = await http.post<LinkDownload>(`/solicitacoes-exame/${id}/link-download`);
  return data;
}

/** Gera um "magic-link" de acesso (login em 1 clique) do paciente, para enviar por WhatsApp. */
export async function gerarLinkAcesso(id: string): Promise<LinkDownload> {
  const { data } = await http.post<LinkDownload>(`/solicitacoes-exame/${id}/link-acesso`);
  return data;
}

/**
 * Abre, em nova aba, o PDF do exame completo (capa + imagens + laudo) para
 * visualização/impressão no navegador — o download fica a cargo do próprio
 * visualizador de PDF do browser. O endpoint exige bearer (popups não levam o
 * token do interceptor), então baixamos como blob e abrimos uma blob URL.
 */
export async function abrirExameCompleto(id: string): Promise<void> {
  const resp = await http.get(`/solicitacoes-exame/${id}/exame-completo-pdf`, {
    responseType: 'blob',
  });
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const janela = window.open(url, `exame-completo-${id}`);
  if (!janela) {
    alert('A janela do documento foi bloqueada pelo navegador. Libere os popups para este site.');
    URL.revokeObjectURL(url);
    return;
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/**
 * True se o erro for o 409 de falha ao remover o item de worklist no dcm4chee
 * (código `solicitacaoExame.exclusao_pacs_falhou`) — sinaliza que dá pra forçar.
 */
export function ehFalhaExclusaoPacs(e: unknown): boolean {
  const tipo = (e as { response?: { data?: { type?: string } } })?.response?.data?.type;
  return tipo === 'solicitacaoExame.exclusao_pacs_falhou';
}
