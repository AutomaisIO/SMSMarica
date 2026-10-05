import { useMemo, useState } from 'react';
import { ArrowLeft, CalendarClock, CalendarPlus, CheckCircle2, FilePen, Pencil, Trash2, XCircle } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { descreverDias } from '@/shared/lib/diasSemana';
import { formatarDuracao } from '@/shared/lib/tempoMedio';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { ListaAcompanhantes } from '@/features/acompanhantes/components/ListaAcompanhantes';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import {
  useAdicionarSessao,
  useAtualizarSessao,
  useCancelarSessao,
  useDefinirAcompanhantesSessao,
  useTratamentoPorId,
} from '@/features/tratamentos/api/queries';
// useAtualizarSessao e useDefinirAcompanhantesSessao são consumidos pelo EditorSessao (mesmo arquivo).
import { ChipsNecessidades } from '@/features/tratamentos/components/ChipsNecessidades';
import { EditorAgenda } from '@/features/tratamentos/components/EditorAgenda';
import { EditorDadosTratamento } from '@/features/tratamentos/components/EditorDadosTratamento';
import { PainelConfirmacao } from '@/features/tratamentos/components/PainelConfirmacao';
import { SeletorAcompanhantesSessao } from '@/features/tratamentos/components/SeletorAcompanhantesSessao';
import { formatarDataBr } from '@/features/tratamentos/lib/agenda';
import {
  ROTULO_MOBILIDADE,
  statusSessaoDeNumero,
  type Sessao,
  type StatusSessao,
} from '@/features/tratamentos/types';

const CLASSE_STATUS: Record<StatusSessao, string> = {
  Pendente: 'bg-gray-100 text-gray-700',
  Confirmada: 'bg-blue-100 text-blue-800',
  Realizada: 'bg-green-100 text-green-800',
  Cancelada: 'bg-gray-200 text-gray-500',
  NaoRealizada: 'bg-red-100 text-red-800',
  AguardandoRetorno: 'bg-amber-100 text-amber-800',
};

const ROTULO_STATUS: Record<StatusSessao, string> = {
  Pendente: 'Pendente',
  Confirmada: 'Confirmada',
  Realizada: 'Realizada',
  Cancelada: 'Cancelada',
  NaoRealizada: 'Não realizada',
  AguardandoRetorno: 'Aguardando retorno',
};

export function TratamentoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const detalhe = useTratamentoPorId(id ?? null);
  const adicionar = useAdicionarSessao();
  const cancelar = useCancelarSessao();

  const [paraCancelar, setParaCancelar] = useState<Sessao | null>(null);
  const [sessaoConfirmando, setSessaoConfirmando] = useState<Sessao | null>(null);
  const [sessaoEditando, setSessaoEditando] = useState<Sessao | null>(null);
  const [editandoDados, setEditandoDados] = useState(false);
  const [trocandoAgenda, setTrocandoAgenda] = useState(false);
  const podeEditar = usePermissao('Tratamentos', 'Edicao');
  const [novaData, setNovaData] = useState('');
  const [erroAcao, setErroAcao] = useState<string | null>(null);
  const [erroCancelar, setErroCancelar] = useState<string | null>(null);

  const sessoes = useMemo(() => detalhe.data?.sessoes ?? [], [detalhe.data]);

  const colunas: Coluna<Sessao>[] = [
    {
      chave: 'data',
      cabecalho: 'Data',
      render: (s) => (
        <span className="font-medium text-gray-900">{formatarDataBr(s.dataPrevista)}</span>
      ),
    },
    { chave: 'hora', cabecalho: 'Busca prevista', render: (s) => s.horaPrevistaBusca?.slice(0, 5) ?? '—' },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (s) => {
        const st = statusSessaoDeNumero(s.status);
        return (
          <div className="flex flex-wrap items-center gap-1.5">
            <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${CLASSE_STATUS[st]}`}>
              {ROTULO_STATUS[st]}
            </span>
            {s.alocadaEmRotaId ? (
              <button
                type="button"
                onClick={() => navigate(`/app/translados/${s.alocadaEmRotaId}`)}
                className="inline-flex items-center rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-xs font-medium text-sky-800 hover:border-sky-400 hover:bg-sky-100"
                title="Abrir translado"
              >
                Alocado · {s.alocadaNaData ? formatarDataBr(s.alocadaNaData) : ''}
                {s.fileiraAssentoAlocado != null && s.numeroAssentoAlocado != null
                  ? ` · F${s.fileiraAssentoAlocado}·${s.numeroAssentoAlocado}`
                  : ''}
              </button>
            ) : null}
          </div>
        );
      },
    },
    {
      chave: 'acompanhantes',
      cabecalho: 'Acompanhantes',
      render: (s) =>
        s.acompanhantes.length > 0 ? s.acompanhantes.map((a) => a.nome).join(', ') : (s.nomeAcompanhante ?? '—'),
    },
    {
      chave: 'obs',
      cabecalho: 'Observação',
      render: (s) => s.motivoNaoRealizacao ?? s.observacoes ?? '—',
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (s) => {
        const st = statusSessaoDeNumero(s.status);
        const imutavel = st === 'Realizada';
        return (
          <div className="flex items-center justify-end gap-1">
            <button
              type="button"
              className="rounded-md px-2 py-1 text-xs text-gray-600 hover:bg-gray-100"
              onClick={() => setSessaoConfirmando(s)}
              disabled={st === 'Cancelada'}
              title={st === 'Cancelada' ? 'Sessão cancelada não pode ser confirmada' : 'Confirmar realização'}
            >
              <CheckCircle2 className="mr-1 inline h-3.5 w-3.5" /> Confirmar
            </button>
            <button
              type="button"
              className="rounded-md px-2 py-1 text-xs text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50"
              onClick={() => setSessaoEditando(s)}
              disabled={imutavel || st === 'Cancelada'}
              title={imutavel ? 'Sessão já realizada — imutável' : 'Editar data e acompanhantes'}
            >
              <Pencil className="mr-1 inline h-3.5 w-3.5" /> Editar
            </button>
            <button
              type="button"
              className="rounded-md px-2 py-1 text-xs text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
              onClick={() => setParaCancelar(s)}
              disabled={imutavel || st === 'Cancelada'}
            >
              <Trash2 className="mr-1 inline h-3.5 w-3.5" /> Cancelar
            </button>
          </div>
        );
      },
    },
  ];

  async function confirmarCancelamento() {
    if (!id || !paraCancelar) return;
    setErroCancelar(null);
    try {
      await cancelar.mutateAsync({ id, sessaoId: paraCancelar.id });
      setParaCancelar(null);
    } catch (e) {
      setErroCancelar(extrairMensagemDeErro(e));
    }
  }

  async function adicionarSessaoNova() {
    if (!id || !novaData) return;
    setErroAcao(null);
    try {
      await adicionar.mutateAsync({
        id,
        payload: { dataPrevista: novaData, horaPrevistaBusca: null, horaPrevistaRetorno: null },
      });
      setNovaData('');
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  if (detalhe.isLoading) {
    return <p className="text-sm text-gray-500">Carregando atendimento…</p>;
  }
  if (detalhe.isError || !detalhe.data) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(detalhe.error) || 'Atendimento não encontrado.'}
      </div>
    );
  }

  const t = detalhe.data;
  const realizadas = sessoes.filter((s) => statusSessaoDeNumero(s.status) === 'Realizada').length;
  const canceladas = sessoes.filter((s) => statusSessaoDeNumero(s.status) === 'Cancelada').length;
  const validas = sessoes.length - canceladas;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <button
            type="button"
            onClick={() => navigate('/app/tratamentos')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex items-center gap-1.5">
              <h1 className="text-2xl font-semibold text-gray-900">
                {t.tipoTratamentoNome ? `${t.tipoTratamentoNome} — ` : ''}{t.descricao}
              </h1>
              <AjudaManual artigo="atendimentos-transporte" />
            </div>
            <p className="text-sm text-gray-600">
              Paciente{' '}
              <NomePacienteComResumo
                pacienteId={t.pacienteId}
                nome={t.pacienteNome}
                classNameNome="font-bold"
              />{' '}
              · Unidade de atendimento <strong>{t.unidadeAtendimentoNome}</strong>
              {t.unidadeAtendimentoCidade ? <> · {t.unidadeAtendimentoCidade}</> : null}
              {!t.ativo ? <> · <span className="text-gray-500">encerrado</span></> : null}
            </p>
          </div>
        </div>
        {t.ativo && podeEditar ? (
          <div className="flex gap-2">
            <Button variante="outline" onClick={() => setTrocandoAgenda(true)}>
              <CalendarClock className="mr-1.5 h-4 w-4" /> Trocar agenda
            </Button>
            <Button variante="outline" onClick={() => setEditandoDados(true)}>
              <FilePen className="mr-1.5 h-4 w-4" /> Editar dados
            </Button>
          </div>
        ) : null}
      </header>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
        <Card
          rotulo={t.agenda.continuo ? 'Sessões (contínuo)' : 'Sessões'}
          valor={t.agenda.continuo ? String(validas) : `${validas}/${t.agenda.quantidadeSessoes ?? validas}`}
          detalhe={
            t.agenda.continuo && t.agenda.sessoesGeradasAte
              ? `criadas até ${formatarDataBr(t.agenda.sessoesGeradasAte)}`
              : undefined
          }
        />
        <Card rotulo="Realizadas" valor={String(realizadas)} tom="success" />
        <Card rotulo="Canceladas" valor={String(canceladas)} tom="muted" />
        <Card rotulo="Dias" valor={descreverDias(t.agenda.diasSemanaMascara) || '—'} pequeno />
        <Card rotulo="Tempo médio (do tipo)" valor={formatarDuracao(t.tempoMedioMinutos)} />
      </div>

      <section className="grid grid-cols-1 gap-6 rounded-lg border border-gray-200 bg-white p-6 shadow-sm lg:grid-cols-2">
        <div className="space-y-3">
          <h2 className="text-base font-semibold text-gray-900">Condição do paciente</h2>
          <p className="text-sm text-gray-700">{ROTULO_MOBILIDADE[t.necessidades.mobilidade]}</p>
          <ChipsNecessidades necessidades={t.necessidades} mostrarVazio />
          {t.necessidades.necessitaAjuda && t.necessidades.ajudaDescricao ? (
            <p className="text-sm text-gray-700">Ajuda: {t.necessidades.ajudaDescricao}</p>
          ) : null}
          <div className="border-t border-gray-100 pt-3 text-sm text-gray-700">
            <p>
              Até <strong>{t.acompanhantes.quantidade}</strong> acompanhante{t.acompanhantes.quantidade > 1 ? 's' : ''} por viagem
              {t.acompanhantes.quantidade === 2 ? ' (liberado).' : ' (direito de todo paciente).'}
            </p>
            {t.acompanhantes.quantidade === 2 && t.acompanhantes.justificativaSegundo ? (
              <p className="mt-1 text-xs text-gray-500">
                Justificativa: {t.acompanhantes.justificativaSegundo}
                {t.acompanhantes.liberadoPorNome ? ` — liberado por ${t.acompanhantes.liberadoPorNome}` : ''}
              </p>
            ) : null}
          </div>
        </div>
        <ListaAcompanhantes
          pacienteId={t.pacienteId}
          podeEditar={podeEditar}
          dica={`Valem para todos os atendimentos do paciente. Neste, até ${t.acompanhantes.quantidade} por viagem.`}
        />
      </section>

      <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <h2 className="text-base font-semibold text-gray-900">Sessões</h2>
          {t.ativo && podeEditar ? (
            <div className="flex items-center gap-2">
              <Input
                type="date"
                value={novaData}
                onChange={(e) => setNovaData(e.target.value)}
                className="max-w-xs"
              />
              <Button variante="outline" onClick={adicionarSessaoNova} disabled={!novaData || adicionar.isPending}>
                <CalendarPlus className="mr-1.5 h-4 w-4" /> Adicionar sessão
              </Button>
            </div>
          ) : null}
        </div>
        {erroAcao ? (
          <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroAcao}
          </div>
        ) : null}
        <div className="mt-4">
          <Tabela colunas={colunas} dados={sessoes} chaveLinha={(s) => s.id} />
        </div>
      </section>

      <ConfirmDialog
        aberto={Boolean(paraCancelar)}
        titulo="Cancelar sessão"
        mensagem={paraCancelar ? `Cancelar a sessão de ${formatarDataBr(paraCancelar.dataPrevista)}?` : ''}
        destrutivo
        rotuloConfirmar="Cancelar sessão"
        carregando={cancelar.isPending}
        erro={erroCancelar}
        aoConfirmar={confirmarCancelamento}
        aoCancelar={() => { setParaCancelar(null); setErroCancelar(null); }}
      />

      <Modal
        aberto={Boolean(sessaoConfirmando)}
        aoFechar={() => setSessaoConfirmando(null)}
        titulo="Confirmar realização"
        descricao={sessaoConfirmando ? `Sessão de ${formatarDataBr(sessaoConfirmando.dataPrevista)}` : ''}
        largura="lg"
      >
        {sessaoConfirmando && id ? (
          <PainelConfirmacao
            tratamentoId={id}
            pacienteId={t.pacienteId}
            limiteAcompanhantes={t.acompanhantes.quantidade}
            sessao={sessaoConfirmando}
            aoConcluir={() => setSessaoConfirmando(null)}
          />
        ) : null}
      </Modal>

      <Modal
        aberto={editandoDados}
        aoFechar={() => setEditandoDados(false)}
        titulo="Editar dados do atendimento"
        descricao="Destino, tipo, condição do paciente e acompanhantes. A agenda se troca em “Trocar agenda”."
        largura="lg"
      >
        {editandoDados ? (
          <EditorDadosTratamento tratamento={t} aoConcluir={() => setEditandoDados(false)} />
        ) : null}
      </Modal>

      <Modal
        aberto={trocandoAgenda}
        aoFechar={() => setTrocandoAgenda(false)}
        titulo="Trocar agenda"
        descricao="Refaz as sessões pendentes e ainda sem rota a partir da data escolhida. As realizadas, confirmadas e já alocadas ficam."
        largura="lg"
      >
        {trocandoAgenda ? <EditorAgenda tratamento={t} aoConcluir={() => setTrocandoAgenda(false)} /> : null}
      </Modal>

      <Modal
        aberto={Boolean(sessaoEditando)}
        aoFechar={() => setSessaoEditando(null)}
        titulo="Editar sessão"
        descricao="Ajuste a data, o horário previsto e quem vai acompanhar."
        largura="md"
      >
        {sessaoEditando && id ? (
          <EditorSessao
            tratamentoId={id}
            pacienteId={t.pacienteId}
            limiteAcompanhantes={t.acompanhantes.quantidade}
            sessao={sessaoEditando}
            aoConcluir={() => setSessaoEditando(null)}
          />
        ) : null}
      </Modal>
    </div>
  );
}

function Card({
  rotulo,
  valor,
  tom,
  detalhe,
  pequeno,
}: {
  rotulo: string;
  valor: string;
  tom?: 'success' | 'muted';
  detalhe?: string;
  pequeno?: boolean;
}) {
  const classe = tom === 'success' ? 'text-green-700' : tom === 'muted' ? 'text-gray-500' : 'text-gray-900';
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 shadow-sm">
      <p className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</p>
      <p className={`mt-1 font-semibold ${pequeno ? 'text-base' : 'text-2xl'} ${classe}`}>{valor}</p>
      {detalhe ? <p className="text-xs text-gray-500">{detalhe}</p> : null}
    </div>
  );
}

function EditorSessao({
  tratamentoId,
  pacienteId,
  limiteAcompanhantes,
  sessao,
  aoConcluir,
}: {
  tratamentoId: string;
  pacienteId: string;
  limiteAcompanhantes: number;
  sessao: Sessao;
  aoConcluir: () => void;
}) {
  const atualizar = useAtualizarSessao();
  const definirAcompanhantes = useDefinirAcompanhantesSessao();
  const idsIniciais = sessao.acompanhantes.map((a) => a.id);
  const [acompanhanteIds, setAcompanhanteIds] = useState<string[]>(idsIniciais);
  const [dataPrevista, setDataPrevista] = useState(sessao.dataPrevista);
  const [hora, setHora] = useState(sessao.horaPrevistaBusca?.slice(0, 5) ?? '');
  const [obs, setObs] = useState(sessao.observacoes ?? '');
  const [erro, setErro] = useState<string | null>(null);
  const salvando = atualizar.isPending || definirAcompanhantes.isPending;

  async function salvar() {
    setErro(null);
    try {
      await atualizar.mutateAsync({
        id: tratamentoId,
        sessaoId: sessao.id,
        payload: {
          dataPrevista,
          horaPrevistaBusca: hora || null,
          horaPrevistaRetorno: null,
          observacoes: obs || null,
        },
      });
      const mudou =
        acompanhanteIds.length !== idsIniciais.length || acompanhanteIds.some((x) => !idsIniciais.includes(x));
      if (mudou) {
        await definirAcompanhantes.mutateAsync({ id: tratamentoId, sessaoId: sessao.id, acompanhanteIds });
      }
      aoConcluir();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <label className="flex flex-col">
          <span className="label">Data prevista</span>
          <Input type="date" value={dataPrevista} onChange={(e) => setDataPrevista(e.target.value)} />
        </label>
        <label className="flex flex-col">
          <span className="label">Hora prevista da busca</span>
          <Input type="time" value={hora} onChange={(e) => setHora(e.target.value)} />
        </label>
      </div>
      <SeletorAcompanhantesSessao
        pacienteId={pacienteId}
        limite={limiteAcompanhantes}
        selecionados={acompanhanteIds}
        aoMudar={setAcompanhanteIds}
      />
      <label className="flex flex-col">
        <span className="label">Observações</span>
        <textarea
          className="input min-h-[80px]"
          value={obs}
          onChange={(e) => setObs(e.target.value)}
        />
      </label>
      {erro ? (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}
      <div className="flex justify-end gap-2">
        <Button type="button" variante="ghost" onClick={aoConcluir}>Cancelar</Button>
        <Button type="button" onClick={salvar} disabled={salvando}>
          {salvando ? 'Salvando…' : 'Salvar'}
        </Button>
      </div>
      <p className="flex items-center gap-1 text-xs text-gray-500">
        <XCircle className="h-3.5 w-3.5" /> Sessões já realizadas não podem ser alteradas — apenas
        re-confirmadas.
      </p>
    </div>
  );
}
