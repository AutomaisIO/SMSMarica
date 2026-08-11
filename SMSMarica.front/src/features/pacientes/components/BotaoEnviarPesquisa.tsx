import { useState } from 'react';
import { MessageSquareHeart } from 'lucide-react';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { enviarPesquisaSatisfacao } from '@/features/pacientes/api/pacientesApi';
import type { Atendimento } from '@/features/pacientes/types';

/**
 * Prazo para avaliar, contado do fim do atendimento. Espelha `JANELA_DIAS` no app do cidadão e
 * a regra do servidor — a de valer é a do servidor; esta só evita oferecer o que seria recusado.
 */
const JANELA_DIAS = 15;

function diasDesde(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return Math.floor((Date.now() - d.getTime()) / 86_400_000);
}

/**
 * Envio manual da pesquisa de satisfação de UM atendimento.
 *
 * <p>Discreto de propósito: fica ao lado da etiqueta de origem, sem competir com o conteúdo
 * clínico do card. Só aparece para quem tem a permissão e para atendimento dentro da janela —
 * convite de atendimento velho mede lembrança, não experiência.</p>
 */
export function BotaoEnviarPesquisa({
  pacienteId,
  atendimento,
}: {
  pacienteId: string;
  atendimento: Atendimento;
}) {
  const pode = usePermissao('PesquisaSatisfacao', 'Edicao');
  const [enviando, setEnviando] = useState(false);
  const [enviado, setEnviado] = useState(false);

  const dias = diasDesde(atendimento.fim ?? atendimento.inicio);
  const dentroDaJanela = dias !== null && dias <= JANELA_DIAS;
  if (!pode || !dentroDaJanela) return null;

  async function enviar() {
    setEnviando(true);
    try {
      await enviarPesquisaSatisfacao(pacienteId, atendimento.id);
      setEnviado(true);
      notificar('Pesquisa enviada ao paciente pelo WhatsApp.');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={enviar}
      disabled={enviando || enviado}
      title={
        enviado
          ? 'Pesquisa já enviada para este atendimento.'
          : 'Enviar a pesquisa de satisfação deste atendimento ao paciente'
      }
      className="inline-flex shrink-0 items-center gap-1.5 rounded-md border border-gray-200 px-2 py-1 text-xs
                 font-medium text-gray-600 transition hover:border-gray-300 hover:bg-gray-50
                 disabled:cursor-default disabled:opacity-60"
    >
      <MessageSquareHeart className="h-3.5 w-3.5" />
      {enviado ? 'Pesquisa enviada' : enviando ? 'Enviando…' : 'Enviar pesquisa'}
    </button>
  );
}
