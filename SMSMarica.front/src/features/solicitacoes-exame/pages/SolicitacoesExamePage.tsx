import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  CalendarDays,
  ClipboardCheck,
  Loader2,
  Plus,
  Search,
  Siren,
  Trash2,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { aoColarSoDigitosSeDocumento } from '@/shared/lib/colarDocumento';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, hojeSP } from '@/shared/lib/datas';
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
import { SituacaoBadge, derivarSituacao } from '@/features/solicitacoes-exame/components/SituacaoSolicitacao';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { BotaoDeclaracaoComparecimento } from '@/features/solicitacoes-exame/components/BotaoDeclaracaoComparecimento';
import { BotaoBaixarExameCompleto } from '@/features/solicitacoes-exame/components/BotaoBaixarExameCompleto';
import { BotaoVisualizarLaudo } from '@/features/solicitacoes-exame/components/BotaoVisualizarLaudo';
import { BotaoAnamnese } from '@/features/anamnese/components/BotaoAnamnese';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import type {
  FiltroSolicitacoes,
  SolicitacaoExameListItem,
  StatusSolicitacao,
} from '@/features/solicitacoes-exame/types';

const CHAVE_TOGGLE_HOJE = 'solicitacoes-exame:filtro-hoje';

/**
 * Direção relativa à unidade ativa: recebida (executora, seta para dentro) vs.
 * enviada (a unidade ativa é a solicitante, seta para fora). Nada quando não há
 * unidade de referência (ex.: "Todas as unidades").
 */
function DirecaoIcone({ direcao }: { direcao: SolicitacaoExameListItem['direcao'] }) {
  if (direcao === 'Recebida') {
    return (
      <span title="Recebida — sua unidade é a executora deste exame" aria-label="Recebida">
        <ArrowDownToLine className="h-4 w-4 text-emerald-600" />
      </span>
    );
  }
  if (direcao === 'Enviada') {
    return (
      <span title="Enviada — sua unidade solicitou este exame" aria-label="Enviada">
        <ArrowUpFromLine className="h-4 w-4 text-sky-600" />
      </span>
    );
  }
  return null;
}

/** Data de "hoje" em Brasília, yyyy-mm-dd (compatível com <input type="date">). */
const hojeISO = hojeSP;

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

  // Toggle "Hoje": persistido em localStorage. Quando ligado, trava as datas no dia
  // corrente e desabilita os campos de período. Deep-link do PACS tem prioridade.
  const [hojeAtivo, setHojeAtivo] = useState<boolean>(
    () => !accessionUrl && localStorage.getItem(CHAVE_TOGGLE_HOJE) === '1',
  );

  const filtroInicial: FiltroSolicitacoes = accessionUrl
    ? { limite: 50, busca: accessionUrl }
    : hojeAtivo
      ? { limite: 50, dataInicial: hojeISO(), dataFinal: hojeISO() }
      : { limite: 50 };
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

  // A cada abertura da página com o toggle ligado, recalcula "hoje" e reaplica o filtro.
  useEffect(() => {
    if (!hojeAtivo || accessionUrl) return;
    const hoje = hojeISO();
    setFiltroDigitado((f) => ({ ...f, dataInicial: hoje, dataFinal: hoje }));
    setFiltroAplicado((f) => ({ ...f, dataInicial: hoje, dataFinal: hoje }));
    // Intencional: roda no mount (e ao ligar o toggle), não a cada tecla.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hojeAtivo]);

  function alternarHoje() {
    setHojeAtivo((atual) => {
      const proximo = !atual;
      localStorage.setItem(CHAVE_TOGGLE_HOJE, proximo ? '1' : '0');
      if (!proximo) {
        // Desligou: libera os campos e limpa o período travado.
        setFiltroDigitado((f) => ({ ...f, dataInicial: undefined, dataFinal: undefined }));
        setFiltroAplicado((f) => ({ ...f, dataInicial: undefined, dataFinal: undefined }));
      }
      return proximo;
    });
  }

  const lista = useListarSolicitacoes(filtroAplicado);

  function setCampo<K extends keyof FiltroSolicitacoes>(k: K, v: FiltroSolicitacoes[K]) {
    setFiltroDigitado((f) => ({ ...f, [k]: v }));
  }

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    setFiltroAplicado(filtroDigitado);
  }

  // Itens por página (backend limita a 500). Aplica na hora, sem precisar clicar em Buscar.
  const limiteAtual = filtroAplicado.limite ?? 50;
  function mudarLimite(novo: number) {
    setFiltroDigitado((f) => ({ ...f, limite: novo }));
    setFiltroAplicado((f) => ({ ...f, limite: novo }));
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
      ordenar: (s) => s.accessionNumber,
      render: (s) => (
        <div className="flex items-start gap-1.5">
          <DirecaoIcone direcao={s.direcao} />
          <div className="min-w-0">
            <CodigoCopiavel codigo={s.accessionNumber} />
            {s.codigoSolicitacao ? (
              <div className="truncate text-xs text-gray-500">SISREG {s.codigoSolicitacao}</div>
            ) : null}
          </div>
        </div>
      ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (s) => s.pacienteNome || null,
      render: (s) => (
        <div className="min-w-0">
          <NomePacienteComResumo
            pacienteId={s.pacienteId}
            nome={s.pacienteNome}
            className="min-w-0"
            classNameNome="truncate font-medium text-gray-900"
            sufixo={
              s.prioridade === 'Urgente' ? (
                <span title="Solicitação URGENTE" className="inline-flex text-red-600" aria-label="Urgente">
                  <Siren className="h-4 w-4" />
                </span>
              ) : undefined
            }
          />
          <div className="truncate text-xs text-gray-500">Por {s.solicitanteNome}</div>
        </div>
      ),
    },
    {
      chave: 'exame',
      cabecalho: 'Exame',
      ordenar: (s) => s.tipoExameNome || null,
      render: (s) => (
        <div className="min-w-0">
          <div className="truncate text-gray-900">{s.tipoExameNome}</div>
          <div className="truncate text-xs text-gray-500">
            <span className="uppercase">{s.modalidadeDicom}</span>
            {s.unidadeNome ? <> — {s.unidadeNome}</> : null}
          </div>
        </div>
      ),
    },
    {
      chave: 'data',
      cabecalho: 'Data Agendamento',
      ordenar: (s) => s.dataAgendada,
      render: (s) => formatarInstante(s.dataAgendada),
    },
    {
      chave: 'status',
      cabecalho: 'Situação',
      ordenar: (s) => derivarSituacao(s)?.rotulo ?? s.status,
      render: (s) => {
        const sit = derivarSituacao(s);
        return (
          <span className="inline-flex items-center gap-1.5">
            {sit ? <SituacaoBadge situacao={sit} /> : <StatusBadgeSolicitacao status={s.status} />}
            {/* Checks das comunicações: confirmação do agendamento, exame liberado e laudo pronto. */}
            <ChecksComunicacao chip={s.chipConfirmacao} finalidade="ConfirmacaoAgendamento" />
            <ChecksComunicacao chip={s.chipExameLiberado} finalidade="ExameLiberado" />
            <ChecksComunicacao chip={s.chipLaudoPronto} finalidade="LaudoPronto" />
          </span>
        );
      },
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (s) => {
        const realizadaOuLaudada = s.status === 'Realizada' || s.status === 'Laudada';
        return (
          <div className="flex items-center justify-end gap-2">
            <BotaoAnamnese
              solicitacaoExameId={s.id}
              accessionNumber={s.accessionNumber}
              temAnamnese={s.temAnamnese}
              iconeApenas
            />
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
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-7"
      >
        <Campo label="Buscar" htmlFor="busca" className="sm:col-span-2">
          <Input
            id="busca"
            value={filtroDigitado.busca ?? ''}
            onChange={(e) => setCampo('busca', e.target.value)}
            onPaste={aoColarSoDigitosSeDocumento((v) => setCampo('busca', v))}
            placeholder="Nome, CPF, CNS ou nº do pedido"
          />
          {/* O backend ignora o período em busca pontual — avisa para as datas
              preenchidas (ex.: toggle Hoje) não parecerem contraditórias. */}
          {filtroDigitado.busca?.trim() ? (
            <p className="mt-1 text-xs text-gray-500">
              A busca localiza em qualquer data (período ignorado).
            </p>
          ) : null}
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
            disabled={hojeAtivo}
          />
        </Campo>
        <Campo label="Data final" htmlFor="df">
          <Input
            id="df"
            type="date"
            value={filtroDigitado.dataFinal ?? ''}
            onChange={(e) => setCampo('dataFinal', e.target.value || undefined)}
            disabled={hojeAtivo}
          />
        </Campo>
        <Campo label="Período" htmlFor="hoje">
          <button
            id="hoje"
            type="button"
            onClick={alternarHoje}
            aria-pressed={hojeAtivo}
            title="Filtrar apenas as solicitações de hoje"
            className={cn(
              'inline-flex h-9 w-full items-center justify-center gap-1.5 rounded-md border px-3 text-sm font-medium transition-colors',
              hojeAtivo
                ? 'border-primary-600 bg-primary-600 text-white hover:bg-primary-700'
                : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50',
            )}
          >
            <CalendarDays className="h-4 w-4" />
            Hoje
          </button>
        </Campo>
        <div className="flex items-end">
          {/* isLoading (1ª carga do filtro), não isFetching: o auto-refresh de 10s em
              background não pode ficar piscando/desabilitando o botão. */}
          <Button type="submit" disabled={lista.isLoading} className="w-full">
            {lista.isLoading ? (
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

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-gray-500">
          {lista.data ? (
            <>
              {lista.data.length} {lista.data.length === 1 ? 'solicitação' : 'solicitações'}
              {lista.data.length >= limiteAtual ? (
                <span className="text-gray-400"> · pode haver mais — aumente os itens por página</span>
              ) : null}
            </>
          ) : null}
        </p>
        <label className="flex items-center gap-2 text-sm text-gray-600">
          Itens por página:
          <Select
            value={String(limiteAtual)}
            onChange={(e) => mudarLimite(Number(e.target.value))}
            className="w-24"
          >
            {[25, 50, 100, 200, 500].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </Select>
        </label>
      </div>

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(s) => s.id}
        carregando={lista.isPending}
        vazio="Nenhuma solicitação encontrada."
        scrollXFlutuante
        aoClicarLinha={podeVer ? (s) => navigate(`/app/solicitacoes-exame/${s.id}`) : undefined}
        dicaLinha="Clique para visualizar"
        // Solicitações URGENTES: fundo vermelho claro + filete vermelho fininho à
        // esquerda (na 1ª célula — renderiza em qualquer border-model da tabela).
        classeLinha={(s) =>
          s.statusConfirmacao === 'Cancelada'
            ? 'opacity-55 bg-gray-50 hover:bg-gray-100'
            : s.prioridade === 'Urgente'
              ? 'bg-red-50 hover:bg-red-100 [&>td:first-child]:border-l-[3px] [&>td:first-child]:border-l-red-600'
              : undefined
        }
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
