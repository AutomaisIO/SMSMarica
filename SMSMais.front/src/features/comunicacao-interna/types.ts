/**
 * Tipos da Comunicação interna (ADR-0079) — espelho de
 * `SMSMais.Core/ComunicacaoInterna/Dtos/ComunicacaoInternaDtos.cs` (JSON camelCase, enum como string).
 */

export type TipoConversaInterna = 'Unidade' | 'Direta';

export type UnidadeVinculoInterno = { unidadeId: string; nome: string; principal: boolean };

export type ContextoComunicacaoInterna = {
  usuarioId: string;
  minhasUnidades: UnidadeVinculoInterno[];
  unidadeAtualId: string | null;
};

export type ResumoComunicacaoInterna = { naoLidas: number };

export type ConversaInternaItem = {
  id: string;
  tipo: TipoConversaInterna;
  titulo: string;
  subtitulo: string | null;
  unidadeAId: string | null;
  unidadeANome: string | null;
  unidadeBId: string | null;
  unidadeBNome: string | null;
  outroUsuarioId: string | null;
  outroOnline: boolean;
  ultimaMensagemEm: string | null;
  ultimaMensagemPreview: string | null;
  ultimaMensagemAutorNome: string | null;
  naoLidas: number;
  silenciada: boolean;
};

export type ParticipanteInterno = {
  usuarioId: string;
  nome: string;
  unidadeNome: string | null;
  online: boolean;
};

export type ConversaInternaDetalhe = {
  conversa: ConversaInternaItem;
  participantes: ParticipanteInterno[];
  /** Só na direta: até onde a outra pessoa leu (✓✓). */
  outroLidaAte: string | null;
};

export type AnexoInterno = {
  nome: string;
  mime: string;
  tamanhoBytes: number;
  duracaoSegundos: number | null;
  ehAudio: boolean;
};

export type MensagemInterna = {
  id: string;
  conversaId: string;
  autorId: string;
  autorNome: string;
  autorUnidadeNome: string | null;
  texto: string | null;
  enviadaEm: string;
  anexo: AnexoInterno | null;
};

/** Página em ordem cronológica (a mais antiga primeiro). `temMais` = há anteriores. */
export type PaginaMensagensInternas = { mensagens: MensagemInterna[]; temMais: boolean };

export type UnidadeInterna = { id: string; nome: string; usuarios: number; online: number; minha: boolean };

export type UsuarioInterno = { id: string; nome: string; online: boolean; souEu: boolean };

export type ResultadoBuscaInterna = {
  mensagemId: string;
  conversaId: string;
  conversaTitulo: string;
  autorNome: string;
  trecho: string;
  enviadaEm: string;
};

// ---- Tempo real (payloads do hub /hubs/comunicacao-interna) ----

export type MensagemInternaEvento = {
  mensagem: MensagemInterna;
  tipo: TipoConversaInterna;
  unidadeANome: string | null;
  unidadeBNome: string | null;
};

export type LeituraInternaEvento = { conversaId: string; usuarioId: string; lidaAte: string };

export type PresencaInternaEvento = { usuarioId: string; online: boolean };

export type DigitandoInternoEvento = { conversaId: string; usuarioId: string; nome: string };
