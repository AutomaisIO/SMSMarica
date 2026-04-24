import { useQuery } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarGeofences } from '@/features/rastreamento/api/rastreamentoApi';
import type { Geofence, TipoGeofence } from '@/features/rastreamento/types';

const MAPA_TIPO: Record<number, TipoGeofence> = {
  0: 'Unidade',
  1: 'Paciente',
  2: 'PontoLogistico',
};

function rotuloTipo(tipo: TipoGeofence | number): string {
  if (typeof tipo === 'number') return MAPA_TIPO[tipo] ?? String(tipo);
  return tipo;
}

export function RastreamentoPage() {
  const lista = useQuery({
    queryKey: ['rastreamento', 'geofences'],
    queryFn: listarGeofences,
  });

  const colunas: Coluna<Geofence>[] = [
    { chave: 'tipo', cabecalho: 'Tipo', render: (g) => rotuloTipo(g.tipo) },
    {
      chave: 'referencia',
      cabecalho: 'Referência',
      render: (g) => <code className="text-xs text-gray-600">{g.referenciaId}</code>,
    },
    {
      chave: 'coordenadas',
      cabecalho: 'Coordenadas',
      render: (g) => (
        <span className="text-xs text-gray-600">
          {g.latitude.toFixed(5)}, {g.longitude.toFixed(5)}
        </span>
      ),
    },
    {
      chave: 'raio',
      cabecalho: 'Raio (m)',
      render: (g) => g.raioMetros,
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Rastreamento</h1>
        <p className="mt-1 text-sm text-gray-600">
          Pontos de GPS, geofences e eventos de chegada.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Exibindo geofences (GET /rastreamento/geofences). Ingestão de GPS e eventos de chegada consumidos em /rastreamento/pontos e /rastreamento/eventos/rota entram com a entrega S3.2." />

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(g) => g.id}
        carregando={lista.isLoading}
      />
    </div>
  );
}
