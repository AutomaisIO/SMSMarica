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

/**
 * O SISCAN recusou por um dado da paciente que vem do CADSUS (nome da mãe, endereço…) e que o
 * painel ainda não preenche — a tela oferece o link do CADSUSWEB. (Raça/Cor tem combo próprio.)
 */
export function recusaPorCadastroCadsus(erro: unknown): boolean {
  return codigosDoErro(erro).includes('siscan.cadastro_cadsus_incompleto');
}

/** A falha pede um novo login no SISCAN (e não uma nova tentativa)? */
export function perdeuSessaoSiscan(erro: unknown): boolean {
  return codigosDoErro(erro).some((c) => CODIGOS_SEM_SESSAO.includes(c));
}

/**
 * Instabilidade do PRÓPRIO SISCAN (DATASUS): timeout, conexão resetada, rede ou HTTP 5xx — o
 * backend devolve 503 com `type: "siscan.indisponivel"` (ver `SiscanIndisponivelException`). Não é
 * erro nosso nem credencial inválida: é "o SISCAN está lento/caiu", comum no pico da manhã, e a
 * saída é tentar de novo. É esta a falha que a tela re-tenta sozinha com contagem.
 */
export function ehSiscanIndisponivel(erro: unknown): boolean {
  if (!(erro instanceof AxiosError)) return false;
  const dados = erro.response?.data as ProblemaApi | undefined;
  return erro.response?.status === 503 && dados?.type === 'siscan.indisponivel';
}
