import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  CalendarDays,
  ClipboardCheck,
  ListOrdered,
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
import { useAuth, usePermissao } from '@/shared/auth/authStore';
import { useVisaoSolicitacoes } from '@/features/solicitacoes-exame/store/visaoPreferencia';
import { Button } from '@/shared/ui/Button';
import { TextoLimitado } from '@/shared/ui/TextoLimitado';
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
import { BlocoPendenciasBusca } from '@/features/painel-inicio/components/BlocoPendenciasBusca';
import type {
  FiltroSolicitacoes,
  RecortePainel,
  SolicitacaoExameListItem,
  StatusSolicitacao,
} from '@/features/solicitacoes-exame/types';

const CHAVE_TOGGLE_HOJE = 'solicitacoes-exame:filtro-hoje';

/**
 * Filtro em curso, guardado para sobreviver à ida e volta ao detalhe da solicitação. O botão
 * "Voltar" do detalhe faz uma navegação NOVA (`navigate('/app/solicitacoes-exame')`), não history
 * back — então a página remonta e o estado de React se perde. Sem isto, quem filtrava um dia e
 * abria uma solicitação voltava para a lista inteira e refazia o filtro a cada exame conferido.
 *
 * <p><b>sessionStorage, não localStorage:</b> um período é contexto de trabalho, não preferência.
 * Reabrir o sistema amanhã com o filtro de hoje ainda aplicado — silenciosamente — esconderia
 * exames sem o operador entender por quê. O toggle "Hoje" ao lado é preferência de verdade e
 * continua em localStorage, de propósito.</p>
 */
const CHAVE_FILTRO = 'solicitacoes-exame:filtro';

function lerFiltroGuardado(): FiltroSolicitacoes | null {
  try {
    const bruto = sessionStorage.getItem(CHAVE_FILTRO);
    if (!bruto) return null;
    const f = JSON.parse(bruto) as FiltroSolicitacoes;
    return typeof f === 'object' && f !== null ? f : null;
  } catch {
    // Guardado corrompido não pode quebrar a tela: cai no filtro padrão.
    return null;
  }
}

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
  // Visão executante x solicitante (ticket #84): só existe com UMA unidade ativa (referência
  // única). A preferência é por-usuário (servidor); configura uma vez e fica.
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);
  const verComoSolicitante = useVisaoSolicitacoes((s) => s.verComoSolicitante);
  const definirVisao = useVisaoSolicitacoes((s) => s.definir);
  const excluir = useExcluirSolicitacao();
  const [paraExcluir, setParaExcluir] = useState<SolicitacaoExameListItem | null>(null);
  const [erroExcluir, setErroExcluir] = useState<string | null>(null);
  const [forcarExclusao, setForcarExclusao] = useState(false);

  // Deep-link vindo da coluna "Pedido" do PACS: cai na busca livre (que casa accession).
  const accessionUrl = searchParams.get('accessionNumber') ?? undefined;

  // Deep-link vindo do "ver todos" de uma raia do painel de início. O recorte é filtro de
  // SERVIDOR (ADR-0033 §4): o painel nunca cria listagem nova, aponta para esta.
  const painelUrl = (() => {
    const v = searchParams.get('painel');
    return v === 'cancelados' ? 'Cancelados' : v === 'aguardando' ? 'Aguardando' : undefined;
  })() as RecortePainel | undefined;

  // Toggle "Hoje": persistido em localStorage. Quando ligado, trava as datas no dia
  // corrente e desabilita os campos de período. Deep-link do PACS tem prioridade.
  // O recorte do painel também ignora o "Hoje": a raia de cancelados inclui exames de qualquer
  // data (inclusive passada — é justamente o caso esquecido).
  const [hojeAtivo, setHojeAtivo] = useState<boolean>(
    () => !accessionUrl && !painelUrl && localStorage.getItem(CHAVE_TOGGLE_HOJE) === '1',
  );

  // Ordem de Chegada: reordena a página por quando a recepção AUTORIZOU (autorizadoEm),
  // do primeiro ao último — quem chegou/foi autorizado primeiro fica no topo. Só faz
  // sentido dentro de um dia, então o toggle só aparece com "Hoje" ligado.
  const [ordemChegada, setOrdemChegada] = useState(false);

  // Precedência: deep-link do PACS > recorte do painel > filtro guardado da sessão > padrão.
  // O deep-link é uma intenção explícita e recém-expressa; o guardado é contexto anterior.
  const filtroInicialBase: FiltroSolicitacoes = accessionUrl
    ? { limite: 50, busca: accessionUrl }
    : painelUrl
      ? { limite: 50, painel: painelUrl }
      : (() => {
          const guardado = lerFiltroGuardado();
          if (guardado) {
            // Com "Hoje" ligado o período é sempre o dia corrente — o efeito abaixo reaplica,
            // mas já entra certo aqui para a lista não piscar com as datas antigas.
            return hojeAtivo
              ? { ...guardado, dataInicial: hojeISO(), dataFinal: hojeISO() }
              : guardado;
          }
          return hojeAtivo
            ? { limite: 50, dataInicial: hojeISO(), dataFinal: hojeISO() }
            : { limite: 50 };
        })();
  // A visão (ticket #84) vem da preferência do usuário, não do filtro guardado da sessão —
  // por isso sobrepõe qualquer valor herdado, para a lista já entrar na visão certa.
  const filtroInicial: FiltroSolicitacoes = { ...filtroInicialBase, visaoSolicitante: verComoSolicitante };
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

  // Guarda o filtro em vigor para a volta do detalhe. Deep-link não é guardado: ele é uma
  // navegação pontual (veio do PACS ou do painel), não o recorte de trabalho do operador —
  // gravá-lo faria a próxima visita à tela repetir um filtro que ninguém pediu.
  useEffect(() => {
    if (accessionUrl || painelUrl) return;
    try {
      sessionStorage.setItem(CHAVE_FILTRO, JSON.stringify(filtroAplicado));
    } catch {
      // Sem sessionStorage (aba anônima restrita, cota cheia) a tela segue funcionando —
      // só perde a memória do filtro, que é exatamente o comportamento anterior.
    }
  }, [filtroAplicado, accessionUrl, painelUrl]);

  // A cada abertura da página com o toggle ligado, recalcula "hoje" e reaplica o filtro.
  useEffect(() => {
    if (!hojeAtivo || accessionUrl) return;
    const hoje = hojeISO();
    setFiltroDigitado((f) => ({ ...f, dataInicial: hoje, dataFinal: hoje }));
    setFiltroAplicado((f) => ({ ...f, dataInicial: hoje, dataFinal: hoje }));
    // Intencional: roda no mount (e ao ligar o toggle), não a cada tecla.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hojeAtivo]);

  // Visão (ticket #84): quando a preferência muda (toggle ou hidratação do servidor), reaplica
  // na lista na hora, sem passar por "Buscar" — é troca de visão, não de recorte digitado.
  useEffect(() => {
    setFiltroDigitado((f) => ({ ...f, visaoSolicitante: verComoSolicitante }));
    setFiltroAplicado((f) => ({ ...f, visaoSolicitante: verComoSolicitante }));
  }, [verComoSolicitante]);

  function alternarHoje() {
    setHojeAtivo((atual) => {
      const proximo = !atual;
      localStorage.setItem(CHAVE_TOGGLE_HOJE, proximo ? '1' : '0');
      if (!proximo) {
        // Desligou: libera os campos, limpa o período travado e a ordem de chegada
        // (que só existe no contexto "Hoje").
        setFiltroDigitado((f) => ({ ...f, dataInicial: undefined, dataFinal: undefined }));
        setFiltroAplicado((f) => ({ ...f, dataInicial: undefined, dataFinal: undefined }));
        setOrdemChegada(false);
      }
      return proximo;
    });
  }

  const lista = useListarSolicitacoes(filtroAplicado);

  // Reordena a página por ordem de chegada (autorização da recepção), do mais antigo
  // ao mais recente. Não-autorizadas (autorizadoEm null) ficam no fim. Datas ISO em
  // UTC comparam corretamente como string. Clicar num cabeçalho na Tabela sobrepõe.
  const dadosOrdenados = useMemo(() => {
    const base = lista.data ?? [];
    if (!ordemChegada) return base;
    return [...base].sort((a, b) => {
      if (a.autorizadoEm && b.autorizadoEm) return a.autorizadoEm.localeCompare(b.autorizadoEm);
      if (a.autorizadoEm) return -1;
      if (b.autorizadoEm) return 1;
      return 0;
    });
  }, [lista.data, ordemChegada]);

  function setCampo<K extends keyof FiltroSolicitacoes>(k: K, v: FiltroSolicitacoes[K]) {
    setFiltroDigitado((f) => ({ ...f, [k]: v }));
  }

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    // Buscar manualmente encerra o recorte do painel: a partir daqui quem dirige o filtro é o
    // operador, e manter um recorte invisível faria a lista "esconder" resultados sem explicação.
    setFiltroAplicado({ ...filtroDigitado, painel: undefined });
    setFiltroDigitado((f) => ({ ...f, painel: undefined }));
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
      className: 'w-44 whitespace-nowrap',
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
        <div className="min-w-0" title={s.tipoExameNome ?? undefined}>
          <TextoLimitado texto={s.tipoExameNome} max={40} className="block truncate text-gray-900" />
          <div className="truncate text-xs text-gray-500">
            <span className="uppercase">{s.modalidadeDicom}</span>
            {s.unidadeNome ? <> — {s.unidadeNome}</> : null}
          </div>
        </div>
      ),
    },
    {
      chave: 'data',
      // Rótulo curto: "Data Agendamento" + seta de ordenação estouravam a largura da coluna.
      cabecalho: 'Agendamento',
      className: 'w-40 whitespace-nowrap',
      ordenar: (s) => s.dataAgendada,
      render: (s) => formatarInstante(s.dataAgendada),
    },
    {
      chave: 'status',
      cabecalho: 'Situação',
      // Estreita (era w-56, sobrava espaço): os checks quebram para a 2ª linha no pior
      // caso (badge longo + 3 comunicações) — a linha já tem duas linhas de altura.
      className: 'w-44',
      ordenar: (s) => derivarSituacao(s)?.rotulo ?? s.status,
      render: (s) => {
        const sit = derivarSituacao(s);
        return (
          <span className="inline-flex flex-wrap items-center gap-1.5">
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
      // No máximo 4 ícones (anamnese + declaração + exame completo + laudo).
      className: 'w-36 whitespace-nowrap text-right',
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

      {/* Recorte ativo vindo do painel de início: dito em voz alta, para a lista nunca esconder
          resultados sem o operador saber por quê. */}
      {filtroAplicado.painel ? (
        <div className="flex flex-wrap items-center gap-2 rounded-md border border-primary-200 bg-primary-50 px-3 py-2 text-sm text-primary-800">
          <span>
            Mostrando apenas:{' '}
            <strong>
              {filtroAplicado.painel === 'Cancelados'
                ? 'quem cancelou pelo WhatsApp e ainda não foi tratado'
                : 'quem ainda não respondeu, com exame próximo'}
            </strong>
          </span>
          <button
            type="button"
            className="ml-auto font-medium underline"
            onClick={() => {
              setFiltroAplicado((f) => ({ ...f, painel: undefined }));
              setFiltroDigitado((f) => ({ ...f, painel: undefined }));
            }}
          >
            ver todas
          </button>
        </div>
      ) : null}

      {/* A pessoa que chegou e "não tem agendamento" pode estar numa linha do SISREG que não
          entrou. O bloco fica ACIMA e visualmente distinto — nunca uma linha da tabela. */}
      <BlocoPendenciasBusca busca={filtroAplicado.busca ?? ''} />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-3">
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
          {/* Ordem de Chegada: só no contexto "Hoje". Ordena por quando a recepção autorizou. */}
          {hojeAtivo ? (
            <button
              type="button"
              onClick={() => setOrdemChegada((v) => !v)}
              aria-pressed={ordemChegada}
              title="Ordenar pela ordem em que a recepção autorizou (quem chegou primeiro no topo)"
              className={cn(
                'inline-flex h-8 items-center gap-1.5 rounded-md border px-2.5 text-xs font-medium transition-colors',
                ordemChegada
                  ? 'border-primary-600 bg-primary-600 text-white hover:bg-primary-700'
                  : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50',
              )}
            >
              <ListOrdered className="h-4 w-4" />
              Ordem de Chegada
            </button>
          ) : null}
          {/* Visão solicitante (ticket #84): só aparece com UMA unidade ativa (referência única).
              Desmarcado = executante (o que a unidade REALIZA, padrão da recepção); marcado = o que
              a unidade SOLICITOU a outra. A preferência fica salva no usuário. */}
          {unidadeAtivaId ? (
            <label
              className="inline-flex h-8 cursor-pointer select-none items-center gap-1.5 rounded-md border border-gray-300 bg-white px-2.5 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50"
              title="Marque para ver os exames que sua unidade SOLICITOU a outra. Desmarcado, mostra os que sua unidade REALIZA (executante)."
            >
              <input
                type="checkbox"
                checked={verComoSolicitante}
                onChange={(e) => definirVisao(e.target.checked)}
                className="h-3.5 w-3.5 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              <ArrowUpFromLine className="h-4 w-4 text-sky-600" />
              Ver como solicitante
            </label>
          ) : null}
        </div>
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
        dados={dadosOrdenados}
        chaveLinha={(s) => s.id}
        carregando={lista.isPending}
        vazio="Nenhuma solicitação encontrada."
        layoutFixo
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
