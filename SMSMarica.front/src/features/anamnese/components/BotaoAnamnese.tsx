import { useState } from 'react';
import { ClipboardList } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Props = {
  /** Vínculo da anamnese ao pedido/exame (accession). */
  accessionNumber?: string | null;
  /** Nome do paciente, para contexto no cabeçalho da anamnese. */
  pacienteNome?: string | null;
  /** 'compacto' = botão pequeno de linha de tabela; 'normal' = botão padrão. */
  variante?: 'compacto' | 'normal';
  className?: string;
};

/**
 * Botão de Anamnese — a anamnese é preenchida pela enfermeira/atendente no
 * encaminhamento e pode ser reaberta para edição; a médica a consulta ao laudar.
 *
 * Por ora abre apenas um placeholder; a tela da anamnese será construída na
 * sequência (este botão é o ponto de entrada já posicionado nos dois fluxos:
 * Solicitações de Exame e a linha do exame em Exames de Imagem).
 */
export function BotaoAnamnese({
  accessionNumber,
  pacienteNome,
  variante = 'compacto',
  className,
}: Props) {
  const [aberto, setAberto] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        title="Anamnese do paciente (pré-exame)"
        className={cn(
          variante === 'compacto'
            ? 'inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-100'
            : 'inline-flex items-center gap-1.5 rounded-md border border-indigo-300 bg-indigo-50 px-3 py-2 text-sm font-medium text-indigo-700 hover:bg-indigo-100',
          className,
        )}
      >
        <ClipboardList className={variante === 'compacto' ? 'h-3.5 w-3.5' : 'h-4 w-4'} />
        Anamnese
      </button>

      {aberto ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
          onClick={() => setAberto(false)}
        >
          <div
            className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
              <ClipboardList className="h-5 w-5 text-indigo-600" />
              Anamnese
            </h3>
            <p className="mt-2 text-sm text-gray-600">
              A tela da anamnese está <strong>em construção</strong>.
              {pacienteNome ? (
                <>
                  {' '}
                  Paciente: <strong>{pacienteNome}</strong>.
                </>
              ) : null}
              {accessionNumber ? (
                <>
                  {' '}
                  Pedido: <span className="font-mono">{accessionNumber}</span>.
                </>
              ) : null}
            </p>
            <div className="mt-5 flex justify-end">
              <button
                type="button"
                onClick={() => setAberto(false)}
                className="text-sm text-gray-600 hover:text-gray-900"
              >
                Fechar
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
