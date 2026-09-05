import { http } from '@/shared/api/httpClient';

/**
 * Recorte da análise de demanda. `eixoData` muda a pergunta, não a ordenação:
 * `agendada` = "o que está marcado neste período"; `solicitada` = "o que foi pedido nele".
 */
export type DemandaFiltro = {
  de: string;
  ate: string;
  eixoData: 'agendada' | 'solicitada';
  unidadeExecutanteId?: string | null;
  unidadeSolicitanteId?: string | null;
  procedimento?: string | null;
  prioridade?: number | null;
};

export type DemandaResumo = {
  regulados: number;
  comEspera: number;
  /** Mediana, não média: a cauda vai a anos e a média não descreveria o caso de ninguém. */
  esperaMediana: number;
  esperaP90: number;
  esperaMaxima: number;
  /** Da regulação até o dia marcado. A diferença para `esperaMediana` é o tempo pré-regulação. */
  esperaRegulacaoMediana: number;
  ate30Dias: number;
  acima180Dias: number;
  /** Agendado antes de solicitado: data errada na origem, exposta em vez de descartada. */
  inconsistentes: number;
  procedimentos: number;
  unidadesSolicitantes: number;
  confirmados: number;
};

export type DemandaProcedimento = {
  procedimento: string;
  volume: number;
  esperaMediana: number;
  esperaP90: number;
  acima90Dias: number;
  unidadesSolicitantes: number;
  unidadesExecutantes: number;
};

export type DemandaFaixaEspera = { ordem: number; rotulo: string; volume: number };

export type DemandaOrigem = { chave: string; rotulo: string; volume: number; esperaMediana: number };

export type DemandaSerie = { mes: string; volume: number; esperaMediana: number; esperaP90: number };

export type DemandaOpcoes = {
  unidadesExecutantes: { valor: string; rotulo: string }[];
  unidadesSolicitantes: { valor: string; rotulo: string }[];
  procedimentos: { valor: string; rotulo: string }[];
};

/** Até onde o dado existe — sem isto a tela apresentaria falta de importação como falta de movimento. */
export type AgendaCobertura = {
  primeiroDiaAgendado: string | null;
  ultimoDiaAgendado: string | null;
  ultimoDiaDeEscala: string | null;
  agendamentos: number;
};

function params(f: DemandaFiltro, extra: Record<string, unknown> = {}) {
  return {
    de: f.de,
    ate: f.ate,
    eixoData: f.eixoData,
    unidadeExecutanteId: f.unidadeExecutanteId || undefined,
    unidadeSolicitanteId: f.unidadeSolicitanteId || undefined,
    procedimento: f.procedimento || undefined,
    prioridade: f.prioridade ?? undefined,
    ...extra,
  };
}

export async function obterCobertura(): Promise<AgendaCobertura> {
  const { data } = await http.get<AgendaCobertura>('/agenda/cobertura');
  return data;
}

export async function obterOpcoesDemanda(): Promise<DemandaOpcoes> {
  const { data } = await http.get<DemandaOpcoes>('/agenda/demanda/opcoes');
  return data;
}

export async function obterResumoDemanda(f: DemandaFiltro): Promise<DemandaResumo> {
  const { data } = await http.get<DemandaResumo>('/agenda/demanda/resumo', { params: params(f) });
  return data;
}

export async function listarProcedimentosDemanda(
  f: DemandaFiltro,
  ordenarPor: 'volume' | 'espera' | 'atraso',
  limite = 20,
): Promise<DemandaProcedimento[]> {
  const { data } = await http.get<DemandaProcedimento[]>('/agenda/demanda/procedimentos', {
    params: params(f, { ordenarPor, limite }),
  });
  return data;
}

export async function obterFaixasEspera(f: DemandaFiltro): Promise<DemandaFaixaEspera[]> {
  const { data } = await http.get<DemandaFaixaEspera[]>('/agenda/demanda/faixas-espera', {
    params: params(f),
  });
  return data;
}

export async function obterOrigemDemanda(
  f: DemandaFiltro,
  eixo: 'solicitante' | 'executante',
  limite = 15,
): Promise<DemandaOrigem[]> {
  const { data } = await http.get<DemandaOrigem[]>('/agenda/demanda/origem', {
    params: params(f, { eixo, limite }),
  });
  return data;
}

export async function obterSerieDemanda(f: DemandaFiltro): Promise<DemandaSerie[]> {
  const { data } = await http.get<DemandaSerie[]>('/agenda/demanda/serie', { params: params(f) });
  return data;
}
