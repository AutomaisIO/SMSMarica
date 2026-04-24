import { useMemo } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { MapaDeAssentos, type LinhaLayout } from '@/features/veiculos/components/MapaDeAssentos';
import { useVeiculoPorId } from '@/features/veiculos/api/queries';
import { ROTULOS_TIPO_VEICULO } from '@/features/veiculos/types';
import { useListarRotas } from '@/features/translados/api/queries';
import type { RotaDiariaListItem, StatusRota } from '@/features/translados/types';

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

const ROTULOS_STATUS: Record<StatusRota, string> = {
  Planejada: 'Planejada',
  EmAndamento: 'Em andamento',
  Concluida: 'Concluída',
  Cancelada: 'Cancelada',
};

const CORES_STATUS: Record<StatusRota, string> = {
  Planejada: 'bg-blue-50 text-blue-800 border-blue-200',
  EmAndamento: 'bg-amber-50 text-amber-800 border-amber-200',
  Concluida: 'bg-emerald-50 text-emerald-800 border-emerald-200',
  Cancelada: 'bg-gray-100 text-gray-700 border-gray-200',
};

function Dado({ rotulo, valor }: { rotulo: string; valor?: string | null }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="text-sm text-gray-900">{valor || <span className="text-gray-400">—</span>}</span>
    </div>
  );
}

export function VeiculoDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';

  const detalhe = useVeiculoPorId(id || null);
  const translados = useListarRotas({ veiculoId: id });

  const v = detalhe.data;

  const linhas: LinhaLayout[] = useMemo(
    () =>
      (v?.fileiras ?? [])
        .slice()
        .sort((a, b) => a.ordem - b.ordem)
        .map((f) => ({
          ordem: f.ordem,
          assentos: f.assentos
            .slice()
            .sort((a, b) => a.numero - b.numero)
            .map((a) => ({ numero: a.numero, tipo: a.tipo, bloqueado: a.bloqueado })),
        })),
    [v],
  );

  const colunas: Coluna<RotaDiariaListItem>[] = [
    {
      chave: 'data',
      cabecalho: 'Data',
      render: (r) => (
        <button
          type="button"
          onClick={() => navigate(`/operador/translados/${r.id}`)}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {formatarData(r.data)}
        </button>
      ),
    },
    { chave: 'motorista', cabecalho: 'Motorista', render: (r) => r.motoristaNome },
    {
      chave: 'pacientes',
      cabecalho: 'Pacientes',
      render: (r) => <span className="text-xs text-gray-600">{r.totalAlocacoes}</span>,
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (r) => (
        <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs ${CORES_STATUS[r.status]}`}>
          {ROTULOS_STATUS[r.status]}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/operador/veiculos')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {v?.placa ?? 'Carregando…'}
              </h1>
              {v ? <StatusBadge ativo={v.ativo} /> : null}
            </div>
            {v ? (
              <p className="text-sm text-gray-500">
                {v.fabricante} {v.modelo} · {v.cor}
              </p>
            ) : null}
          </div>
        </div>
      </header>

      {detalhe.isLoading ? (
        <div className="text-sm text-gray-500">Carregando veículo…</div>
      ) : detalhe.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(detalhe.error)}
        </div>
      ) : v ? (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
          <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold text-gray-900">Layout de assentos</h2>
            <MapaDeAssentos linhas={linhas} />
          </section>
          <aside className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold text-gray-900">Informações</h2>
            <div className="grid grid-cols-1 gap-4">
              <Dado rotulo="Placa" valor={v.placa} />
              <Dado rotulo="Tipo" valor={ROTULOS_TIPO_VEICULO[v.tipo] ?? v.tipo} />
              <Dado rotulo="Fabricante" valor={v.fabricante} />
              <Dado rotulo="Modelo" valor={v.modelo} />
              <Dado rotulo="Cor" valor={v.cor} />
              <Dado rotulo="Cadastrado em" valor={new Date(v.criadoEm).toLocaleString('pt-BR')} />
            </div>
          </aside>
        </div>
      ) : null}

      <section className="space-y-3">
        <h2 className="text-sm font-semibold text-gray-900">Translados recentes</h2>
        {translados.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(translados.error)}
          </div>
        ) : null}
        <Tabela
          colunas={colunas}
          dados={translados.data ?? []}
          chaveLinha={(r) => r.id}
          carregando={translados.isLoading}
          vazio={
            !translados.isLoading && (translados.data?.length ?? 0) === 0
              ? 'Nenhum translado associado a este veículo.'
              : undefined
          }
        />
      </section>
    </div>
  );
}
