// Distribuição da extensão do Chrome pela plataforma (ADR-0064). Espelha os DTOs de
// SMSMais.Core/Extensao/Distribuicao (enums viajam como string).

export type Artefato = 'Extensao' | 'Atualizador';
export type Canal = 'Teste' | 'Prod';

/** O que há para baixar. Versão nula = ainda não foi publicado e posto em produção. */
export type SituacaoDistribuicao = {
  versaoAtualizador: string | null;
  versaoExtensao: string | null;
};

export type SituacaoAtivacao = 'Pendente' | 'Autorizada' | 'Usada' | 'Vencida';

/** O pedido de um computador para ser autorizado (a página que alguém logado abre). */
export type AtivacaoPendente = {
  codigoPublico: string;
  computador: string | null;
  pedidoEm: string;
  expiraEm: string;
  situacao: SituacaoAtivacao;
};

/** Como a extensão está no Chrome, segundo o próprio computador. */
export type SituacaoChrome =
  | 'carregada'
  | 'nao-carregada'
  | 'desativada'
  | 'modo-dev-desligado'
  | 'fechado'
  | 'sem-perfil';

export type Dispositivo = {
  id: string;
  computador: string;
  canal: Canal;
  autorizadoEm: string;
  autorizadoPorNome: string | null;
  ultimoContatoEm: string | null;
  versaoExtensao: string | null;
  versaoAtualizador: string | null;
  situacaoChrome: string | null;
  revogadoEm: string | null;
};

export type Pacote = {
  id: string;
  artefato: Artefato;
  versao: string;
  tamanho: number;
  sha256: string;
  notas: string | null;
  publicadoEm: string;
  publicadoPorNome: string | null;
  publicadoPelaApi: boolean;
  promovidoEm: string | null;
  promovidoPelaApi: boolean;
  retiradoEm: string | null;
  /** É esta a versão que os computadores de teste recebem agora. */
  atualEmTeste: boolean;
  /** É esta a versão que todos os computadores recebem agora. */
  atualEmProd: boolean;
};

export type PublicarPacote = {
  artefato: Artefato;
  arquivo: File;
  /** Só o atualizador informa: a versão da extensão é lida do manifest do pacote. */
  versao?: string;
  notas?: string;
};

/** A API de publicação é uma opção: só existe enquanto houver chave ativa. */
export type ChavePublicacao = {
  ativa: boolean;
  prefixo: string | null;
  criadaEm: string | null;
  criadaPorNome: string | null;
  ultimoUsoEm: string | null;
};

/** A chave em claro só vem aqui, uma única vez, logo depois de gerada. */
export type ChaveGerada = {
  chave: string;
  situacao: ChavePublicacao;
};
