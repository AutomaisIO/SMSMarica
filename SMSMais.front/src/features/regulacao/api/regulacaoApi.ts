import { http } from '@/shared/api/httpClient';
import type {
  BuscaProcedimentoResultado,
  CatalogoSyncResultado,
  ConfiguracaoFluxo,
  ConfiguracaoRegulacao,
  PacienteCadsus,
  PacienteResumoRegulacao,
  RegulacaoProcedimentoDetalhe,
  SugestaoPareamento,
  TipoProcedimentoRegulacao,
} from '../types';

const base = '/regulacao/procedimentos';

export async function buscarProcedimentos(
  q: string,
  tipo?: TipoProcedimentoRegulacao,
  limite = 20,
): Promise<BuscaProcedimentoResultado> {
  const { data } = await http.get<BuscaProcedimentoResultado>(`${base}/buscar`, {
    params: { q, tipo, limite },
  });
  return data;
}

export async function obterProcedimento(id: string): Promise<RegulacaoProcedimentoDetalhe> {
  const { data } = await http.get<RegulacaoProcedimentoDetalhe>(`${base}/${id}`);
  return data;
}

export async function sincronizarCatalogo(): Promise<CatalogoSyncResultado> {
  const { data } = await http.post<CatalogoSyncResultado>(`${base}/sincronizar`);
  return data;
}

export async function listarSugestoesPareamento(): Promise<SugestaoPareamento[]> {
  const { data } = await http.get<SugestaoPareamento[]>(`${base}/sugestoes`);
  return data;
}

export async function confirmarPareamento(origemId: string, procedimentoId: string): Promise<void> {
  await http.post(`${base}/origens/${origemId}/confirmar`, { procedimentoId });
}

export async function rejeitarPareamento(origemId: string): Promise<void> {
  await http.post(`${base}/origens/${origemId}/rejeitar`);
}

export async function renomearCanonico(id: string, nome: string): Promise<void> {
  await http.put(`${base}/${id}/nome`, { nome });
}

// ---------------------------------------------------------------- configuração

const baseConfig = '/regulacao/configuracao';

export async function obterConfiguracaoRegulacao(): Promise<ConfiguracaoRegulacao> {
  const { data } = await http.get<ConfiguracaoRegulacao>(baseConfig);
  return data;
}

export async function obterConfiguracaoFluxo(): Promise<ConfiguracaoFluxo> {
  const { data } = await http.get<ConfiguracaoFluxo>(`${baseConfig}/fluxo`);
  return data;
}

export async function salvarConfiguracaoRegulacao(
  payload: Omit<ConfiguracaoRegulacao, 'atualizadoEm' | 'atualizadoPorNome'>,
): Promise<ConfiguracaoRegulacao> {
  const { data } = await http.put<ConfiguracaoRegulacao>(baseConfig, payload);
  return data;
}

// ---------------------------------------------------------------- paciente

const basePaciente = '/regulacao/pacientes';

export async function buscarPacienteLocal(termo: string): Promise<PacienteResumoRegulacao[]> {
  const { data } = await http.get<PacienteResumoRegulacao[]>(`${basePaciente}/buscar`, {
    params: { termo },
  });
  return data;
}

export async function consultarCadsus(documento: string): Promise<PacienteCadsus> {
  const { data } = await http.get<PacienteCadsus>(`${basePaciente}/cadsus`, {
    params: { documento },
  });
  return data;
}

export async function confirmarCadsus(dto: PacienteCadsus): Promise<PacienteResumoRegulacao> {
  const { data } = await http.post<PacienteResumoRegulacao>(`${basePaciente}/cadsus/confirmar`, dto);
  return data;
}

export async function informarCpfPaciente(
  id: string,
  cpf: string,
): Promise<PacienteResumoRegulacao> {
  const { data } = await http.post<PacienteResumoRegulacao>(`${basePaciente}/${id}/cpf`, { cpf });
  return data;
}
