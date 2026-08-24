import { useCallback, useRef, useState } from 'react';
import { AxiosError } from 'axios';

import { useSessaoOperadorSer } from '@/features/ser/api/queries';
import { useAuth } from '@/shared/auth/authStore';
import type { ProblemaApi } from '@/shared/api/httpClient';

/** Código que o backend devolve quando não há sessão de escrita no SER. */
export const SEM_SESSAO_SER = 'ser.sessao_operador_ausente';

/**
 * O código de validação vem como CHAVE em `errors` — é assim que `ValidacaoException` vira
 * `ValidationProblemDetails`.
 */
export function temCodigo(erro: unknown, codigo: string): boolean {
  if (!(erro instanceof AxiosError)) return false;
  const dados = erro.response?.data as ProblemaApi | undefined;
  return Boolean(dados?.errors && codigo in dados.errors);
}

/**
 * Primeiro e último nome — "BERNARDO DOS SANTOS LEITE ALMEIDA" vira "Bernardo Almeida".
 *
 * Preposições não entram no sobrenome ("Maria de Souza" → "Maria Souza"), senão o último token
 * útil se perderia em nomes que terminam em partícula.
 */
export function primeiroEUltimoNome(completo: string | null | undefined): string | null {
  const partes = (completo ?? '').trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return null;

  const capitalizar = (t: string) =>
    t.charAt(0).toLocaleUpperCase('pt-BR') + t.slice(1).toLocaleLowerCase('pt-BR');

  const particulas = new Set(['de', 'da', 'do', 'das', 'dos', 'e']);
  const uteis = partes.filter((t) => !particulas.has(t.toLocaleLowerCase('pt-BR')));
  const lista = uteis.length > 0 ? uteis : partes;
  if (lista.length === 1) return capitalizar(lista[0]);
  return `${capitalizar(lista[0])} ${capitalizar(lista[lista.length - 1])}`;
}

/**
 * Garante a sessão de escrita no SER antes de uma ação — pedindo a senha, nunca mostrando erro.
 *
 * <p><b>Por que existe:</b> a recusa do backend ("é preciso entrar com o SEU usuário do SER…")
 * é um pedido de credencial, não um erro de negócio. Renderizá-la como erro vermelho fazia o
 * operador ler uma explicação de arquitetura em vez de simplesmente entrar. Aqui ela nunca chega
 * à tela: vira o modal de autenticação.</p>
 *
 * <p>Cobre os dois caminhos: <b>antes</b> de agir (não há sessão) e <b>durante</b> (a sessão
 * caiu no meio — expirou, ou a API reiniciou). Nos dois casos a ação pendente é retomada sozinha
 * depois do login, para o operador não redigitar o que já tinha escrito.</p>
 */
export function useSessaoSerObrigatoria() {
  const { data: sessao } = useSessaoOperadorSer();
  // Quem opera é o usuário do SMSMarica; o login do SER é credencial, não identificação de
  // pessoa — mostrar "56840827" não diz a ninguém quem está assinando.
  const nomeDoOperador = useAuth((e) => primeiroEUltimoNome(e.usuario?.nome));
  const [pedindoSenha, setPedindoSenha] = useState(false);
  // Ref, não estado: a ação é retomada dentro do callback do modal, e um `useState` com função
  // exigiria o embrulho `() => fn` toda vez — fácil de errar e difícil de ver quando erra.
  const pendente = useRef<(() => void | Promise<void>) | null>(null);

  /** Roda a ação, ou pede a senha e a roda logo depois de autenticar. */
  const comSessao = useCallback(
    (acao: () => void | Promise<void>) => {
      if (sessao?.autenticado) {
        void acao();
        return;
      }
      pendente.current = acao;
      setPedindoSenha(true);
    },
    [sessao?.autenticado],
  );

  /**
   * Trata um erro de ação: se for falta de sessão, pede a senha e agenda a repetição.
   * Devolve `true` quando cuidou do erro — o chamador então NÃO deve mostrar mensagem nenhuma.
   */
  const tratouFaltaDeSessao = useCallback((erro: unknown, repetir: () => void | Promise<void>) => {
    if (!temCodigo(erro, SEM_SESSAO_SER)) return false;
    pendente.current = repetir;
    setPedindoSenha(true);
    return true;
  }, []);

  const aoFechar = useCallback(() => {
    // Desistiu de entrar: a ação pendente morre com o modal, senão ela dispararia sozinha na
    // próxima autenticação, fora de contexto.
    pendente.current = null;
    setPedindoSenha(false);
  }, []);

  const aoAutenticar = useCallback(() => {
    const acao = pendente.current;
    pendente.current = null;
    setPedindoSenha(false);
    if (acao) void acao();
  }, []);

  return {
    autenticado: Boolean(sessao?.autenticado),
    /** Nome de quem está assinando, para a tela. O login fica no `title`, para suporte. */
    operador: nomeDoOperador ?? sessao?.usuarioSer ?? null,
    usuarioSer: sessao?.usuarioSer ?? null,
    comSessao,
    tratouFaltaDeSessao,
    /** Props prontas para o `<ModalLoginSer />`. */
    modal: { aberto: pedindoSenha, aoFechar, aoAutenticar },
  };
}
