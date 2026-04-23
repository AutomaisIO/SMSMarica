import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarTranslados } from '@/features/translados/api/transladosApi';
import type { TransladoListItem } from '@/features/translados/types';

export function TransladosPage() {
  const lista = useQuery({ queryKey: ['translados', 'lista'], queryFn: listarTranslados });

  const colunas: Coluna<TransladoListItem>[] = [
    { chave: 'descricao', cabecalho: 'Descrição', render: (t) => t.descricao },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusBadge ativo={t.ativo} /> },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Translados</h1>
        <p className="mt-1 text-sm text-gray-600">
          Rotas diárias, alocação de sessões em assentos e acompanhamento da operação.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe apenas GET /translados. Alocação paciente→assento e mapa operacional entram com a entrega S3.3." />

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
