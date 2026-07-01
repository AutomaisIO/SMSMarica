import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ClipboardCheck, Eye, Loader2, Plus, Search, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { useExcluirSolicitacao, useListarSolicitacoes } from '@/features/solicitacoes-exame/api/queries';
import { ehFalhaExclusaoPacs } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';
import { BotaoDeclaracaoComparecimento } from '@/features/solicitacoes-exame/components/BotaoDeclaracaoComparecimento';
import { BotaoBaixarExameCompleto } from '@/features/solicitacoes-exame/components/BotaoBaixarExameCompleto';
import { BotaoVisualizarLaudo } from '@/features/solicitacoes-exame/components/BotaoVisualizarLaudo';
import { BotaoAnamnese } from '@/features/anamnese/components/BotaoAnamnese';
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
  const podeExcluir = usePermissao('SolicitacoesExame', 'Exclusao');
  const excluir = useExcluirSolicitacao();
  const [paraExcluir, setParaExcluir] = useState<SolicitacaoExameListItem | null>(null);
  const [erroExcluir, setErroExcluir] = useState<string | null>(null);
  const [forcarExclusao, setForcarExclusao] = useState(false);

  // Deep-link vindo da coluna "Pedido" do PACS: cai na busca livre (que casa accession).
  const accessionUrl = searchParams.get('accessionNumber') ?? undefined;
  const filtroInicial: FiltroSolicitacoes = { limite: 50, busca: accessionUrl };
  const [filtroAplicado, setFiltroAplicado] = useState<FiltroSolicitacoes>(filtroInicial);
  const [filtroDigitado, setFiltroDigitado] = useState<FiltroSolicitacoes>(filtroInicial);

  // Sincroniza com a querystring quando o usuário entra pela coluna "Pedido" do PACS.
  useEffect(() => {
    if (accessionUrl) {
      const novo: FiltroSolicitacoes = { limite: 50, busca: accessionUrl };
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

  async function confirmarExclusao(force: boolean) {
    if (!paraExcluir) return;
    setErroExcluir(null);
    try {
      await excluir.mutateAsync({ id: paraExcluir.id, force });
      setParaExcluir(null);
      setForcarExclusao(false);
    } catch (e) {
      setErroExcluir(extrairMensagemDeErro(e));
      if (!force && ehFalhaExclusaoPacs(e)) setForcarExclusao(true);
    }
  }

  const colunas: Coluna<SolicitacaoExameListItem>[] = useMemo(() => [
    {
      chave: 'accession',
      cabecalho: 'Pedido',
      render: (s) => (
        <div className="flex items-center gap-2">
          {s.prioridade === 'Urgente' ? (
            <span
              title="Solicitação URGENTE"
              className="inline-flex items-center gap-0.5 rounded-full bg-red-100 px-1.5 py-0.5 text-[11px] font-bold text-red-700"
            >
              ⚠ Urgente
            </span>
          ) : null}
          <CodigoCopiavel codigo={s.accessionNumber} />
        </div>
      ),
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
      render: (s) => {
        const realizadaOuLaudada = s.status === 'Realizada' || s.status === 'Laudada';
        return (
          <div className="flex items-center justify-end gap-2">
            {podeVer ? (
              <button
                type="button"
                onClick={() => navigate(`/app/solicitacoes-exame/${s.id}`)}
                title="Abrir solicitação"
                aria-label="Abrir solicitação"
                className="inline-flex items-center rounded p-0.5 text-gray-600 transition-colors hover:text-gray-900"
              >
                <Eye className="h-3.5 w-3.5" />
              </button>
            ) : null}
            <BotaoAnamnese solicitacaoExameId={s.id} accessionNumber={s.accessionNumber} iconeApenas />
            {realizadaOuLaudada ? (
              <>
                <BotaoDeclaracaoComparecimento solicitacaoId={s.id} />
                <BotaoBaixarExameCompleto solicitacaoId={s.id} />
                <BotaoVisualizarLaudo laudoId={s.laudoId} assinado={s.laudoAssinado} />
              </>
            ) : null}
            {podeExcluir && s.status !== 'EmExecucao' && !realizadaOuLaudada ? (
              <button
                type="button"
                onClick={() => {
                  setErroExcluir(null);
                  setForcarExclusao(false);
                  setParaExcluir(s);
                }}
                title="Excluir solicitação"
                aria-label="Excluir solicitação"
                className="inline-flex items-center rounded p-0.5 text-red-600 transition-colors hover:text-red-800"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            ) : null}
          </div>
        );
      },
    },
  ], [navigate, podeVer, podeExcluir]);

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
        <Campo label="Buscar" htmlFor="busca" className="sm:col-span-2">
          <Input
            id="busca"
            value={filtroDigitado.busca ?? ''}
            onChange={(e) => setCampo('busca', e.target.value)}
            placeholder="Nome, CPF, CNS ou nº do pedido"
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
            <option value="Recebida">Recebida</option>
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
          <Button type="submit" disabled={lista.isFetching} className="w-full">
            {lista.isFetching ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Search className="mr-2 h-4 w-4" />
            )}
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
        scrollXFlutuante
      />

      <Modal
        aberto={!!paraExcluir}
        aoFechar={() => setParaExcluir(null)}
        titulo="Excluir solicitação"
        descricao={
          paraExcluir
            ? `O pedido ${paraExcluir.accessionNumber} será removido permanentemente. Esta ação não pode ser desfeita.`
            : ''
        }
      >
        <div className="space-y-3">
          {erroExcluir ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroExcluir}
            </div>
          ) : null}
          {forcarExclusao ? (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              O dcm4chee não confirmou a remoção do item de worklist. Você pode <strong>forçar</strong> a exclusão —
              limpa apenas a base local e pode deixar o item órfão na worklist do equipamento.
            </div>
          ) : null}
          <div className="flex items-center justify-end gap-2">
            <Button variante="outline" onClick={() => setParaExcluir(null)}>
              Voltar
            </Button>
            {forcarExclusao ? (
              <Button variante="danger" disabled={excluir.isPending} onClick={() => confirmarExclusao(true)}>
                {excluir.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Trash2 className="mr-2 h-4 w-4" />}
                Forçar exclusão
              </Button>
            ) : (
              <Button variante="danger" disabled={excluir.isPending} onClick={() => confirmarExclusao(false)}>
                {excluir.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Trash2 className="mr-2 h-4 w-4" />}
                Confirmar exclusão
              </Button>
            )}
          </div>
        </div>
      </Modal>
    </div>
  );
}
