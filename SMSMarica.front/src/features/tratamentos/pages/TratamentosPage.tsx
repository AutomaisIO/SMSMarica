import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarTratamentos } from '@/features/tratamentos/api/tratamentosApi';
import type { TratamentoListItem } from '@/features/tratamentos/types';

export function TratamentosPage() {
  const lista = useQuery({ queryKey: ['tratamentos', 'lista'], queryFn: listarTratamentos });

  const colunas: Coluna<TratamentoListItem>[] = [
    { chave: 'descricao', cabecalho: 'Descrição', render: (t) => t.descricao },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (t) => <code className="text-xs text-gray-600">{t.pacienteId}</code>,
    },
    {
      chave: 'unidade',
      cabecalho: 'Unidade',
      render: (t) => <code className="text-xs text-gray-600">{t.unidadeId}</code>,
    },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusBadge ativo={t.ativo} /> },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Tratamentos</h1>
        <p className="mt-1 text-sm text-gray-600">
          Associação paciente ↔ unidade com periodicidade. Gera sessões de translado.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe apenas GET /tratamentos. Cadastro + algoritmo de expansão de periodicidade entram com a entrega S3.1." />

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(t) => t.id}
        carregando={lista.isLoading}
      />
    </div>
  );
}
