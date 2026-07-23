export type TicketTipo = 'Bug' | 'Mudanca' | 'Sugestao' | 'Duvida';
export type TicketStatus = 'Aberto' | 'EmAnalise' | 'Concluido' | 'Negado';
export type TicketPrioridade = 'Baixa' | 'Normal' | 'Alta';
export type TicketVisibilidade = 'Privado' | 'PorUnidade' | 'Publico';

export type TicketAnexo = {
  id: string;
  midiaId: string;
  nomeArquivo: string;
  /** URL relativa (`/midias/{id}`). Resolver com `urlMidiaAbsoluta`. */
  url: string;
};

export type TicketComentario = {
  id: string;
  autorId: string | null;
  autorNome: string | null;
  texto: string;
  interno: boolean;
  criadoEm: string;
  anexos: TicketAnexo[];
};

export type TicketListItem = {
  id: string;
  /** Número sequencial curto — referência humana do ticket ("#42"). */
  numero: number;
  titulo: string;
  tipo: TicketTipo;
  status: TicketStatus;
  prioridade: TicketPrioridade;
  autorNome: string | null;
  autorId: string | null;
  unidadeId: string | null;
  arquivado: boolean;
  qtdComentarios: number;
  criadoEm: string;
  atualizadoEm: string | null;
  /** Autor: há resposta da equipe ainda não reconhecida (mostra a "bandeira"). */
  respostaNaoReconhecida: boolean;
  /** Gestão: ticket novo/sem visualização (ou com atividade nova do autor). */
  novoParaGestao: boolean;
  /** Gestão: a equipe já respondeu ao autor (comentário público ou conclusão/negação). */
  respondido: boolean;
};

export type Ticket = {
  id: string;
  /** Número sequencial curto — referência humana do ticket ("#42"). */
  numero: number;
  titulo: string;
  descricao: string;
  tipo: TicketTipo;
  status: TicketStatus;
  prioridade: TicketPrioridade;
  respostaFinal: string | null;
  autorNome: string | null;
  autorId: string | null;
  unidadeId: string | null;
  arquivadoPeloAutor: boolean;
  arquivadoPeloAdmin: boolean;
  criadoEm: string;
  atualizadoEm: string | null;
  anexos: TicketAnexo[];
  comentarios: TicketComentario[];
};

export type AnexoRef = { midiaId: string; nomeArquivo: string };

export type AbrirTicketPayload = {
  titulo: string;
  descricao: string;
  tipo: TicketTipo;
  anexos?: AnexoRef[];
};

export type ComentarPayload = {
  texto: string;
  interno: boolean;
  anexos?: AnexoRef[];
};

export type AtualizarGestaoPayload = {
  status?: TicketStatus;
  prioridade?: TicketPrioridade;
  respostaFinal?: string;
};

export type TicketConfiguracao = { visibilidade: TicketVisibilidade };

/** Resumo do autor: quantas respostas ainda não reconhecidas (badge/bandeira). */
export type TicketResumoAutor = { naoReconhecidos: number };

/** Resumo da gestão para o badge do menu e o cabeçalho. */
export type TicketResumoGestao = { novos: number; abertos: number; emAnalise: number };

// ---- rótulos ----

export const ROTULO_TIPO: Record<TicketTipo, string> = {
  Bug: 'Bug',
  Mudanca: 'Mudança',
  Sugestao: 'Sugestão',
  Duvida: 'Dúvida',
};

export const ROTULO_STATUS: Record<TicketStatus, string> = {
  Aberto: 'Aberto',
  EmAnalise: 'Em análise',
  Concluido: 'Concluído',
  Negado: 'Negado',
};

export const ROTULO_PRIORIDADE: Record<TicketPrioridade, string> = {
  Baixa: 'Baixa',
  Normal: 'Normal',
  Alta: 'Alta',
};

export const ROTULO_VISIBILIDADE: Record<TicketVisibilidade, string> = {
  Privado: 'Privado — cada um vê apenas os seus',
  PorUnidade: 'Por unidade — a mesma unidade vê os tickets uns dos outros',
  Publico: 'Público — todos veem todos os tickets',
};
