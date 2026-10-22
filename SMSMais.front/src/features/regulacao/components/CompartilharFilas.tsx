import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, Share2, X } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';

import { useCompartilhar } from '../api/solicitacoesQueries';
import type { FluxoRegulacao, SolicitacaoRegulacao } from '../tiposSolicitacao';
import { FILAS, ROTULO_FILA } from './StatusRegulacaoBadge';

/**
 * Compartilhar o pedido com outra fila da Gestão de fila (pedido do Bernardo, 22/10/2026): o NAR
 * compartilhado com a Interna aparece para quem só tem a Interna no perfil, e assim por diante.
 *
 * <p>O compartilhamento fica à vista — uma etiqueta por fila, e clicar nela desliga. A janela tem um
 * interruptor por fila; vale na hora, sem "salvar".</p>
 */
export function CompartilharFilas({ s, podeAlterar }: { s: SolicitacaoRegulacao; podeAlterar: boolean }) {
  const navegar = useNavigate();
  const compartilhar = useCompartilhar();
  const [aberto, setAberto] = useState(false);
  const [mexendo, setMexendo] = useState<FluxoRegulacao | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const outras = FILAS.filter((f) => f !== s.fluxo);
  const ligadas = s.filasCompartilhadas ?? [];

  async function alternar(fila: FluxoRegulacao, ligar: boolean) {
    setErro(null);
    setMexendo(fila);
    try {
      const r = await compartilhar.mutateAsync({ id: s.id, fila, compartilhar: ligar });
      if (!r.continuaVisivel) {
        notificar(`Pedido fora da sua fila agora — ficou só na fila ${ROTULO_FILA[s.fluxo]}.`);
        navegar('/app/regulacao/gestao-fila');
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setMexendo(null);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      {ligadas.map((f) => (
        <button
          key={f}
          type="button"
          disabled={!podeAlterar || mexendo !== null}
          onClick={() => void alternar(f, false)}
          title={podeAlterar ? `Parar de compartilhar com a fila ${ROTULO_FILA[f]}` : undefined}
          className={cn(
            'inline-flex items-center gap-1 rounded-full border border-sky-300 bg-sky-50 px-2.5 py-0.5 text-xs font-medium text-sky-800',
            podeAlterar && 'hover:border-sky-400 hover:bg-sky-100',
          )}
        >
          <Share2 className="size-3.5" aria-hidden />
          Também na fila {ROTULO_FILA[f]}
          {podeAlterar &&
            (mexendo === f ? <Loader2 className="size-3.5 animate-spin" /> : <X className="size-3.5" aria-label="Desligar" />)}
        </button>
      ))}

      {podeAlterar && (
        <Button variante="secundaria" tamanho="sm" onClick={() => setAberto(true)}>
          <Share2 className="size-4" />
          Compartilhar
        </Button>
      )}

      {erro && !aberto && <span className="text-xs text-red-700">{erro}</span>}

      <Modal aberto={aberto} aoFechar={() => setAberto(false)} titulo="Compartilhar com outra fila">
        <ul className="divide-y divide-slate-100">
          {outras.map((f) => {
            const ligada = ligadas.includes(f);
            return (
              <li key={f} className="flex items-center justify-between gap-3 py-3">
                <span className="text-sm font-medium text-slate-900">Fila {ROTULO_FILA[f]}</span>
                <Interruptor
                  ligado={ligada}
                  ocupado={mexendo === f}
                  desabilitado={mexendo !== null}
                  rotulo={`Fila ${ROTULO_FILA[f]}`}
                  aoAlternar={() => void alternar(f, !ligada)}
                />
              </li>
            );
          })}
        </ul>
        {erro && <p className="mt-2 rounded bg-red-50 p-2 text-sm text-red-800">{erro}</p>}
        <div className="mt-3 flex justify-end">
          <Button onClick={() => setAberto(false)}>Pronto</Button>
        </div>
      </Modal>
    </div>
  );
}

function Interruptor({
  ligado,
  ocupado,
  desabilitado,
  rotulo,
  aoAlternar,
}: {
  ligado: boolean;
  ocupado: boolean;
  desabilitado: boolean;
  rotulo: string;
  aoAlternar: () => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={ligado}
      aria-label={rotulo}
      disabled={desabilitado}
      onClick={aoAlternar}
      className={cn(
        'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors disabled:opacity-60',
        ligado ? 'bg-sky-600' : 'bg-slate-300',
      )}
    >
      <span
        className={cn(
          'inline-flex size-5 items-center justify-center rounded-full bg-white shadow transition-transform',
          ligado ? 'translate-x-5' : 'translate-x-0.5',
        )}
      >
        {ocupado && <Loader2 className="size-3 animate-spin text-slate-500" />}
      </span>
    </button>
  );
}
