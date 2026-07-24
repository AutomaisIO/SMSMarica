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
  /** Gestão: o ticket já foi encaminhado ao Agente IA (marca "Enviado à IA"). */
  enviadoIa: boolean;
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
  /** Última vez que a equipe respondeu ao autor (gestão usa para saber se ele já visualizou). */
  respondidoEm: string | null;
  /** Quando o autor reconheceu/visualizou a última resposta (null = ainda não viu). */
  respostaReconhecidaEm: string | null;
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

/** Ticket com resposta pendente de reconhecimento (alimenta o modal do autor). */
export type TicketPendente = {
  id: string;
  numero: number;
  titulo: string;
  status: TicketStatus;
  respostaFinal: string | null;
};

/** Resumo do autor: quantas respostas ainda não reconhecidas + a lista delas (para o modal). */
export type TicketResumoAutor = { naoReconhecidos: number; pendentes: TicketPendente[] };

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
