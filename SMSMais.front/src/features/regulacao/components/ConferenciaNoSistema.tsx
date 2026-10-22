import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Loader2, ScanSearch } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { cn } from '@/shared/lib/cn';

import { useConferencia, useConferirNoSistema } from '../api/solicitacoesQueries';
import type { ConferenciaExterna, ResultadoItemConferencia, SolicitacaoRegulacao } from '../tiposSolicitacao';
import { ROTULO_SISTEMA_REGULACAO } from '../types';

const MARCA: Record<ResultadoItemConferencia, { texto: string; classe: string }> = {
  Igual: { texto: 'confere', classe: 'text-emerald-700' },
  Diferente: { texto: 'diverge', classe: 'font-semibold text-red-700' },
  SoNoSistema: { texto: 'só lá', classe: 'text-slate-500' },
  Informativo: { texto: '', classe: 'text-slate-500' },
};

/**
 * O resultado de uma conferência, campo a campo: o que pedimos aqui × o que está lá.
 *
 * <p>É registro, não trava (pedido do Bernardo, 22/10/2026): o técnico lançou à mão, a plataforma
 * mostra onde o lançamento ficou diferente do pedido — "e vida que segue". Divergência em vermelho
 * para saltar aos olhos; o resto em tom neutro.</p>
 */
export function ResultadoConferencia({ c }: { c: ConferenciaExterna }) {
  const sistema = ROTULO_SISTEMA_REGULACAO[c.sistema] ?? c.sistema;

  if (c.erro) {
    return (
      <p className="flex gap-2 rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        <AlertTriangle className="mt-0.5 size-4 shrink-0" />
        <span>{c.erro}</span>
      </p>
    );
  }

  if (!c.encontrado) {
    return (
      <p className="flex gap-2 rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        <AlertTriangle className="mt-0.5 size-4 shrink-0" />
        <span>
          O {sistema} não achou o número <strong>{c.numero}</strong>. Confira se foi digitado certo — o número
          continua registrado assim.
        </span>
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {c.divergencias === 0 ? (
        <p className="flex items-center gap-1.5 text-sm text-emerald-700">
          <CheckCircle2 className="size-4" /> O pedido no {sistema} confere com o que foi pedido aqui.
        </p>
      ) : (
        <p className="flex items-center gap-1.5 text-sm font-medium text-red-800">
          <AlertTriangle className="size-4" />
          {c.divergencias} divergência{c.divergencias > 1 ? 's' : ''} entre o pedido aqui e o {sistema}.
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-xs text-slate-500">
            <tr>
              <th className="py-1 pr-3 font-medium">Campo</th>
              <th className="py-1 pr-3 font-medium">Pedido aqui</th>
              <th className="py-1 pr-3 font-medium">No {sistema}</th>
              <th className="py-1 font-medium" />
            </tr>
          </thead>
          <tbody>
            {c.itens.map((i) => (
              <tr
                key={i.campo}
                className={cn('border-t border-slate-100 align-top', i.resultado === 'Diferente' && 'bg-red-50')}
              >
                <td className="py-1 pr-3 text-slate-600">{i.rotulo}</td>
                <td className="break-words py-1 pr-3 text-slate-900">{i.nosso ?? '—'}</td>
                <td className="break-words py-1 pr-3 text-slate-900">{i.noSistema ?? '—'}</td>
                <td className={cn('whitespace-nowrap py-1 text-xs', MARCA[i.resultado].classe)}>
                  {MARCA[i.resultado].texto}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/**
 * Cartão "Conferência no sistema": a última conferência do número registrado e, para a regulação,
 * o "Conferir de novo" — o pedido muda lá fora (o regulador de lá reclassifica, agenda), e a
 * conferência é uma foto daquele instante.
 */
export function ConferenciaNoSistema({ s, podeConferir }: { s: SolicitacaoRegulacao; podeConferir: boolean }) {
  const conferencia = useConferencia(s.numeroExterno ? s.id : null);
  const conferir = useConferirNoSistema();
  const [erro, setErro] = useState<string | null>(null);

  if (!s.numeroExterno || !s.sistemaDestino) return null;
  const sistema = ROTULO_SISTEMA_REGULACAO[s.sistemaDestino] ?? s.sistemaDestino;

  async function conferirAgora() {
    setErro(null);
    try {
      await conferir.mutateAsync(s.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Conferência no {sistema}</h2>
          <p className="mt-0.5 text-xs text-slate-500">
            O pedido nº {s.numeroExterno} lido no {sistema} pela plataforma e comparado com o que foi pedido aqui.
            {conferencia.data && <> Última conferência: {formatarInstante(conferencia.data.conferidoEm)}.</>}
          </p>
        </div>
        {podeConferir && (
          <Button variante="secundaria" tamanho="sm" onClick={conferirAgora} disabled={conferir.isPending}>
            {conferir.isPending ? <Loader2 className="size-4 animate-spin" /> : <ScanSearch className="size-4" />}
            {conferencia.data ? 'Conferir de novo' : 'Conferir agora'}
          </Button>
        )}
      </div>

      <div className="mt-3">
        {erro && <p className="mb-2 rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}
        {conferir.isPending ? (
          <p className="text-sm text-slate-500">Lendo o pedido no {sistema}…</p>
        ) : conferencia.isLoading ? (
          <p className="text-sm text-slate-500">Carregando…</p>
        ) : conferencia.data ? (
          <ResultadoConferencia c={conferencia.data} />
        ) : (
          <p className="text-sm text-slate-500">Ainda não conferido.</p>
        )}
      </div>
    </section>
  );
}
