import { useState } from 'react';
import { ArrowLeft, Ban, CheckCircle2, Hash, Undo2 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTemConsulta } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { formatarInstante } from '@/shared/lib/datas';

import { LinhaDoTempo } from '../components/LinhaDoTempo';
import { ModalRegistrarEnvio } from '../components/ModalRegistrarEnvio';
import { ROTULO_FLUXO, StatusRegulacaoBadge } from '../components/StatusRegulacaoBadge';
import {
  useAssumirSolicitacao,
  useCancelarSolicitacao,
  useDevolverSolicitacao,
  useEventosSolicitacao,
  useOkInterno,
  useRecusarSolicitacao,
  useRegistrarEnvio,
  useSolicitacao,
} from '../api/solicitacoesQueries';

/**
 * O caso inteiro numa tela: cabeçalho, formulário preenchido e a linha do tempo.
 *
 * <p><b>As ações mudam com quem está olhando</b> — a unidade cancela o que ainda é dela; o agente
 * assume, devolve e recusa. O que decide é a permissão, não um seletor: quem tem o módulo 48 vê
 * as ações da regulação. O backend recusa de qualquer forma; aqui é só não oferecer o que não vai
 * funcionar.</p>
 */
export function SolicitacaoDetalhePage() {
  const { id = '' } = useParams();
  const navegar = useNavigate();
  const ehAgente = useTemConsulta('RegulacaoTriagem');

  const solicitacao = useSolicitacao(id);
  const eventos = useEventosSolicitacao(id);

  const assumir = useAssumirSolicitacao();
  const devolver = useDevolverSolicitacao();
  const recusar = useRecusarSolicitacao();
  const cancelar = useCancelarSolicitacao();
  const registrarEnvio = useRegistrarEnvio();
  const okInterno = useOkInterno();

  const [erro, setErro] = useState<string | null>(null);
  const [modalEnvio, setModalEnvio] = useState(false);

  const s = solicitacao.data;

  async function comMotivo(
    rotulo: string,
    executar: (motivo: string) => Promise<unknown>,
  ) {
    // `prompt` é feio, e é honesto enquanto não há modal próprio: o motivo é obrigatório no
    // backend, e um botão que falha por falta dele seria pior do que um campo simples.
    const motivo = window.prompt(rotulo)?.trim();
    if (!motivo) return;
    setErro(null);
    try {
      await executar(motivo);
      await Promise.all([solicitacao.refetch(), eventos.refetch()]);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function executar(acao: () => Promise<unknown>) {
    setErro(null);
    try {
      await acao();
      await Promise.all([solicitacao.refetch(), eventos.refetch()]);
      return true;
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
      return false;
    }
  }

  if (solicitacao.isLoading) return <p className="text-sm text-slate-500">Carregando…</p>;
  if (!s) return <p className="text-sm text-slate-500">Solicitação não encontrada.</p>;

  const podeAgente = ehAgente && ['PendenteRegulacao', 'EmAnalise', 'Devolvida'].includes(s.status);
  const podeCancelar = ['Rascunho', 'PendenteRegulacao'].includes(s.status);

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <button
        type="button"
        onClick={() => navegar(-1)}
        className="flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
      >
        <ArrowLeft className="size-4" /> Voltar
      </button>

      <header className="rounded-lg border border-slate-200 bg-white p-4">
        <div className="flex flex-wrap items-start gap-3">
          <div className="min-w-0 flex-1">
            {/* Depois que existe número externo, é ele que identifica o caso lá fora. */}
            <p className="font-mono text-lg font-semibold text-slate-900">
              {s.numeroExterno ?? `PR-${s.numeroLocal}`}
            </p>
            <h1 className="text-lg font-semibold text-slate-900">{s.pacienteNome}</h1>
            <p className="text-sm text-slate-600">{s.procedimentoNome}</p>
          </div>
          <StatusRegulacaoBadge status={s.status} />
        </div>

        <dl className="mt-3 grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-xs text-slate-500">Fluxo</dt>
            <dd className="text-slate-800">{ROTULO_FLUXO[s.fluxo]}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500">Destino</dt>
            <dd className="text-slate-800">{s.sistemaDestino ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500">Aberta em</dt>
            <dd className="text-slate-800">{formatarInstante(s.criadoEm)}</dd>
          </div>
          {s.pacienteCpf && (
            <div>
              <dt className="text-xs text-slate-500">CPF</dt>
              <dd className="text-slate-800">{s.pacienteCpf}</dd>
            </div>
          )}
        </dl>

        {s.statusMotivo && (
          <p className="mt-3 rounded bg-amber-50 p-2 text-sm text-amber-900">{s.statusMotivo}</p>
        )}
      </header>

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}

      <div className="flex flex-wrap gap-2">
        {podeAgente && s.status === 'PendenteRegulacao' && (
          <Button
            // 409 quando outro agente chegou primeiro — a mensagem do backend já diz isso.
            onClick={() => executar(() => assumir.mutateAsync(id))}
            disabled={assumir.isPending}
          >
            <CheckCircle2 className="size-4" />
            Assumir
          </Button>
        )}

        {podeAgente && s.status === 'EmAnalise' && (
          <Button
            variante="secundaria"
            onClick={() =>
              comMotivo('O que a unidade precisa corrigir?', (motivo) =>
                devolver.mutateAsync({ id, motivo }),
              )
            }
            disabled={devolver.isPending}
          >
            <Undo2 className="size-4" />
            Devolver à unidade
          </Button>
        )}

        {podeAgente && ['EmAnalise', 'Devolvida'].includes(s.status) && (
          <Button
            variante="secundaria"
            onClick={() =>
              comMotivo('Motivo da recusa (a unidade vai ler):', (motivo) =>
                recusar.mutateAsync({ id, motivo }),
              )
            }
            disabled={recusar.isPending}
          >
            <Ban className="size-4" />
            Recusar
          </Button>
        )}

        {podeAgente && s.status === 'EmAnalise' && (
          <Button variante="secundaria" onClick={() => setModalEnvio(true)}>
            <Hash className="size-4" />
            Registrar envio
          </Button>
        )}

        {/* D-8: a interna já nasceu no SISREG pela unidade; o OK do agente é ação local. */}
        {podeAgente && s.status === 'PendenteRegulacao' && s.fluxo === 'Interno' && (
          <Button
            variante="secundaria"
            onClick={() => executar(() => okInterno.mutateAsync(id))}
            disabled={okInterno.isPending}
          >
            <CheckCircle2 className="size-4" />
            OK — já está no SISREG
          </Button>
        )}

        {podeCancelar && (
          <Button
            variante="secundaria"
            onClick={() =>
              comMotivo('Motivo do cancelamento:', (motivo) => cancelar.mutateAsync({ id, motivo }))
            }
            disabled={cancelar.isPending}
          >
            Cancelar solicitação
          </Button>
        )}
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="mb-3 text-sm font-semibold text-slate-900">Linha do tempo</h2>
        <LinhaDoTempo eventos={eventos.data} carregando={eventos.isLoading} />
      </section>

      <ModalRegistrarEnvio
        solicitacao={s}
        aberto={modalEnvio}
        aoFechar={() => setModalEnvio(false)}
        salvando={registrarEnvio.isPending}
        aoConfirmar={async (sistema, numeroExterno) => {
          const ok = await executar(() =>
            registrarEnvio.mutateAsync({ id, sistema, numeroExterno }),
          );
          // Fecha só quando deu certo: com número duplicado, o agente precisa ver o erro e
          // corrigir sem redigitar tudo.
          if (ok) setModalEnvio(false);
        }}
      />
    </div>
  );
}
