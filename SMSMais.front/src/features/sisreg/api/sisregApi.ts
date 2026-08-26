import { http } from '@/shared/api/httpClient';
import type {
  AtualizarSisregConfiguracaoPayload,
  ConsultaSisreg,
  MapeamentoLoteAceito,
  MapeamentoLoteAgendamento,
  MapeamentoLoteStatus,
  RegistroSisreg,
  SisregBuscaResultado,
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

export async function obterAgendamentoMapeamentoLote(): Promise<MapeamentoLoteAgendamento> {
  const { data } = await http.get<MapeamentoLoteAgendamento>('/sisreg/mapeamento/lote/agendamento');
  return data;
}

export async function salvarAgendamentoMapeamentoLote(
  payload: MapeamentoLoteAgendamento,
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
