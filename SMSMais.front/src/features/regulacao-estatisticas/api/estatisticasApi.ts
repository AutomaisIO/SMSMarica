import { http } from '@/shared/api/httpClient';
import type {
  EquipeExternaEstatistica,
  FonteExterna,
  IndividualExternoEstatistica,
  OperadoresExternosConfiguracao,
  Periodo,
} from '../types';

/** SER e SERNIT têm o mesmo contrato sob rotas irmãs — a fonte só troca o prefixo. */
function base(fonte: FonteExterna) {
  return `/regulacao/${fonte}/estatisticas/operadores`;
}

export async function obterEquipe(fonte: FonteExterna, periodo: Periodo): Promise<EquipeExternaEstatistica> {
  const { data } = await http.get<EquipeExternaEstatistica>(`${base(fonte)}/equipe`, { params: periodo });
  return data;
}

export async function obterIndividual(
  fonte: FonteExterna,
  chave: string,
  periodo: Periodo,
): Promise<IndividualExternoEstatistica> {
  const { data } = await http.get<IndividualExternoEstatistica>(`${base(fonte)}/individual`, {
    params: { chave, ...periodo },
  });
  return data;
}

export async function obterConfiguracaoOperadores(fonte: FonteExterna): Promise<OperadoresExternosConfiguracao> {
  const { data } = await http.get<OperadoresExternosConfiguracao>(`${base(fonte)}/configuracao`);
  return data;
}

export async function salvarOperadoresHabilitados(
  fonte: FonteExterna,
  nomes: string[],
): Promise<OperadoresExternosConfiguracao> {
  const { data } = await http.put<OperadoresExternosConfiguracao>(`${base(fonte)}/configuracao`, { nomes });
  return data;
}
