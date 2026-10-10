import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowLeft, Ban, CheckCircle2, Hash, Send, Undo2 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';

import { AjusteRiscoCid } from '../components/AjusteRiscoCid';
import { AnexosSolicitacao } from '../components/AnexosSolicitacao';
import { CabecalhoSolicitacao } from '../components/CabecalhoSolicitacao';
import { LinhaDoTempo } from '../components/LinhaDoTempo';
import { MedicoPendenteCard } from '../components/MedicoPendenteCard';
import { ModalEnviarAoSistema } from '../components/ModalEnviarAoSistema';
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
  EnviandoAoSistema: 'Envio ao sistema de destino em andamento — a plataforma está preenchendo e gravando lá.',
  EnviadaAoSistema: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  EmFilaExterna: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  Agendada: 'Já está no sistema de destino — o acompanhamento vem do espelho de lá.',
  FalhaEnvio: 'O envio ao sistema de destino falhou. Leia o motivo abaixo antes de tentar de novo.',
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
 * Aqui não há cancelar nem editar o pedido inteiro: isso é de quem pediu, e fica no detalhe da
 * unidade. A exceção é a Classificação de risco e o CID, que o regulador ajusta antes de enviar.</p>
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
  const [modalAutomatico, setModalAutomatico] = useState(false);
  const [motivoDe, setMotivoDe] = useState<'devolver' | 'recusar' | null>(null);

  const s = solicitacao.data;

  // Enviando ao SER/SERNIT (outra aba, outra pessoa, ou quem fechou o modal): o desfecho aparece sozinho.
  const enviando = s?.status === 'EnviandoAoSistema';
  const { refetch: recarregar } = solicitacao;
  useEffect(() => {
    if (!enviando) return;
    const t = window.setInterval(() => void recarregar(), 10_000);
    return () => window.clearInterval(t);
  }, [enviando, recarregar]);

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
  // O envio automático existe para o SER e o SERNIT (ADR-0069); o SISREG segue pelo "registrar
  // envio" (o agente lança na tela do sistema e digita o número).
  const automatico =
    s.fluxo === 'Externo' && (s.sistemaDestino === 'Ser' || s.sistemaDestino === 'Sernit')
      ? s.sistemaDestino
      : null;
  const nomeSistema = automatico === 'Sernit' ? 'SERNIT' : 'SER';

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

        {s.status === 'FalhaEnvio' && s.statusMotivo && (
          <p className="mt-3 flex gap-2 rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            <span>{s.statusMotivo}</span>
          </p>
        )}

        {s.status === 'FalhaEnvio' && (
          <div className="mt-3 flex flex-wrap gap-2">
            {automatico && (
              <Button onClick={() => setModalAutomatico(true)}>
                <Send className="size-4" />
                Enviar ao {nomeSistema} de novo
              </Button>
            )}
            {/* O Gravar pode ter chegado ao sistema: quem conferiu lá e achou o pedido registra o número. */}
            <Button variante="secundaria" onClick={() => setModalEnvio(true)}>
              <Hash className="size-4" />
              Registrar número (já está no sistema)
            </Button>
          </div>
        )}

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

            {/* Aceitar não é um estado: é levar o pedido ao sistema de destino e trazer o número.
                Para o SER e o SERNIT, a plataforma faz o lançamento; o "registrar" fica para quem já lançou à mão. */}
            {s.status === 'EmAnalise' && automatico && (
              <Button onClick={() => setModalAutomatico(true)}>
                <Send className="size-4" />
                Aceitar e enviar ao {nomeSistema}
              </Button>
            )}
            {s.status === 'EmAnalise' && (
              <Button variante={automatico ? 'secundaria' : 'primaria'} onClick={() => setModalEnvio(true)}>
                <Hash className="size-4" />
                {automatico ? `Já lancei no ${nomeSistema} — registrar número` : 'Aceitar e registrar envio'}
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

      <AjusteRiscoCid s={s} aoSalvar={() => void Promise.all([solicitacao.refetch(), eventos.refetch()])} />

      <MedicoPendenteCard
        valorMedico={s.formulario?.medico_solicitante}
        podeResolver={emAberto}
        cadastroPeloEnvio={!!automatico}
      />

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

      {automatico && (
        <ModalEnviarAoSistema
          sistema={automatico}
          solicitacaoId={id}
          pacienteId={solicitacao.data?.pacienteId}
          aberto={modalAutomatico}
          aoFechar={() => {
            setModalAutomatico(false);
            void Promise.all([solicitacao.refetch(), eventos.refetch()]);
          }}
        />
      )}

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
