import { AxiosError } from 'axios';
import { extrairMensagemDeErro, http, type ProblemaApi } from '@/shared/api/httpClient';
import type { CategoriaSolicitacao } from '@/features/solicitacoes/types';

/**
 * Consulta SISREG na NOSSA base (`/sisreg/base/*`). Não fala com o SISREG: lê o que a importação
 * e o sincronismo diário já trouxeram — não gasta o orçamento anti-robô do operador.
 */

export type SituacaoAgendamentoSisreg =
  | 'NaFila'
  | 'Agendada'
  | 'Pendente'
  | 'Compareceu'
  | 'Faltou'
  | 'Cancelada';

export const SITUACOES_SISREG: { valor: SituacaoAgendamentoSisreg; rotulo: string; dica: string }[] = [
  { valor: 'NaFila', rotulo: 'Na fila', dica: 'Solicitada, ainda sem data.' },
  { valor: 'Agendada', rotulo: 'Agendada', dica: 'Com data de hoje em diante.' },
  {
    valor: 'Pendente',
    rotulo: 'Pendente de atualização',
    dica: 'A data passou e a unidade não apontou no SISREG nem a chegada nem a falta.',
  },
  { valor: 'Compareceu', rotulo: 'Compareceu', dica: 'Chegada confirmada no SISREG ou na recepção.' },
  { valor: 'Faltou', rotulo: 'Faltou', dica: 'Está na lista oficial de faltas do SISREG.' },
  { valor: 'Cancelada', rotulo: 'Cancelada', dica: 'Solicitação cancelada.' },
];

export function rotuloSituacao(s: SituacaoAgendamentoSisreg): string {
  return SITUACOES_SISREG.find((x) => x.valor === s)?.rotulo ?? s;
}

export type EixoDataConsultaSisreg = 'Agendamento' | 'Solicitacao';

export type FiltroConsultaBase = {
  inicio: string;
  fim: string;
  eixo: EixoDataConsultaSisreg;
  unidadeIds: string[];
  situacoes: SituacaoAgendamentoSisreg[];
  procedimentos: string[];
  incluirExames: boolean;
  incluirConsultas: boolean;
  pagina: number;
  tamanho: number;
};

export type AgendamentoBaseSisreg = {
  solicitacaoId: string;
  /** Id que abre o detalhe da solicitação (o do exame de imagem quando há). */
  detalheId: string;
  pacienteId: string;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  codigoSolicitacao: string | null;
  procedimento: string;
  categoria: CategoriaSolicitacao;
  /** Instante UTC (ISO com fuso). */
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  unidadeExecutante: string | null;
  situacao: SituacaoAgendamentoSisreg;
};

export type ResultadoConsultaBase = {
  totalAtendimentos: number;
  totalPessoas: number;
  porSituacao: { situacao: SituacaoAgendamentoSisreg; quantidade: number }[];
  pagina: number;
  tamanho: number;
  itens: AgendamentoBaseSisreg[];
};

export type OpcoesConsultaBase = {
  unidades: { id: string; nome: string; quantidade: number }[];
  procedimentos: { nome: string; quantidade: number }[];
};

export async function buscarAgendamentosBase(filtro: FiltroConsultaBase): Promise<ResultadoConsultaBase> {
  const { data } = await http.post<ResultadoConsultaBase>('/sisreg/base/agendamentos/buscar', filtro);
  return data;
}

export async function obterOpcoesConsultaBase(
  inicio: string,
  fim: string,
  eixo: EixoDataConsultaSisreg,
): Promise<OpcoesConsultaBase> {
  const { data } = await http.get<OpcoesConsultaBase>('/sisreg/base/opcoes', { params: { inicio, fim, eixo } });
  return data;
}

/**
 * Baixa o PDF nominal do filtro inteiro (sem paginação). O endpoint exige o bearer no header, então
 * o download passa pelo axios e vira blob — mesmo padrão do PDF dos indicadores.
 */
export async function baixarPdfAgendamentosBase(filtro: FiltroConsultaBase): Promise<void> {
  const resp = await http.post<Blob>('/sisreg/base/agendamentos/pdf', filtro, { responseType: 'blob' });

  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  let nome = `sisreg-atendimentos-${filtro.inicio}-${filtro.fim}.pdf`;
  if (match?.[1]) {
    try {
      nome = decodeURIComponent(match[1]);
    } catch {
      nome = match[1];
    }
  }

  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/**
 * Mensagem de erro do download. Com `responseType: 'blob'` o ProblemDetails chega como Blob e o
 * `extrairMensagemDeErro` não enxerga o texto — aqui o blob é lido antes (inclusive os `errors`
 * de validação, como "o PDF sai com até 5.000 atendimentos").
 */
export async function mensagemErroPdfBase(erro: unknown): Promise<string> {
  if (erro instanceof AxiosError && erro.response?.data instanceof Blob) {
    try {
      const dados = JSON.parse(await erro.response.data.text()) as ProblemaApi;
      const primeiroErro = dados.errors ? Object.values(dados.errors).flat()[0] : undefined;
      const msg = primeiroErro || dados.detail || dados.title;
      if (msg) return dados.codigoReferencia ? `${msg} (código ${dados.codigoReferencia})` : msg;
    } catch {
      // Corpo não é JSON: cai na mensagem genérica abaixo.
    }
  }
  return extrairMensagemDeErro(erro);
}
