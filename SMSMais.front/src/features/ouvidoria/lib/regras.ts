import type {
  OuvidoriaIdentificacao,
  OuvidoriaSituacaoFinal,
  OuvidoriaStatus,
  OuvidoriaTipo,
} from '@/features/ouvidoria/types';

/**
 * Regras de negócio espelhadas do plano §2 — só para a UI decidir o que mostrar. A trava de verdade
 * é o backend: aqui é a lista LOCAL, usada como fallback quando `acoesPermitidas` não vem no detalhe.
 */

// ---- Identificação × tipo (§2.1.1, D-5) ----

/** Solicitação/Informação → só identificada; denúncia → as três; demais → identificada ou sigilosa. */
export function identificacoesPermitidas(tipo: OuvidoriaTipo): OuvidoriaIdentificacao[] {
  if (tipo === 'Solicitacao' || tipo === 'Informacao') return ['Identificada'];
  if (tipo === 'Denuncia') return ['Identificada', 'Sigilosa', 'Anonima'];
  return ['Identificada', 'Sigilosa'];
}

// ---- Situação final por tipo (§2.6) ----

export function situacoesFinaisPorTipo(tipo: OuvidoriaTipo): OuvidoriaSituacaoFinal[] {
  switch (tipo) {
    case 'Solicitacao':
      return ['Atendida', 'NaoAtendida', 'NaoLocalizado', 'Faleceu'];
    case 'Reclamacao':
    case 'Denuncia':
      return ['Procede', 'NaoProcede', 'Inconclusiva'];
    default:
      return ['Atendida'];
  }
}

// ---- Conteúdo mínimo da resposta conclusiva (PN CGU 116/2021, art. 29) ----

export function ajudaConteudoMinimo(tipo: OuvidoriaTipo): string {
  switch (tipo) {
    case 'Elogio':
      return 'Informe que o elogio foi encaminhado ao agente público elogiado e à chefia imediata dele.';
    case 'Reclamacao':
      return 'Apresente a análise do fato relatado e as providências adotadas (ou por que não foram).';
    case 'Solicitacao':
      return 'Informe a providência adotada ou, se ainda não foi possível, a possibilidade, a forma e o meio de atendimento.';
    case 'Sugestao':
      return 'Informe a posição do gestor sobre a sugestão (acatada, em estudo, não acatada e por quê) e, se for adotar, o prazo estimado.';
    case 'Denuncia':
      return 'Informe se a denúncia foi encaminhada à unidade apuratória competente ou arquivada — e, no arquivamento, o motivo.';
    case 'Informacao':
      return 'Dê a informação pedida, de forma clara e completa. Se não for possível, explique o motivo e onde obtê-la.';
  }
}

// ---- Ações por status (§2.2–2.7) ----

export type AcaoManifestacao =
  | 'triar'
  | 'encaminhar'
  | 'pedirComplementacao'
  | 'complementar'
  | 'responderArea'
  | 'devolverArea'
  | 'responderCidadao'
  | 'prorrogar'
  | 'cobrar'
  | 'escalonar'
  | 'recurso'
  | 'concluir'
  | 'arquivar'
  | 'encaminharExterno'
  | 'habilitar'
  | 'editarTeorPseudonimizado'
  | 'anotar';

export const ROTULO_ACAO: Record<AcaoManifestacao, string> = {
  triar: 'Triar',
  encaminhar: 'Encaminhar à área',
  pedirComplementacao: 'Pedir complementação',
  complementar: 'Registrar complementação',
  responderArea: 'Responder pela área',
  devolverArea: 'Devolver à área',
  responderCidadao: 'Responder ao cidadão',
  prorrogar: 'Prorrogar prazo',
  cobrar: 'Cobrar a área',
  escalonar: 'Escalonar',
  recurso: 'Registrar recurso',
  concluir: 'Concluir',
  arquivar: 'Arquivar',
  encaminharExterno: 'Encaminhar a outro órgão',
  habilitar: 'Habilitar denúncia',
  editarTeorPseudonimizado: 'Editar teor pseudonimizado',
  anotar: 'Anotar',
};

/**
 * Nomes com que o backend pode devolver cada ação em `acoesPermitidas` (normalizados: minúsculas,
 * sem separadores). O contrato diz só "nomes das ações válidas"; aceitamos o nome da rota, do
 * método do service e o nosso identificador.
 */
const APELIDOS_BACKEND: Record<AcaoManifestacao, string[]> = {
  triar: ['triar', 'triagem'],
  encaminhar: ['encaminhar', 'encaminhamento'],
  pedirComplementacao: ['pedircomplementacao'],
  complementar: ['complementar', 'complementacao'],
  responderArea: ['responderarea', 'respostaarea'],
  devolverArea: ['devolverarea', 'devolverparareanalise', 'devolucaoparareanalise'],
  responderCidadao: ['respondercidadao', 'responder'],
  prorrogar: ['prorrogar', 'prorrogacao'],
  cobrar: ['cobrar', 'cobranca'],
  escalonar: ['escalonar', 'escalonamento'],
  recurso: ['recurso', 'registrarrecurso'],
  concluir: ['concluir', 'conclusao'],
  arquivar: ['arquivar', 'arquivamento'],
  encaminharExterno: ['encaminharexterno', 'encaminhamentoexterno'],
  habilitar: ['habilitar', 'habilitardenuncia', 'habilitacao'],
  editarTeorPseudonimizado: ['teorpseudonimizado', 'atualizarteorpseudonimizado', 'editarteorpseudonimizado'],
  anotar: ['anotar', 'anotacao'],
};

function normalizar(s: string): string {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

/** Status em que a manifestação ainda está "viva" na ouvidoria (não final, não suspensa). */
const ATIVOS: OuvidoriaStatus[] = [
  'Registrada',
  'EmTriagem',
  'Encaminhada',
  'AguardandoComplementacao',
  'RespondidaPelaArea',
  'EmValidacao',
  'EmRecurso',
];

export const FINAIS: OuvidoriaStatus[] = ['Concluida', 'Arquivada', 'EncaminhadaOutroOrgao'];

export function ehStatusFinal(status: OuvidoriaStatus): boolean {
  return FINAIS.includes(status);
}

export type FlagsManifestacao = {
  /** `Ouvidoria.Edicao` */
  podeEditar: boolean;
  /** `Ouvidoria.Exclusao` */
  podeArquivar: boolean;
  /** `OuvidoriaGestao.Edicao` */
  podeGestao: boolean;
  /** `OuvidoriaSigilo.Inclusao` */
  podeHabilitar: boolean;
  /** `OuvidoriaSigilo.Edicao` */
  podeEditarSigilo: boolean;
  /** `OuvidoriaPontoResposta.Edicao` */
  podeResponderPonto: boolean;
  identificacao: OuvidoriaIdentificacao;
  complementacaoUsada: boolean;
  prorrogadoEm: string | null;
  habilitadaEm: string | null;
  /** Já houve recurso (evento `Recurso`)? Só pode uma vez. */
  recursoUsado: boolean;
};

/** Regra LOCAL: que botões cabem neste status + tipo + perfil (plano §2). */
function acoesLocais(status: OuvidoriaStatus, tipo: OuvidoriaTipo, f: FlagsManifestacao): AcaoManifestacao[] {
  const acoes: AcaoManifestacao[] = [];
  const ativo = ATIVOS.includes(status);
  const naoFinal = !ehStatusFinal(status);
  const em = (...s: OuvidoriaStatus[]) => s.includes(status);

  if (f.podeEditar) {
    if (em('Registrada', 'EmTriagem')) acoes.push('triar');
    if (em('Registrada', 'EmTriagem', 'RespondidaPelaArea', 'EmValidacao', 'EmRecurso')) acoes.push('encaminhar');
    if (ativo && status !== 'AguardandoComplementacao' && !f.complementacaoUsada && f.identificacao !== 'Anonima')
      acoes.push('pedirComplementacao');
    if (em('AguardandoComplementacao')) acoes.push('complementar');
    if (em('Encaminhada')) acoes.push('responderArea');
    if (em('RespondidaPelaArea', 'EmValidacao')) acoes.push('devolverArea');
    if (em('EmTriagem', 'Encaminhada', 'RespondidaPelaArea', 'EmValidacao', 'EmRecurso')) acoes.push('responderCidadao');
    if (ativo && !f.prorrogadoEm) acoes.push('prorrogar');
    if (em('Encaminhada')) acoes.push('cobrar');
    if (em('Respondida') && !f.recursoUsado) acoes.push('recurso');
    if (em('Respondida')) acoes.push('concluir');
    if (naoFinal) acoes.push('encaminharExterno');
    acoes.push('anotar');
  } else if (f.podeResponderPonto && em('Encaminhada')) {
    acoes.push('responderArea');
  }

  if (f.podeGestao && ativo) acoes.push('escalonar');
  if (f.podeArquivar && naoFinal) acoes.push('arquivar');

  if (tipo === 'Denuncia') {
    if (f.podeHabilitar && !f.habilitadaEm && naoFinal) acoes.push('habilitar');
    if (f.podeEditarSigilo && naoFinal) acoes.push('editarTeorPseudonimizado');
  }

  return acoes;
}

/**
 * Ações a exibir. Quando o detalhe traz `acoesPermitidas` reconhecíveis, ela manda (é o backend que
 * sabe o status + perfil de verdade) — cruzada com as permissões locais para não mostrar botão que
 * daria 403. Sem lista (ou com nomes que não reconhecemos), vale a regra local.
 */
export function acoesPorStatus(
  status: OuvidoriaStatus,
  tipo: OuvidoriaTipo,
  flags: FlagsManifestacao,
  acoesPermitidas?: readonly string[] | null,
): AcaoManifestacao[] {
  const locais = acoesLocais(status, tipo, flags);
  if (!acoesPermitidas || acoesPermitidas.length === 0) return locais;

  const vindas = new Set(acoesPermitidas.map(normalizar));
  const reconhecidas = (Object.keys(APELIDOS_BACKEND) as AcaoManifestacao[]).filter((a) =>
    APELIDOS_BACKEND[a].some((apelido) => vindas.has(apelido)),
  );
  if (reconhecidas.length === 0) return locais;

  // Interseção: o backend diz o que o status permite; as flags dizem o que ESTE usuário pode.
  const permitidoPeloPerfil = new Set<AcaoManifestacao>(
    acoesLocaisSemStatus(tipo, flags),
  );
  return reconhecidas.filter((a) => permitidoPeloPerfil.has(a));
}

/** Só o eixo de permissão (ignora status) — para cruzar com a lista do backend. */
function acoesLocaisSemStatus(tipo: OuvidoriaTipo, f: FlagsManifestacao): AcaoManifestacao[] {
  const acoes: AcaoManifestacao[] = [];
  if (f.podeEditar)
    acoes.push(
      'triar',
      'encaminhar',
      'pedirComplementacao',
      'complementar',
      'responderArea',
      'devolverArea',
      'responderCidadao',
      'prorrogar',
      'cobrar',
      'recurso',
      'concluir',
      'encaminharExterno',
      'anotar',
    );
  if (f.podeResponderPonto) acoes.push('responderArea');
  if (f.podeGestao) acoes.push('escalonar');
  if (f.podeArquivar) acoes.push('arquivar');
  if (tipo === 'Denuncia') {
    if (f.podeHabilitar) acoes.push('habilitar');
    if (f.podeEditarSigilo) acoes.push('editarTeorPseudonimizado');
  }
  return acoes;
}
