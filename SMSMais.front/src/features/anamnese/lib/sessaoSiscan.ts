import { AxiosError } from 'axios';

import type { ProblemaApi } from '@/shared/api/httpClient';

/**
 * Códigos com que o backend diz "não há mais sessão do SISCAN para este operador" — a senha
 * não está mais em memória (API reiniciou, 8 h de inatividade) ou o SISCAN a recusou ao
 * relogar. Em qualquer um deles a saída é a mesma: pedir o login de novo e retomar (#137).
 *
 * `siscan.sessao_expirou` NÃO está aqui: esse é a sessão do SISCAN que caiu por ociosidade, e a
 * reconexão é automática — basta tentar de novo.
 */
const CODIGOS_SEM_SESSAO = [
  'siscan.sessao_operador_ausente',
  'siscan.sem_credencial',
  'siscan.credencial_invalida',
];

/** O código de validação vem como CHAVE em `errors` (`ValidacaoException` → ProblemDetails). */
function codigosDoErro(erro: unknown): string[] {
  if (!(erro instanceof AxiosError)) return [];
  const dados = erro.response?.data as ProblemaApi | undefined;
  return dados?.errors ? Object.keys(dados.errors) : [];
}

/** A falha pede um novo login no SISCAN (e não uma nova tentativa)? */
export function perdeuSessaoSiscan(erro: unknown): boolean {
  return codigosDoErro(erro).some((c) => CODIGOS_SEM_SESSAO.includes(c));
}
