import { useState } from 'react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { WhatsappIcon } from '@/shared/ui/WhatsappIcon';
import { useChat } from '@/features/conversas/store/chatStore';
import { abrirJanelaChat, ehJanelaChat } from '@/features/conversas/lib/janelaChat';
import { obterSituacaoContato } from '@/features/conversas/api/conversasApi';
import { NovaConversaDialog } from '@/features/conversas/components/NovaConversaDialog';

type Props = {
  pacienteId: string;
};

function irParaConversa(id: string) {
  if (ehJanelaChat()) useChat.getState().setConversaAtiva(id);
  else abrirJanelaChat(id);
}

/**
 * Atalho "falar no WhatsApp" ao lado do nome do paciente, em qualquer tela. Some para quem não
 * tem a Central de Atendimento no perfil, porque só quem tem o módulo recebe as respostas.
 *
 * Se o paciente escreveu nas últimas 24h (janela aberta), abre a CONVERSA direto, com o histórico
 * — sem passar pelo diálogo de template. Senão, abre a nova conversa já com o paciente escolhido
 * (nome e telefone vêm do cadastro) para o operador conferir e disparar o template.
 */
export function BotaoWhatsAppPaciente({ pacienteId }: Props) {
  const podeConversar = useTemConsulta('Conversas');
  const [aberto, setAberto] = useState(false);
  const [consultando, setConsultando] = useState(false);

  if (!podeConversar) return null;

  async function clicar() {
    if (consultando) return;
    setConsultando(true);
    try {
      const s = await obterSituacaoContato(pacienteId);
      if (s.conversaId && s.podeTextoLivre) {
        irParaConversa(s.conversaId);
        return;
      }
    } catch {
      // Sem resposta do servidor, cai no caminho antigo (template).
    } finally {
      setConsultando(false);
    }
    setAberto(true);
  }

  return (
    <>
      <button
        type="button"
        onClick={() => void clicar()}
        disabled={consultando}
        className="shrink-0 rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-emerald-600 disabled:opacity-50"
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
            irParaConversa(id);
          }}
        />
      ) : null}
    </>
  );
}
