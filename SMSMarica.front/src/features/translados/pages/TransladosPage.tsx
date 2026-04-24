import { useState } from 'react';
import { Trash2 } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BannerEscritaPendente } from '@/shared/ui/BannerEscritaPendente';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { cancelarRota, listarRotas } from '@/features/translados/api/transladosApi';
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

function ehCancelada(r: RotaDiariaListItem): boolean {
  return r.status === 'Cancelada' || (typeof r.status === 'number' && r.status === 3);
}

export function TransladosPage() {
  const client = useQueryClient();
  const lista = useQuery({ queryKey: ['rotas', 'lista'], queryFn: () => listarRotas() });
  const cancelar = useMutation({
    mutationFn: (id: string) => cancelarRota(id),
    onSuccess: () => client.invalidateQueries({ queryKey: ['rotas', 'lista'] }),
  });
  const [paraCancelar, setParaCancelar] = useState<RotaDiariaListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const visiveis = (lista.data ?? []).filter((r) => !ehCancelada(r));

  const colunas: Coluna<RotaDiariaListItem>[] = [
    { chave: 'data', cabecalho: 'Data', render: (r) => r.data },
    { chave: 'veiculo', cabecalho: 'Veículo', render: (r) => <code className="text-xs text-gray-600">{r.veiculoId}</code> },
    { chave: 'motorista', cabecalho: 'Motorista', render: (r) => <code className="text-xs text-gray-600">{r.motoristaId}</code> },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (r) => (
        <span className="inline-flex items-center rounded-full border border-gray-200 bg-gray-50 px-2 py-0.5 text-xs text-gray-700">
          {rotuloStatus(r.status)}
        </span>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (r) => (
        <div className="flex items-center justify-end gap-1">
          {!ehCancelada(r) ? (
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
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Translados</h1>
        <p className="mt-1 text-sm text-gray-600">
          Rotas diárias, alocação de sessões em assentos e acompanhamento da operação.
        </p>
      </header>

      <BannerEscritaPendente mensagem="Server expõe GET/POST/PUT/DELETE /rotas + iniciar/concluir. Mapa operacional entra com a entrega S3.3." />

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
      />

      <ConfirmDialog
        aberto={Boolean(paraCancelar)}
        titulo="Cancelar rota"
        mensagem={paraCancelar ? `Cancelar a rota de ${paraCancelar.data}? Ela some da listagem ativa, mas o registro permanece para auditoria.` : ''}
        destrutivo
        rotuloConfirmar="Cancelar rota"
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
