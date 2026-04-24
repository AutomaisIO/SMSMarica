import { useMemo, useState } from 'react';
import { ArrowLeft, CalendarPlus, CheckCircle2, Pencil, Trash2, XCircle } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useAdicionarSessao,
  useAtualizarSessao,
  useCancelarSessao,
  useTratamentoPorId,
} from '@/features/tratamentos/api/queries';
// useAtualizarSessao é consumido pelo EditorSessao (mesma arquivo).
import { PainelConfirmacao } from '@/features/tratamentos/components/PainelConfirmacao';
import {
  statusSessaoDeNumero,
  type Sessao,
  type StatusSessao,
} from '@/features/tratamentos/types';
import { formatarDataBr } from '@/features/tratamentos/lib/expansor';

const CLASSE_STATUS: Record<StatusSessao, string> = {
  Pendente: 'bg-gray-100 text-gray-700',
  Confirmada: 'bg-blue-100 text-blue-800',
  Realizada: 'bg-green-100 text-green-800',
  Cancelada: 'bg-gray-200 text-gray-500',
  NaoRealizada: 'bg-red-100 text-red-800',
};

const ROTULO_STATUS: Record<StatusSessao, string> = {
  Pendente: 'Pendente',
  Confirmada: 'Confirmada',
  Realizada: 'Realizada',
  Cancelada: 'Cancelada',
  NaoRealizada: 'Não realizada',
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
  const [novaData, setNovaData] = useState('');
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const sessoes = useMemo(() => detalhe.data?.sessoes ?? [], [detalhe.data]);

  const colunas: Coluna<Sessao>[] = [
    {
      chave: 'data',
      cabecalho: 'Data',
      render: (s) => (
        <span className="font-medium text-gray-900">{formatarDataBr(s.dataPrevista)}</span>
      ),
    },
    { chave: 'hora', cabecalho: 'Hora prevista', render: (s) => s.horaPrevistaBusca ?? '—' },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (s) => {
        const st = statusSessaoDeNumero(s.status);
        return (
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${CLASSE_STATUS[st]}`}>
            {ROTULO_STATUS[st]}
          </span>
        );
      },
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
              disabled={imutavel}
              title={imutavel ? 'Sessão já realizada — imutável' : 'Editar data'}
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
    setErroAcao(null);
    try {
      await cancelar.mutateAsync({ id, sessaoId: paraCancelar.id });
      setParaCancelar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
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
    return <p className="text-sm text-gray-500">Carregando tratamento…</p>;
  }
  if (detalhe.isError || !detalhe.data) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(detalhe.error) || 'Tratamento não encontrado.'}
      </div>
    );
  }

  const t = detalhe.data;
  const realizadas = sessoes.filter((s) => statusSessaoDeNumero(s.status) === 'Realizada').length;
  const canceladas = sessoes.filter((s) => statusSessaoDeNumero(s.status) === 'Cancelada').length;

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <button
          type="button"
          onClick={() => navigate('/operador/tratamentos')}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">
            {t.tipoTratamentoNome ? `${t.tipoTratamentoNome} — ` : ''}{t.descricao}
          </h1>
          <p className="text-sm text-gray-600">
            Paciente <strong>{t.pacienteNome}</strong> · Unidade <strong>{t.unidadeNome}</strong>
            {t.codigoSusLiberacao ? (
              <> · SUS <code className="text-xs">{t.codigoSusLiberacao}</code></>
            ) : null}
          </p>
        </div>
      </header>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <Card rotulo="Total de sessões" valor={String(sessoes.length)} />
        <Card rotulo="Realizadas" valor={String(realizadas)} tom="success" />
        <Card rotulo="Canceladas" valor={String(canceladas)} tom="muted" />
        <Card rotulo="Horário padrão" valor={t.horaPrevistaBusca ?? '—'} />
      </div>

      <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-base font-semibold text-gray-900">Sessões</h2>
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
        </div>
        <div className="mt-4">
          <Tabela colunas={colunas} dados={sessoes} chaveLinha={(s) => s.id} />
        </div>
      </section>

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}

      <ConfirmDialog
        aberto={Boolean(paraCancelar)}
        titulo="Cancelar sessão"
        mensagem={paraCancelar ? `Cancelar a sessão de ${formatarDataBr(paraCancelar.dataPrevista)}?` : ''}
        destrutivo
        rotuloConfirmar="Cancelar sessão"
        carregando={cancelar.isPending}
        aoConfirmar={confirmarCancelamento}
        aoCancelar={() => { setParaCancelar(null); setErroAcao(null); }}
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
            sessao={sessaoConfirmando}
            aoConcluir={() => setSessaoConfirmando(null)}
          />
        ) : null}
      </Modal>

      <Modal
        aberto={Boolean(sessaoEditando)}
        aoFechar={() => setSessaoEditando(null)}
        titulo="Editar sessão"
        descricao="Ajuste a data e horários previstos."
        largura="md"
      >
        {sessaoEditando && id ? (
          <EditorSessao
            tratamentoId={id}
            sessao={sessaoEditando}
            aoConcluir={() => setSessaoEditando(null)}
          />
        ) : null}
      </Modal>
    </div>
  );
}

function Card({ rotulo, valor, tom }: { rotulo: string; valor: string; tom?: 'success' | 'muted' }) {
  const classe = tom === 'success' ? 'text-green-700' : tom === 'muted' ? 'text-gray-500' : 'text-gray-900';
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 shadow-sm">
      <p className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</p>
      <p className={`mt-1 text-2xl font-semibold ${classe}`}>{valor}</p>
    </div>
  );
}

function EditorSessao({
  tratamentoId,
  sessao,
  aoConcluir,
}: {
  tratamentoId: string;
  sessao: Sessao;
  aoConcluir: () => void;
}) {
  const atualizar = useAtualizarSessao();
  const [dataPrevista, setDataPrevista] = useState(sessao.dataPrevista);
  const [hora, setHora] = useState(sessao.horaPrevistaBusca ?? '');
  const [obs, setObs] = useState(sessao.observacoes ?? '');
  const [erro, setErro] = useState<string | null>(null);

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
      <label className="flex flex-col">
        <span className="label">Observações</span>
        <textarea
          className="input min-h-[80px]"
          value={obs}
          onChange={(e) => setObs(e.target.value)}
        />
      </label>
      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}
      <div className="flex justify-end gap-2">
        <Button type="button" variante="ghost" onClick={aoConcluir}>Cancelar</Button>
        <Button type="button" onClick={salvar} disabled={atualizar.isPending}>
          {atualizar.isPending ? 'Salvando…' : 'Salvar'}
        </Button>
      </div>
      <p className="text-xs text-gray-500 flex items-center gap-1">
        <XCircle className="h-3.5 w-3.5" /> Sessões já realizadas não podem ser alteradas — apenas
        re-confirmadas.
      </p>
    </div>
  );
}
