import { useState } from 'react';
import { Lock, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { formatarInstante } from '@/shared/lib/datas';
import { useComentar } from '@/features/tickets/api/queries';
import { AnexosGaleria, AnexosInput } from '@/features/tickets/components/AnexosInput';
import type { AnexoRef, TicketComentario } from '@/features/tickets/types';

type Props = {
  ticketId: string;
  gestao: boolean;
  comentarios: TicketComentario[];
};

export function ConversaTicket({ ticketId, gestao, comentarios }: Props) {
  const [texto, setTexto] = useState('');
  const [interno, setInterno] = useState(false);
  const [anexos, setAnexos] = useState<AnexoRef[]>([]);
  const comentar = useComentar(ticketId, gestao);

  async function enviar() {
    if (!texto.trim()) return;
    try {
      await comentar.mutateAsync({ texto: texto.trim(), interno, anexos });
      setTexto('');
      setInterno(false);
      setAnexos([]);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <div className="space-y-4">
      <h3 className="text-sm font-semibold text-slate-700">Conversa</h3>

      {comentarios.length === 0 ? (
        <p className="text-sm text-slate-400">Nenhuma resposta ainda.</p>
      ) : (
        <ul className="space-y-3">
          {comentarios.map((c) => (
            <li
              key={c.id}
              className={`rounded-lg border p-3 ${c.interno ? 'border-amber-200 bg-amber-50' : 'border-slate-200 bg-white'}`}
            >
              <div className="mb-1 flex items-center gap-2 text-xs text-slate-500">
                <span className="font-medium text-slate-700">{c.autorNome ?? 'Usuário'}</span>
                <span>·</span>
                <span>{formatarInstante(c.criadoEm)}</span>
                {c.interno && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 font-medium text-amber-700">
                    <Lock className="h-3 w-3" /> Nota interna
                  </span>
                )}
              </div>
              <p className="whitespace-pre-wrap text-sm text-slate-800">{c.texto}</p>
              {c.anexos.length > 0 && (
                <div className="mt-2">
                  <AnexosGaleria anexos={c.anexos} />
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      <div className="space-y-2 rounded-lg border border-slate-200 p-3">
        <textarea
          value={texto}
          rows={3}
          maxLength={5000}
          placeholder={gestao ? 'Responder ao usuário…' : 'Escreva uma resposta…'}
          onChange={(e) => setTexto(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-red-500 focus:outline-none focus:ring-1 focus:ring-red-500"
        />
        <AnexosInput anexos={anexos} aoMudar={setAnexos} disabled={comentar.isPending} />
        <div className="flex items-center justify-between">
          {gestao ? (
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={interno} onChange={(e) => setInterno(e.target.checked)} />
              Nota interna (não visível ao autor)
            </label>
          ) : (
            <span />
          )}
          <Button onClick={enviar} disabled={comentar.isPending || !texto.trim()} tamanho="sm">
            <Send className="h-4 w-4" />
            {comentar.isPending ? 'Enviando…' : 'Enviar'}
          </Button>
        </div>
      </div>
    </div>
  );
}
