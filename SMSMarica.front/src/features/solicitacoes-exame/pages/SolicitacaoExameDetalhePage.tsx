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
  Trash2,
  XCircle,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import { Input } from '@/shared/ui/Input';
import { Campo } from '@/shared/ui/Campo';
import { notificar } from '@/shared/ui/Notificacoes';
import {
  useAutorizarSolicitacao,
  useCancelarSolicitacao,
  useExcluirSolicitacao,
  useHistoricoSolicitacao,
  useReenviarComunicacao,
  useReenviarWorklist,
  useRegistrarContato,
  useSolicitacaoPorId,
} from '@/features/solicitacoes-exame/api/queries';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { Select } from '@/shared/ui/Select';
import type { HistoricoComunicacao } from '@/features/solicitacoes-exame/types';
import { ehFalhaExclusaoPacs } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';
import { ConfirmacaoBadge, canalConfirmacaoTexto } from '@/features/solicitacoes-exame/components/ConfirmacaoBadge';
import { BotaoDeclaracaoComparecimento } from '@/features/solicitacoes-exame/components/BotaoDeclaracaoComparecimento';
import { BotaoBaixarExameCompleto } from '@/features/solicitacoes-exame/components/BotaoBaixarExameCompleto';
import { BotaoLinkDownload } from '@/features/solicitacoes-exame/components/BotaoLinkDownload';
import { BotaoLinkAcesso } from '@/features/solicitacoes-exame/components/BotaoLinkAcesso';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import type { SolicitacaoExame, StatusSolicitacao } from '@/features/solicitacoes-exame/types';

const ETAPAS: StatusSolicitacao[] = ['Solicitada', 'Enviada', 'Recebida', 'EmExecucao', 'Realizada', 'Laudada'];

export function SolicitacaoExameDetalhePage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const detalhe = useSolicitacaoPorId(id ?? null);
  const cancelar = useCancelarSolicitacao();
  const reenviar = useReenviarWorklist();
  const excluir = useExcluirSolicitacao();

  const podeEditar = usePermissao('SolicitacoesExame', 'Edicao');
  const podeExcluir = usePermissao('SolicitacoesExame', 'Exclusao');

  const [modalCancelar, setModalCancelar] = useState(false);
  const [modalExcluir, setModalExcluir] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [erroExcluir, setErroExcluir] = useState<string | null>(null);
  const [forcarExclusao, setForcarExclusao] = useState(false);
  const [mostrarRaw, setMostrarRaw] = useState(false);

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
  const podeCancelar =
    podeEditar && (s.status === 'Solicitada' || s.status === 'Enviada' || s.status === 'Recebida');
  const podeReenviar = podeEditar && s.status === 'Solicitada' && !!s.erroIntegracaoPacs;
  const podeEditarForm = podeEditar && s.status === 'Solicitada';
  // Exclusão (admin): permitida em qualquer status, exceto exame iniciado/realizado/laudado.
  const podeExcluirAgora =
    podeExcluir && s.status !== 'EmExecucao' && s.status !== 'Realizada' && s.status !== 'Laudada';

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

  function abrirModalExcluir() {
    setErroExcluir(null);
    setForcarExclusao(false);
    setModalExcluir(true);
  }

  async function confirmarExclusao(force: boolean) {
    setErroExcluir(null);
    try {
      await excluir.mutateAsync({ id: s.id, force });
      navigate('/app/solicitacoes-exame');
    } catch (e) {
      setErroExcluir(extrairMensagemDeErro(e));
      // Falha ao remover do dcm4chee → habilita "Forçar" (limpa só a base local).
      if (!force && ehFalhaExclusaoPacs(e)) setForcarExclusao(true);
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
            <CodigoCopiavel codigo={s.accessionNumber} />
            <StatusBadgeSolicitacao status={s.status} />
            <ConfirmacaoBadge status={s.statusConfirmacao} />
            {(s.status === 'Realizada' || s.status === 'Laudada') && (
              <>
                <BotaoDeclaracaoComparecimento solicitacaoId={s.id} />
                <BotaoBaixarExameCompleto solicitacaoId={s.id} />
              </>
            )}
          </h1>
        </div>
        <div className="flex items-center gap-2">
          {(s.status === 'Realizada' || s.status === 'Laudada') ? (
            <>
              <BotaoLinkAcesso solicitacaoId={s.id} />
              <BotaoLinkDownload solicitacaoId={s.id} />
            </>
          ) : null}
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
          {podeExcluirAgora ? (
            <Button variante="outline" onClick={abrirModalExcluir}>
              <Trash2 className="mr-2 h-4 w-4 text-red-600" />
              Excluir
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
          <NomePacienteComResumo
            pacienteId={s.pacienteId}
            nome={s.pacienteNome}
            classNameNome="text-base font-medium text-gray-900"
          />
          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-gray-600">
            {s.pacienteCpf ? (
              <span className="inline-flex items-center gap-0.5">
                CPF
                <CodigoCopiavel
                  codigo={formatarCpf(s.pacienteCpf)}
                  valorCopiar={s.pacienteCpf.replace(/\D/g, '')}
                  dica="Copiar CPF (só números)"
                />
              </span>
            ) : null}
            {s.pacienteCns ? (
              <span className="inline-flex items-center gap-0.5">
                CNS
                <CodigoCopiavel
                  codigo={s.pacienteCns}
                  valorCopiar={s.pacienteCns.replace(/\D/g, '')}
                  dica="Copiar CNS (só números)"
                />
              </span>
            ) : null}
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
          <div className="text-base font-medium text-gray-900">{s.solicitanteNome || '—'}</div>
          {s.unidadeSolicitanteNome ? (
            <div className="mt-1 text-sm text-gray-600">Unidade solicitante: {s.unidadeSolicitanteNome}</div>
          ) : null}
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Regulação</h2>
          <div className="text-sm text-gray-700">
            {s.codigoSolicitacao ? (
              <>
                Código de Solicitação: <span className="font-medium">{s.codigoSolicitacao}</span>
                <br />
              </>
            ) : null}
            {s.chaveConfirmacao ? (
              <>
                Chave de Confirmação: <span className="font-medium">{s.chaveConfirmacao}</span>
                <br />
              </>
            ) : null}
            <span className="text-gray-600">
              Prioridade:{' '}
              {s.prioridade === 'Urgente' ? (
                <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                  ⚠ Urgente
                </span>
              ) : (
                <span className="font-medium">{s.prioridade}</span>
              )}
            </span>
            {s.justificativa ? (
              <>
                <br />
                <span className="text-gray-600">Justificativa: {s.justificativa}</span>
              </>
            ) : null}
          </div>
        </section>

        <CardAutorizacao s={s} />

        <CardHistoricoComunicacao solicitacaoId={s.id} />

        {s.observacoes ? (
          <section className="lg:col-span-2 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Observações</h2>
            <p className="text-sm text-gray-700">{s.observacoes}</p>
          </section>
        ) : null}

        <section className="lg:col-span-2 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Linha do tempo</h2>
          <ul className="space-y-1 text-sm text-gray-700">
            {s.dataSolicitacao ? <li>Solicitada em {formatarWallClock(s.dataSolicitacao)}</li> : null}
            {s.dataRegulacao ? <li>Regulada em {formatarWallClock(s.dataRegulacao)}</li> : null}
            <li>Cadastrada no sistema em {fmt(s.criadoEm)}</li>
            {s.dataAgendada ? <li>Agendada para {fmt(s.dataAgendada)}</li> : null}
            {s.iniciadoEm ? <li>Início da execução em {fmt(s.iniciadoEm)}</li> : null}
            {/* Data do exame = DICOM (StudyDate/StudyTime), a data real de execução. Só
                cai no RealizadoEm (hora de detecção) quando o PACS não trouxe a tag. */}
            {s.dataEstudo ? (
              <li>Exame realizado em {formatarWallClock(s.dataEstudo)}</li>
            ) : s.realizadoEm ? (
              <li>Exame realizado em {fmt(s.realizadoEm)} (detectado pelo PACS)</li>
            ) : null}
            {s.confirmadoEm ? (
              <li className="text-green-700">
                Paciente confirmou a presença {canalConfirmacaoTexto(s.confirmadoCanal)} em {fmt(s.confirmadoEm)}
              </li>
            ) : null}
            {s.confirmacaoCanceladaEm ? (
              <li className="text-red-700">
                Paciente informou que não poderá comparecer {canalConfirmacaoTexto(s.confirmadoCanal)} em{' '}
                {fmt(s.confirmacaoCanceladaEm)}
                {s.motivoCancelamentoPaciente ? ` — motivo: ${s.motivoCancelamentoPaciente}` : ''}
              </li>
            ) : null}
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

        {s.rawSisreg ? (
          <div className="lg:col-span-2">
            <button
              type="button"
              onClick={() => setMostrarRaw((v) => !v)}
              className="rounded border border-gray-200 bg-gray-50 px-2.5 py-1 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
              title="Dado bruto da importação do SISREG (TXT)"
            >
              {mostrarRaw ? 'Ocultar' : 'Raw data (SISREG)'}
            </button>
            {mostrarRaw ? (
              <div className="mt-2 rounded-lg border border-gray-200 bg-gray-50 p-4">
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Arquivo Agendamento (TXT) — linha de origem
                </h3>
                <dl className="grid grid-cols-1 gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
                  {parsearRawSisreg(s.rawSisreg).map((campo) => (
                    <div key={campo.rotulo} className="flex gap-2">
                      <dt className="min-w-[11rem] shrink-0 text-gray-500">{campo.rotulo}:</dt>
                      <dd className="break-all font-medium text-gray-800">{campo.valor || '—'}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            ) : null}
          </div>
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

      <Modal
        aberto={modalExcluir}
        aoFechar={() => setModalExcluir(false)}
        titulo="Excluir solicitação"
        descricao="Primeiro o item é removido da worklist do dcm4chee e confirmado; só então o pedido sai da base. Esta ação não pode ser desfeita."
      >
        <div className="space-y-3">
          {erroExcluir ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erroExcluir}</div>
          ) : null}
          {forcarExclusao ? (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              O dcm4chee não confirmou a remoção do item de worklist. Você pode <strong>forçar</strong> a exclusão —
              isso limpa apenas a base local e pode deixar o item órfão na worklist do equipamento.
            </div>
          ) : null}
          <div className="flex items-center justify-end gap-2">
            <Button variante="outline" onClick={() => setModalExcluir(false)}>
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

const fmt = formatarInstante;

/** Autorização presencial: recepção entra com a chave (só se o paciente tem número verificado). */
function CardAutorizacao({ s }: { s: SolicitacaoExame }) {
  const podeEditar = usePermissao('SolicitacoesExame', 'Edicao');
  const autorizar = useAutorizarSolicitacao();
  const [chave, setChave] = useState(s.chaveConfirmacao ?? '');
  const [erro, setErro] = useState<string | null>(null);

  const preRecebido = s.status === 'Solicitada' || s.status === 'Enviada';
  if (!preRecebido && !s.autorizadoEm) return null;

  async function submeter() {
    setErro(null);
    try {
      await autorizar.mutateAsync({ id: s.id, chaveConfirmacao: chave.trim() });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="lg:col-span-2 rounded-lg border border-orange-200 bg-orange-50/40 p-4 shadow-sm">
      <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Autorização (recepção)</h2>
      {s.autorizadoEm ? (
        <p className="flex items-center gap-2 text-sm text-green-700">
          <CheckCircle2 className="h-4 w-4" />
          Autorizado em {fmt(s.autorizadoEm)}
          {s.chaveConfirmacao ? ` — chave ${s.chaveConfirmacao}` : ''}. Envio ao PACS liberado.
        </p>
      ) : !podeEditar ? (
        <p className="text-sm text-gray-500">Você não tem permissão para autorizar.</p>
      ) : !s.pacienteContatoVerificado ? (
        <p className="flex items-center gap-2 text-sm text-amber-800">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          Paciente sem número verificado. Verifique o contato (no <strong>resumo do paciente</strong>, ao lado do
          nome) antes de autorizar.
        </p>
      ) : (
        <div className="flex items-end gap-2">
          <Campo label="Chave de autorização" htmlFor="chave-autorizacao" className="flex-1">
            <Input
              id="chave-autorizacao"
              value={chave}
              onChange={(e) => setChave(e.target.value)}
              placeholder="Chave da confirmação do SISREG"
            />
          </Campo>
          <Button onClick={submeter} disabled={autorizar.isPending}>
            {autorizar.isPending ? 'Autorizando…' : 'Autorizar e enviar'}
          </Button>
        </div>
      )}
      {erro ? <p className="mt-2 text-sm text-red-700">{erro}</p> : null}
    </section>
  );
}

const ROTULO_FINALIDADE: Record<HistoricoComunicacao['finalidade'], string> = {
  ConfirmacaoAgendamento: 'Confirmação de agendamento',
  ExameLiberado: 'Exame liberado',
  LaudoPronto: 'Laudo pronto',
};

const ROTULO_MEIO: Record<string, string> = {
  Ligacao: 'Ligação', WhatsApp: 'WhatsApp', Presencial: 'Presencial', Outro: 'Outro',
};
const ROTULO_RESULTADO: Record<string, string> = {
  Atendeu: 'Atendeu', NaoAtendeu: 'Não atendeu', CaixaPostal: 'Caixa postal',
  NumeroInvalido: 'Número inválido', Outro: 'Outro',
};

/** Histórico do processo: comunicações WhatsApp (com checks) + contatos manuais + registrar. */
function CardHistoricoComunicacao({ solicitacaoId }: { solicitacaoId: string }) {
  const podeEditar = usePermissao('SolicitacoesExame', 'Edicao');
  const q = useHistoricoSolicitacao(solicitacaoId);
  const registrar = useRegistrarContato();
  const reenviar = useReenviarComunicacao();
  const [aberto, setAberto] = useState(false);
  const [meio, setMeio] = useState('Ligacao');
  const [resultado, setResultado] = useState('NaoAtendeu');
  const [observacao, setObservacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  // Comunicação aguardando confirmação do reenvio (abre o ConfirmDialog).
  const [paraReenviar, setParaReenviar] = useState<HistoricoComunicacao | null>(null);

  const h = q.data;
  const vazio = !h || (h.comunicacoes.length === 0 && h.contatos.length === 0);

  async function confirmarReenvio() {
    if (!paraReenviar) return;
    try {
      await reenviar.mutateAsync({ id: solicitacaoId, comunicacaoId: paraReenviar.id });
      notificar('Comunicação reenviada — links anteriores revogados.', 'sucesso');
      setParaReenviar(null);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
      setParaReenviar(null);
    }
  }

  async function salvarContato() {
    setErro(null);
    try {
      await registrar.mutateAsync({ id: solicitacaoId, meio, resultado, observacao: observacao.trim() || null });
      setAberto(false);
      setObservacao('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="lg:col-span-2 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
          Comunicação com o paciente
        </h2>
        {podeEditar ? (
          <Button variante="outline" tamanho="sm" onClick={() => setAberto(true)}>
            Registrar contato
          </Button>
        ) : null}
      </div>

      {q.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : vazio ? (
        <p className="text-sm text-gray-500">Nenhuma comunicação registrada ainda.</p>
      ) : (
        <div className="space-y-3">
          {h!.comunicacoes.map((c) => (
            <div key={c.id} className="rounded-md border border-gray-100 bg-gray-50/60 px-3 py-2 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <ChecksComunicacao
                  chip={{ status: c.status, visualizado: c.visualizadoEm != null, motivo: c.motivoFalha }}
                  finalidade={c.finalidade === 'LaudoPronto' ? 'LaudoPronto' : 'ExameLiberado'}
                />
                <span className="font-medium text-gray-900">{ROTULO_FINALIDADE[c.finalidade]}</span>
                {c.telefone ? <span className="text-xs text-gray-500">→ {c.telefone}</span> : null}
                {c.tentativas > 1 ? <span className="text-xs text-gray-500">({c.tentativas} tentativas)</span> : null}
                {podeEditar && c.status !== 'Pendente' ? (
                  <button
                    type="button"
                    onClick={() => setParaReenviar(c)}
                    disabled={reenviar.isPending}
                    title="Reenviar: revoga os links anteriores e reenvia para o contato ATUAL do paciente"
                    className="ml-auto inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700 hover:bg-indigo-100 disabled:opacity-50"
                  >
                    <RotateCw className="h-3 w-3" /> Reenviar
                  </button>
                ) : null}
              </div>
              <div className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-gray-600">
                <span>Fila: {fmt(c.criadoEm)}</span>
                {c.enviadoEm ? <span>Enviada: {fmt(c.enviadoEm)}</span> : null}
                {c.entregueEm ? <span>Entregue: {fmt(c.entregueEm)}</span> : null}
                {c.lidoEm ? <span>Lida: {fmt(c.lidoEm)}</span> : null}
                {c.visualizadoEm ? <span className="text-sky-600">Visualizada: {fmt(c.visualizadoEm)}</span> : null}
              </div>
              {c.motivoFalha || c.erroMeta ? (
                <p className="mt-1 text-xs text-red-700">{c.motivoFalha ?? c.erroMeta}</p>
              ) : null}
            </div>
          ))}

          {h!.contatos.length > 0 ? (
            <ul className="space-y-1 border-t border-gray-100 pt-2 text-sm text-gray-700">
              {h!.contatos.map((c) => (
                <li key={c.id}>
                  <span className="font-medium">{ROTULO_MEIO[c.meio] ?? c.meio}</span>
                  {' — '}
                  {ROTULO_RESULTADO[c.resultado] ?? c.resultado} em {fmt(c.criadoEm)}
                  {c.registradoPorNome ? ` · por ${c.registradoPorNome}` : ''}
                  {c.observacao ? <span className="text-gray-500"> · {c.observacao}</span> : null}
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      )}

      <ConfirmDialog
        aberto={paraReenviar != null}
        titulo="Reenviar comunicação"
        mensagem={
          paraReenviar
            ? `Reenviar "${ROTULO_FINALIDADE[paraReenviar.finalidade]}"? Os links de acesso anteriores serão REVOGADOS ` +
              `(quem os recebeu perde o acesso, inclusive sessões abertas) e a mensagem será reconstruída ` +
              `com o contato ATUAL do paciente${paraReenviar.telefone ? ` (envio anterior: ${paraReenviar.telefone})` : ''}.`
            : ''
        }
        rotuloConfirmar="Revogar e reenviar"
        carregando={reenviar.isPending}
        aoConfirmar={() => void confirmarReenvio()}
        aoCancelar={() => setParaReenviar(null)}
      />

      <Modal aberto={aberto} aoFechar={() => setAberto(false)} titulo="Registrar contato com o paciente" largura="sm">
        <div className="space-y-3">
          <Campo label="Meio" htmlFor="contato-meio">
            <Select id="contato-meio" value={meio} onChange={(e) => setMeio(e.target.value)}>
              <option value="Ligacao">Ligação</option>
              <option value="WhatsApp">WhatsApp</option>
              <option value="Presencial">Presencial</option>
              <option value="Outro">Outro</option>
            </Select>
          </Campo>
          <Campo label="Resultado" htmlFor="contato-resultado">
            <Select id="contato-resultado" value={resultado} onChange={(e) => setResultado(e.target.value)}>
              <option value="Atendeu">Atendeu</option>
              <option value="NaoAtendeu">Não atendeu</option>
              <option value="CaixaPostal">Caixa postal</option>
              <option value="NumeroInvalido">Número inválido</option>
              <option value="Outro">Outro</option>
            </Select>
          </Campo>
          <Campo label="Observação (opcional)" htmlFor="contato-obs">
            <Input
              id="contato-obs"
              value={observacao}
              onChange={(e) => setObservacao(e.target.value)}
              placeholder="Ex.: pediu para ligar após as 14h"
              maxLength={500}
            />
          </Campo>
          {erro ? <p className="text-sm text-red-700">{erro}</p> : null}
          <div className="flex justify-end gap-2">
            <Button variante="ghost" onClick={() => setAberto(false)}>Cancelar</Button>
            <Button onClick={salvarContato} disabled={registrar.isPending}>
              {registrar.isPending ? 'Salvando…' : 'Salvar'}
            </Button>
          </div>
        </div>
      </Modal>
    </section>
  );
}

function formatarCpf(cpf: string) {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

// Rótulos do "Arquivo Agendamento (TXT)" do SISREG (38 campos ;-delimitados).
const ROTULOS_RAW_SISREG = [
  'Código Solicitação', 'Cód. proc. interno', 'Código SIGTAP', 'Procedimento',
  'CPF prof. executante', 'Prof. executante', 'Data atendimento', 'Hora atendimento',
  'Vaga (flag)', 'CNS paciente', 'Paciente', 'Nascimento', 'Idade', 'Registro',
  'Nome da mãe', 'Tipo logradouro', 'Logradouro', 'Complemento', 'Número', 'Bairro', 'CEP',
  'Telefone', 'Município', 'IBGE', 'Município (2)', 'IBGE (2)',
  'CNES unid. solicitante', 'Unidade solicitante', 'Sexo', 'Data solicitação', 'Op. solicitante',
  'Data aprovação', 'Op. autorizador', 'Valor SIGTAP', 'Situação', 'CID',
  'CPF médico solicitante', 'Médico solicitante',
];
// CPFs de profissionais → mascarados (XX***XX). Não expor CPF de médico/executante.
const IDX_CPF_MASCARAR = new Set([4, 36]);

function mascararCpf(v: string) {
  const d = v.replace(/\D/g, '');
  return d.length < 4 ? v : `${d.slice(0, 2)}***${d.slice(-2)}`;
}

function parsearRawSisreg(raw: string): { rotulo: string; valor: string }[] {
  const campos = raw.split(';');
  return ROTULOS_RAW_SISREG.map((rotulo, i) => {
    let valor = (campos[i] ?? '').trim();
    if (IDX_CPF_MASCARAR.has(i) && valor) valor = mascararCpf(valor);
    return { rotulo, valor };
  });
}
