import { useCallback, useEffect, useRef, useState } from 'react';

import { ehSiscanIndisponivel } from '@/features/anamnese/lib/sessaoSiscan';

/**
 * Re-tentativa automática para instabilidade do SISCAN (o 503 `siscan.indisponivel`), com contagem
 * visível entre as tentativas — é o "tentando de novo em 5…4…3…" que o operador vê quando o SISCAN
 * está lento/caindo no pico da manhã.
 *
 * <p><b>Só re-tenta a falha do SISCAN.</b> Credencial inválida, sessão perdida e qualquer erro
 * nosso NÃO são re-tentados: voltam na hora para o chamador tratar como sempre. Re-tentar senha
 * errada três vezes só faria o usuário esperar 10 s para ler o mesmo "senha incorreta".</p>
 *
 * <p><b>Use só em operação de LEITURA</b> (entrar/conferir). Escrita (gerar requisição) não entra
 * aqui: um reset pode ter gravado do lado do SISCAN, e re-tentar cego duplicaria a requisição.</p>
 */

export const SISCAN_MAX_TENTATIVAS = 3;
export const SISCAN_ESPERA_SEGUNDOS = 5;

export type EstadoRetrySiscan =
  | { fase: 'ocioso' }
  /** Chamando o SISCAN agora (spinner). */
  | { fase: 'tentando'; tentativa: number }
  /** Entre tentativas: contagem regressiva até a próxima. */
  | { fase: 'aguardando'; tentativa: number; proxima: number; segundos: number }
  /** As 3 tentativas falharam por instabilidade do SISCAN. */
  | { fase: 'esgotado' };

export type ResultadoRetry<T> =
  | { ok: true; valor: T }
  /** Falhou e NÃO era instabilidade do SISCAN (credencial, sessão, erro nosso) — trate normal. */
  | { ok: false; esgotado: false; cancelado: boolean; erro: unknown }
  /** Esgotou as 3 tentativas por instabilidade do SISCAN. */
  | { ok: false; esgotado: true; cancelado: false; erro: unknown };

export function useRetrySiscan() {
  const [estado, setEstado] = useState<EstadoRetrySiscan>({ fase: 'ocioso' });
  const cancelado = useRef(false);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const limparTimer = useCallback(() => {
    if (timer.current) {
      clearInterval(timer.current);
      timer.current = null;
    }
  }, []);

  // Desmontar no meio da contagem (fechar o modal) não pode deixar o timer rodando.
  useEffect(
    () => () => {
      cancelado.current = true;
      limparTimer();
    },
    [limparTimer],
  );

  const contar = useCallback(
    (tentativa: number) =>
      new Promise<void>((resolve) => {
        let s = SISCAN_ESPERA_SEGUNDOS;
        setEstado({ fase: 'aguardando', tentativa, proxima: tentativa + 1, segundos: s });
        timer.current = setInterval(() => {
          if (cancelado.current) {
            limparTimer();
            resolve();
            return;
          }
          s -= 1;
          if (s <= 0) {
            limparTimer();
            resolve();
          } else {
            setEstado({ fase: 'aguardando', tentativa, proxima: tentativa + 1, segundos: s });
          }
        }, 1000);
      }),
    [limparTimer],
  );

  const executar = useCallback(
    async <T>(acao: () => Promise<T>): Promise<ResultadoRetry<T>> => {
      cancelado.current = false;
      for (let t = 1; t <= SISCAN_MAX_TENTATIVAS; t++) {
        if (cancelado.current) return { ok: false, esgotado: false, cancelado: true, erro: null };
        setEstado({ fase: 'tentando', tentativa: t });
        try {
          const valor = await acao();
          setEstado({ fase: 'ocioso' });
          return { ok: true, valor };
        } catch (erro) {
          if (!ehSiscanIndisponivel(erro)) {
            setEstado({ fase: 'ocioso' });
            return { ok: false, esgotado: false, cancelado: false, erro };
          }
          if (t === SISCAN_MAX_TENTATIVAS) {
            setEstado({ fase: 'esgotado' });
            return { ok: false, esgotado: true, cancelado: false, erro };
          }
          await contar(t);
          if (cancelado.current) return { ok: false, esgotado: false, cancelado: true, erro: null };
        }
      }
      return { ok: false, esgotado: true, cancelado: false, erro: null };
    },
    [contar],
  );

  const cancelar = useCallback(() => {
    cancelado.current = true;
    limparTimer();
    setEstado({ fase: 'ocioso' });
  }, [limparTimer]);

  const resetar = useCallback(() => {
    cancelado.current = false;
    limparTimer();
    setEstado({ fase: 'ocioso' });
  }, [limparTimer]);

  return { estado, executar, cancelar, resetar };
}
