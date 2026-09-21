import { useCallback, useRef, useState } from 'react';
import { AxiosError } from 'axios';

import { useSessaoSisreg } from '@/features/confirmacoes/api';
import type { ProblemaApi } from '@/shared/api/httpClient';

/** Código que o backend devolve quando não há sessão de escrita no SISREG. */
export const SEM_SESSAO_SISREG = 'sisreg.sessao_operador_ausente';

/**
 * O código de validação vem como CHAVE em `errors` — é assim que `ValidacaoException` vira
 * `ValidationProblemDetails`.
 */
function temCodigo(erro: unknown, codigo: string): boolean {
  if (!(erro instanceof AxiosError)) return false;
  const dados = erro.response?.data as ProblemaApi | undefined;
  return Boolean(dados?.errors && codigo in dados.errors);
}

/**
 * Garante a sessão de escrita no SISREG antes de cancelar — pedindo a senha, nunca mostrando erro.
 *
 * <p><b>Por que existe:</b> a recusa do backend ("é preciso entrar com o SEU usuário do SISREG…")
 * é um pedido de credencial, não um erro de negócio. Renderizá-la em vermelho faria a atendente
 * ler uma explicação de arquitetura em vez de simplesmente entrar.</p>
 *
 * <p>Cobre os dois caminhos: <b>antes</b> de agir (não há sessão) e <b>durante</b> (caiu no meio —
 * expirou, ou a API reiniciou). Nos dois a ação pendente é retomada sozinha depois do login, para
 * a atendente não redigitar o motivo que já tinha escrito.</p>
 */
export function useSessaoSisregObrigatoria() {
  const { data: sessao } = useSessaoSisreg();
  const [pedindoSenha, setPedindoSenha] = useState(false);
  // Ref, não estado: a ação é retomada dentro do callback do modal, e um `useState` com função
  // exigiria o embrulho `() => fn` toda vez — fácil de errar e difícil de ver quando erra.
  const pendente = useRef<(() => void | Promise<void>) | null>(null);

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
    if (!temCodigo(erro, SEM_SESSAO_SISREG)) return false;
    pendente.current = repetir;
    setPedindoSenha(true);
    return true;
  }, []);

  const aoFechar = useCallback(() => {
    // Desistiu de entrar: a ação pendente morre com o modal, senão dispararia sozinha na próxima
    // autenticação, fora de contexto.
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
    usuarioSisreg: sessao?.usuarioSisreg ?? null,
    comSessao,
    tratouFaltaDeSessao,
    /** Props prontas para o `<ModalLoginSisreg />`. */
    modal: { aberto: pedindoSenha, aoFechar, aoAutenticar },
  };
}
