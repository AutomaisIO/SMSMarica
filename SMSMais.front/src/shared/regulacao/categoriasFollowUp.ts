/**
 * Categorias do classificador de FollowUP do SER/SERNIT (spike d, 05/09/2026 — nove classes
 * medidas sobre 18.904 FollowUPs reais). O backend grava a categoria em cada evento; aqui fica só
 * o que a pessoa lê e o que merece destaque.
 *
 * A lista é fechada de propósito: categoria nova pede regra nova na configuração da regulação e
 * rótulo aqui — sem rótulo, a tela mostra o nome cru, que ainda é melhor do que esconder.
 */
export type CategoriaFollowUp =
  | 'FalhaContato'
  | 'ContatoRealizado'
  | 'SemVaga'
  | 'ReclassificacaoRisco'
  | 'OrientacaoAoPaciente'
  | 'SolicitacaoAoSolicitante'
  | 'CancelamentoOuReagendamento'
  | 'Agendamento'
  | 'Outro';

export const ROTULO_CATEGORIA_FOLLOWUP: Record<CategoriaFollowUp, string> = {
  FalhaContato: 'Falha de contato',
  ContatoRealizado: 'Contato realizado',
  SemVaga: 'Sem vaga',
  ReclassificacaoRisco: 'Risco reclassificado',
  OrientacaoAoPaciente: 'Orientação ao paciente',
  SolicitacaoAoSolicitante: 'Pedido à unidade',
  CancelamentoOuReagendamento: 'Cancelamento / reagendamento',
  Agendamento: 'Agendamento',
  Outro: 'Outro',
};

/** Ordem de exibição no filtro: o que pede ação primeiro. */
export const CATEGORIAS_FOLLOWUP: CategoriaFollowUp[] = [
  'FalhaContato',
  'SolicitacaoAoSolicitante',
  'ContatoRealizado',
  'SemVaga',
  'ReclassificacaoRisco',
  'OrientacaoAoPaciente',
  'CancelamentoOuReagendamento',
  'Agendamento',
  'Outro',
];

/**
 * Categorias que PEDEM AÇÃO da unidade e por isso ganham ponto de atenção no card. Falha de
 * contato é a maior (40% do SER): a central não achou o paciente, e nós temos telefone e
 * WhatsApp que ela não tem. Pedido à unidade é a central cobrando documento ou informação.
 */
export const CATEGORIAS_FOLLOWUP_ATENCAO: ReadonlySet<CategoriaFollowUp> = new Set([
  'FalhaContato',
  'SolicitacaoAoSolicitante',
]);

export function rotuloCategoriaFollowUp(categoria: string | null | undefined): string | null {
  if (!categoria) return null;
  return ROTULO_CATEGORIA_FOLLOWUP[categoria as CategoriaFollowUp] ?? categoria;
}

export function ehCategoriaDeAtencao(categoria: string | null | undefined): boolean {
  return !!categoria && CATEGORIAS_FOLLOWUP_ATENCAO.has(categoria as CategoriaFollowUp);
}
