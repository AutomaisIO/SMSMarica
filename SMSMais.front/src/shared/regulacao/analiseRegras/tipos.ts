import type { PerguntaPendente, RegraAvaliada } from '@/features/regulacao/tiposSolicitacao';

/**
 * Análise automática das regras de elegibilidade sobre os pedidos que CHEGAM dos sistemas
 * externos — o espelho do SER, do SERNIT e do ESUS de São Gonçalo (ADR-0063 §4).
 *
 * É o mesmo avaliador do assistente de Nova Solicitação, rodando com o que o espelho sabe (idade,
 * sexo, CPF, CID). É um PARECER ao lado do pedido: nada disso é escrito no sistema externo.
 *
 * Espelha `AnaliseRegrasDtos.cs` e o enum `VereditoAnaliseRegras` (viaja como string).
 */

/** Ordem de leitura: o que exige ação primeiro. */
export const VEREDITOS_ANALISE = [
  'Bloqueado',
  'AConferir',
  'ComRessalva',
  'Apto',
  'SemRegras',
  'SemProcedimento',
] as const;

export type VereditoAnaliseRegras = (typeof VEREDITOS_ANALISE)[number];

/** O que a fila mostra ao lado do pedido. */
export type AnaliseRegrasResumo = {
  veredito: VereditoAnaliseRegras;
  /** A frase mais específica — o motivo que decidiu o veredito. */
  resumo: string | null;
  bloqueios: number;
  ressalvas: number;
  perguntasPendentes: number;
  documentosPendentes: number;
  /** Instante UTC. */
  analisadoEm: string;
};

/** O que o detalhe mostra: o resumo e cada regra avaliada, com resultado e motivo. */
export type AnaliseRegrasDetalhe = {
  resumo: AnaliseRegrasResumo;
  procedimentoId: string | null;
  procedimentoNome: string | null;
  regras: RegraAvaliada[];
  perguntas: PerguntaPendente[];
  documentos: string[];
};

/** Contagem por veredito — os chips da fila. */
export type AnaliseRegrasContagem = { veredito: VereditoAnaliseRegras; quantidade: number };

/** Sistemas com espelho analisado — é o segmento da rota (`/regulacao/{sistema}/...`). */
export type SistemaAnaliseEspelho = 'ser' | 'sernit' | 'esussg';

export const ROTULO_VEREDITO: Record<VereditoAnaliseRegras, string> = {
  Bloqueado: 'Bloqueado',
  AConferir: 'A conferir',
  ComRessalva: 'Com ressalva',
  Apto: 'Apto',
  SemRegras: 'Sem regras',
  SemProcedimento: 'Sem procedimento',
};

/** O significado de cada veredito, como o backend define — vai no tooltip e no manual. */
export const DESCRICAO_VEREDITO: Record<VereditoAnaliseRegras, string> = {
  Bloqueado:
    'Uma regra bloqueia este sistema com o dado que o pedido já tem (idade, sexo, CPF, CID).',
  AConferir:
    'Uma regra que bloqueia depende de uma pergunta que só uma pessoa responde — a máquina não conclui; alguém precisa conferir.',
  ComRessalva: 'Alguma regra fez ressalva — quem regula decide.',
  Apto: 'Nenhuma regra bloqueou nem fez ressalva, e nada que trave ficou em aberto.',
  SemRegras: 'O procedimento não tem regra ativa que valha para este sistema.',
  SemProcedimento:
    'O procedimento do pedido não está ligado a nenhum procedimento do catálogo canônico — sem isso não há como saber quais regras valem.',
};

/**
 * Cores: vermelho trava, âmbar pede alguém, amarelo alerta, verde passa. Os dois cinzas não são
 * "bom" nem "ruim" — são ausência de régua; o tracejado marca o que falta ligar no catálogo.
 */
export const CLASSE_VEREDITO: Record<VereditoAnaliseRegras, string> = {
  Bloqueado: 'border-red-200 bg-red-50 text-red-700',
  AConferir: 'border-amber-300 bg-amber-50 text-amber-800',
  ComRessalva: 'border-yellow-300 bg-yellow-50 text-yellow-800',
  Apto: 'border-green-200 bg-green-50 text-green-700',
  SemRegras: 'border-slate-200 bg-slate-100 text-slate-600',
  SemProcedimento: 'border-dashed border-slate-300 bg-white text-slate-500',
};
