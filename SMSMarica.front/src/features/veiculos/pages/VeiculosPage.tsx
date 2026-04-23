import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarVeiculos } from '@/features/veiculos/api/veiculosApi';
import type { VeiculoListItem } from '@/features/veiculos/types';

export function VeiculosPage() {
  const lista = useQuery({ queryKey: ['veiculos', 'lista'], queryFn: listarVeiculos });

  const colunas: Coluna<VeiculoListItem>[] = [
    { chave: 'placa', cabecalho: 'Placa', render: (v) => v.placa },
    { chave: 'modelo', cabecalho: 'Modelo', render: (v) => v.modelo },
    { chave: 'status', cabecalho: 'Status', render: (v) => <StatusBadge ativo={v.ativo} /> },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Veículos</h1>
        <p className="mt-1 text-sm text-gray-600">
          Frota utilizada para translado. Cada veículo tem layout de assentos por fileira.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe apenas GET /veiculos no momento. Cadastro, edição e editor visual de layout (fileiras e assentos) entram com a entrega S2.2." />

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(v) => v.id}
        carregando={lista.isLoading}
      />
    </div>
  );
}
