import { useState } from 'react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { WhatsappIcon } from '@/shared/ui/WhatsappIcon';
import { useChat } from '@/features/conversas/store/chatStore';
import { abrirJanelaChat, ehJanelaChat } from '@/features/conversas/lib/janelaChat';
import { NovaConversaDialog } from '@/features/conversas/components/NovaConversaDialog';

type Props = {
  pacienteId: string;
};

/**
 * Atalho "falar no WhatsApp" ao lado do nome do paciente, em qualquer tela. Abre a nova
 * conversa já com o paciente escolhido (nome e telefone vêm do cadastro) — o operador só
 * confere e dispara o template. Some para quem não tem a Central de Atendimento no perfil,
 * porque só quem tem o módulo recebe as respostas.
 *
 * Criada a conversa, leva o operador até ela: dentro da janela do chat troca a thread
 * in-place; fora dela, abre/foca a janela solta do chat.
 */
export function BotaoWhatsAppPaciente({ pacienteId }: Props) {
  const podeConversar = useTemConsulta('Conversas');
  const [aberto, setAberto] = useState(false);

  if (!podeConversar) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="shrink-0 rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-emerald-600"
        aria-label="Falar no WhatsApp"
        title="Falar no WhatsApp"
      >
        <WhatsappIcon className="h-4 w-4" />
      </button>

      {aberto ? (
        <NovaConversaDialog
          pacienteInicialId={pacienteId}
          onFechar={() => setAberto(false)}
          onCriada={(id) => {
            setAberto(false);
            if (ehJanelaChat()) useChat.getState().setConversaAtiva(id);
            else abrirJanelaChat(id);
          }}
        />
      ) : null}
    </>
  );
}
