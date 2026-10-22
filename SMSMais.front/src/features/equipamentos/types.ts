export type ModalidadeDicom = 'CR' | 'DX' | 'MG' | 'US' | 'CT' | 'MR' | 'NM' | 'PT' | 'ECG' | 'OT';

export const MODALIDADES: { id: ModalidadeDicom; rotulo: string }[] = [
  { id: 'US', rotulo: 'US — Ultrassom' },
  { id: 'MG', rotulo: 'MG — Mamografia' },
  { id: 'CR', rotulo: 'CR — Radiografia (placa)' },
  { id: 'DX', rotulo: 'DX — Radiografia digital' },
  { id: 'CT', rotulo: 'CT — Tomografia' },
  { id: 'MR', rotulo: 'MR — Ressonância magnética' },
  { id: 'NM', rotulo: 'NM — Medicina nuclear' },
  { id: 'PT', rotulo: 'PT — PET' },
  { id: 'ECG', rotulo: 'ECG — Cardiologia (Holter)' },
  { id: 'OT', rotulo: 'OT — Outro' },
];

export function rotuloModalidade(m: ModalidadeDicom): string {
  return MODALIDADES.find((x) => x.id === m)?.rotulo ?? m;
}

export type Equipamento = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string | null;
  descricaoMaxCaracteres: number;
  ativo: boolean;
  criadoEm: string;
} & CodigoProcedimentoConfig &
  CamposWorklistConfig &
  RegraDoExameConfig;

export type EquipamentoListItem = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string | null;
  descricaoMaxCaracteres: number;
  ativo: boolean;
} & CodigoProcedimentoConfig &
  CamposWorklistConfig &
  RegraDoExameConfig;

/**
 * Código do procedimento no item de worklist (ADR-0074): o código interno do SISREG vai em
 * (0008,0100), dentro da sequência que o aparelho lê. Desligado por padrão — liga-se aparelho a
 * aparelho, quando ele está cadastrado com esses códigos (ex.: o raio-X do CDT).
 */
export type CodigoProcedimentoConfig = {
  /** Requested Procedure Code Sequence (0032,1064). */
  codigoNoProcedimentoSolicitado: boolean;
  /** Scheduled Protocol Code Sequence (0040,0008), dentro do passo agendado. */
  codigoNoProtocoloAgendado: boolean;
  /** Esquema em (0008,0102). Vazio = o padrão, 99SMS. */
  codigoProcedimentoEsquema: string | null;
};

export type SalvarEquipamentoPayload = {
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom?: string | null;
  descricaoMaxCaracteres?: number;
  ativo?: boolean;
} & Partial<CodigoProcedimentoConfig> & {
    /** Vazio ou 0008,0100 = o padrão (na sequência); outra tag = o código vai como texto nela. */
    codigoProcedimentoTag?: string | null;
    enviarMedicoSolicitante?: boolean;
    medicoSolicitanteTag?: string | null;
    enviarUnidade?: boolean;
    unidadeOrigem?: OrigemUnidadeWorklist;
    unidadeTag?: string | null;
  } & Partial<RegraDoExameConfig>;

/** Onde nasce o laudo dos exames do aparelho (ADR-0075). */
export type OrigemLaudo = 'Plataforma' | 'JuntoComExame' | 'ForaDaPlataforma';

/** Quando o exame do aparelho chega ao paciente — app do cidadão e WhatsApp (ADR-0075). */
export type LiberacaoExamePaciente = 'ComLaudoAssinado' | 'AoChegar';

/**
 * O que acontece com o exame depois que chega (ADR-0075). É do aparelho, não do tipo de exame: quem
 * decide se a imagem pode ir antes do laudo é quem lauda.
 */
export type RegraDoExameConfig = {
  laudo: OrigemLaudo;
  liberacaoPaciente: LiberacaoExamePaciente;
  usaAnamnese: boolean;
};

export const ORIGENS_LAUDO: { id: OrigemLaudo; rotulo: string; dica: string }[] = [
  {
    id: 'Plataforma',
    rotulo: 'Laudado na plataforma',
    dica: 'O médico lauda e assina aqui (mamografia, densitometria, raio-X). O exame entra na fila de laudo.',
  },
  {
    id: 'JuntoComExame',
    rotulo: 'Laudo vem junto com o exame',
    dica: 'O exame já chega com o laudo pronto, assinado fora, no mesmo PDF (MAPA, Holter). Sem laudo nem assinatura nossa.',
  },
  {
    id: 'ForaDaPlataforma',
    rotulo: 'Laudado fora da plataforma',
    dica: 'O laudo é feito em outro sistema e não passa por aqui (ultrassom). O exame não entra na fila de laudo.',
  },
];

export const LIBERACOES_PACIENTE: { id: LiberacaoExamePaciente; rotulo: string; dica: string }[] = [
  {
    id: 'ComLaudoAssinado',
    rotulo: 'Só com o laudo assinado',
    dica: 'A imagem fica retida e chega ao paciente junto com o laudo, numa mensagem só ("Laudo pronto"). Para quando a médica revê a imagem depois e ela ainda pode mudar.',
  },
  {
    id: 'AoChegar',
    rotulo: 'Assim que chegar',
    dica: 'A imagem aparece no app do cidadão e sai o aviso "Exame liberado" assim que o estudo chega ao PACS.',
  },
];

/**
 * A regra que um aparelho novo recebe pela modalidade, a mesma da implantação de 20/10/2026:
 * ultrassom é laudado fora e libera ao chegar; o resto é laudado aqui e retém até o laudo.
 */
export function regraPadraoDaModalidade(m: ModalidadeDicom): RegraDoExameConfig {
  return m === 'US'
    ? { laudo: 'ForaDaPlataforma', liberacaoPaciente: 'AoChegar', usaAnamnese: true }
    : { laudo: 'Plataforma', liberacaoPaciente: 'ComLaudoAssinado', usaAnamnese: true };
}

/** De qual unidade do pedido sai o nome que vai no campo "unidade/clínica" do aparelho. */
export type OrigemUnidadeWorklist = 'Executante' | 'Solicitante';

export const ORIGENS_UNIDADE: { id: OrigemUnidadeWorklist; rotulo: string }[] = [
  { id: 'Executante', rotulo: 'Unidade executante (onde o exame é feito)' },
  { id: 'Solicitante', rotulo: 'Unidade solicitante (quem pediu)' },
];

/**
 * Campos opcionais do item de worklist, ligados aparelho a aparelho (ADR-0074, adendo de
 * 16/10/2026). Tag no formato "0008,0090". Cada aparelho lê o seu campo — quem descobre qual é o
 * técnico, testando —, por isso a tag é editável. Tudo nasce desligado.
 */
export type CamposWorklistConfig = {
  /** Null = o padrão: (0008,0100) dentro da sequência. */
  codigoProcedimentoTag: string | null;
  /** Null = não envia. */
  medicoSolicitanteTag: string | null;
  /** Null = não envia. */
  unidadeTag: string | null;
  unidadeOrigem: OrigemUnidadeWorklist;
};

/** (0008,0100) Code Value — o código dentro da sequência de código, como sempre foi. */
export const TAG_CODIGO_PADRAO = '0008,0100';
/** (0008,0090) Referring Physician's Name — o campo de médico que o CardioNet pede. */
export const TAG_MEDICO_PADRAO = '0008,0090';
/** (0008,0080) Institution Name para a executante; (0032,1033) Requesting Service para a solicitante. */
export const TAG_UNIDADE_PADRAO: Record<OrigemUnidadeWorklist, string> = {
  Executante: '0008,0080',
  Solicitante: '0032,1033',
};

/** "(0008,0090)", "0008 0090" ou "00080090" → "00080090"; null quando fora do formato. */
export function normalizarTag(tag: string): string | null {
  const hex = tag.replace(/[(), ]/g, '').toUpperCase();
  return /^[0-9A-F]{8}$/.test(hex) ? hex : null;
}

/** A tag do código é o padrão (0008,0100, ou vazia)? Então ele vai na sequência. */
export function tagCodigoEhPadrao(tag: string): boolean {
  return !tag.trim() || normalizarTag(tag) === normalizarTag(TAG_CODIGO_PADRAO);
}

/** Esquema do código quando o campo fica vazio (esquema privado: começa com "99"). */
export const CODIGO_ESQUEMA_PADRAO = '99SMS';

/**
 * Teto do VR `LO` do DICOM — nenhum aparelho lê mais que isto, e é o padrão de todo
 * equipamento novo. Baixar só quando um console concreto falhar: o mamógrafo Fuji
 * FDR-3000AWS não monta a imagem (erro 31027) com descrição longa e fica em 16.
 */
export const DESCRICAO_MAX_PADRAO = 64;
export const DESCRICAO_MAX_MINIMO = 4;
