import { useEffect, useState } from 'react';
import { Search } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { useBuscarMedicos } from '@/features/medicos/api/queries';
import type { MedicoListItem } from '@/features/medicos/types';

function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}

type Props = {
  aoSelecionar: (m: MedicoListItem) => void;
  placeholder?: string;
};

/** Busca de médico com dropdown — usa o hub FHIR via useBuscarMedicos. */
export function BuscaMedico({ aoSelecionar, placeholder }: Props) {
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);
  const busca = useBuscarMedicos(debounced, { conselho: 'CRM' });

  return (
    <div className="relative">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
        <Input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder={placeholder ?? 'Nome do médico — mín. 2 caracteres'}
          className="pl-9"
        />
      </div>
      {debounced.trim().length >= 2 && !busca.isLoading && (busca.data?.length ?? 0) > 0 ? (
        <ul className="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-md border border-gray-200 bg-white shadow">
          {busca.data!.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  aoSelecionar(m);
                  setTermo('');
                }}
                className="flex w-full flex-col items-start px-3 py-2 text-left text-sm hover:bg-gray-50"
              >
                <span className="font-medium text-gray-900">{m.nomeCompleto}</span>
                <span className="text-xs text-gray-500">
                  {m.conselho} {m.registro}
                  {m.especialidade ? ` · ${m.especialidade}` : ''}
                </span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
