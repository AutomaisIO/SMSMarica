import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  titulo: string;
  descricao?: string;
  children: ReactNode;
  largura?: 'sm' | 'md' | 'lg';
};

const larguras: Record<NonNullable<Props['largura']>, string> = {
  sm: 'max-w-md',
  md: 'max-w-xl',
  lg: 'max-w-3xl',
};

/**
 * Trava de rolagem do corpo — CONTADA, não por modal.
 *
 * <p>Cada modal salvava e restaurava o `overflow` do `body` por conta própria. Com dois abertos
 * — o de cancelar e, por cima dele, o de senha do SISREG — bastava a página re-renderizar para o
 * de baixo reexecutar o efeito e passar a guardar `hidden` como "valor original". Fechados os
 * dois, o `body` ficava travado em `hidden` e a página só voltava a rolar com F5.</p>
 *
 * <p>Um contador único resolve a classe inteira do problema: o primeiro modal trava, o último
 * destrava, e reabrir o efeito no meio não corrompe nada.</p>
 */
let modaisComRolagemTravada = 0;
let overflowAntesDaTrava = '';

function travarRolagem(): () => void {
  if (modaisComRolagemTravada === 0) {
    overflowAntesDaTrava = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
  }
  modaisComRolagemTravada += 1;

  let liberado = false;
  return () => {
    if (liberado) return;
    liberado = true;
    modaisComRolagemTravada -= 1;
    if (modaisComRolagemTravada === 0) document.body.style.overflow = overflowAntesDaTrava;
  };
}

export function Modal({ aberto, aoFechar, titulo, descricao, children, largura = 'md' }: Props) {
  // `aoFechar` costuma ser uma função declarada no corpo de quem renderiza, ou seja, uma
  // identidade nova a cada render. Na lista de dependências ela fazia o efeito inteiro ser
  // desmontado e remontado sem que nada tivesse mudado — era esse churn que corrompia a trava de
  // rolagem. Por ref, o efeito roda uma vez por abertura e ainda assim chama sempre a versão atual.
  const aoFecharRef = useRef(aoFechar);
  useEffect(() => {
    aoFecharRef.current = aoFechar;
  });

  useEffect(() => {
    if (!aberto) return;
    function aoPressionar(e: KeyboardEvent) {
      if (e.key === 'Escape') aoFecharRef.current();
    }
    document.addEventListener('keydown', aoPressionar);
    const destravarRolagem = travarRolagem();
    return () => {
      document.removeEventListener('keydown', aoPressionar);
      destravarRolagem();
    };
  }, [aberto]);

  if (!aberto) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-8">
      <div
        className="absolute inset-0 bg-black/50"
        onClick={aoFechar}
        aria-hidden="true"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={titulo}
        // Coluna flex em vez de altura calculada: o `max-h-[calc(90vh-4rem)]` do corpo assumia
        // um cabeçalho de 4rem, mas com descrição ele passa disso — e o `overflow-hidden` daqui
        // comia o padding de baixo, deixando o último item colado na borda. Com flex, o corpo
        // ocupa o que sobra, seja qual for a altura do cabeçalho.
        className={cn(
          'relative flex w-full max-h-[90vh] flex-col overflow-hidden rounded-xl bg-white shadow-xl',
          larguras[largura],
        )}
      >
        <div className="flex shrink-0 items-start justify-between border-b border-gray-200 px-6 py-4">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">{titulo}</h2>
            {descricao ? <p className="mt-0.5 text-sm text-gray-500">{descricao}</p> : null}
          </div>
          <button
            type="button"
            onClick={aoFechar}
            className="rounded-md p-1 text-gray-500 hover:bg-gray-100 hover:text-gray-900"
            aria-label="Fechar"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        {/* pb-8: respiro no fim do conteúdo. Sem ele, conteúdo longo termina colado no fim da
            rolagem e dá a impressão de que ainda há coisa cortada embaixo. */}
        <div className="flex-1 overflow-y-auto px-6 pb-8 pt-5">{children}</div>
      </div>
    </div>,
    document.body,
  );
}
