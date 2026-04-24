import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarRotas } from '@/features/translados/api/transladosApi';
import type { RotaDiariaListItem, StatusRota } from '@/features/translados/types';

const MAPA_STATUS: Record<number, StatusRota> = {
  0: 'Planejada',
  1: 'EmAndamento',
  2: 'Concluida',
  3: 'Cancelada',
};

function rotuloStatus(status: StatusRota | number): string {
  if (typeof status === 'number') return MAPA_STATUS[status] ?? String(status);
  return status;
}

export function TransladosPage() {
  const lista = useQuery({ queryKey: ['rotas', 'lista'], queryFn: () => listarRotas() });

  const colunas: Coluna<RotaDiariaListItem>[] = [
    { chave: 'data', cabecalho: 'Data', render: (r) => r.data },
    {
      chave: 'veiculo',
      cabecalho: 'Veículo',
      render: (r) => <code className="text-xs text-gray-600">{r.veiculoId}</code>,
    },
    {
      chave: 'motorista',
      cabecalho: 'Motorista',
      render: (r) => <code className="text-xs text-gray-600">{r.motoristaId}</code>,
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (r) => (
        <span className="inline-flex items-center rounded-full border border-gray-200 bg-gray-50 px-2 py-0.5 text-xs text-gray-700">
          {rotuloStatus(r.status)}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Translados</h1>
        <p className="mt-1 text-sm text-gray-600">
          Rotas diárias, alocação de sessões em assentos e acompanhamento da operação.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe GET/POST/PUT/DELETE /rotas + iniciar/concluir. Alocação paciente→assento e mapa operacional entram com a entrega S3.3." />

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
