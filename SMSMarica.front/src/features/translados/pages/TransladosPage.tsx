import { useState } from 'react';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useCancelarRota,
  useListarRotas,
} from '@/features/translados/api/queries';
import type { RotaDiariaListItem, StatusRota } from '@/features/translados/types';

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

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

export function TransladosPage() {
  const navigate = useNavigate();
  const lista = useListarRotas();
  const cancelar = useCancelarRota();

  const [paraCancelar, setParaCancelar] = useState<RotaDiariaListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const visiveis = (lista.data ?? []).filter((r) => r.status !== 'Cancelada');

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
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (r) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/operador/translados/${r.id}`)}>
            <Eye className="h-3.5 w-3.5" /> Abrir
          </BotaoLinhaAcao>
          {r.status !== 'Concluida' ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaCancelar(r)}>
              <Trash2 className="h-3.5 w-3.5" /> Cancelar
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function confirmar() {
    if (!paraCancelar) return;
    setErroAcao(null);
    try {
      await cancelar.mutateAsync(paraCancelar.id);
      setParaCancelar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Translados</h1>
          <p className="mt-1 text-sm text-gray-600">
            Rotas diárias. Clique em uma data para operar alocações de pacientes nos assentos.
          </p>
        </div>
        <Button onClick={() => navigate('/operador/translados/novo')}>
          <Plus className="h-4 w-4" />
          Novo translado
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={visiveis}
        chaveLinha={(r) => r.id}
        carregando={lista.isLoading}
        vazio={visiveis.length === 0 && !lista.isLoading ? 'Nenhum translado ativo. Clique em "Novo translado" para criar.' : undefined}
      />

      <ConfirmDialog
        aberto={Boolean(paraCancelar)}
        titulo="Cancelar translado"
        mensagem={
          paraCancelar
            ? `Cancelar a rota de ${formatarData(paraCancelar.data)}? Ela some da listagem ativa, mas o registro permanece para auditoria.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Cancelar translado"
        carregando={cancelar.isPending}
        aoConfirmar={confirmar}
        aoCancelar={() => { setParaCancelar(null); setErroAcao(null); }}
      />

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}
    </div>
  );
}
