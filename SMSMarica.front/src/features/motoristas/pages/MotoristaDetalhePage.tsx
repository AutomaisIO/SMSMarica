import { ArrowLeft, Pencil } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useMotoristaPorId } from '@/features/motoristas/api/queries';
import { useListarRotas } from '@/features/translados/api/queries';
import type { RotaDiariaListItem, StatusRota } from '@/features/translados/types';

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

function formatarCpf(cpf: string) {
  const d = cpf.replace(/\D/g, '');
  return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : cpf;
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

export function MotoristaDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';

  const detalhe = useMotoristaPorId(id || null);
  const translados = useListarRotas({ motoristaId: id });

  const m = detalhe.data;

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
    { chave: 'veiculo', cabecalho: 'Veículo', render: (r) => r.veiculoPlaca },
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
            onClick={() => navigate('/operador/motoristas')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <Avatar src={m?.fotoBase64} nome={m?.nomeCompleto} tamanho="lg" />
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {m?.nomeCompleto ?? 'Carregando…'}
              </h1>
              {m ? <StatusBadge ativo={m.ativo} /> : null}
            </div>
            {m ? <p className="text-sm text-gray-500">CPF {formatarCpf(m.cpf)}</p> : null}
          </div>
        </div>
        <Button variante="outline" onClick={() => navigate('/operador/motoristas')}>
          <Pencil className="h-4 w-4" /> Editar na lista
        </Button>
      </header>

      {detalhe.isLoading ? (
        <div className="text-sm text-gray-500">Carregando motorista…</div>
      ) : detalhe.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(detalhe.error)}
        </div>
      ) : m ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <Dado rotulo="CPF" valor={formatarCpf(m.cpf)} />
            <Dado rotulo="CNH" valor={m.cnh} />
            <Dado rotulo="Telefone" valor={m.telefone ?? undefined} />
            <Dado rotulo="Cadastrado em" valor={new Date(m.criadoEm).toLocaleString('pt-BR')} />
          </div>
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
              ? 'Nenhum translado associado a este motorista.'
              : undefined
          }
        />
      </section>
    </div>
  );
}
