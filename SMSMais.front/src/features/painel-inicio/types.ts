import type { DirecaoSolicitacao } from '@/features/solicitacoes-exame/types';

/**
 * Lente de escopo do painel. `Municipio` é a modelagem da REGULAÇÃO — que não é executante nem
 * solicitante e deliberadamente não é uma unidade (ADR-0029/0033). Só aparece para quem tem
 * acesso global ou um dos módulos de regulação de visão global.
 */
export type LenteEscopo = 'Unidade' | 'Municipio';

/** Recorte executante × solicitante. Ignorado na lente Município. */
export type DirecaoPainel = 'Tudo' | 'Executante' | 'Solicitante';

export type CausaFalhaImportacao =
  | 'SemCns'
  | 'CadsusIndisponivel'
  | 'CpfNaoResolvido'
  | 'UnidadeNaoResolvida'
  | 'LinhaInvalida'
  | 'ArquivoIncompativel'
  | 'Outro';

export type RaiaPainel<T> = {
  total: number;
  itens: T[];
};

export type ItemSolicitacaoPainel = {
  /** Id público — abre direto a tela de detalhe da solicitação. */
  id: string;
  pacienteId: string;
  pacienteNome: string | null;
  procedimento: string | null;
  dataAgendada: string | null;
  /** Texto literal digitado pelo paciente. Só na raia de cancelados. */
  motivoCancelamento: string | null;
  canal: string | null;
  respondidoEm: string | null;
  direcao: DirecaoSolicitacao | null;
  /** Preenchido só na lente Município (onde não há seta de direção). */
  unidadeNome: string | null;
};

export type ItemPendenciaImportacaoPainel = {
  id: string;
  codigoSolicitacao: string | null;
  pacienteNome: string | null;
  procedimento: string | null;
  dataAgendada: string | null;
  causa: CausaFalhaImportacao;
  motivo: string;
  tentativas: number;
  podeInformarCpf: boolean;
  unidadeNome: string | null;
};

export type ConfirmadosPainel = {
  recebidos: number;
  enviados: number;
  janelaDias: number;
};

/**
 * A resposta inteira da home, numa requisição. Raia `null` = SEM PERMISSÃO (não renderiza);
 * raia com `total: 0` = com permissão e sem itens (também não renderiza, por regra de UX — mas a
 * distinção existe para o badge e para diagnóstico).
 */
export type PainelInicio = {
  lente: LenteEscopo;
  podeAlternarLente: boolean;
  direcao: DirecaoPainel;
  unidadeReferenciaId: string | null;
  unidadeReferenciaNome: string | null;
  confirmados: ConfirmadosPainel | null;
  cancelados: RaiaPainel<ItemSolicitacaoPainel> | null;
  aguardando: RaiaPainel<ItemSolicitacaoPainel> | null;
  pendenciasImportacao: RaiaPainel<ItemPendenciaImportacaoPainel> | null;
  janelaAguardandoDias: number;
  /** Cancelados + pendências — o número do badge no item "Início" do menu. */
  totalCritico: number;
};

/** Pendência exibida no bloco da busca de Solicitações (contrato de `ImportacaoFalhaDto`). */
export type PendenciaImportacaoBusca = {
  id: string;
  codigoSolicitacao: string | null;
  nomePaciente: string | null;
  procedimentoTexto: string | null;
  dataAgendada: string | null;
  causa: CausaFalhaImportacao;
  motivo: string;
  tentativas: number;
  podeInformarCpf: boolean;
  pacienteCns: string | null;
  nomeExecutante: string | null;
};
