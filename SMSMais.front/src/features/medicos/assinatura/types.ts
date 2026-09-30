/** Proporção/frame da rubrica — define o enquadramento e orienta o carimbo no PDF. */
export type FormatoAssinaturaMedico = 'Quadrada' | 'Horizontal';

export const FORMATOS_ASSINATURA: {
  id: FormatoAssinaturaMedico;
  rotulo: string;
  descricao: string;
  aspecto: number;
  largura: number;
  altura: number;
}[] = [
  {
    id: 'Horizontal',
    rotulo: 'Faixa (2:1)',
    descricao: 'Assinatura "deitada" — 800×400',
    aspecto: 2,
    largura: 800,
    altura: 400,
  },
  {
    id: 'Quadrada',
    rotulo: 'Quadrada (1:1)',
    descricao: 'Assinatura compacta — 800×800',
    aspecto: 1,
    largura: 800,
    altura: 800,
  },
];

export type AssinaturaMedico = {
  medicoId: string;
  imagemBase64: string;
  contentType: string;
  formato: FormatoAssinaturaMedico;
  atualizadoEm: string;
};

export type SalvarAssinaturaMedicoPayload = {
  imagemBase64: string;
  contentType: string;
  formato: FormatoAssinaturaMedico;
};

/** Como o médico oficializa o laudo (ADR-0061). */
export type ModoAssinaturaMedico = 'Desktop' | 'Nuvem' | 'SemCertificado';

export type ModoAssinaturaMedicoDto = {
  medicoId: string;
  modo: ModoAssinaturaMedico;
  /** false = ninguém escolheu ainda e vale o padrão (SemCertificado, "login e senha"). */
  configurado: boolean;
  atualizadoEm: string | null;
  /** Modo Nuvem: o que fazer com a autorização do VIDaaS (ADR-0061 §2.1). */
  sessaoNuvem: PreferenciaSessaoNuvem;
};

/** Modo Nuvem: manter a autorização do VIDaaS até sair, perguntar ou aprovar cada laudo. */
export type PreferenciaSessaoNuvem = 'Perguntar' | 'Manter' | 'CadaLaudo';

export const PREFERENCIAS_SESSAO_NUVEM: {
  id: PreferenciaSessaoNuvem;
  rotulo: string;
  descricao: string;
}[] = [
  {
    id: 'Perguntar',
    rotulo: 'Perguntar ao assinar',
    descricao: 'O padrão. No primeiro laudo de cada acesso, o médico escolhe.',
  },
  {
    id: 'Manter',
    rotulo: 'Manter até sair',
    descricao: 'Aprova no app uma vez e assina os demais laudos sem o celular, até sair do sistema.',
  },
  {
    id: 'CadaLaudo',
    rotulo: 'Aprovar cada laudo',
    descricao: 'Cada assinatura pede aprovação no app.',
  },
];

export const MODOS_ASSINATURA: {
  id: ModoAssinaturaMedico;
  rotulo: string;
  descricao: string;
}[] = [
  {
    id: 'Desktop',
    rotulo: 'Assinador no computador',
    descricao:
      'Certificado ICP-Brasil instalado na máquina (VIDaaS Connect, token ou A1). Precisa do Automais Assinador instalado.',
  },
  {
    id: 'Nuvem',
    rotulo: 'VIDaaS em nuvem',
    descricao:
      'Certificado VIDaaS em nuvem. O médico aprova a assinatura no aplicativo do celular; nada instalado no computador.',
  },
  {
    id: 'SemCertificado',
    rotulo: 'Login e senha (sem certificado)',
    descricao:
      'O padrão. O médico assina com o próprio acesso ao sistema; o laudo sai com a rubrica e o QR de verificação, mas SEM certificado ICP-Brasil. O próprio PDF declara isso.',
  },
];
