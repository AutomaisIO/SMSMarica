import { useEffect, useMemo, useState } from 'react';
import { BellRing, RefreshCw, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { usePermissao } from '@/shared/auth/authStore';
import {
  useNotificacaoDetalhe,
  useNotificacoes,
  useReenviarNotificacao,
} from '@/features/notificacoes-agendamento/api/queries';
import type {
  FinalidadeComunicacao,
  NotificacaoFiltro,
  NotificacaoResumo,
  StatusConfirmacao,
  StatusNotificacao,
} from '@/features/notificacoes-agendamento/types';

const ROTULO_FINALIDADE: Record<FinalidadeComunicacao, string> = {
  ConfirmacaoAgendamento: 'Confirmação de agendamento',
  ExameLiberado: 'Exame liberado',
  LaudoPronto: 'Laudo pronto',
};

function useDebounce<T>(valor: T, ms = 400): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR');
}

const ROTULO_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'Na fila',
  Enviada: 'Enviada',
  Entregue: 'Entregue',
  Lida: 'Lida',
  Falha: 'Falha',
  SemTelefoneValido: 'Sem celular válido',
  AguardandoTelefoneVerificado: 'Aguardando contato verificado',
  AguardandoVerificacaoCadastral: 'Aguardando o paciente se identificar',
  AguardandoCorrecaoContato: 'Número inválido (não é do paciente)',
};

const CLASSE_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'badge-gray',
  Enviada: 'badge-info',
  Entregue: 'badge-info',
  Lida: 'badge-success',
  Falha: 'badge-danger',
  SemTelefoneValido: 'badge-warning',
  AguardandoTelefoneVerificado: 'badge-warning',
  AguardandoVerificacaoCadastral: 'badge-warning',
  AguardandoCorrecaoContato: 'badge-danger',
};

const ROTULO_CONFIRMACAO: Record<StatusConfirmacao, string> = {
  Pendente: 'Sem resposta',
  Confirmada: 'Confirmou',
  Cancelada: 'Não irá',
};

const CLASSE_CONFIRMACAO: Record<StatusConfirmacao, string> = {
  Pendente: 'badge-gray',
  Confirmada: 'badge-success',
  Cancelada: 'badge-danger',
};

const ROTULO_CANAL: Record<string, string> = {
  'whatsapp-link': 'link do WhatsApp',
  'whatsapp-quickreply': 'botões do WhatsApp',
  app: 'app do cidadão',
};

function BadgeStatus({ status }: { status: StatusNotificacao }) {
  return <span className={`badge ${CLASSE_STATUS[status] ?? 'badge-gray'}`}>{ROTULO_STATUS[status] ?? status}</span>;
}

function BadgeConfirmacao({ status }: { status: StatusConfirmacao }) {
  return (
    <span className={`badge ${CLASSE_CONFIRMACAO[status] ?? 'badge-gray'}`}>
      {ROTULO_CONFIRMACAO[status] ?? status}
    </span>
  );
}

function Info({
  rotulo,
  valor,
  className,
}: {
  rotulo: string;
  valor: import('react').ReactNode;
  className?: string;
}) {
  return (
    <div className={`flex flex-col gap-0.5 ${className ?? ''}`}>
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="break-words text-sm text-gray-900">{valor}</span>
    </div>
  );
}

/** Detalhe: linha do tempo do envio + resposta do paciente + reenvio. */
function DetalheNotificacao({ id, aoFechar }: { id: string; aoFechar: () => void }) {
  const q = useNotificacaoDetalhe(id);
  const reenviar = useReenviarNotificacao();
  const podeEditar = usePermissao('NotificacoesAgendamento', 'Edicao');
  const d = q.data;
  const r = d?.resumo;

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Notificação de agendamento" largura="lg">
      {q.isLoading ? (
        <div className="text-sm text-gray-500">Carregando…</div>
      ) : q.isError || !d || !r ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar o detalhe.
        </div>
      ) : (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Info rotulo="Paciente" valor={r.pacienteNome ?? '—'} />
            <Info rotulo="Telefone" valor={<TelefoneCopiavel numero={r.telefone} />} />
            <Info rotulo="Exame" valor={r.tipoExameNome ?? '—'} />
            <Info rotulo="Unidade" valor={r.unidadeNome ?? '—'} />
            <Info rotulo="Data agendada" valor={formatarDataHora(r.dataAgendada)} />
            <Info rotulo="Nº SISREG / Accession" valor={`${r.codigoSolicitacao ?? '—'} / ${r.accessionNumber ?? '—'}`} />
          </div>

          <div className="rounded-md border border-gray-200 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">Linha do tempo do envio</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Info rotulo="Criada" valor={formatarDataHora(r.criadoEm)} />
              <Info rotulo="Tentativas" valor={String(r.tentativas)} />
              <Info rotulo="Enviada" valor={formatarDataHora(r.enviadoEm)} />
              <Info rotulo="Entregue" valor={formatarDataHora(r.entregueEm)} />
              <Info rotulo="Lida" valor={formatarDataHora(r.lidoEm)} />
              <Info rotulo="Visualizada (abriu o conteúdo)" valor={formatarDataHora(r.visualizadoEm)} />
              <Info rotulo="Próxima tentativa" valor={formatarDataHora(d.proximaTentativaEm)} />
            </div>
            {r.motivoFalha ? (
              <p className="mt-2 rounded-md bg-red-50 px-2 py-1 text-sm text-red-700">{r.motivoFalha}</p>
            ) : null}
            {d.mensagemErroMeta ? (
              <p className="mt-2 rounded-md bg-red-50 px-2 py-1 text-sm text-red-700">
                Erro de entrega (Meta): {d.mensagemErroMeta}
              </p>
            ) : null}
            {d.mensagemConteudo ? (
              <p className="mt-2 whitespace-pre-wrap rounded-md bg-gray-50 px-2 py-1 text-xs text-gray-600">
                {d.mensagemConteudo}
              </p>
            ) : null}
          </div>

          <div className="rounded-md border border-gray-200 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">Resposta do paciente</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="flex flex-col gap-0.5">
                <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Situação</span>
                <span><BadgeConfirmacao status={r.statusConfirmacao} /></span>
              </div>
              <Info
                rotulo="Quando / canal"
                valor={
                  r.confirmadoEm || r.confirmadoCanal
                    ? `${formatarDataHora(r.confirmadoEm)} · ${ROTULO_CANAL[r.confirmadoCanal ?? ''] ?? r.confirmadoCanal ?? '—'}`
                    : '—'
                }
              />
              {r.motivoCancelamentoPaciente ? (
                <Info rotulo="Motivo do paciente" valor={r.motivoCancelamentoPaciente} className="sm:col-span-2" />
              ) : null}
            </div>
          </div>

          <div className="rounded-md border border-gray-200 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">Link de acesso (magic link)</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Info rotulo="Expira em" valor={formatarDataHora(d.linkExpiraEm)} />
              <Info
                rotulo="Usado"
                valor={d.linkUsadoEm ? `${formatarDataHora(d.linkUsadoEm)} (${d.linkUsadoIp ?? 'IP —'})` : 'Não usado'}
              />
            </div>
          </div>

          {podeEditar ? (
            <div className="flex items-center justify-end gap-2">
              {reenviar.isError ? (
                <span className="text-sm text-red-700">{extrairMensagemDeErro(reenviar.error)}</span>
              ) : null}
              <Button
                variante="outline"
                disabled={reenviar.isPending}
                onClick={() => reenviar.mutate(r.id)}
              >
                <RefreshCw className="mr-1.5 h-4 w-4" />
                Reenviar notificação
              </Button>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  );
}

export function NotificacoesAgendamentoPage() {
  const [texto, setTexto] = useState('');
  const [status, setStatus] = useState('');
  const [finalidade, setFinalidade] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [detalhe, setDetalhe] = useState<string | null>(null);
  const textoDebounced = useDebounce(texto, 400);

  const filtro = useMemo<NotificacaoFiltro>(
    () => ({
      texto: textoDebounced.trim() || undefined,
      status: status || undefined,
      finalidade: finalidade || undefined,
      confirmacao: confirmacao || undefined,
      de: de ? `${de}T00:00:00` : undefined,
      ate: ate ? `${ate}T00:00:00` : undefined,
      tamanho: 100,
    }),
    [textoDebounced, status, finalidade, confirmacao, de, ate],
  );

  const q = useNotificacoes(filtro);

  const colunas: Coluna<NotificacaoResumo>[] = useMemo(
    () => [
      {
        chave: 'paciente',
        cabecalho: 'Paciente',
        render: (n) => (
          <button
            type="button"
            onClick={() => setDetalhe(n.id)}
            className="font-medium text-red-700 hover:underline"
          >
            {n.pacienteNome ?? '(sem nome)'}
          </button>
        ),
      },
      {
        chave: 'finalidade',
        cabecalho: 'Finalidade',
        render: (n) => (
          <span className="text-xs text-gray-600">{ROTULO_FINALIDADE[n.finalidade] ?? n.finalidade}</span>
        ),
      },
      { chave: 'exame', cabecalho: 'Exame', render: (n) => n.tipoExameNome ?? '—' },
      { chave: 'data', cabecalho: 'Data agendada', render: (n) => formatarDataHora(n.dataAgendada) },
      { chave: 'telefone', cabecalho: 'Telefone', render: (n) => <TelefoneCopiavel numero={n.telefone} /> },
      { chave: 'status', cabecalho: 'Envio', render: (n) => <BadgeStatus status={n.status} /> },
      {
        chave: 'confirmacao',
        cabecalho: 'Resposta',
        render: (n) => <BadgeConfirmacao status={n.statusConfirmacao} />,
      },
      { chave: 'tentativas', cabecalho: 'Tentativas', render: (n) => n.tentativas },
      { chave: 'enviado', cabecalho: 'Enviada em', render: (n) => formatarDataHora(n.enviadoEm) },
    ],
    [],
  );

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <BellRing className="mt-1 h-6 w-6 text-red-600" />
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Notificações de agendamento</h1>
          <p className="mt-1 text-sm text-gray-600">
            Envio das confirmações de exame pelo WhatsApp: entrega, leitura, falhas e a resposta
            do paciente (confirmou ou avisou que não irá, com o motivo).
          </p>
        </div>
      </header>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Nº SISREG, accession ou telefone…"
            className="pl-9"
          />
        </div>
        <Select value={finalidade} onChange={(e) => setFinalidade(e.target.value)} aria-label="Finalidade">
          <option value="">Finalidade: todas</option>
          <option value="ConfirmacaoAgendamento">Confirmação de agendamento</option>
          <option value="ExameLiberado">Exame liberado</option>
          <option value="LaudoPronto">Laudo pronto</option>
        </Select>
        <Select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Status do envio">
          <option value="">Envio: todos</option>
          <option value="Pendente">Na fila</option>
          <option value="Enviada">Enviada</option>
          <option value="Entregue">Entregue</option>
          <option value="Lida">Lida</option>
          <option value="Falha">Falha</option>
          <option value="SemTelefoneValido">Sem celular válido</option>
          <option value="AguardandoTelefoneVerificado">Aguardando contato verificado</option>
          <option value="AguardandoVerificacaoCadastral">Aguardando identificação</option>
          <option value="AguardandoCorrecaoContato">Número inválido</option>
        </Select>
        <Select
          value={confirmacao}
          onChange={(e) => setConfirmacao(e.target.value)}
          aria-label="Resposta do paciente"
        >
          <option value="">Resposta: todas</option>
          <option value="Pendente">Sem resposta</option>
          <option value="Confirmada">Confirmou</option>
          <option value="Cancelada">Não irá</option>
        </Select>
        <div className="flex items-center gap-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="De" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Até" />
        </div>
      </div>

      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={q.data?.itens ?? []}
        chaveLinha={(n) => n.id}
        carregando={q.isLoading}
        vazio="Nenhuma notificação registrada."
      />

      {q.data ? (
        <p className="text-xs text-gray-500">
          {q.data.total} notificação(ões)
          {q.data.total > q.data.itens.length ? ` · exibindo as ${q.data.itens.length} mais recentes` : ''}.
        </p>
      ) : null}

      {detalhe ? <DetalheNotificacao id={detalhe} aoFechar={() => setDetalhe(null)} /> : null}
    </div>
  );
}
