import { AlertTriangle } from 'lucide-react';

type Props = {
  /** Telefone (formatado ou canônico) só para o operador conferir o que está prestes a fazer. */
  telefone?: string | null;
  onConfirmar: () => void;
  onCancelar: () => void;
};

/**
 * Aviso EXPLÍCITO antes de enviar mensagem para um número com pendência de "número errado" —
 * quem atende já disse que NÃO é o paciente. O envio não é proibido (o operador pode estar
 * justamente respondendo à pessoa que está na linha), mas nunca pode acontecer sem consciência:
 * foi mandando de novo para o número negado que geraram as reclamações de 01-02/09.
 */
export function ModalNumeroNegado({ telefone, onConfirmar, onCancelar }: Props) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onCancelar}>
      <div
        className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
        role="alertdialog"
        aria-modal="true"
      >
        <div className="flex items-start gap-3">
          <span className="rounded-full bg-amber-100 p-2 text-amber-600">
            <AlertTriangle className="h-5 w-5" />
          </span>
          <div className="min-w-0">
            <h3 className="text-sm font-semibold text-gray-900">Este contato está com número errado</h3>
            <p className="mt-1 text-sm text-gray-600">
              Quem atende {telefone ? <b>{telefone}</b> : 'este número'} já avisou que <b>não é o paciente</b>{' '}
              (há pendência aberta em Pendências de Cadastro). Mensagens enviadas aqui chegam à pessoa errada
              — não envie dados do paciente.
            </p>
          </div>
        </div>
        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancelar}
            className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={onConfirmar}
            className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700"
          >
            Estou ciente — enviar mesmo assim
          </button>
        </div>
      </div>
    </div>
  );
}
