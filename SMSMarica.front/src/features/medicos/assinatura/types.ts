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
