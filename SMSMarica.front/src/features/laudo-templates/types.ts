export type LaudoTemplate = {
  id: string;
  nome: string;
  categoria: string;
  descricao: string | null;
  conteudoJson: string;
  conteudoHtml: string;
  estruturaJson: string | null;
  criadoPorUsuarioId: string;
  criadoPorNome: string | null;
  criadoEm: string;
  atualizadoPorUsuarioId: string | null;
  atualizadoPorNome: string | null;
  atualizadoEm: string | null;
  ativo: boolean;
};

export type LaudoTemplateListItem = {
  id: string;
  nome: string;
  categoria: string;
  descricao: string | null;
  temChecklist: boolean;
  criadoEm: string;
  ativo: boolean;
};

export type SalvarLaudoTemplatePayload = {
  nome: string;
  categoria: string;
  descricao: string | null;
  conteudoJson: string;
  conteudoHtml: string;
  estruturaJson: string | null;
};
