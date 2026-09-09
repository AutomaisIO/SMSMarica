import type { Ofertas } from '../types';
import { http } from '@/shared/api/httpClient';
import type {
  AtualizarSisregConfiguracaoPayload,
  BackfillExecutanteResultado,
  ConsultaSisreg,
  MapeamentoLoteAceito,
  MapeamentoLoteAgendamento,
  MapeamentoLoteExecucao,
  MapeamentoLoteExecucaoItem,
  MapeamentoLoteStatus,
  AlternarAgendamentoRede,
  PrepararRede,
  PrepararRedePayload,
  PreverAgendamento,
  PreverAgendamentoPayload,
  RegistroSisreg,
  SalvarMapeamentoLoteAgendamento,
  SisregBuscaResultado,
  EscalasAgendamento,
  EscalasSincronizacaoAceita,
  EscalasSincronizacaoExecucao,
  EscalasSincronizacaoStatus,
  SalvarEscalasAgendamento,
  SincronismoAutomaticoSisreg,
  SisregConfiguracao,
  TestarConexaoSisregResultado,
} from '@/features/sisreg/types';

export async function obterConfiguracaoSisreg(): Promise<SisregConfiguracao> {
  const { data } = await http.get<SisregConfiguracao>('/sisreg/configuracao');
  return data;
}

export async function atualizarConfiguracaoSisreg(
  payload: AtualizarSisregConfiguracaoPayload,
): Promise<void> {
  await http.put('/sisreg/configuracao', payload);
}

/**
 * Liga/desliga o sincronismo automático. Endpoint separado do PUT da configuração de propósito:
 * é um interruptor de emergência, e submeter o formulário inteiro junto arrastaria edições
 * pendentes de credencial que ninguém pediu para salvar.
 */
export async function alternarSincronismoAutomatico(
  ativo: boolean,
): Promise<SincronismoAutomaticoSisreg> {
  const { data } = await http.put<SincronismoAutomaticoSisreg>(
    '/sisreg/configuracao/sincronismo-automatico',
    { ativo },
  );
  return data;
}

/**
 * Completa as solicitações já importadas com o profissional executante, relendo a linha crua do
 * SISREG que ficou guardada. Idempotente e sem nenhuma requisição externa.
 */
export async function backfillExecutante(): Promise<BackfillExecutanteResultado> {
  const { data } = await http.post<BackfillExecutanteResultado>(
    '/sisreg/configuracao/backfill-executante',
  );
  return data;
}

export async function testarConexaoSisreg(): Promise<TestarConexaoSisregResultado> {
  const { data } = await http.post<TestarConexaoSisregResultado>('/sisreg/configuracao/testar-conexao');
  return data;
}

// ----------------------------------------------------------- lote "sincroniza tudo" (#118)

/** Dispara AGORA a sincronização de profissionais/procedimentos + FHIR de todas as unidades. */
export async function sincronizarMapeamentoLote(): Promise<MapeamentoLoteAceito> {
  const { data } = await http.post<MapeamentoLoteAceito>('/sisreg/mapeamento/lote/sincronizar');
  return data;
}

/** Progresso do lote em curso. 204 (sem corpo) quando não há nenhum → null. */
export async function obterStatusMapeamentoLote(): Promise<MapeamentoLoteStatus | null> {
  const { data, status } = await http.get<MapeamentoLoteStatus | ''>('/sisreg/mapeamento/lote/status');
  return status === 204 || !data ? null : data;
}

export async function cancelarMapeamentoLote(): Promise<{ cancelada: boolean }> {
  const { data } = await http.post<{ cancelada: boolean }>('/sisreg/mapeamento/lote/cancelar');
  return data;
}

/** Sincronizações recentes — o histórico que sobrevive ao fim da execução. */
export async function listarExecucoesMapeamentoLote(limite = 10): Promise<MapeamentoLoteExecucao[]> {
  const { data } = await http.get<MapeamentoLoteExecucao[]>('/sisreg/mapeamento/lote/execucoes', {
    params: { limite },
  });
  return data;
}

/** Detalhe por unidade de uma sincronização. */
export async function listarItensMapeamentoLote(id: string): Promise<MapeamentoLoteExecucaoItem[]> {
  const { data } = await http.get<MapeamentoLoteExecucaoItem[]>(
    `/sisreg/mapeamento/lote/execucoes/${id}/itens`,
  );
  return data;
}

export async function obterAgendamentoMapeamentoLote(): Promise<MapeamentoLoteAgendamento> {
  const { data } = await http.get<MapeamentoLoteAgendamento>('/sisreg/mapeamento/lote/agendamento');
  return data;
}

export async function salvarAgendamentoMapeamentoLote(
  payload: SalvarMapeamentoLoteAgendamento,
): Promise<MapeamentoLoteAgendamento> {
  const { data } = await http.put<MapeamentoLoteAgendamento>('/sisreg/mapeamento/lote/agendamento', payload);
  return data;
}

type IntervaloParams = { inicio?: string; fim?: string; tamanho: number };

/** Executa uma das 6 consultas de leitura do SISREG. */
export async function consultarSisreg(
  consulta: ConsultaSisreg,
  params: IntervaloParams,
): Promise<SisregBuscaResultado<RegistroSisreg>> {
  const rota: Record<ConsultaSisreg, string> = {
    'novas-solicitacoes': '/sisreg/ambulatorial/novas-solicitacoes',
    fila: '/sisreg/ambulatorial/fila',
    agendadas: '/sisreg/ambulatorial/agendadas',
    atendidas: '/sisreg/ambulatorial/atendidas',
    'canceladas-devolvidas': '/sisreg/ambulatorial/canceladas-devolvidas',
    internacoes: '/sisreg/hospitalar/internacoes',
  };
  const { data } = await http.get<SisregBuscaResultado<RegistroSisreg>>(rota[consulta], { params });
  return data;
}

/**
 * Programa a rede inteira para o sincronismo diário: habilita os médicos e procedimentos já
 * mapeados e liga a varredura de cada unidade em horários escalonados, fora da janela 08h–15h.
 * Deixa o aviso por WhatsApp desligado em todas.
 */
export async function prepararRedeSisreg(payload: PrepararRedePayload): Promise<PrepararRede> {
  const { data } = await http.post<PrepararRede>('/sisreg/mapeamento/lote/preparar-rede', payload);
  return data;
}

/** Telefones que recebem aviso quando o sincronismo falha (sisreg | ser | sernit). */
export async function listarTelefonesNotificacao(provedor: string): Promise<string[]> {
  const { data } = await http.get<string[]>(`/integracoes/${provedor}/notificacoes`);
  return data;
}

export async function salvarTelefonesNotificacao(
  provedor: string,
  telefones: string[],
): Promise<string[]> {
  const { data } = await http.put<string[]>(`/integracoes/${provedor}/notificacoes`, { telefones });
  return data;
}

export async function testarNotificacaoSincronismo(provedor: string): Promise<{ enviados: number }> {
  const { data } = await http.post<{ enviados: number }>(`/integracoes/${provedor}/notificacoes/testar`);
  return data;
}

/** Prévia da distribuição: a que horas a fila termina, antes de confirmar. */
export async function preverAgendamentoSisreg(
  payload: PreverAgendamentoPayload,
): Promise<PreverAgendamento> {
  const { data } = await http.post<PreverAgendamento>(
    '/sisreg/mapeamento/lote/prever-agendamento',
    payload,
  );
  return data;
}

/** Liga ou desliga a importação diária de todas as unidades de uma vez. */
export async function alternarAgendamentoRede(ativo: boolean): Promise<AlternarAgendamentoRede> {
  const { data } = await http.put<AlternarAgendamentoRede>(
    '/sisreg/mapeamento/lote/agendamento-rede',
    { ativo },
  );
  return data;
}

// ----------------------------------------------------------- escalas (a OFERTA de vagas)

/** Dispara AGORA a sincronização da grade de escalas — uma requisição traz a rede inteira. */
export async function sincronizarEscalas(): Promise<EscalasSincronizacaoAceita> {
  const { data } = await http.post<EscalasSincronizacaoAceita>('/sisreg/escalas/sincronizar');
  return data;
}

/** Progresso em curso. 204 (sem corpo) quando não há nenhuma → null. */
export async function obterStatusEscalas(): Promise<EscalasSincronizacaoStatus | null> {
  const { data, status } = await http.get<EscalasSincronizacaoStatus | ''>('/sisreg/escalas/status');
  return status === 204 || !data ? null : data;
}

export async function cancelarEscalas(): Promise<{ cancelada: boolean }> {
  const { data } = await http.post<{ cancelada: boolean }>('/sisreg/escalas/cancelar');
  return data;
}

export async function listarExecucoesEscalas(limite = 10): Promise<EscalasSincronizacaoExecucao[]> {
  const { data } = await http.get<EscalasSincronizacaoExecucao[]>('/sisreg/escalas/execucoes', {
    params: { limite },
  });
  return data;
}

export async function obterAgendamentoEscalas(): Promise<EscalasAgendamento> {
  const { data } = await http.get<EscalasAgendamento>('/sisreg/escalas/agendamento');
  return data;
}

export async function salvarAgendamentoEscalas(
  payload: SalvarEscalasAgendamento,
): Promise<EscalasAgendamento> {
  const { data } = await http.put<EscalasAgendamento>('/sisreg/escalas/agendamento', payload);
  return data;
}

export async function listarOfertas(dias: number): Promise<Ofertas> {
  const { data } = await http.get<Ofertas>('/sisreg/ofertas', { params: { dias } });
  return data;
}
