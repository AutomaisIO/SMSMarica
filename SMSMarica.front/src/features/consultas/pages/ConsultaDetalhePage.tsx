import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  CalendarClock,
  Loader2,
  MessageSquarePlus,
  Phone,
  RefreshCw,
  Send,
  Stethoscope,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, formatarInstanteData } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { ConfirmacaoBadge } from '@/features/solicitacoes-exame/components/ConfirmacaoBadge';
import { RawSisregDisclosure } from '@/features/solicitacoes-exame/components/RawSisregDisclosure';
import type { StatusConfirmacaoPaciente } from '@/features/solicitacoes-exame/types';
import {
  useHistoricoConsulta,
  useObterConsulta,
  useRegistrarContatoConsulta,
  useReenviarComunicacaoConsulta,
} from '@/features/consultas/api/queries';

const STATUS_COR: Record<string, string> = {
  Solicitada: 'bg-amber-50 text-amber-700 ring-amber-200',
  Realizada: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Cancelada: 'bg-red-50 text-red-700 ring-red-200',
};

function StatusConsultaBadge({ status }: { status: string }) {
  const cor = STATUS_COR[status] ?? 'bg-gray-100 text-gray-700 ring-gray-200';
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cor}`}>
      {status}
    </span>
  );
}

const ROTULO_CATEGORIA: Record<string, string> = {
  Consulta: 'Consulta',
  Laboratorio: 'Laboratório',
  GraficoFuncional: 'Gráfico/funcional',
  Endoscopia: 'Endoscopia',
  Cirurgia: 'Cirurgia',
  Outro: 'Outro',
};

const ROTULO_MEIO: Record<string, string> = {
  Ligacao: 'Ligação',
  WhatsApp: 'WhatsApp',
  Presencial: 'Presencial',
  Outro: 'Outro',
};
const ROTULO_RESULTADO: Record<string, string> = {
  Atendeu: 'Atendeu',
  NaoAtendeu: 'Não atendeu',
  CaixaPostal: 'Caixa postal',
  NumeroInvalido: 'Número inválido',
  Outro: 'Outro',
};

/**
 * Detalhe da solicitação de CONSULTA — mesma tela de Exames nas partes que se aplicam:
 * dados da solicitação, histórico de comunicação (WhatsApp de confirmação, com reenvio),
 * registro de contato manual e linha do tempo. Sem accession/worklist/laudo/autorização,
 * que são do exame de imagem (ADR-0021).
 */
export function ConsultaDetalhePage() {
  const { id = '' } = useParams();
  const consulta = useObterConsulta(id);
  const c = consulta.data;

  return (
    <div className="space-y-5">
      <Link to="/app/consultas" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-800">
        <ArrowLeft className="h-4 w-4" /> Voltar para Consultas
      </Link>

      {consulta.isPending ? (
        <div className="py-10 text-center text-gray-400">
          <Loader2 className="inline h-5 w-5 animate-spin" /> Carregando…
        </div>
      ) : consulta.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(consulta.error)}
        </div>
      ) : !c ? (
        <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-6 text-center text-gray-500">
          Consulta não encontrada.
        </div>
      ) : (
        <>
          <header>
            <h1 className="flex flex-wrap items-center gap-3 text-2xl font-semibold text-gray-900">
              <Stethoscope className="h-6 w-6 text-primary-600" />
              {c.codigoSolicitacao ? (
                <CodigoCopiavel codigo={c.codigoSolicitacao} />
              ) : (
                <span className="text-gray-400">sem nº</span>
              )}
              <StatusConsultaBadge status={c.status} />
              <ConfirmacaoBadge status={c.statusConfirmacao as StatusConfirmacaoPaciente} />
            </h1>
            <p className="mt-1 flex flex-wrap items-center gap-1.5 text-sm text-gray-600">
              <NomePacienteComResumo pacienteId={c.pacienteId} nome={c.pacienteNome} classNameNome="font-medium" />
              <span className="text-gray-400">·</span>
              {ROTULO_CATEGORIA[c.categoria] ?? c.categoria}
              {c.especialidade ? ` · ${c.especialidade}` : ''}
            </p>
          </header>

          <div className="grid gap-5 lg:grid-cols-2">
            <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <h2 className="mb-3 text-sm font-semibold text-gray-900">Dados da solicitação</h2>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
                <Item rotulo="Paciente" valor={c.pacienteNome} />
                <Item rotulo="CPF" valor={c.pacienteCpf} />
                <Item rotulo="CNS" valor={c.pacienteCns} />
                <Item rotulo="SIGTAP" valor={c.procedimentoSigtapCodigo} />
                <Item rotulo="Unidade executante" valor={c.unidadeExecutanteNome} />
                <Item rotulo="Unidade solicitante" valor={c.unidadeSolicitanteNome} />
                <Item rotulo="Solicitante" valor={c.solicitanteNome} />
                <Item rotulo="Status" valor={c.status} />
                {c.observacoes ? (
                  <div className="col-span-2">
                    <Item rotulo="Observações" valor={c.observacoes} />
                  </div>
                ) : null}
              </dl>
            </section>

            <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <h2 className="mb-3 flex items-center gap-1.5 text-sm font-semibold text-gray-900">
                <CalendarClock className="h-4 w-4 text-gray-400" /> Linha do tempo
              </h2>
              <ul className="space-y-2 text-sm">
                <Evento rotulo="Solicitação" valor={c.dataSolicitacao} />
                <Evento rotulo="Regulação" valor={c.dataRegulacao} />
                <Evento rotulo="Agendada" valor={formatarInstanteData(c.dataAgendada)} />
              </ul>
            </section>
          </div>

          <CardComunicacao consultaId={c.id} />

          {c.rawSisreg ? <RawSisregDisclosure raw={c.rawSisreg} /> : null}
        </>
      )}
    </div>
  );
}

/** Histórico de comunicação (WhatsApp de confirmação, com reenvio) + registro de contato manual. */
function CardComunicacao({ consultaId }: { consultaId: string }) {
  const historico = useHistoricoConsulta(consultaId);
  const reenviar = useReenviarComunicacaoConsulta();
  const registrar = useRegistrarContatoConsulta();

  const [registrando, setRegistrando] = useState(false);
  const [meio, setMeio] = useState('Ligacao');
  const [resultado, setResultado] = useState('NaoAtendeu');
  const [observacao, setObservacao] = useState('');
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null);

  async function confirmarReenvio(comunicacaoId: string) {
    if (!window.confirm('Reenviar? Isso revoga os links e sessões anteriores e reconstrói o envio com o contato atual.'))
      return;
    setAviso(null);
    try {
      await reenviar.mutateAsync({ id: consultaId, comunicacaoId });
      setAviso({ ok: true, texto: 'Comunicação reenviada com o contato atual.' });
    } catch (e) {
      setAviso({ ok: false, texto: extrairMensagemDeErro(e) });
    }
  }

  async function salvarContato() {
    setAviso(null);
    try {
      await registrar.mutateAsync({ id: consultaId, meio, resultado, observacao: observacao.trim() || null });
      setRegistrando(false);
      setObservacao('');
      setAviso({ ok: true, texto: 'Contato registrado.' });
    } catch (e) {
      setAviso({ ok: false, texto: extrairMensagemDeErro(e) });
    }
  }

  const h = historico.data;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="flex items-center gap-1.5 text-sm font-semibold text-gray-900">
          <Send className="h-4 w-4 text-gray-400" /> Comunicação com o paciente
        </h2>
        <Button variante="outline" onClick={() => setRegistrando(true)}>
          <MessageSquarePlus className="h-4 w-4" /> Registrar contato
        </Button>
      </div>

      {aviso ? (
        <div
          className={`mb-3 rounded-md border px-3 py-2 text-sm ${
            aviso.ok ? 'border-emerald-100 bg-emerald-50 text-emerald-800' : 'border-red-100 bg-red-50 text-red-700'
          }`}
        >
          {aviso.texto}
        </div>
      ) : null}

      {historico.isPending ? (
        <p className="text-sm text-gray-400">
          <Loader2 className="inline h-4 w-4 animate-spin" /> Carregando…
        </p>
      ) : (
        <div className="space-y-4">
          <div>
            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">Envios automáticos</div>
            {h && h.comunicacoes.length > 0 ? (
              <ul className="divide-y divide-gray-100">
                {h.comunicacoes.map((com) => (
                  <li key={com.id} className="flex items-center justify-between gap-2 py-2 text-sm">
                    <div className="flex items-center gap-2">
                      <ChecksComunicacao
                        chip={{ status: com.status, visualizado: com.visualizadoEm != null, motivo: com.motivoFalha }}
                        finalidade="ConfirmacaoAgendamento"
                      />
                      <span className="text-gray-700">Confirmação de agendamento</span>
                      <span className="text-xs text-gray-400">{formatarInstante(com.criadoEm)}</span>
                    </div>
                    <button
                      type="button"
                      onClick={() => confirmarReenvio(com.id)}
                      disabled={reenviar.isPending}
                      className="inline-flex items-center gap-1 text-xs text-primary-700 underline hover:text-primary-900"
                    >
                      <RefreshCw className="h-3.5 w-3.5" /> Reenviar
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-gray-400">Nenhum envio automático ainda.</p>
            )}
          </div>

          <div>
            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">Contatos manuais</div>
            {h && h.contatos.length > 0 ? (
              <ul className="divide-y divide-gray-100">
                {h.contatos.map((ct) => (
                  <li key={ct.id} className="flex items-start gap-2 py-2 text-sm">
                    <Phone className="mt-0.5 h-3.5 w-3.5 shrink-0 text-gray-400" />
                    <div>
                      <span className="text-gray-800">
                        {ROTULO_MEIO[ct.meio] ?? ct.meio} — {ROTULO_RESULTADO[ct.resultado] ?? ct.resultado}
                      </span>
                      {ct.observacao ? <span className="text-gray-500"> · {ct.observacao}</span> : null}
                      <div className="text-xs text-gray-400">
                        {formatarInstante(ct.criadoEm)}
                        {ct.registradoPorNome ? ` · ${ct.registradoPorNome}` : ''}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-gray-400">Nenhum contato registrado.</p>
            )}
          </div>
        </div>
      )}

      <Modal aberto={registrando} aoFechar={() => setRegistrando(false)} titulo="Registrar contato">
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <label className="text-sm">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-gray-500">Meio</span>
              <Select value={meio} onChange={(e) => setMeio(e.target.value)}>
                {Object.entries(ROTULO_MEIO).map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </Select>
            </label>
            <label className="text-sm">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-gray-500">Resultado</span>
              <Select value={resultado} onChange={(e) => setResultado(e.target.value)}>
                {Object.entries(ROTULO_RESULTADO).map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </Select>
            </label>
          </div>
          <label className="block text-sm">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-gray-500">Observação</span>
            <textarea
              value={observacao}
              onChange={(e) => setObservacao(e.target.value)}
              rows={3}
              className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
              placeholder="Ex.: liguei, não atendeu; retornar amanhã"
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variante="ghost" onClick={() => setRegistrando(false)}>
              Cancelar
            </Button>
            <Button onClick={salvarContato} disabled={registrar.isPending}>
              {registrar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Salvar'}
            </Button>
          </div>
        </div>
      </Modal>
    </section>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor: string | null | undefined }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-400">{rotulo}</dt>
      <dd className="text-gray-900">{valor || '—'}</dd>
    </div>
  );
}

function Evento({ rotulo, valor }: { rotulo: string; valor: string | null | undefined }) {
  return (
    <li className="flex items-center justify-between">
      <span className="text-gray-500">{rotulo}</span>
      <span className="text-gray-900">{valor || '—'}</span>
    </li>
  );
}
