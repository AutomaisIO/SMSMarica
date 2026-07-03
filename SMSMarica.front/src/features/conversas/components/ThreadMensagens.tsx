import { useEffect, useRef } from 'react';
import { useConversa, useMarcarLida, useMensagens } from '@/features/conversas/api/queries';
import { ComposerMensagem } from '@/features/conversas/components/ComposerMensagem';
import type { Mensagem } from '@/features/conversas/types';

function hora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  });
}

function Bolha({ m }: { m: Mensagem }) {
  const saida = m.direcao === 'Saida';
  const nota = m.tipoMensagem === 'NotaInterna';
  return (
    <div className={`flex ${saida ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[80%] rounded-2xl px-3 py-2 text-sm shadow-sm ${
          nota
            ? 'bg-amber-50 text-amber-900'
            : saida
              ? 'bg-primary-600 text-white'
              : 'bg-white text-gray-800 ring-1 ring-gray-200'
        }`}
      >
        {saida && m.autorNomeExibicao && (
          <p className="mb-0.5 text-[11px] font-semibold opacity-80">{m.autorNomeExibicao}</p>
        )}
        {m.template && !m.conteudo && <p className="italic opacity-90">[modelo: {m.template}]</p>}
        {m.conteudo && <p className="whitespace-pre-wrap break-words">{m.conteudo}</p>}
        <p className={`mt-1 text-[10px] ${saida && !nota ? 'text-white/70' : 'text-gray-400'}`}>{hora(m.ocorridoEm)}</p>
      </div>
    </div>
  );
}

export function ThreadMensagens({ conversaId }: { conversaId: string }) {
  const { data: conversa } = useConversa(conversaId);
  const { data: mensagens, isLoading } = useMensagens(conversaId);
  const marcarLida = useMarcarLida();
  const fimRef = useRef<HTMLDivElement | null>(null);

  // Zera não-lidas ao abrir (e quando muda a conversa).
  useEffect(() => {
    if (conversa && conversa.naoLidas > 0) {
      marcarLida.mutate(conversaId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversaId, conversa?.naoLidas]);

  useEffect(() => {
    fimRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [mensagens]);

  return (
    <div className="flex h-full flex-col bg-gray-50">
      <div className="border-b border-gray-200 bg-white px-4 py-2.5">
        <p className="text-sm font-semibold text-gray-900">
          {conversa?.nomeContato || conversa?.telefoneCanonical || 'Conversa'}
        </p>
        <p className="text-xs text-gray-500">
          {conversa?.telefoneCanonical}
          {conversa?.operadorResponsavelNome && ` · Atendendo: ${conversa.operadorResponsavelNome}`}
          {conversa?.unidadeNome && ` · ${conversa.unidadeNome}`}
        </p>
      </div>

      <div className="flex-1 space-y-2 overflow-y-auto p-3">
        {isLoading && <p className="text-sm text-gray-500">Carregando mensagens…</p>}
        {mensagens?.map((m) => <Bolha key={m.id} m={m} />)}
        <div ref={fimRef} />
      </div>

      <ComposerMensagem conversaId={conversaId} podeTextoLivre={conversa?.podeTextoLivre ?? false} />
    </div>
  );
}
