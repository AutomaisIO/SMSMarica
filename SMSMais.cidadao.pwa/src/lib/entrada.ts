import { codigosDoErro, extrairMensagemDeErro } from '@/lib/httpClient';

// Erros da entrada do cidadão (ADR-0078). O servidor já manda a mensagem para a pessoa; só dois
// códigos pedem outra coisa da tela.

/** O desafio da entrada venceu (vive 10 min no servidor): volta ao CPF com este aviso. */
export const AVISO_ENTRADA_EXPIRADA = 'O tempo para entrar acabou. Comece de novo.';

/** As rotas antigas de login respondem 410 `entrada.atualizar`: este app está desatualizado. */
const AVISO_ATUALIZAR = 'Atualize o aplicativo (ou recarregue a página) para entrar.';

export function entradaExpirou(erro: unknown): boolean {
  return codigosDoErro(erro).includes('entrada.expirada');
}

/** Mensagem para a tela: a do servidor (bloqueio, dados que não conferem, e-SUS fora do ar…). */
export function mensagemDaEntrada(erro: unknown): string {
  if (codigosDoErro(erro).includes('entrada.atualizar')) return AVISO_ATUALIZAR;
  return extrairMensagemDeErro(erro);
}
