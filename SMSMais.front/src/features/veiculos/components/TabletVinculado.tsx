import { useState } from 'react';
import { KeyRound, Tablet, Unlink } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useDesvincularDispositivo,
  useDispositivoVeiculo,
  useGerarCodigoDispositivo,
} from '@/features/veiculos/api/queries';
import type { CodigoAtivacao } from '@/features/veiculos/api/veiculosApi';

function haQuanto(iso: string | null): string {
  if (!iso) return 'nunca';
  const s = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
  if (s < 60) return `há ${s}s`;
  if (s < 3600) return `há ${Math.round(s / 60)} min`;
  if (s < 86400) return `há ${Math.round(s / 3600)} h`;
  return new Date(iso).toLocaleString('pt-BR');
}

/**
 * Tablet fixo neste veículo: o carro aparece no Mapa da frota pela posição que ele manda.
 * Vincular = gerar um código de 8 letras e digitar no tablet (app do motorista › Vincular veículo).
 */
export function TabletVinculado({ veiculoId, podeEditar }: { veiculoId: string; podeEditar: boolean }) {
  const dispositivo = useDispositivoVeiculo(veiculoId);
  const gerar = useGerarCodigoDispositivo(veiculoId);
  const desvincular = useDesvincularDispositivo(veiculoId);
  const [codigo, setCodigo] = useState<CodigoAtivacao | null>(null);
  const [confirmando, setConfirmando] = useState(false);

  const d = dispositivo.data;
  const erro = gerar.error ?? desvincular.error ?? dispositivo.error;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <Tablet className="h-4 w-4" /> Tablet vinculado
      </h2>

      {dispositivo.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : d?.ativo ? (
        <div className="space-y-1 text-sm text-gray-700">
          <p>
            <span className="font-medium text-emerald-700">Vinculado</span>
            {d.modelo ? ` · ${d.modelo}` : null}
          </p>
          <p className="text-xs text-gray-500">Última posição recebida: {haQuanto(d.ultimoContatoEm)}</p>
          <p className="text-xs text-gray-500">
            O carro aparece no Mapa da frota enquanto o tablet estiver mandando posição.
          </p>
        </div>
      ) : (
        <p className="text-sm text-gray-500">
          Nenhum tablet vinculado. Gere um código e digite no tablet, em <strong>Vincular veículo</strong>.
        </p>
      )}

      {codigo ? (
        <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 p-3 text-center">
          <p className="text-xs text-amber-800">Digite no tablet:</p>
          <p className="my-1 font-mono text-2xl font-bold tracking-[0.3em] text-gray-900">{codigo.codigo}</p>
          <p className="text-[11px] text-amber-800">
            Vale até {new Date(codigo.expiraEm).toLocaleString('pt-BR')} e só pode ser usado uma vez.
          </p>
        </div>
      ) : d?.codigoPendente ? (
        <p className="mt-3 text-xs text-amber-700">
          Há um código aguardando uso (vale até {new Date(d.codigoExpiraEm ?? '').toLocaleString('pt-BR')}).
        </p>
      ) : null}

      {erro ? (
        <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {extrairMensagemDeErro(erro)}
        </div>
      ) : null}

      {podeEditar ? (
        <div className="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            disabled={gerar.isPending}
            onClick={() => gerar.mutate(undefined, { onSuccess: (c) => setCodigo(c) })}
            className="inline-flex items-center gap-1.5 rounded-md bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60"
          >
            <KeyRound className="h-4 w-4" />
            {d?.ativo ? 'Trocar tablet (novo código)' : 'Gerar código'}
          </button>
          {d?.ativo || d?.codigoPendente ? (
            confirmando ? (
              <span className="inline-flex items-center gap-2 text-xs text-gray-700">
                Desvincular?
                <button
                  type="button"
                  className="font-medium text-red-700 hover:underline"
                  onClick={() =>
                    desvincular.mutate(undefined, {
                      onSuccess: () => {
                        setCodigo(null);
                        setConfirmando(false);
                      },
                    })
                  }
                >
                  Sim
                </button>
                <button type="button" className="text-gray-500 hover:underline" onClick={() => setConfirmando(false)}>
                  Não
                </button>
              </span>
            ) : (
              <button
                type="button"
                onClick={() => setConfirmando(true)}
                className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
              >
                <Unlink className="h-4 w-4" /> Desvincular
              </button>
            )
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
