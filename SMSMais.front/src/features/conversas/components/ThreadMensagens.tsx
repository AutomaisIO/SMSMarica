import { useEffect, useRef, useState } from 'react';
import { ArrowRightLeft, Building2, Check, MoreVertical, Undo2 } from 'lucide-react';
import {
  useAssumirConversa,
  useConversa,
  useDevolverConversa,
  useMarcarLida,
  useMensagens,
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

function Bolha({ m }: { m: Mensagem }) {
  const saida = m.direcao === 'Saida';
  const nota = m.tipoMensagem === 'NotaInterna';
  return (
    <div className={`flex ${saida ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[80%] rounded-2xl px-3 py-2 text-sm shadow-sm ${
          nota
            ? 'bg-amber-50 text-amber-900'
            : saida
              ? 'bg-primary-600 text-white'
              : 'bg-white text-gray-800 ring-1 ring-gray-200'
        }`}
      >
        {saida && m.autorNomeExibicao && (
          <p className="mb-0.5 text-[11px] font-semibold opacity-80">{m.autorNomeExibicao}</p>
        )}
        {m.template && !m.conteudo && <p className="italic opacity-90">[modelo: {m.template}]</p>}
        {m.conteudo && <p className="whitespace-pre-wrap break-words">{m.conteudo}</p>}
        <p className={`mt-1 text-[10px] ${saida && !nota ? 'text-white/70' : 'text-gray-400'}`}>{hora(m.ocorridoEm)}</p>
      </div>
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
        {mensagens?.map((m) => <Bolha key={m.id} m={m} />)}
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
    </div>
  );
}
