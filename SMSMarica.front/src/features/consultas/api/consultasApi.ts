import { http } from '@/shared/api/httpClient';
import type { ConsultaDetalhe, ConsultaListItem, FiltroConsultas } from '@/features/consultas/types';
import type { HistoricoSolicitacao } from '@/features/solicitacoes-exame/types';

export async function listarConsultas(filtro: FiltroConsultas = {}): Promise<ConsultaListItem[]> {
  const { data } = await http.get<ConsultaListItem[]>('/consultas', {
    params: {
      pacienteId: filtro.pacienteId || undefined,
      busca: filtro.busca || undefined,
      dataInicial: filtro.dataInicial || undefined,
      dataFinal: filtro.dataFinal || undefined,
      status: filtro.status || undefined,
      limite: filtro.limite || undefined,
    },
  });
  return data;
}

export async function obterConsulta(id: string): Promise<ConsultaDetalhe> {
  const { data } = await http.get<ConsultaDetalhe>(`/consultas/${id}`);
  return data;
}

/** Histórico de comunicação (WhatsApp de confirmação + contatos manuais) da consulta. */
export async function obterHistoricoConsulta(id: string): Promise<HistoricoSolicitacao> {
  const { data } = await http.get<HistoricoSolicitacao>(`/consultas/${id}/historico`);
  return data;
}

/** Reenvia uma comunicação: revoga links/sessões anteriores e reconstrói com o contato atual. */
export async function reenviarComunicacaoConsulta(id: string, comunicacaoId: string): Promise<void> {
  await http.post(`/consultas/${id}/comunicacoes/${comunicacaoId}/reenviar`);
}

/** Registra um contato manual ("liguei, não atendeu"...). */
export async function registrarContatoConsulta(
  id: string,
  body: { meio: string; resultado: string; observacao: string | null },
): Promise<void> {
  await http.post(`/consultas/${id}/contatos`, body);
}
