import { hojeSP } from '@/shared/lib/datas';
import type {
  OuvidoriaCanal,
  OuvidoriaIdentificacao,
  OuvidoriaMotivoArquivamento,
  OuvidoriaMotivoNaoAtendimento,
  OuvidoriaOrigem,
  OuvidoriaPrioridade,
  OuvidoriaResolutividade,
  OuvidoriaSituacaoFinal,
  OuvidoriaStatus,
  OuvidoriaTipo,
  OuvidoriaTipoEvento,
  OuvidoriaTipoPontoResposta,
} from '@/features/ouvidoria/types';

/** Rótulos pt-BR de todos os enums da Ouvidoria (plano §1.1) + cores de badge + faixa de prazo. */

export const TIPOS: OuvidoriaTipo[] = ['Solicitacao', 'Reclamacao', 'Denuncia', 'Sugestao', 'Elogio', 'Informacao'];
export const ROTULO_TIPO: Record<OuvidoriaTipo, string> = {
  Solicitacao: 'Solicitação',
  Reclamacao: 'Reclamação',
  Denuncia: 'Denúncia',
  Sugestao: 'Sugestão',
  Elogio: 'Elogio',
  Informacao: 'Informação',
};

export const IDENTIFICACOES: OuvidoriaIdentificacao[] = ['Identificada', 'Sigilosa', 'Anonima'];
export const ROTULO_IDENTIFICACAO: Record<OuvidoriaIdentificacao, string> = {
  Identificada: 'Identificada',
  Sigilosa: 'Sigilosa (identidade restrita à ouvidoria)',
  Anonima: 'Anônima (sem acompanhamento)',
};

export const CANAIS: OuvidoriaCanal[] = [
  'Painel',
  'Presencial',
  'Telefone',
  'WhatsApp',
  'Email',
  'Carta',
  'Urna',
  'BuscaAtiva',
  'SitePublico',
  'AppCidadao',
  'Disque136',
  'FalaBr',
  'OuvidoriaGeral',
  'Outro',
];
export const ROTULO_CANAL: Record<OuvidoriaCanal, string> = {
  Painel: 'Painel (registro interno)',
  SitePublico: 'Site público',
  AppCidadao: 'App do cidadão',
  WhatsApp: 'WhatsApp',
  Presencial: 'Presencial',
  Telefone: 'Telefone',
  Email: 'E-mail',
  Carta: 'Carta',
  Urna: 'Urna',
  BuscaAtiva: 'Busca ativa',
  Disque136: 'Disque 136',
  FalaBr: 'Fala.BR',
  OuvidoriaGeral: 'Ouvidoria-geral do município',
  Outro: 'Outro',
};

export const ORIGENS: OuvidoriaOrigem[] = ['Cidadao', 'OuvidoriaAtiva', 'DeOficio', 'Coletiva'];
export const ROTULO_ORIGEM: Record<OuvidoriaOrigem, string> = {
  Cidadao: 'Cidadão',
  OuvidoriaAtiva: 'Ouvidoria ativa',
  DeOficio: 'De ofício',
  Coletiva: 'Coletiva',
};

export const PRIORIDADES: OuvidoriaPrioridade[] = ['Normal', 'Alta', 'Urgente'];
export const ROTULO_PRIORIDADE: Record<OuvidoriaPrioridade, string> = {
  Normal: 'Normal',
  Alta: 'Alta',
  Urgente: 'Urgente',
};

export const STATUS: OuvidoriaStatus[] = [
  'Registrada',
  'EmTriagem',
  'Encaminhada',
  'AguardandoComplementacao',
  'RespondidaPelaArea',
  'EmValidacao',
  'Respondida',
  'EmRecurso',
  'Concluida',
  'Arquivada',
  'EncaminhadaOutroOrgao',
];
export const ROTULO_STATUS: Record<OuvidoriaStatus, string> = {
  Registrada: 'Registrada',
  EmTriagem: 'Em triagem',
  Encaminhada: 'Encaminhada à área',
  AguardandoComplementacao: 'Aguardando complementação',
  RespondidaPelaArea: 'Respondida pela área',
  EmValidacao: 'Em validação',
  Respondida: 'Respondida ao cidadão',
  EmRecurso: 'Em recurso',
  Concluida: 'Concluída',
  Arquivada: 'Arquivada',
  EncaminhadaOutroOrgao: 'Encaminhada a outro órgão',
};

export const ROTULO_RESOLUTIVIDADE: Record<OuvidoriaResolutividade, string> = {
  Resolvida: 'Resolvida',
  NaoResolvida: 'Não resolvida',
};

export const ROTULO_SITUACAO_FINAL: Record<OuvidoriaSituacaoFinal, string> = {
  Atendida: 'Atendida',
  NaoAtendida: 'Não atendida',
  NaoLocalizado: 'Cidadão não localizado',
  Faleceu: 'Cidadão faleceu',
  Procede: 'Procede',
  NaoProcede: 'Não procede',
  Inconclusiva: 'Inconclusiva',
};

export const MOTIVOS_NAO_ATENDIMENTO: OuvidoriaMotivoNaoAtendimento[] = [
  'FaltaRecursos',
  'NaoCobertoSus',
  'FezParticular',
  'VagasInsuficientes',
  'NaoCompareceu',
  'Outro',
];
export const ROTULO_MOTIVO_NAO_ATENDIMENTO: Record<OuvidoriaMotivoNaoAtendimento, string> = {
  FaltaRecursos: 'Falta de recursos',
  NaoCobertoSus: 'Não coberto pelo SUS',
  FezParticular: 'Cidadão fez pelo particular',
  VagasInsuficientes: 'Vagas insuficientes',
  NaoCompareceu: 'Cidadão não compareceu',
  Outro: 'Outro',
};

export const MOTIVOS_ARQUIVAMENTO: OuvidoriaMotivoArquivamento[] = [
  'Duplicidade',
  'TextoIncompreensivel',
  'FaltaUrbanidade',
  'Impropria',
  'CopiaConhecimento',
  'PerdaObjeto',
  'SemComplementacao',
  'SemElementosMinimos',
  'Outro',
];
export const ROTULO_MOTIVO_ARQUIVAMENTO: Record<OuvidoriaMotivoArquivamento, string> = {
  Duplicidade: 'Duplicidade (já existe manifestação igual)',
  TextoIncompreensivel: 'Texto incompreensível',
  FaltaUrbanidade: 'Falta de urbanidade',
  Impropria: 'Imprópria (não é assunto de ouvidoria)',
  CopiaConhecimento: 'Cópia para conhecimento',
  PerdaObjeto: 'Perda de objeto',
  SemComplementacao: 'Sem complementação no prazo',
  SemElementosMinimos: 'Sem elementos mínimos',
  Outro: 'Outro',
};

export const ROTULO_TIPO_EVENTO: Record<OuvidoriaTipoEvento, string> = {
  Registro: 'Registro',
  Triagem: 'Triagem',
  Reclassificacao: 'Reclassificação',
  Encaminhamento: 'Encaminhamento',
  PedidoComplementacao: 'Pedido de complementação',
  Complementacao: 'Complementação recebida',
  RespostaArea: 'Resposta da área',
  DevolucaoParaReanalise: 'Devolvida à área para reanálise',
  RespostaIntermediaria: 'Resposta intermediária ao cidadão',
  RespostaConclusiva: 'Resposta conclusiva ao cidadão',
  Prorrogacao: 'Prorrogação',
  Cobranca: 'Cobrança à área',
  Escalonamento: 'Escalonamento',
  Recurso: 'Recurso do cidadão',
  Conclusao: 'Conclusão',
  Arquivamento: 'Arquivamento',
  EncaminhamentoExterno: 'Encaminhamento a outro órgão',
  Anotacao: 'Anotação interna',
  Habilitacao: 'Denúncia habilitada para apuração',
  Reabertura: 'Reabertura',
  AcessoIdentidade: 'Acesso à identidade do manifestante',
};

export const TIPOS_PONTO: OuvidoriaTipoPontoResposta[] = ['Unidade', 'AreaCentral', 'Apuracao'];
export const ROTULO_TIPO_PONTO: Record<OuvidoriaTipoPontoResposta, string> = {
  Unidade: 'Unidade de saúde',
  AreaCentral: 'Área central (secretaria)',
  Apuracao: 'Unidade apuratória (denúncias)',
};

// ---- Cores ----

export const CLASSE_STATUS: Record<OuvidoriaStatus, string> = {
  Registrada: 'bg-slate-100 text-slate-700 ring-slate-600/20',
  EmTriagem: 'bg-sky-50 text-sky-700 ring-sky-600/20',
  Encaminhada: 'bg-blue-50 text-blue-700 ring-blue-600/20',
  AguardandoComplementacao: 'bg-amber-50 text-amber-700 ring-amber-600/20',
  RespondidaPelaArea: 'bg-indigo-50 text-indigo-700 ring-indigo-600/20',
  EmValidacao: 'bg-violet-50 text-violet-700 ring-violet-600/20',
  Respondida: 'bg-green-50 text-green-700 ring-green-600/20',
  EmRecurso: 'bg-orange-50 text-orange-700 ring-orange-600/20',
  Concluida: 'bg-emerald-50 text-emerald-800 ring-emerald-600/20',
  Arquivada: 'bg-gray-100 text-gray-600 ring-gray-500/20',
  EncaminhadaOutroOrgao: 'bg-stone-100 text-stone-700 ring-stone-500/20',
};

export const CLASSE_PRIORIDADE: Record<OuvidoriaPrioridade, string> = {
  Normal: 'bg-slate-100 text-slate-700 ring-slate-600/20',
  Alta: 'bg-orange-50 text-orange-700 ring-orange-600/20',
  Urgente: 'bg-red-50 text-red-700 ring-red-600/20',
};

export const CLASSE_TIPO: Record<OuvidoriaTipo, string> = {
  Solicitacao: 'bg-blue-50 text-blue-700 ring-blue-600/20',
  Reclamacao: 'bg-amber-50 text-amber-800 ring-amber-600/20',
  Denuncia: 'bg-red-50 text-red-700 ring-red-600/20',
  Sugestao: 'bg-teal-50 text-teal-700 ring-teal-600/20',
  Elogio: 'bg-green-50 text-green-700 ring-green-600/20',
  Informacao: 'bg-slate-100 text-slate-700 ring-slate-600/20',
};

// ---- Prazo ----

export type FaixaPrazo = 'ate30' | '31a60' | 'mais60';
export const ROTULO_FAIXA_PRAZO: Record<FaixaPrazo, string> = {
  ate30: 'Até 30 dias',
  '31a60': '31 a 60 dias',
  mais60: 'Mais de 60 dias',
};

/** Mesma régua do backend (`OuvidoriaPrazos.FaixaPrazo`). */
export function faixaPrazo(dias: number): FaixaPrazo {
  if (dias <= 30) return 'ate30';
  if (dias <= 60) return '31a60';
  return 'mais60';
}

/** Dias inteiros de `hoje` (Brasília) até a data "aaaa-mm-dd"; negativo = já passou. */
export function diasAte(dataIso: string | null | undefined): number | null {
  if (!dataIso) return null;
  const m = String(dataIso).match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (!m) return null;
  const hoje = hojeSP().split('-').map(Number);
  const alvo = Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  const base = Date.UTC(hoje[0], hoje[1] - 1, hoje[2]);
  return Math.round((alvo - base) / 86_400_000);
}
