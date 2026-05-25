import { useEffect, useState } from 'react';
import { BookOpen, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
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

export function ProcedimentosSigtapPage() {
  const [busca, setBusca] = useState('');
  const debounced = useDebounce(busca, 300);
  const lista = useListarProcedimentos(debounced || undefined, undefined, 100);

  const colunas: Coluna<ProcedimentoSigtap>[] = [
    {
      chave: 'codigo',
      cabecalho: 'Código',
      render: (p) => <span className="font-mono text-sm">{p.codigo}</span>,
    },
    {
      chave: 'nome',
      cabecalho: 'Procedimento',
      render: (p) => (
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-gray-900">{p.nome}</div>
          <div className="truncate text-xs text-gray-500">
            {p.grupo} · {p.subgrupo}
          </div>
        </div>
      ),
    },
    {
      chave: 'forma',
      cabecalho: 'Forma',
      render: (p) => <span className="text-gray-700">{p.forma}</span>,
    },
  ];

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <BookOpen className="h-6 w-6 text-primary-600" />
          Catálogo SIGTAP
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Procedimentos oficiais do SUS — consulta read-only. No MVP, traz uma curadoria de exames de imagem (~30 itens). Importador completo do CSV oficial entra em iteração futura.
        </p>
      </header>

      <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <Campo label="Buscar por código ou nome" htmlFor="busca">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
            <Input
              id="busca"
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              placeholder="Ex.: MAMOGRAFIA ou 02.04.03"
              className="pl-9"
            />
          </div>
        </Campo>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(p) => p.id}
        carregando={lista.isPending}
        vazio="Nenhum procedimento encontrado."
      />
    </div>
  );
}
