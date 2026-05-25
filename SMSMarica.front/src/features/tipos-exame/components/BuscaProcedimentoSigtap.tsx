import { useEffect, useState } from 'react';
import { Loader2, Search } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useListarProcedimentos } from '@/features/procedimentos-sigtap/api/queries';
import type { ProcedimentoSigtap } from '@/features/procedimentos-sigtap/types';

function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aoSelecionar: (p: ProcedimentoSigtap) => void;
};

export function BuscaProcedimentoSigtap({ aberto, aoFechar, aoSelecionar }: Props) {
  const [busca, setBusca] = useState('');
  const debounced = useDebounce(busca, 300);
  const lista = useListarProcedimentos(debounced || undefined, undefined, 100);

  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo="Buscar procedimento SIGTAP"
      descricao="Selecione o código oficial do SUS que descreve este tipo de exame."
      largura="lg"
    >
      <div className="space-y-3">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Ex.: MAMOGRAFIA ou 02.04.03"
            className="pl-9"
            autoFocus
          />
        </div>

        {lista.isPending ? (
          <div className="flex items-center justify-center py-10 text-gray-500">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Carregando…
          </div>
        ) : (lista.data?.length ?? 0) === 0 ? (
          <p className="py-6 text-center text-sm text-gray-500">Nenhum procedimento encontrado.</p>
        ) : (
          <ul className="max-h-96 overflow-auto rounded-md border border-gray-200">
            {(lista.data ?? []).map((p) => (
              <li key={p.id} className="border-b border-gray-100 last:border-0">
                <button
                  type="button"
                  onClick={() => {
                    aoSelecionar(p);
                    aoFechar();
                  }}
                  className="flex w-full items-start gap-3 px-3 py-2 text-left hover:bg-gray-50"
                >
                  <span className="font-mono text-xs text-gray-500 w-28 flex-shrink-0 pt-0.5">{p.codigo}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-gray-900">{p.nome}</span>
                    <span className="block truncate text-xs text-gray-500">
                      {p.grupo} · {p.subgrupo}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
