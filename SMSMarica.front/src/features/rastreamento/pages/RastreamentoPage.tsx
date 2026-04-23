import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarRastreamento } from '@/features/rastreamento/api/rastreamentoApi';
import type { RastreamentoListItem } from '@/features/rastreamento/types';

export function RastreamentoPage() {
  const lista = useQuery({ queryKey: ['rastreamento', 'lista'], queryFn: listarRastreamento });

  const colunas: Coluna<RastreamentoListItem>[] = [
    { chave: 'descricao', cabecalho: 'Descrição', render: (r) => r.descricao },
    { chave: 'status', cabecalho: 'Status', render: (r) => <StatusBadge ativo={r.ativo} /> },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Rastreamento</h1>
        <p className="mt-1 text-sm text-gray-600">
          Pontos de GPS, geofences e eventos de chegada.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe apenas GET /rastreamento. Ingestão de GPS, geofencing e eventos entram com a entrega S3.2." />

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(r) => r.id}
        carregando={lista.isLoading}
      />
    </div>
  );
}
