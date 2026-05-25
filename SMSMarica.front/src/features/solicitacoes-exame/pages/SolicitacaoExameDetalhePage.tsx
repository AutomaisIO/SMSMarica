import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  ClipboardCheck,
  Edit2,
  Loader2,
  RotateCw,
  ScanLine,
  XCircle,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { Input } from '@/shared/ui/Input';
import { Campo } from '@/shared/ui/Campo';
import {
  useCancelarSolicitacao,
  useReenviarWorklist,
  useSolicitacaoPorId,
} from '@/features/solicitacoes-exame/api/queries';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';
import type { StatusSolicitacao } from '@/features/solicitacoes-exame/types';

const ETAPAS: StatusSolicitacao[] = ['Solicitada', 'Enviada', 'Agendada', 'EmExecucao', 'Realizada', 'Laudada'];

export function SolicitacaoExameDetalhePage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const detalhe = useSolicitacaoPorId(id ?? null);
  const cancelar = useCancelarSolicitacao();
  const reenviar = useReenviarWorklist();

  const podeEditar = usePermissao('SolicitacoesExame', 'Edicao');

  const [modalCancelar, setModalCancelar] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  if (detalhe.isPending) {
    return (
      <div className="flex items-center justify-center py-20 text-gray-500">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Carregando…
      </div>
    );
  }
  if (detalhe.isError || !detalhe.data) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(detalhe.error) || 'Solicitação não encontrada.'}
      </div>
    );
  }

  const s = detalhe.data;
  const cancelado = s.status === 'Cancelada';
  const podeCancelar = podeEditar && (s.status === 'Solicitada' || s.status === 'Agendada');
  const podeReenviar = podeEditar && s.status === 'Solicitada' && !!s.erroIntegracaoPacs;
  const podeEditarForm = podeEditar && s.status === 'Solicitada';

  async function confirmarCancelamento() {
    setErro(null);
    try {
      await cancelar.mutateAsync({ id: s.id, motivo });
      setModalCancelar(false);
      setMotivo('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function reenviarAgora() {
    setErro(null);
    try {
      await reenviar.mutateAsync(s.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={() => navigate('/app/solicitacoes-exame')}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar
          </button>
          <h1 className="mt-1 flex items-center gap-3 text-2xl font-semibold text-gray-900">
            <ClipboardCheck className="h-6 w-6 text-primary-600" />
            <span className="font-mono">{s.accessionNumber}</span>
            <StatusBadgeSolicitacao status={s.status} />
          </h1>
        </div>
        <div className="flex items-center gap-2">
          {podeReenviar ? (
            <Button onClick={reenviarAgora} disabled={reenviar.isPending} variante="outline">
              {reenviar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RotateCw className="mr-2 h-4 w-4" />}
              Reenviar worklist
            </Button>
          ) : null}
          {podeEditarForm ? (
            <Button variante="outline" onClick={() => navigate(`/app/solicitacoes-exame/${s.id}/editar`)}>
              <Edit2 className="mr-2 h-4 w-4" />
              Editar
            </Button>
          ) : null}
          {podeCancelar ? (
            <Button variante="danger" onClick={() => setModalCancelar(true)}>
              <XCircle className="mr-2 h-4 w-4" />
              Cancelar
            </Button>
          ) : null}
        </div>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {s.erroIntegracaoPacs ? (
        <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
          <div>
            <div className="font-medium">Última tentativa de envio ao PACS falhou.</div>
            <div className="text-xs">{s.erroIntegracaoPacs}</div>
            <div className="mt-1 text-xs">
              Tentativas: <strong>{s.tentativasEnvio}</strong>
              {s.ultimaTentativaEm ? <> · última {fmt(s.ultimaTentativaEm)}</> : null}
              {s.proximaTentativaEm ? <> · próxima {fmt(s.proximaTentativaEm)}</> : null}
            </div>
          </div>
        </div>
      ) : null}

      {!s.erroIntegracaoPacs &&
      (s.status === 'Solicitada' || s.status === 'Enviada') &&
      s.tentativasEnvio > 0 ? (
        <div className="rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800">
          Envio em andamento — {s.tentativasEnvio} tentativa(s).
          {s.ultimaTentativaEm ? <> Última em {fmt(s.ultimaTentativaEm)}.</> : null}
          {s.proximaTentativaEm ? <> Próxima em {fmt(s.proximaTentativaEm)}.</> : null}
        </div>
      ) : null}

      {/* Timeline */}
      {!cancelado ? (
        <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <div className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">Ciclo do exame</div>
          <ol className="flex flex-wrap items-center gap-2">
            {ETAPAS.map((etapa, i) => {
              const idxAtual = ETAPAS.indexOf(s.status as StatusSolicitacao);
              const atingida = idxAtual >= 0 && i <= idxAtual;
              return (
                <li key={etapa} className="flex items-center gap-2">
                  <span
                    className={
                      atingida
                        ? 'inline-flex h-7 items-center gap-1.5 rounded-full bg-primary-50 px-3 text-xs font-medium text-primary-700 ring-1 ring-primary-200'
                        : 'inline-flex h-7 items-center gap-1.5 rounded-full bg-gray-50 px-3 text-xs font-medium text-gray-500 ring-1 ring-gray-200'
                    }
                  >
                    {atingida ? <CheckCircle2 className="h-3.5 w-3.5" /> : null}
                    {etapa}
                  </span>
                  {i < ETAPAS.length - 1 ? <span className="text-gray-300">→</span> : null}
                </li>
              );
            })}
          </ol>
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Paciente</h2>
          <div className="text-base font-medium text-gray-900">{s.pacienteNome}</div>
          <div className="mt-1 text-sm text-gray-600">
            {s.pacienteCpf ? <>CPF {formatarCpf(s.pacienteCpf)} · </> : null}
            {s.pacienteCns ? <>CNS {s.pacienteCns}</> : null}
          </div>
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Exame</h2>
          <div className="text-base font-medium text-gray-900">{s.tipoExameNome}</div>
          <div className="mt-1 flex flex-wrap gap-x-3 text-sm text-gray-600">
            <span className="uppercase">{s.modalidadeDicom}</span>
            <span>{s.unidadeNome}</span>
            <span className="font-mono text-xs">Study {s.studyInstanceUID}</span>
          </div>
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Solicitante</h2>
          <div className="text-base font-medium text-gray-900">{s.solicitanteNome}</div>
          <div className="mt-1 text-sm text-gray-600">
            CRM {s.solicitanteUfCrm}/{s.solicitanteCrm}
            {s.solicitanteUsuarioId ? ' · Cadastrado no sistema' : ' · Médico externo'}
          </div>
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Regulação</h2>
          <div className="text-sm text-gray-700">
            {s.numeroRegulacaoSus ? (
              <>
                Nº SUS: <span className="font-medium">{s.numeroRegulacaoSus}</span>
                <br />
              </>
            ) : null}
            <span className="text-gray-600">Prioridade: {s.prioridade}</span>
            {s.justificativa ? (
              <>
                <br />
                <span className="text-gray-600">Justificativa: {s.justificativa}</span>
              </>
            ) : null}
          </div>
        </section>

        {s.observacoes ? (
          <section className="lg:col-span-2 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Observações</h2>
            <p className="text-sm text-gray-700">{s.observacoes}</p>
          </section>
        ) : null}

        <section className="lg:col-span-2 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Linha do tempo</h2>
          <ul className="space-y-1 text-sm text-gray-700">
            <li>Solicitada em {fmt(s.criadoEm)}</li>
            {s.dataAgendada ? <li>Agendada para {fmt(s.dataAgendada)}</li> : null}
            {s.iniciadoEm ? <li>Início da execução em {fmt(s.iniciadoEm)}</li> : null}
            {s.realizadoEm ? <li>Exame realizado em {fmt(s.realizadoEm)} (detectado pelo PACS)</li> : null}
            {s.canceladoEm ? (
              <li className="text-red-700">
                Cancelada em {fmt(s.canceladoEm)} — {s.motivoCancelamento}
              </li>
            ) : null}
          </ul>
        </section>

        {s.status === 'Realizada' || s.status === 'Laudada' ? (
          <section className="lg:col-span-2 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
            <div className="flex items-center gap-2">
              <ScanLine className="h-4 w-4" />
              Exame disponível no PACS — abra em <strong>Exames de Imagem</strong> para visualizar/laudar.
            </div>
          </section>
        ) : null}
      </div>

      <Modal
        aberto={modalCancelar}
        aoFechar={() => setModalCancelar(false)}
        titulo="Cancelar solicitação"
        descricao="O cancelamento também notifica o dcm4chee. Informe o motivo."
      >
        <div className="space-y-3">
          <Campo label="Motivo" htmlFor="motivo">
            <Input id="motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} autoFocus />
          </Campo>
          <div className="flex items-center justify-end gap-2">
            <Button variante="outline" onClick={() => setModalCancelar(false)}>
              Voltar
            </Button>
            <Button variante="danger" disabled={!motivo.trim() || cancelar.isPending} onClick={confirmarCancelamento}>
              {cancelar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Confirmar cancelamento
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

function fmt(iso: string) {
  return new Date(iso).toLocaleString('pt-BR');
}

function formatarCpf(cpf: string) {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}
