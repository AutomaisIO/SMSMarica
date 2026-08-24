import { useState } from 'react';
import { ShieldOff } from 'lucide-react';
import { ModalDispensarVerificacao } from '@/features/telefone-validacao/components/ModalDispensarVerificacao';
import { useDispensaAtiva } from '@/features/telefone-validacao/api/queries';

type Props = {
  /** Id do paciente (hub FHIR). Sem id não há o que dispensar. */
  pacienteId: string;
  pacienteNome?: string | null;
  className?: string;
  aoRegistrado?: () => void;
  /**
   * O chamador já sabe que existe dispensa ativa (ex.: veio no DTO da solicitação) e cuida do
   * selo por conta própria. Evita a consulta e o piscar do botão antes dela responder.
   */
  jaDispensado?: boolean;
};

/**
 * Botão discreto "Não vai validar" ao lado do "Verificar". Deliberadamente menos chamativo que
 * o de verificar: dispensar é a exceção, verificar é o caminho normal — o balcão não pode
 * pegar o atalho por ser o botão mais fácil de acertar.
 */
export function BotaoDispensarVerificacao({
  pacienteId, pacienteNome, className, aoRegistrado, jaDispensado,
}: Props) {
  const [aberto, setAberto] = useState(false);
  // Só consulta quando o chamador não sabe. Com dispensa ativa quem manda na tela é o selo.
  const dispensa = useDispensaAtiva(pacienteId, !jaDispensado);
  if (!pacienteId || jaDispensado || dispensa.data) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        title="Registrar que o paciente não vai validar o WhatsApp (com o motivo)"
        className={`inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2 py-0.5 text-xs font-medium text-gray-600 hover:bg-gray-50 ${className ?? ''}`}
      >
        <ShieldOff className="h-3.5 w-3.5" />
        Não vai validar
      </button>
      <ModalDispensarVerificacao
        aberto={aberto}
        pacienteId={pacienteId}
        pacienteNome={pacienteNome}
        aoFechar={() => setAberto(false)}
        aoRegistrado={aoRegistrado}
      />
    </>
  );
}
