import { useEffect, useRef, useState } from 'react';
import { Clock, Sticker } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { CATEGORIAS_EMOJI, lerRecentes, registrarRecente } from '@/features/comunicacao-interna/lib/emojis';
import { SELOS, SeloVisual, type Selo } from '@/features/comunicacao-interna/lib/selos';

type Props = {
  /** Emoji escolhido — entra no texto na posição do cursor (quem chama decide). */
  aoEscolherEmoji: (emoji: string) => void;
  /** Selo escolhido — vai como mensagem própria. Ausente = aba Selos não aparece (sem permissão de enviar). */
  aoEscolherSelo?: (selo: Selo) => void;
  aoFechar: () => void;
};

type Aba = 'recentes' | 'selos' | (typeof CATEGORIAS_EMOJI)[number]['id'];

/**
 * Seletor de emojis + selos da Comunicação interna (sem biblioteca). Abre acima do composer;
 * fecha com Esc ou clique fora. Selo é enviado na hora (é uma "figurinha"); emoji entra no texto.
 */
export function SeletorEmoji({ aoEscolherEmoji, aoEscolherSelo, aoFechar }: Props) {
  const [recentes, setRecentes] = useState<string[]>(() => lerRecentes());
  const [aba, setAba] = useState<Aba>(() => (lerRecentes().length > 0 ? 'recentes' : 'carinhas'));
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) aoFechar();
    }
    function aoTeclar(e: KeyboardEvent) {
      if (e.key === 'Escape') aoFechar();
    }
    document.addEventListener('mousedown', aoClicarFora);
    document.addEventListener('keydown', aoTeclar);
    return () => {
      document.removeEventListener('mousedown', aoClicarFora);
      document.removeEventListener('keydown', aoTeclar);
    };
  }, [aoFechar]);

  function escolher(e: string) {
    registrarRecente(e);
    setRecentes(lerRecentes());
    aoEscolherEmoji(e);
  }

  const categoria = CATEGORIAS_EMOJI.find((c) => c.id === aba);
  const lista = aba === 'recentes' ? recentes : (categoria?.emojis ?? []);

  return (
    <div
      ref={ref}
      className="absolute bottom-full left-0 z-20 mb-2 w-[19rem] max-w-[calc(100vw-2rem)] overflow-hidden rounded-xl border border-gray-200 bg-white shadow-xl"
      role="dialog"
      aria-label="Emojis e selos"
    >
      <div className="flex items-center gap-0.5 border-b border-gray-100 bg-gray-50 px-1.5 py-1">
        <BotaoAba ativo={aba === 'recentes'} onClick={() => setAba('recentes')} titulo="Recentes">
          <Clock className="h-4 w-4" />
        </BotaoAba>
        {CATEGORIAS_EMOJI.map((c) => (
          <BotaoAba key={c.id} ativo={aba === c.id} onClick={() => setAba(c.id)} titulo={c.rotulo}>
            <span className="text-base leading-none">{c.icone}</span>
          </BotaoAba>
        ))}
        {aoEscolherSelo && (
          <BotaoAba ativo={aba === 'selos'} onClick={() => setAba('selos')} titulo="Selos (figurinhas da equipe)">
            <Sticker className="h-4 w-4" />
          </BotaoAba>
        )}
      </div>

      <p className="px-3 pt-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        {aba === 'recentes' ? 'Recentes' : aba === 'selos' ? 'Selos — enviados na hora' : categoria?.rotulo}
      </p>

      {aba === 'selos' && aoEscolherSelo ? (
        <div className="flex max-h-56 flex-wrap gap-1.5 overflow-y-auto p-2">
          {SELOS.map((s) => (
            <button
              key={s.chave}
              type="button"
              onClick={() => aoEscolherSelo(s)}
              className="rounded-full transition-transform hover:scale-105 focus:outline-none focus:ring-2 focus:ring-orange-400"
              title={`Enviar o selo "${s.rotulo}"`}
            >
              <SeloVisual selo={s} />
            </button>
          ))}
        </div>
      ) : lista.length === 0 ? (
        <p className="px-3 py-6 text-center text-xs text-gray-400">Os emojis que você usar aparecem aqui.</p>
      ) : (
        <div className="grid max-h-56 grid-cols-8 gap-0.5 overflow-y-auto p-2">
          {lista.map((e, i) => (
            <button
              key={`${e}-${i}`}
              type="button"
              onClick={() => escolher(e)}
              className="flex h-8 w-8 items-center justify-center rounded-md text-xl leading-none hover:bg-orange-50"
              title={e}
            >
              {e}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function BotaoAba({
  ativo,
  onClick,
  titulo,
  children,
}: {
  ativo: boolean;
  onClick: () => void;
  titulo: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={titulo}
      aria-label={titulo}
      aria-pressed={ativo}
      className={cn(
        'flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-white',
        ativo && 'bg-white text-orange-600 shadow-sm ring-1 ring-gray-200',
      )}
    >
      {children}
    </button>
  );
}
