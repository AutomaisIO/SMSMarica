import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, BotOff, Building2, Check, CheckCheck, Clock, MoreVertical, Undo2 } from 'lucide-react';
import {
  useAssumirConversa,
  useConversa,
  useDevolverConversa,
  useMarcarLida,
  useMarcarRoboErro,
  useMensagens,
  usePararRoboConversa,
} from '@/features/conversas/api/queries';
import { useAssinaturaConversa } from '@/features/conversas/hooks/useChatHub';
import { ComposerMensagem } from '@/features/conversas/components/ComposerMensagem';
import { EncaminharConversaDialog } from '@/features/conversas/components/EncaminharConversaDialog';
import { TransferirConversaDialog } from '@/features/conversas/components/TransferirConversaDialog';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { PacientesDoTelefone } from '@/features/conversas/components/PacientesDoTelefone';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAuth, useTemConsulta } from '@/shared/auth/authStore';
import type { Mensagem } from '@/features/conversas/types';

function hora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  });
}

/**
 * Check de entrega/leitura estilo WhatsApp, só para mensagens que saíram para o WhatsApp.
 * O status vem pronto do backend (whatsapp_mensagem.status, promovido pelo webhook da Meta):
 *   Enviada → 1 check · Entregue → 2 checks · Lida → 2 checks azuis · Falha → alerta.
 * `emBolhaEscura` = bolha vermelha (saída normal); fora dela o fundo é claro e a cor muda.
 */
function StatusEntrega({ status, emBolhaEscura }: { status: Mensagem['status']; emBolhaEscura: boolean }) {
  const corBase = emBolhaEscura ? 'text-white/70' : 'text-gray-400';
  const corLida = emBolhaEscura ? 'text-sky-300' : 'text-sky-500';
  const corFalha = emBolhaEscura ? 'text-amber-200' : 'text-error-500';
  switch (status) {
    case 'Enviada':
      return <Check className={`h-3 w-3 ${corBase}`} aria-label="Enviada" />;
    case 'Entregue':
      return <CheckCheck className={`h-3 w-3 ${corBase}`} aria-label="Entregue" />;
    case 'Lida':
      return <CheckCheck className={`h-3 w-3 ${corLida}`} aria-label="Lida" />;
    case 'Falha':
      return <AlertTriangle className={`h-3 w-3 ${corFalha}`} aria-label="Falha no envio" />;
    default:
      // Sem confirmação ainda (ex.: acabou de sair) — relógio, como no WhatsApp.
      return <Clock className={`h-3 w-3 ${corBase}`} aria-label="Enviando" />;
  }
}

function Bolha({
  m,
  onPararRobo,
  onMarcarErro,
  pararPendente,
}: {
  m: Mensagem;
  onPararRobo?: () => void;
  onMarcarErro?: () => void;
  pararPendente?: boolean;
}) {
  const saida = m.direcao === 'Saida';
  const nota = m.tipoMensagem === 'NotaInterna';
  const robo = m.tipoMensagem === 'Robo';
  return (
    <div className={`flex flex-col ${saida ? 'items-end' : 'items-start'}`}>
      <div
        className={`max-w-[80%] rounded-2xl px-3 py-2 text-sm shadow-sm ${
          nota
            ? 'bg-amber-50 text-amber-900'
            : robo
              ? 'bg-indigo-50 text-indigo-900 ring-1 ring-indigo-200'
              : saida
                ? 'bg-primary-600 text-white'
                : 'bg-white text-gray-800 ring-1 ring-gray-200'
        }`}
      >
        {saida && m.autorNomeExibicao && (
          <p className="mb-0.5 text-[11px] font-semibold opacity-80">
            {robo ? '🤖 ' : ''}
            {m.autorNomeExibicao}
          </p>
        )}
        {m.template && !m.conteudo && <p className="italic opacity-90">[modelo: {m.template}]</p>}
        {m.conteudo && <p className="whitespace-pre-wrap break-words">{m.conteudo}</p>}
        <div className={`mt-1 flex items-center gap-1 ${saida ? 'justify-end' : ''}`}>
          <p className={`text-[10px] ${saida && !nota && !robo ? 'text-white/70' : 'text-gray-400'}`}>{hora(m.ocorridoEm)}</p>
          {saida && !nota && m.status !== 'Recebida' && (
            <StatusEntrega status={m.status} emBolhaEscura={saida && !nota && !robo} />
          )}
        </div>
      </div>
      {robo && (onPararRobo || onMarcarErro) && (
        <div className="mt-0.5 flex items-center gap-3">
          {onPararRobo && (
            <button
              type="button"
              onClick={onPararRobo}
              disabled={pararPendente}
              className="flex items-center gap-1 text-[11px] font-medium text-indigo-500 hover:text-indigo-700 disabled:opacity-50"
              title="Para o robô nesta conversa e assume para você corrigir. A mensagem já enviada não pode ser apagada no WhatsApp."
            >
              <BotOff className="h-3.5 w-3.5" /> Parar robô
            </button>
          )}
          {onMarcarErro && (
            <button
              type="button"
              onClick={onMarcarErro}
              className="flex items-center gap-1 text-[11px] font-medium text-amber-600 hover:text-amber-700"
              title="Marca esta resposta como errada, para revisão e treinamento do robô."
            >
              <AlertTriangle className="h-3.5 w-3.5" /> Marcar erro
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export function ThreadMensagens({ conversaId }: { conversaId: string }) {
  const { data: conversa } = useConversa(conversaId);
  const { data: mensagens, isLoading } = useMensagens(conversaId);
  const usuarioId = useAuth((s) => s.usuario?.id ?? null);
  const podeSupervisao = useTemConsulta('ConversasSupervisao');
  const assumir = useAssumirConversa();
  const marcarLida = useMarcarLida();
  const devolver = useDevolverConversa();
  const pararRobo = usePararRoboConversa();
  const marcarErro = useMarcarRoboErro();
  const [erroMsgId, setErroMsgId] = useState<string | null>(null);
  const [erroNota, setErroNota] = useState('');
  const fimRef = useRef<HTMLDivElement | null>(null);
  const [menuAberto, setMenuAberto] = useState(false);
  const [encaminharAberto, setEncaminharAberto] = useState(false);
  const [transferirAberto, setTransferirAberto] = useState(false);
  const [erroAcao, setErroAcao] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);

  // Grupo conversa:{id} no hub — garante tempo real na thread aberta mesmo quando o evento
  // não rotearia pro operador (ex.: conversa de outra unidade aberta pela supervisão).
  useAssinaturaConversa(conversaId);

  // Abrir a conversa NÃO zera mais o badge nem gera posse: o claim é uma decisão do atendente
  // (responder, ou o botão "Marcar como lida" — que numa conversa sem dono também assume).
  useEffect(() => {
    setMenuAberto(false);
    setErroAcao(null);
  }, [conversaId]);

  useEffect(() => {
    fimRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [mensagens]);

  useEffect(() => {
    if (!menuAberto) return;
    const fechar = (e: MouseEvent) => {
      if (!menuRef.current?.contains(e.target as Node)) setMenuAberto(false);
    };
    document.addEventListener('mousedown', fechar);
    return () => document.removeEventListener('mousedown', fechar);
  }, [menuAberto]);

  const semDono = Boolean(conversa) && !conversa?.operadorResponsavelId;
  const souDono = Boolean(usuarioId) && conversa?.operadorResponsavelId === usuarioId;
  const donoTerceiro = Boolean(conversa?.operadorResponsavelId) && !souDono;
  // O backend é a autoridade — aqui só escondemos o que sabidamente falharia.
  const podeAgirNaPosse = souDono || semDono || podeSupervisao;

  async function executar(acao: () => Promise<unknown>) {
    setErroAcao(null);
    setMenuAberto(false);
    try {
      await acao();
    } catch (e) {
      // 409 de corrida ("já assumida por Fulano") — o invalidate do onError já refaz a tela.
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="flex h-full flex-col bg-gray-50">
      <div className="border-b border-gray-200 bg-white px-4 py-2.5">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            {/* Título = nome COMPLETO do paciente resolvido do banco (quando há); o nome do
                perfil do WhatsApp fica no subtítulo — os dois convivem para expor divergência. */}
            <p className="flex items-center gap-1 text-sm font-semibold text-gray-900">
              {conversa?.pacienteNome || conversa?.nomeContato || conversa?.telefoneCanonical || 'Conversa'}
              {conversa?.pacienteId ? (
                <NomePacienteComResumo pacienteId={conversa.pacienteId} mostrarWhatsApp={false} />
              ) : null}
            </p>
            <p className="text-xs text-gray-500">
              {conversa?.telefoneCanonical}
              {conversa?.pacienteNome && conversa?.nomeContato && conversa.nomeContato !== conversa.pacienteNome
                ? ` · WhatsApp: ${conversa.nomeContato}`
                : ''}
              {conversa?.unidadeNome && ` · ${conversa.unidadeNome}`}
            </p>
          </div>

          {conversa && (
            <div className="flex shrink-0 items-center gap-1.5">
              {/* Chip de posse: quem atende esta conversa. */}
              {souDono ? (
                <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700">
                  Você atende
                </span>
              ) : donoTerceiro ? (
                <span className="max-w-40 truncate rounded-full bg-sky-50 px-2 py-0.5 text-[11px] font-medium text-sky-700">
                  Atende: {conversa.operadorResponsavelNome}
                </span>
              ) : (
                <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">
                  Sem responsável
                </span>
              )}

              {/* Sem dono: "marcar como lida" também PUXA a conversa (claim). Minha com
                  não-lidas: só zera o contador. */}
              {semDono && (
                <button
                  type="button"
                  onClick={() => void executar(() => assumir.mutateAsync(conversaId))}
                  disabled={assumir.isPending}
                  className="flex items-center gap-1 rounded-md bg-primary-600 px-2 py-1 text-[11px] font-medium text-white hover:bg-primary-700 disabled:opacity-50"
                  title="A conversa sai da fila da unidade e vai para a sua lista"
                >
                  <Check className="h-3.5 w-3.5" />
                  {conversa.naoLidas > 0 ? 'Marcar como lida' : 'Assumir conversa'}
                </button>
              )}
              {souDono && conversa.naoLidas > 0 && (
                <button
                  type="button"
                  onClick={() => void executar(() => marcarLida.mutateAsync(conversaId))}
                  disabled={marcarLida.isPending}
                  className="flex items-center gap-1 rounded-md bg-gray-100 px-2 py-1 text-[11px] font-medium text-gray-700 hover:bg-gray-200 disabled:opacity-50"
                >
                  <Check className="h-3.5 w-3.5" /> Marcar como lida
                </button>
              )}

              {podeAgirNaPosse && (
                <div className="relative" ref={menuRef}>
                  <button
                    type="button"
                    onClick={() => setMenuAberto((v) => !v)}
                    className="rounded-md p-1 text-gray-500 hover:bg-gray-100"
                    aria-label="Ações da conversa"
                    aria-expanded={menuAberto}
                  >
                    <MoreVertical className="h-4 w-4" />
                  </button>
                  {menuAberto && (
                    <div className="absolute right-0 z-20 mt-1 w-56 overflow-hidden rounded-md bg-white py-1 shadow-lg ring-1 ring-gray-200">
                      {(souDono || (donoTerceiro && podeSupervisao)) && (
                        <button
                          type="button"
                          onClick={() => void executar(() => devolver.mutateAsync(conversaId))}
                          className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-50"
                        >
                          <Undo2 className="h-4 w-4 text-gray-400" /> Devolver à fila da unidade
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => {
                          setMenuAberto(false);
                          setEncaminharAberto(true);
                        }}
                        className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-50"
                      >
                        <ArrowRightLeft className="h-4 w-4 text-gray-400" /> Encaminhar para colega…
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setMenuAberto(false);
                          setTransferirAberto(true);
                        }}
                        className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-50"
                      >
                        <Building2 className="h-4 w-4 text-gray-400" /> Transferir para outra unidade…
                      </button>
                      <button
                        type="button"
                        onClick={() => void executar(() => pararRobo.mutateAsync(conversaId))}
                        className="flex w-full items-center gap-2 border-t border-gray-100 px-3 py-2 text-left text-sm text-indigo-700 hover:bg-indigo-50"
                        title="Impede o robô de continuar nesta conversa (vence a virada de horário) e assume para você"
                      >
                        <BotOff className="h-4 w-4 text-indigo-500" /> Parar robô nesta conversa
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        {erroAcao && <p className="mt-1 text-xs text-error-600">{erroAcao}</p>}

        <PacientesDoTelefone conversaId={conversaId} />
      </div>

      <div className="flex-1 space-y-2 overflow-y-auto p-3">
        {isLoading && <p className="text-sm text-gray-500">Carregando mensagens…</p>}
        {mensagens?.map((m) => (
          <Bolha
            key={m.id}
            m={m}
            onPararRobo={
              m.tipoMensagem === 'Robo' && podeAgirNaPosse
                ? () => void executar(() => pararRobo.mutateAsync(conversaId))
                : undefined
            }
            onMarcarErro={
              m.tipoMensagem === 'Robo'
                ? () => {
                    setErroNota('');
                    setErroMsgId(m.id);
                  }
                : undefined
            }
            pararPendente={pararRobo.isPending}
          />
        ))}
        <div ref={fimRef} />
      </div>

      <ComposerMensagem conversaId={conversaId} podeTextoLivre={conversa?.podeTextoLivre ?? false} />

      {encaminharAberto && (
        <EncaminharConversaDialog
          conversaId={conversaId}
          onFechar={() => setEncaminharAberto(false)}
          onEncaminhada={() => setEncaminharAberto(false)}
        />
      )}
      {transferirAberto && conversa && (
        <TransferirConversaDialog
          conversaId={conversaId}
          unidadeAtualId={conversa.unidadeId}
          onFechar={() => setTransferirAberto(false)}
          onTransferida={() => setTransferirAberto(false)}
        />
      )}

      {erroMsgId && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white p-4 shadow-xl">
            <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
              <AlertTriangle className="h-4 w-4 text-amber-500" /> Marcar resposta do robô como erro
            </h2>
            <p className="mt-1 text-xs text-gray-500">
              Fica registrada para revisão e treinamento do robô. Não altera a mensagem já enviada.
            </p>
            <textarea
              value={erroNota}
              onChange={(e) => setErroNota(e.target.value)}
              rows={3}
              maxLength={2000}
              placeholder="O que saiu errado? (opcional)"
              className="mt-3 w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
            />
            <div className="mt-3 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setErroMsgId(null)}
                className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
              >
                Cancelar
              </button>
              <button
                type="button"
                disabled={marcarErro.isPending}
                onClick={() =>
                  marcarErro.mutate(
                    { id: conversaId, mensagemWhatsAppId: erroMsgId, nota: erroNota.trim() || undefined },
                    {
                      onSuccess: () => setErroMsgId(null),
                      onError: (e) => setErroAcao(extrairMensagemDeErro(e)),
                    },
                  )
                }
                className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
              >
                {marcarErro.isPending ? 'Registrando…' : 'Marcar erro'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
