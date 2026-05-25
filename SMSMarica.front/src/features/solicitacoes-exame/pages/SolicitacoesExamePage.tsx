import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ClipboardCheck, Eye, Plus, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useListarSolicitacoes } from '@/features/solicitacoes-exame/api/queries';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';
import type {
  FiltroSolicitacoes,
  SolicitacaoExameListItem,
  StatusSolicitacao,
} from '@/features/solicitacoes-exame/types';

export function SolicitacoesExamePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const podeCriar = usePermissao('SolicitacoesExame', 'Inclusao');
  const podeVer = usePermissao('SolicitacoesExame', 'Consulta');

  const accessionUrl = searchParams.get('accessionNumber') ?? undefined;
  const filtroInicial: FiltroSolicitacoes = { limite: 50, accessionNumber: accessionUrl };
  const [filtroAplicado, setFiltroAplicado] = useState<FiltroSolicitacoes>(filtroInicial);
  const [filtroDigitado, setFiltroDigitado] = useState<FiltroSolicitacoes>(filtroInicial);

  // Sincroniza com a querystring quando o usuário entra pela coluna "Pedido" do PACS.
  useEffect(() => {
    if (accessionUrl) {
      const novo: FiltroSolicitacoes = { limite: 50, accessionNumber: accessionUrl };
      setFiltroAplicado(novo);
      setFiltroDigitado(novo);
    }
  }, [accessionUrl]);

  const lista = useListarSolicitacoes(filtroAplicado);

  function setCampo<K extends keyof FiltroSolicitacoes>(k: K, v: FiltroSolicitacoes[K]) {
    setFiltroDigitado((f) => ({ ...f, [k]: v }));
  }

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    setFiltroAplicado(filtroDigitado);
  }

  const colunas: Coluna<SolicitacaoExameListItem>[] = useMemo(() => [
    {
      chave: 'accession',
      cabecalho: 'Pedido',
      render: (s) => <span className="font-mono text-sm">{s.accessionNumber}</span>,
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (s) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{s.pacienteNome}</div>
          <div className="truncate text-xs text-gray-500">Por {s.solicitanteNome}</div>
        </div>
      ),
    },
    {
      chave: 'exame',
      cabecalho: 'Exame',
      render: (s) => (
        <div className="min-w-0">
          <div className="truncate text-gray-900">{s.tipoExameNome}</div>
          <div className="text-xs uppercase text-gray-500">{s.modalidadeDicom}</div>
        </div>
      ),
    },
    {
      chave: 'data',
      cabecalho: 'Solicitada em',
      render: (s) => new Date(s.criadoEm).toLocaleString('pt-BR'),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (s) => <StatusBadgeSolicitacao status={s.status} />,
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (s) =>
        podeVer ? (
          <button
            type="button"
            onClick={() => navigate(`/app/solicitacoes-exame/${s.id}`)}
            title="Abrir solicitação"
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Eye className="h-3.5 w-3.5" />
            Abrir
          </button>
        ) : null,
    },
  ], [navigate, podeVer]);

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <ClipboardCheck className="h-6 w-6 text-primary-600" />
            Solicitações de Exame
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Pedidos de exame que geram worklist no PACS e amarram o exame executado de volta ao pedido.
          </p>
        </div>
        {podeCriar ? (
          <Link to="/app/solicitacoes-exame/novo">
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Nova solicitação
            </Button>
          </Link>
        ) : null}
      </header>

      <form
        onSubmit={aoBuscar}
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-6"
      >
        <Campo label="Pedido (Accession)" htmlFor="acc" className="sm:col-span-2">
          <Input
            id="acc"
            value={filtroDigitado.accessionNumber ?? ''}
            onChange={(e) => setCampo('accessionNumber', e.target.value)}
            placeholder="Ex.: SMS2026000001"
          />
        </Campo>
        <Campo label="Status" htmlFor="status">
          <Select
            id="status"
            value={filtroDigitado.status ?? ''}
            onChange={(e) =>
              setCampo('status', (e.target.value || undefined) as StatusSolicitacao | undefined)
            }
          >
            <option value="">Todos</option>
            <option value="Solicitada">Solicitada</option>
            <option value="Enviada">Enviada ao PACS</option>
            <option value="Agendada">Agendada</option>
            <option value="EmExecucao">Em execução</option>
            <option value="Realizada">Realizada</option>
            <option value="Laudada">Laudada</option>
            <option value="Cancelada">Cancelada</option>
          </Select>
        </Campo>
        <Campo label="Data inicial" htmlFor="di">
          <Input
            id="di"
            type="date"
            value={filtroDigitado.dataInicial ?? ''}
            onChange={(e) => setCampo('dataInicial', e.target.value || undefined)}
          />
        </Campo>
        <Campo label="Data final" htmlFor="df">
          <Input
            id="df"
            type="date"
            value={filtroDigitado.dataFinal ?? ''}
            onChange={(e) => setCampo('dataFinal', e.target.value || undefined)}
          />
        </Campo>
        <div className="flex items-end">
          <Button type="submit" disabled={lista.isPending} className="w-full">
            <Search className="mr-2 h-4 w-4" />
            Buscar
          </Button>
        </div>
      </form>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(s) => s.id}
        carregando={lista.isPending}
        vazio="Nenhuma solicitação encontrada."
      />
    </div>
  );
}
