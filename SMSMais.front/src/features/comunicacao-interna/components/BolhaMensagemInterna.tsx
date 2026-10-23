import { Check, CheckCheck } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { horaFmt } from '@/features/comunicacao-interna/lib/formatar';
import { SeloVisual, TextoComSelos, seloUnico } from '@/features/comunicacao-interna/lib/selos';
import { AnexoInterno } from '@/features/comunicacao-interna/components/AnexoInterno';
import type { MensagemInterna } from '@/features/comunicacao-interna/types';

type Props = {
  m: MensagemInterna;
  minha: boolean;
  /** Conversa de unidade: mostra nome + unidade de quem escreveu (nas dos outros). */
  mostrarAutor: boolean;
  /** Só na direta, nas minhas: undefined = não se aplica; false = entregue; true = lida (✓✓ azul). */
  lida?: boolean;
  destacada?: boolean;
};

/** Uma mensagem da thread, estilo WhatsApp: as minhas à direita (laranja-claro), as outras à esquerda. */
export function BolhaMensagemInterna({ m, minha, mostrarAutor, lida, destacada }: Props) {
  const selo = m.anexo ? null : seloUnico(m.texto);
  const rodape = (
    <span className={cn('flex items-center justify-end gap-0.5 text-[10px]', minha ? 'text-orange-900/50' : 'text-gray-400')}>
      {horaFmt(m.enviadaEm)}
      {lida !== undefined &&
        (lida ? (
          <CheckCheck className="h-3.5 w-3.5 text-sky-500" aria-label="Lida" />
        ) : (
          <Check className="h-3.5 w-3.5" aria-label="Entregue" />
        ))}
    </span>
  );

  return (
    <div id={`msg-interna-${m.id}`} className={cn('flex px-3 py-0.5', minha ? 'justify-end' : 'justify-start')}>
      <div className={cn('max-w-[78%]', destacada && 'rounded-xl ring-2 ring-orange-400 ring-offset-2')}>
        {selo ? (
          // Selo sozinho é figurinha: sem bolha em volta, só o selo grande e a hora.
          <div className={cn('flex flex-col gap-1', minha ? 'items-end' : 'items-start')}>
            {mostrarAutor && !minha && <Autor m={m} />}
            <SeloVisual selo={selo} grande />
            {rodape}
          </div>
        ) : (
          <div
            className={cn(
              'rounded-2xl px-3 py-1.5 text-sm shadow-sm',
              minha ? 'rounded-br-md bg-orange-100 text-gray-900' : 'rounded-bl-md bg-white text-gray-900 ring-1 ring-gray-200',
            )}
          >
            {mostrarAutor && !minha && <Autor m={m} />}
            {m.anexo && (
              <div className="pb-1 pt-0.5">
                <AnexoInterno mensagemId={m.id} anexo={m.anexo} />
              </div>
            )}
            {m.texto && (
              <p className="whitespace-pre-wrap break-words leading-snug">
                <TextoComSelos texto={m.texto} />
              </p>
            )}
            {rodape}
          </div>
        )}
      </div>
    </div>
  );
}

function Autor({ m }: { m: MensagemInterna }) {
  return (
    <p className="truncate text-[11px] font-semibold text-orange-700">
      {m.autorNome}
      {m.autorUnidadeNome && <span className="font-normal text-gray-400"> · {m.autorUnidadeNome}</span>}
    </p>
  );
}
