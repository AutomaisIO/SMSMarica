import { useEffect } from 'react';
import { useResumoConversas } from '@/features/conversas/api/queries';
import { useChat } from '@/features/conversas/store/chatStore';

/**
 * Alimenta o total de não-lidas (sino do Header, badge do ChatWidget, título da aba) a partir
 * do endpoint leve /conversas/resumo — minhas + fila. Antes o total era somado da lista
 * carregada, e só dentro da janela solta do chat: na janela principal o sino ficava sempre em
 * zero (cada janela tem seu próprio store). Montar em CADA janela que exibe o contador.
 */
export function useTotalNaoLidas(habilitado: boolean) {
  const { data } = useResumoConversas(habilitado);
  const setTotalNaoLidas = useChat((s) => s.setTotalNaoLidas);

  useEffect(() => {
    if (!data) return;
    setTotalNaoLidas(data.minhasNaoLidas + data.filaNaoLidas);
  }, [data, setTotalNaoLidas]);
}
