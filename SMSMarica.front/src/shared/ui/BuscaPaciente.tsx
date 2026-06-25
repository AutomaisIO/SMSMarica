import { useEffect, useState } from 'react';
import { Search, UserPlus } from 'lucide-react';
import { Avatar } from '@/shared/ui/Avatar';
import { Input } from '@/shared/ui/Input';
import { useBuscarPacientes } from '@/features/pacientes/api/queries';
import type { PacienteListItem } from '@/features/pacientes/types';

function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}

function cpfFmt(cpf: string): string {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

type Props = {
  aoSelecionar: (p: PacienteListItem) => void;
  placeholder?: string;
  /**
   * Quando informado, exibe um botão "Cadastrar paciente" no resultado vazio,
   * passando o termo digitado (para eventual pré-preenchimento).
   */
  aoCadastrarPaciente?: (termo: string) => void;
};

/**
 * Campo de busca de paciente com dropdown de resultados. Dispara
 * `aoSelecionar` ao clicar num item.
 */
export function BuscaPaciente({ aoSelecionar, placeholder, aoCadastrarPaciente }: Props) {
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);
  const busca = useBuscarPacientes(debounced);

  return (
    <div className="relative">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
        <Input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder={placeholder ?? 'Nome ou CPF (qualquer parte) — mín. 2 caracteres'}
          className="pl-9"
          autoFocus
        />
      </div>
      {debounced.trim().length >= 2 && !busca.isLoading && (busca.data?.length ?? 0) > 0 ? (
        <ul className="mt-2 max-h-72 overflow-auto rounded-md border border-gray-200 bg-white shadow">
          {busca.data!.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                onClick={() => {
                  aoSelecionar(p);
                  setTermo('');
                }}
                className="flex w-full items-center gap-3 px-3 py-2 text-left text-sm hover:bg-gray-50"
              >
                <Avatar src={p.fotoBase64} nome={p.nomeCompleto} tamanho="sm" />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium text-gray-900">{p.nomeCompleto}</span>
                  <span className="block truncate text-xs text-gray-500">
                    CPF {cpfFmt(p.cpf)}
                    {p.dataNascimento ? ` · nasc. ${p.dataNascimento.split('-').reverse().join('/')}` : ''}
                    {p.nomeDaMae ? ` · mãe ${p.nomeDaMae}` : ''}
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {debounced.trim().length >= 2 && !busca.isLoading && (busca.data?.length ?? 0) === 0 ? (
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <p className="text-xs text-gray-500">Nenhum paciente encontrado.</p>
          {aoCadastrarPaciente ? (
            <button
              type="button"
              onClick={() => aoCadastrarPaciente(debounced.trim())}
              className="inline-flex items-center gap-1.5 rounded-md border border-primary-200 bg-primary-50 px-2.5 py-1.5 text-sm font-medium text-primary-700 hover:bg-primary-100"
            >
              <UserPlus className="h-4 w-4" />
              Cadastrar paciente
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
