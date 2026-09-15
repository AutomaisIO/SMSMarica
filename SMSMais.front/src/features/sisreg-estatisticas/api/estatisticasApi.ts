import { http } from '@/shared/api/httpClient';
import type {
  EquipeEstatistica,
  IndividualEstatistica,
  OperadoresConfiguracao,
  Periodo,
} from '../types';

const BASE = '/sisreg/estatisticas/operadores';

export async function obterEquipe(periodo: Periodo): Promise<EquipeEstatistica> {
  const { data } = await http.get<EquipeEstatistica>(`${BASE}/equipe`, { params: periodo });
  return data;
}

export async function obterIndividual(chave: string, periodo: Periodo): Promise<IndividualEstatistica> {
  const { data } = await http.get<IndividualEstatistica>(`${BASE}/individual`, {
    params: { chave, ...periodo },
  });
  return data;
}

export async function obterConfiguracaoOperadores(): Promise<OperadoresConfiguracao> {
  const { data } = await http.get<OperadoresConfiguracao>(`${BASE}/configuracao`);
  return data;
}

export async function salvarOperadoresHabilitados(logins: string[]): Promise<OperadoresConfiguracao> {
  const { data } = await http.put<OperadoresConfiguracao>(`${BASE}/configuracao`, { logins });
  return data;
}
