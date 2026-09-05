import { http } from '@/shared/api/httpClient';

/**
 * Recorte de toda consulta da Agenda. Datas em ISO `yyyy-MM-dd` — dia de Brasília, nunca
 * `new Date()` cru, que depois das 21h já está no dia seguinte em UTC.
 */
export type AgendaFiltro = {
  de: string;
  ate: string;
  unidadeId?: string | null;
  /** CBO — é o que serve de especialidade (o SISREG não tem outra). */
  cbo?: string | null;
  profissionalCpf?: string | null;
  procedimentoCodigo?: string | null;
};

export type AgendaResumo = {
  vagas: number;
  agendados: number;
  ocupacaoPercentual: number;
  diasComOferta: number;
  /** Células com vaga e nenhum agendamento — onde está sobrando. */
  diasOciosos: number;
  /** Células com mais agendamento que vaga — onde está faltando. */
  diasSobrecarregados: number;
  /** Agendamentos sem escala do mesmo profissional no dia: mede o quanto a grade descreve a realidade. */
  agendadosSemOferta: number;
  vagasPrimeiraVez: number;
  vagasRetorno: number;
  vagasReserva: number;
  profissionais: number;
  unidades: number;
};

export type AgendaDia = {
  data: string;
  unidadeId: string;
  unidadeNome: string;
  profissionalCpf: string;
  profissionalNome: string;
  cbo: string | null;
  vagas: number;
  agendados: number;
  livres: number;
  horaInicio: string | null;
  horaFim: string | null;
  blocos: number;
  procedimentos: string;
};

export type PaginaAgenda = { total: number; itens: AgendaDia[] };

export type BlocoEscala = {
  horaInicio: string;
  horaFim: string;
  procedimentoCodigo: string;
  procedimentoNome: string;
  vagasPrimeiraVez: number;
  vagasRetorno: number;
  vagasReserva: number;
  /** Duração ÷ vagas. Estimativa nossa — o SISREG manda os minutos zerados em boa parte das linhas. */
  minutosPorVagaEstimado: number | null;
};

export type Ocupante = {
  solicitacaoId: string;
  codigoSolicitacao: string | null;
  dataAgendadaUtc: string;
  pacienteNome: string | null;
  procedimentoTexto: string | null;
  unidadeSolicitanteNome: string | null;
  statusConfirmacao: number;
};

export type AgendaDiaDetalhe = {
  data: string;
  unidadeNome: string;
  profissionalNome: string;
  cbo: string | null;
  vagas: number;
  agendados: number;
  blocos: BlocoEscala[];
  ocupantes: Ocupante[];
};

export type AgendaRankingItem = {
  chave: string;
  rotulo: string;
  vagas: number;
  agendados: number;
  livres: number;
  ocupacaoPercentual: number;
};

export type AgendaPorDiaSemana = {
  diaSemana: number;
  rotulo: string;
  vagas: number;
  agendados: number;
  ocupacaoPercentual: number;
};

export type Opcao = { valor: string; rotulo: string };

export type AgendaOpcoes = {
  unidades: Opcao[];
  cbos: Opcao[];
  profissionais: Opcao[];
  procedimentos: Opcao[];
};

/** Filtros vazios viram `undefined` para não mandar string vazia como se fosse valor. */
function params(f: AgendaFiltro, extra: Record<string, unknown> = {}) {
  return {
    de: f.de,
    ate: f.ate,
    unidadeId: f.unidadeId || undefined,
    cbo: f.cbo || undefined,
    profissionalCpf: f.profissionalCpf || undefined,
    procedimentoCodigo: f.procedimentoCodigo || undefined,
    ...extra,
  };
}

export async function obterOpcoesAgenda(): Promise<AgendaOpcoes> {
  const { data } = await http.get<AgendaOpcoes>('/agenda/opcoes');
  return data;
}

export async function obterResumoAgenda(f: AgendaFiltro): Promise<AgendaResumo> {
  const { data } = await http.get<AgendaResumo>('/agenda/resumo', { params: params(f) });
  return data;
}

export async function listarDiasAgenda(f: AgendaFiltro, pagina = 0, tamanho = 100): Promise<PaginaAgenda> {
  const { data } = await http.get<PaginaAgenda>('/agenda/dias', { params: params(f, { pagina, tamanho }) });
  return data;
}

export async function detalharDiaAgenda(
  unidadeId: string,
  profissionalCpf: string,
  data_: string,
): Promise<AgendaDiaDetalhe> {
  const { data } = await http.get<AgendaDiaDetalhe>('/agenda/dia', {
    params: { unidadeId, profissionalCpf, data: data_ },
  });
  return data;
}

export async function rankingAgenda(
  f: AgendaFiltro,
  eixo: 'unidade' | 'especialidade' | 'profissional',
  limite = 20,
): Promise<AgendaRankingItem[]> {
  const { data } = await http.get<AgendaRankingItem[]>('/agenda/ranking', {
    params: params(f, { eixo, limite }),
  });
  return data;
}

export async function agendaPorDiaSemana(f: AgendaFiltro): Promise<AgendaPorDiaSemana[]> {
  const { data } = await http.get<AgendaPorDiaSemana[]>('/agenda/dias-semana', { params: params(f) });
  return data;
}
