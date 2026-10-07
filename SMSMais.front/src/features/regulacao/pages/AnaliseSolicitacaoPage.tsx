import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowLeft, Ban, CheckCircle2, Hash, Send, Undo2 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';

import { AnexosSolicitacao } from '../components/AnexosSolicitacao';
import { CabecalhoSolicitacao } from '../components/CabecalhoSolicitacao';
import { LinhaDoTempo } from '../components/LinhaDoTempo';
import { MedicoPendenteCard } from '../components/MedicoPendenteCard';
import { ModalEnviarAoSer } from '../components/ModalEnviarAoSer';
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

/** Em que pÃ© o pedido estÃ¡, dito do lado de quem regula â€” e o que cabe fazer agora. */
const SITUACAO_PARA_A_REGULACAO: Record<StatusRegulacao, string> = {
  Rascunho: 'Ainda Ã© rascunho da unidade â€” nÃ£o chegou Ã  regulaÃ§Ã£o.',
  PendenteRegulacao: 'Recebida da unidade. NinguÃ©m assumiu ainda.',
  EmAnalise: 'Em anÃ¡lise. Confira o pedido e decida: aceitar, devolver para correÃ§Ã£o ou recusar.',
  Devolvida: 'Devolvida â€” aguardando a unidade corrigir e reenviar.',
  EnviandoAoSistema: 'Envio ao sistema de destino em andamento â€” a plataforma estÃ¡ preenchendo e gravando no SER.',
  EnviadaAoSistema: 'JÃ¡ estÃ¡ no sistema de destino â€” o acompanhamento vem do espelho de lÃ¡.',
  EmFilaExterna: 'JÃ¡ estÃ¡ no sistema de destino â€” o acompanhamento vem do espelho de lÃ¡.',
  Agendada: 'JÃ¡ estÃ¡ no sistema de destino â€” o acompanhamento vem do espelho de lÃ¡.',
  FalhaEnvio: 'O envio ao sistema de destino falhou. Leia o motivo abaixo antes de tentar de novo.',
  Concluida: 'Encerrada.',
  Cancelada: 'Encerrada.',
  Recusada: 'Encerrada.',
};

/**
 * A anÃ¡lise de uma solicitaÃ§Ã£o recebida â€” a tela de quem avalia e regula (GestÃ£o de fila).
 *
 * <p><b>Ã‰ outra tela, e nÃ£o o detalhe da unidade com mais botÃµes.</b> As duas mostram o mesmo
 * caso, mas respondem perguntas diferentes: a unidade quer saber "em que pÃ© estÃ¡ o meu pedido";
 * o regulador, "este pedido estÃ¡ em condiÃ§Ã£o de seguir?". Juntas, quem tinha o mÃ³dulo 48 via os
 * comandos da regulaÃ§Ã£o atÃ© quando entrava pela fila da prÃ³pria unidade.</p>
 *
 * <p>A rota Ã© gateada por `RegulacaoTriagem` (48), e o backend recusa as aÃ§Ãµes de quem nÃ£o o tem.
 * Aqui nÃ£o hÃ¡ editar nem cancelar: isso Ã© de quem pediu, e fica no detalhe da unidade.</p>
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
  const [modalSer, setModalSer] = useState(false);
  const [motivoDe, setMotivoDe] = useState<'devolver' | 'recusar' | null>(null);

  const s = solicitacao.data;

  // Enviando ao SER (outra aba, outra pessoa, ou quem fechou o modal): o desfecho aparece sozinho.
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

  if (solicitacao.isLoading) return <p className="text-sm text-slate-500">Carregandoâ€¦</p>;
  if (!s) return <p className="text-sm text-slate-500">SolicitaÃ§Ã£o nÃ£o encontrada.</p>;

  const emAberto = ['PendenteRegulacao', 'EmAnalise', 'Devolvida'].includes(s.status);
  // O envio automÃ¡tico sÃ³ existe para o SER por enquanto; os outros destinos seguem pelo
  // "registrar envio" (o agente lanÃ§a na tela do sistema e digita o nÃºmero).
  const vaiAoSer = s.sistemaDestino === 'Ser' && s.fluxo === 'Externo';

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
          GestÃ£o de fila â€” anÃ¡lise
          <AjudaManual artigo="regulacao-gestao-fila" secao="analise" />
        </div>
      </div>

      <CabecalhoSolicitacao s={s} />

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">DecisÃ£o da regulaÃ§Ã£o</h2>
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
            {vaiAoSer && (
              <Button onClick={() => setModalSer(true)}>
                <Send className="size-4" />
                Enviar ao SER de novo
              </Button>
            )}
            {/* O Gravar pode ter chegado ao SER: quem conferiu lÃ¡ e achou o pedido registra o nÃºmero. */}
            <Button variante="secundaria" onClick={() => setModalEnvio(true)}>
              <Hash className="size-4" />
              Registrar nÃºmero (jÃ¡ estÃ¡ no sistema)
            </Button>
          </div>
        )}

        {emAberto && (
          <div className="mt-3 flex flex-wrap gap-2">
            {s.status === 'PendenteRegulacao' && (
              <Button
                // 409 quando outro agente chegou primeiro â€” a mensagem do backend jÃ¡ diz isso.
                onClick={() => executar(() => assumir.mutateAsync(id))}
                disabled={assumir.isPending}
              >
                <CheckCircle2 className="size-4" />
                Assumir
              </Button>
            )}

            {/* D-8: a interna jÃ¡ nasceu no SISREG pela unidade; o OK do agente Ã© aÃ§Ã£o local. */}
            {s.status === 'PendenteRegulacao' && s.fluxo === 'Interno' && (
              <Button
                variante="secundaria"
                onClick={() => executar(() => okInterno.mutateAsync(id))}
                disabled={okInterno.isPending}
              >
                <CheckCircle2 className="size-4" />
                OK â€” jÃ¡ estÃ¡ no SISREG
              </Button>
            )}

            {/* Aceitar nÃ£o Ã© um estado: Ã© levar o pedido ao sistema de destino e trazer o nÃºmero.
                Para o SER, a plataforma faz o lanÃ§amento; o "registrar" fica para quem jÃ¡ lanÃ§ou Ã  mÃ£o. */}
            {s.status === 'EmAnalise' && vaiAoSer && (
              <Button onClick={() => setModalSer(true)}>
                <Send className="size-4" />
                Aceitar e enviar ao SER
              </Button>
            )}
            {s.status === 'EmAnalise' && (
              <Button variante={vaiAoSer ? 'secundaria' : 'primaria'} onClick={() => setModalEnvio(true)}>
                <Hash className="size-4" />
                {vaiAoSer ? 'JÃ¡ lancei no SER â€” registrar nÃºmero' : 'Aceitar e registrar envio'}
              </Button>
            )}

            {s.status === 'EmAnalise' && (
              <Button variante="secundaria" onClick={() => setMotivoDe('devolver')}>
                <Undo2 className="size-4" />
                Devolver Ã  unidade
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
          // Fecha sÃ³ quando deu certo: com nÃºmero duplicado, o agente precisa ver o erro e
          // corrigir sem redigitar tudo.
          if (ok) setModalEnvio(false);
        }}
      />

      <ModalEnviarAoSer
        solicitacaoId={id}
        aberto={modalSer}
        aoFechar={() => {
          setModalSer(false);
          void Promise.all([solicitacao.refetch(), eventos.refetch()]);
        }}
      />

      {motivoDe === 'devolver' && (
        <ModalMotivo
          titulo="Devolver Ã  unidade"
          pergunta="O que a unidade precisa corrigir?"
          rotuloConfirmar="Devolver"
          desfecho="Devolvida Ã  unidade. Ela lÃª o que precisa corrigir no pedido e nas notificaÃ§Ãµes, e reenvia quando acertar."
          aoConfirmar={(motivo) => devolver.mutateAsync({ id, motivo })}
          aoFechar={() => setMotivoDe(null)}
        />
      )}

      {motivoDe === 'recusar' && (
        <ModalMotivo
          titulo="Recusar a solicitaÃ§Ã£o"
          pergunta="Motivo da recusa (a unidade vai ler)"
          rotuloConfirmar="Recusar"
          desfecho="SolicitaÃ§Ã£o recusada. A unidade lÃª o motivo no pedido e nas notificaÃ§Ãµes. A recusa encerra o pedido â€” para tentar de novo, a unidade abre outro."
          perigo
          aoConfirmar={(motivo) => recusar.mutateAsync({ id, motivo })}
          aoFechar={() => setMotivoDe(null)}
        />
      )}
    </div>
  );
}
