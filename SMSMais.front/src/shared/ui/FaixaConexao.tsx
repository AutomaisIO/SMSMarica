import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { RefreshCw, WifiOff } from 'lucide-react';
import { sondarAgora, useConexao, type CausaQueda } from '@/shared/api/conexao';

const TEXTO: Record<CausaQueda, string> = {
  internet: 'Sem internet neste computador. As telas voltam a se atualizar sozinhas quando a conexão voltar.',
  reiniciando: 'O sistema está sendo atualizado. Aguarde alguns segundos — as telas voltam sozinhas.',
  servidor: 'Sem contato com o servidor. Reconectando…',
};

/**
 * Faixa fixa no topo enquanto o servidor está fora há mais de 15s (queda CONFIRMADA pelo
 * /health — ver conexao.ts). Substitui a enxurrada de toasts: aparece uma vez, some sozinha na
 * volta e, quando volta, refaz as consultas da tela. Montar uma única vez na raiz da aplicação,
 * dentro do QueryProvider.
 */
export function FaixaConexao() {
  const situacao = useConexao((s) => s.situacao);
  const causa = useConexao((s) => s.causa);
  const retornos = useConexao((s) => s.retornos);
  const queryClient = useQueryClient();

  // Na volta, tudo o que falhou durante a queda é buscado de novo — inclusive quando a queda foi
  // curta e a faixa nem chegou a aparecer. Sem isto a tela esperava o próximo poll (ou um F5).
  const retornosVistos = useRef(retornos);
  useEffect(() => {
    if (retornos === retornosVistos.current) return;
    retornosVistos.current = retornos;
    void queryClient.invalidateQueries();
  }, [retornos, queryClient]);

  if (situacao !== 'fora') return null;

  return (
    <div className="pointer-events-none fixed inset-x-0 top-2 z-[60] flex justify-center px-4">
      <div
        role="status"
        aria-live="polite"
        className="pointer-events-auto flex max-w-xl items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 shadow-md"
      >
        <WifiOff className="h-4 w-4 shrink-0 text-amber-600" />
        <span className="flex-1">{TEXTO[causa ?? 'servidor']}</span>
        <button
          type="button"
          onClick={sondarAgora}
          className="flex shrink-0 items-center gap-1 rounded px-2 py-0.5 text-xs font-medium text-amber-800 hover:bg-amber-100"
        >
          <RefreshCw className="h-3.5 w-3.5" />
          Tentar agora
        </button>
      </div>
    </div>
  );
}
