import { http } from '@/shared/api/httpClient';
import type {
  AtualizarSisregConfiguracaoPayload,
  ConsultaSisreg,
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
