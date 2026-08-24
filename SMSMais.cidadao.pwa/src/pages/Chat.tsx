import { MessageCircle } from 'lucide-react';
import { PrimaryButton, SectionHeader } from '@/components/ui';

/** Número oficial do WhatsApp da Secretaria de Saúde de Maricá (Cloud API). */
const WHATSAPP_SMS = '552137315313';
const LINK_WHATSAPP = `https://wa.me/${WHATSAPP_SMS}?text=${encodeURIComponent('Olá! Preciso de ajuda com a Saúde de Maricá.')}`;

/**
 * Chat com a Secretaria de Saúde: abre a conversa no WhatsApp oficial — do outro lado, a
 * equipe atende pela Central de Atendimento do painel (módulo Conversas). Um clique, sem
 * cadastro nem tela nova para aprender.
 */
export function Chat() {
  return (
    <div className="animate-rise space-y-5">
      <SectionHeader eyebrow="Fale com a gente" title="Chat" />

      <div className="rounded-3xl border border-areia bg-white p-6 text-center shadow-carta">
        <span className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-lagoa-claro text-lagoa">
          <MessageCircle className="h-8 w-8" />
        </span>
        <h2 className="mt-4 font-display text-lg font-semibold text-tinta">
          Converse com a Saúde de Maricá
        </h2>
        <p className="mt-2 text-sm text-tinta-mute">
          Tire dúvidas sobre exames, agendamentos e documentos direto pelo WhatsApp. Nossa equipe
          responde em horário de atendimento.
        </p>
        <a href={LINK_WHATSAPP} target="_blank" rel="noreferrer" className="mt-5 block">
          <PrimaryButton className="w-full">
            <MessageCircle className="h-5 w-5" /> Abrir conversa no WhatsApp
          </PrimaryButton>
        </a>
        <p className="mt-3 text-xs text-tinta-mute">
          Número oficial: <span className="font-medium text-tinta">(21) 3731-5313</span>
        </p>
      </div>
    </div>
  );
}
