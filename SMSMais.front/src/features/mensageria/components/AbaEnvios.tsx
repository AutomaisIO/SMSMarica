import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { RefreshCw, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { usePermissao } from '@/shared/auth/authStore';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useNotificacaoDetalhe, useNotificacoes, useReenviarNotificacao } from '@/features/mensageria/api/queries';
import {
  CLASSE_RESPOSTA,
  CLASSE_STATUS,
  ROTULO_FINALIDADE,
  ROTULO_RESPOSTA,
  ROTULO_STATUS,
  dataHora,
  rotuloCanal,
  useDebounce,
} from '@/features/mensageria/lib/rotulos';
import type { NotificacaoFiltro, NotificacaoResumo, StatusConfirmacao, StatusNotificacao } from '@/features/mensageria/types';

const TAMANHOS = [50, 100, 200] as const;

function BadgeStatus({ status }: { status: StatusNotificacao }) {
  return <span className={`badge ${CLASSE_STATUS[status] ?? 'badge-gray'}`}>{ROTULO_STATUS[status] ?? status}</span>;
}

function BadgeConfirmacao({ status }: { status: StatusConfirmacao }) {
  return (
    <span className={`badge ${CLASSE_RESPOSTA[status] ?? 'badge-gray'}`}>{ROTULO_RESPOSTA[status] ?? status}</span>
  );
}

function Info({ rotulo, valor, className }: { rotulo: string; valor: ReactNode; className?: string }) {
  return (
    <div className={`flex flex-col gap-0.5 ${className ?? ''}`}>
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="break-words text-sm text-gray-900">{valor}</span>
    </div>
  );
}

/** Detalhe: linha do tempo do envio + resposta do paciente + reenvio. */
function DetalheEnvio({ id, aoFechar }: { id: string; aoFechar: () => void }) {
  const q = useNotificacaoDetalhe(id);
  const reenviar = useReenviarNotificacao();
  const podeEditar = usePermissao('NotificacoesAgendamento', 'Edicao');
  const d = q.data;
  const r = d?.resumo;

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Envio ao paciente" largura="lg">
      {q.isLoading ? (
        <div className="text-sm text-gray-500">Carregando…</div>
      ) : q.isError || !d || !r ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar o detalhe.
        </div>
      ) : (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Info rotulo="Paciente" valor={<NomePacienteComResumo pacienteId={r.pacienteId} nome={r.pacienteNome ?? '—'} />} />
            <Info rotulo="Telefone" valor={<TelefoneCopiavel numero={r.telefone} />} />
            <Info rotulo="Finalidade" valor={ROTULO_FINALIDADE[r.finalidade] ?? r.finalidade} />
            <Info rotulo="Exame / procedimento" valor={r.tipoExameNome ?? '—'} />
            <Info rotulo="Unidade" valor={r.unidadeNome ?? '—'} />
            <Info rotulo="Data agendada" valor={dataHora(r.dataAgendada)} />
            <Info rotulo="Nº SISREG / Accession" valor={`${r.codigoSolicitacao ?? '—'} / ${r.accessionNumber ?? '—'}`} />
          </div>

          <div className="rounded-md border border-gray-200 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">Linha do tempo do envio</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Info rotulo="Situação" valor={<BadgeStatus status={r.status} />} />
              <Info rotulo="Criada" valor={dataHora(r.criadoEm)} />
              <Info rotulo="Tentativas" valor={String(r.tentativas)} />
              <Info rotulo="Enviada" valor={dataHora(r.enviadoEm)} />
              <Info rotulo="Entregue" valor={dataHora(r.entregueEm)} />
              <Info rotulo="Lida" valor={dataHora(r.lidoEm)} />
              <Info rotulo="Visualizada (abriu o conteúdo)" valor={dataHora(r.visualizadoEm)} />
              <Info rotulo="Próxima tentativa" valor={dataHora(d.proximaTentativaEm)} />
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
              <Info rotulo="Situação" valor={<BadgeConfirmacao status={r.statusConfirmacao} />} />
              <Info
                rotulo="Quando / canal"
                valor={r.confirmadoEm || r.confirmadoCanal ? `${dataHora(r.confirmadoEm)} · ${rotuloCanal(r.confirmadoCanal)}` : '—'}
              />
              {r.motivoCancelamentoPaciente ? (
                <Info rotulo="Motivo do paciente" valor={r.motivoCancelamentoPaciente} className="sm:col-span-2" />
              ) : null}
            </div>
          </div>

          <div className="rounded-md border border-gray-200 p-3">
            <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">Link de acesso (magic link)</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Info rotulo="Expira em" valor={dataHora(d.linkExpiraEm)} />
              <Info rotulo="Usado" valor={d.linkUsadoEm ? `${dataHora(d.linkUsadoEm)} (${d.linkUsadoIp ?? 'IP —'})` : 'Não usado'} />
            </div>
          </div>

          {podeEditar ? (
            <div className="flex items-center justify-end gap-2">
              {reenviar.isError ? <span className="text-sm text-red-700">{extrairMensagemDeErro(reenviar.error)}</span> : null}
              <Button variante="outline" disabled={reenviar.isPending} onClick={() => reenviar.mutate(r.id)}>
                <RefreshCw className="mr-1.5 h-4 w-4" />
                Reenviar
              </Button>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  );
}

export function AbaEnvios() {
  const [texto, setTexto] = useState('');
  const [status, setStatus] = useState('');
  const [finalidade, setFinalidade] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const [detalhe, setDetalhe] = useState<string | null>(null);
  const textoDeb = useDebounce(texto, 400);

  useEffect(() => setPagina(1), [textoDeb, status, finalidade, confirmacao, de, ate, tamanho]);

  const filtro = useMemo<NotificacaoFiltro>(
    () => ({
      texto: textoDeb.trim() || undefined,
      status: status || undefined,
      finalidade: finalidade || undefined,
      confirmacao: confirmacao || undefined,
      de: de ? `${de}T00:00:00` : undefined,
      ate: ate ? `${ate}T00:00:00` : undefined,
      pagina,
      tamanho,
    }),
    [textoDeb, status, finalidade, confirmacao, de, ate, pagina, tamanho],
  );

  const q = useNotificacoes(filtro);

  const colunas: Coluna<NotificacaoResumo>[] = useMemo(
    () => [
      {
        chave: 'paciente',
        cabecalho: 'Paciente',
        render: (n) => (
          <div className="flex items-center gap-1">
            <button type="button" onClick={() => setDetalhe(n.id)} className="font-medium text-red-700 hover:underline">
              {n.pacienteNome ?? '(sem nome)'}
            </button>
            <NomePacienteComResumo pacienteId={n.pacienteId} />
          </div>
        ),
      },
      {
        chave: 'finalidade',
        cabecalho: 'Finalidade',
        render: (n) => <span className="text-xs text-gray-600">{ROTULO_FINALIDADE[n.finalidade] ?? n.finalidade}</span>,
      },
      { chave: 'exame', cabecalho: 'Exame', render: (n) => n.tipoExameNome ?? '—' },
      { chave: 'unidade', cabecalho: 'Unidade', render: (n) => n.unidadeNome ?? '—' },
      { chave: 'data', cabecalho: 'Data agendada', render: (n) => dataHora(n.dataAgendada) },
      { chave: 'telefone', cabecalho: 'Telefone', render: (n) => <TelefoneCopiavel numero={n.telefone} /> },
      {
        chave: 'status',
        cabecalho: 'Envio',
        render: (n) => (
          <span title={n.motivoFalha ?? undefined}>
            <BadgeStatus status={n.status} />
          </span>
        ),
      },
      { chave: 'confirmacao', cabecalho: 'Resposta', render: (n) => <BadgeConfirmacao status={n.statusConfirmacao} /> },
      { chave: 'tentativas', cabecalho: 'Tent.', render: (n) => n.tentativas },
      { chave: 'enviado', cabecalho: 'Enviada em', render: (n) => dataHora(n.enviadoEm) },
    ],
    [],
  );

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-6">
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Nº SISREG, accession ou telefone…" className="pl-9" />
        </div>
        <Select value={finalidade} onChange={(e) => setFinalidade(e.target.value)} aria-label="Finalidade">
          <option value="">Finalidade: todas</option>
          <option value="ConfirmacaoAgendamento">Confirmação de agendamento</option>
          <option value="ExameLiberado">Exame liberado</option>
          <option value="LaudoPronto">Laudo pronto</option>
        </Select>
        <Select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Status do envio">
          <option value="">Envio: todos</option>
          {(Object.keys(ROTULO_STATUS) as StatusNotificacao[]).map((s) => (
            <option key={s} value={s}>{ROTULO_STATUS[s]}</option>
          ))}
        </Select>
        <Select value={confirmacao} onChange={(e) => setConfirmacao(e.target.value)} aria-label="Resposta do paciente">
          <option value="">Resposta: todas</option>
          <option value="Pendente">Sem resposta</option>
          <option value="Confirmada">Confirmou</option>
          <option value="Cancelada">Não vai</option>
        </Select>
        <div className="flex items-center gap-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="De" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Até" />
        </div>
      </div>

      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{extrairMensagemDeErro(q.error)}</div>
      ) : null}

      <Tabela colunas={colunas} dados={q.data?.itens ?? []} chaveLinha={(n) => n.id} carregando={q.isLoading} vazio="Nenhum envio no filtro." />

      {q.data ? (
        <Paginacao pagina={pagina} tamanho={tamanho} total={q.data.total} tamanhos={TAMANHOS} aoMudarPagina={setPagina} aoMudarTamanho={setTamanho} />
      ) : null}

      {detalhe ? <DetalheEnvio id={detalhe} aoFechar={() => setDetalhe(null)} /> : null}
    </div>
  );
}
