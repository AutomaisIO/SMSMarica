import { useState, type ReactNode } from 'react';
import { HelpCircle } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';

type Props = {
  /** Título do modal — normalmente o nome do campo. */
  titulo: string;
  /** Explicação do campo (texto ou JSX). */
  children: ReactNode;
};

/**
 * Ícone "?" ao lado de um rótulo de campo que, ao clicar, abre um modal
 * explicando o que é aquele campo. Pensado para a prop `ajuda` do <Campo />.
 */
export function AjudaCampo({ titulo, children }: Props) {
  const [aberto, setAberto] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="text-gray-400 hover:text-primary-600 focus:outline-none focus:text-primary-600"
        aria-label={`O que é "${titulo}"?`}
        title={`O que é "${titulo}"?`}
      >
        <HelpCircle className="h-3.5 w-3.5" />
      </button>
      <Modal aberto={aberto} aoFechar={() => setAberto(false)} titulo={titulo} largura="sm">
        <div className="space-y-2 text-sm leading-relaxed text-gray-700">{children}</div>
      </Modal>
    </>
  );
}
