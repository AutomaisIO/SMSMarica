import { MessageSquareOff, ShieldOff, Undo2 } from 'lucide-react';
import { formatarInstante } from '@/shared/lib/datas';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useDispensaAtiva, useRevogarDispensa } from '@/features/telefone-validacao/api/queries';

type Props = {
  pacienteId: string;
  /** Só consulta quando faz sentido (o paciente ainda não tem número verificado). */
  habilitado?: boolean;
  /** Mostra o botão de revogar (recepção). */
  podeRevogar?: boolean;
  className?: string;
};

/**
 * Selo da dispensa ATIVA: diz por que este paciente pode seguir sem número verificado e — o
 * que a equipe mais precisa saber — se resultado e laudo ainda vão pelo WhatsApp ou se a
 * entrega é presencial. Sem dispensa ativa, não renderiza nada.
 */
export function SeloDispensaContato({ pacienteId, habilitado = true, podeRevogar, className }: Props) {
  const dispensa = useDispensaAtiva(pacienteId, habilitado);
  const revogar = useRevogarDispensa();
  const d = dispensa.data;
  if (!d) return null;

  async function desfazer() {
    try {
      await revogar.mutateAsync({ pacienteId, motivo: 'Revogada no painel' });
      notificar('Dispensa revogada — o contato volta a exigir verificação.');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <span
      className={`inline-flex flex-wrap items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs text-amber-900 ${className ?? ''}`}
      title={
        `Dispensado por ${d.criadoPorNome ?? 'operador'} em ${formatarInstante(d.criadoEm)}.` +
        (d.permiteEnvio
          ? ' Avisos continuam indo para o número do cadastro.'
          : ' Resultado e laudo NÃO vão por WhatsApp — entrega presencial.')
      }
    >
      <ShieldOff className="h-3.5 w-3.5 shrink-0" />
      <span>Não vai validar: {d.motivoTexto}</span>
      {!d.permiteEnvio ? (
        <span className="inline-flex items-center gap-0.5 font-medium">
          <MessageSquareOff className="h-3.5 w-3.5" /> entrega presencial
        </span>
      ) : null}
      {podeRevogar ? (
        <button
          type="button"
          onClick={desfazer}
          disabled={revogar.isPending}
          className="inline-flex items-center gap-0.5 rounded px-1 font-medium underline-offset-2 hover:underline disabled:opacity-50"
          title="Desfazer a dispensa (volta a exigir verificação)"
        >
          <Undo2 className="h-3 w-3" /> desfazer
        </button>
      ) : null}
    </span>
  );
}
