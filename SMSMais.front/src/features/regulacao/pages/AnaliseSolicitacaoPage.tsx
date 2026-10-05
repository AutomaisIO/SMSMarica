import { useState } from 'react';
import { ArrowLeft, Ban, CheckCircle2, Hash, Undo2 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';

import { AnexosSolicitacao } from '../components/AnexosSolicitacao';
import { CabecalhoSolicitacao } from '../components/CabecalhoSolicitacao';
import { LinhaDoTempo } from '../components/LinhaDoTempo';
import { MedicoPendenteCard } from '../components/MedicoPendenteCard';
import { ModalMotivo } from '../components/ModalMotivo';
import { ModalRegistrarEnvio } from '../components/ModalRegistrarEnvio';
import { ParaLancarNoSistema } from '../components/ParaLancarNoSistema';
import { RespostasRegras } from '../components/RespostasRegras';
import {
  useAssumirSolicitacao,
  useDevolverSolicitacao,
  useEventosSolicitacao,
  useOkInterno,
  useRecusarSolicitacao,
  useRegistrarEnvio,
  useSolicitacao,
} from '../api/solicitacoesQueries';
import type { StatusRegulacao } from '../tiposSolicitacao';

/** Em que pé o pedido está, dito do lado de quem regula — e o que cabe fazer agora. */
const SITUACAO_PARA_A_REGULACAO: Record<StatusRegulacao, string> = {
  Rascunho: 'Ainda é rascunho da unidade — não chegou à regulação.',
  PendenteRegulacao: 'Recebida da unidade. Ninguém assumiu ainda.',
  EmAnalise: 'Em análise. Confira o pedido e decida: aceitar, devolver para correção ou recusar.',
  Devolvida: 'Devolvida — aguardando a unidade corrigir e reenviar.',
  EnviandoAoSistema: 'Envio ao sistema de destino em andamento.',
  EnviadaAoSistema: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  EmFilaExterna: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  Agendada: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  FalhaEnvio: 'O envio ao sistema de destino falhou.',
  Concluida: 'Encerrada.',
  Cancelada: 'Encerrada.',
  Recusada: 'Encerrada.',
};

/**
 * A análise de uma solicitação recebida — a tela de quem avalia e regula (Gestão de fila).
 *
 * <p><b>É outra tela, e não o detalhe da unidade com mais botões.</b> As duas mostram o mesmo
 * caso, mas respondem perguntas diferentes: a unidade quer saber "em que pé está o meu pedido";
 * o regulador, "este pedido está em condição de seguir?". Juntas, quem tinha o módulo 48 via os
 * comandos da regulação até quando entrava pela fila da própria unidade.</p>
 *
 * <p>A rota é gateada por `RegulacaoTriagem` (48), e o backend recusa as ações de quem não o tem.
 * Aqui não há editar nem cancelar: isso é de quem pediu, e fica no detalhe da unidade.</p>
 */
export function AnaliseSolicitacaoPage() {
  const { id = '' } = useParams();
  const navegar = useNavigate();

  const solicitacao = useSolicitacao(id);
  const eventos = useEventosSolicitacao(id);

  const assumir = useAssumirSolicitacao();
  const devolver = useDevolverSolicitacao();
  const recusar = useRecusarSolicitacao();
  const registrarEnvio = useRegistrarEnvio();
  const okInterno = useOkInterno();

  const [erro, setErro] = useState<string | null>(null);
  const [modalEnvio, setModalEnvio] = useState(false);
  const [motivoDe, setMotivoDe] = useState<'devolver' | 'recusar' | null>(null);

  const s = solicitacao.data;

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

  const emAberto = ['PendenteRegulacao', 'EmAnalise', 'Devolvida'].includes(s.status);

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={() => navegar(-1)}
          className="flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
        >
          <ArrowLeft className="size-4" /> Voltar
        </button>
        <div className="flex items-center gap-1 text-sm text-slate-500">
          Gestão de fila — análise
          <AjudaManual artigo="regulacao-gestao-fila" secao="analise" />
        </div>
      </div>

      <CabecalhoSolicitacao s={s} />

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Decisão da regulação</h2>
        <p className="mt-1 text-sm text-slate-600">{SITUACAO_PARA_A_REGULACAO[s.status]}</p>

        {erro && <p className="mt-3 rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}

        {emAberto && (
          <div className="mt-3 flex flex-wrap gap-2">
            {s.status === 'PendenteRegulacao' && (
              <Button
                // 409 quando outro agente chegou primeiro — a mensagem do backend já diz isso.
                onClick={() => executar(() => assumir.mutateAsync(id))}
                disabled={assumir.isPending}
              >
                <CheckCircle2 className="size-4" />
                Assumir
              </Button>
            )}

            {/* D-8: a interna já nasceu no SISREG pela unidade; o OK do agente é ação local. */}
            {s.status === 'PendenteRegulacao' && s.fluxo === 'Interno' && (
              <Button
                variante="secundaria"
                onClick={() => executar(() => okInterno.mutateAsync(id))}
                disabled={okInterno.isPending}
              >
                <CheckCircle2 className="size-4" />
                OK — já está no SISREG
              </Button>
            )}

            {/* Aceitar não é um estado: é levar o pedido ao sistema de destino e trazer o número. */}
            {s.status === 'EmAnalise' && (
              <Button onClick={() => setModalEnvio(true)}>
                <Hash className="size-4" />
                Aceitar e registrar envio
              </Button>
            )}

            {s.status === 'EmAnalise' && (
              <Button variante="secundaria" onClick={() => setMotivoDe('devolver')}>
                <Undo2 className="size-4" />
                Devolver à unidade
              </Button>
            )}

            {['EmAnalise', 'Devolvida'].includes(s.status) && (
              <Button variante="secundaria" onClick={() => setMotivoDe('recusar')}>
                <Ban className="size-4" />
                Recusar
              </Button>
            )}
          </div>
        )}
      </section>

      <MedicoPendenteCard valorMedico={s.formulario?.medico_solicitante} podeResolver={emAberto} />

      <RespostasRegras solicitacaoId={id} />

      <AnexosSolicitacao solicitacaoId={id} />

      <ParaLancarNoSistema s={s} />

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

      {motivoDe === 'devolver' && (
        <ModalMotivo
          titulo="Devolver à unidade"
          pergunta="O que a unidade precisa corrigir?"
          rotuloConfirmar="Devolver"
          desfecho="Devolvida à unidade. Ela lê o que precisa corrigir no pedido e nas notificações, e reenvia quando acertar."
          aoConfirmar={(motivo) => devolver.mutateAsync({ id, motivo })}
          aoFechar={() => setMotivoDe(null)}
        />
      )}

      {motivoDe === 'recusar' && (
        <ModalMotivo
          titulo="Recusar a solicitação"
          pergunta="Motivo da recusa (a unidade vai ler)"
          rotuloConfirmar="Recusar"
          desfecho="Solicitação recusada. A unidade lê o motivo no pedido e nas notificações. A recusa encerra o pedido — para tentar de novo, a unidade abre outro."
          perigo
          aoConfirmar={(motivo) => recusar.mutateAsync({ id, motivo })}
          aoFechar={() => setMotivoDe(null)}
        />
      )}
    </div>
  );
}
