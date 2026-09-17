import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, UserRound } from 'lucide-react';

import { AvatarTecnico } from '@/shared/regulacao/AvatarTecnico';
import { rotuloTecnico, type TecnicoNotificacao } from '@/shared/regulacao/tecnicos';

type Props = {
  id?: string;
  tecnicos: TecnicoNotificacao[];
  selecionados: string[];
  aoMudar: (chaves: string[]) => void;
};

/**
 * Dropdown de técnicos reguladores com seleção múltipla. Nada marcado = todos. Os números são as
 * notificações pendentes de cada técnico (sem os outros filtros da tela — é "quanto é de quem").
 *
 * Técnico marcado que não veio na lista (ex.: preferência salva de um nome que sumiu da trilha)
 * continua aparecendo, para dar para desmarcar.
 */
export function FiltroTecnicos({ id, tecnicos, selecionados, aoMudar }: Props) {
  const [aberto, setAberto] = useState(false);
  const [busca, setBusca] = useState('');
  const container = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (container.current && !container.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  const opcoes = useMemo(() => {
    const conhecidas = new Set(tecnicos.map((t) => t.chave));
    const orfas = selecionados
      .filter((c) => !conhecidas.has(c))
      .map((chave) => ({ chave, pendentes: 0 }));
    const termo = busca.trim().toUpperCase();
    return [...orfas, ...tecnicos].filter(
      (t) => !termo || rotuloTecnico(t.chave).toUpperCase().includes(termo),
    );
  }, [tecnicos, selecionados, busca]);

  function alternar(chave: string) {
    aoMudar(
      selecionados.includes(chave)
        ? selecionados.filter((c) => c !== chave)
        : [...selecionados, chave],
    );
  }

  return (
    <div className="relative" ref={container}>
      <button
        id={id}
        type="button"
        onClick={() => setAberto((a) => !a)}
        className={`flex items-center gap-2 rounded border px-2 py-1 text-sm ${
          selecionados.length > 0
            ? 'border-red-700 bg-red-50 text-red-800'
            : 'border-slate-300 bg-white text-slate-700'
        }`}
      >
        {selecionados.length === 0 ? (
          <>
            <UserRound className="size-4 text-slate-400" />
            Todos
          </>
        ) : (
          <>
            <span className="flex -space-x-1.5">
              {selecionados.slice(0, 4).map((c) => (
                <AvatarTecnico key={c} chave={c} tamanho="xs" className="ring-2 ring-white" />
              ))}
            </span>
            {selecionados.length === 1
              ? rotuloTecnico(selecionados[0])
              : `${selecionados.length} técnicos`}
          </>
        )}
        <ChevronDown className="size-4 shrink-0 text-slate-400" />
      </button>

      {/* Painel ancorado pela DIREITA: o botão fica no canto direito da linha das abas e muda
          de largura conforme o nome do técnico selecionado. Alinhado pela esquerda, ele escapava
          para fora da janela. */}
      {aberto && (
        <div className="absolute right-0 z-20 mt-1 w-80 max-w-[calc(100vw-2rem)] rounded-md border border-slate-200 bg-white p-1 shadow-lg">
          <input
            type="search"
            autoFocus
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar técnico…"
            className="mb-1 w-full rounded border border-slate-200 px-2 py-1 text-sm"
          />
          <div className="max-h-80 overflow-y-auto">
            {opcoes.length === 0 && (
              <p className="px-2 py-3 text-center text-xs text-slate-400">Nenhum técnico encontrado.</p>
            )}
            {opcoes.map((t) => {
              const marcado = selecionados.includes(t.chave);
              return (
                <button
                  key={t.chave}
                  type="button"
                  onClick={() => alternar(t.chave)}
                  className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-slate-700 hover:bg-slate-50"
                >
                  <span
                    className={`flex size-4 shrink-0 items-center justify-center rounded border ${
                      marcado ? 'border-red-700 bg-red-700 text-white' : 'border-slate-300 bg-white'
                    }`}
                  >
                    {marcado && <Check className="size-3" />}
                  </span>
                  <AvatarTecnico chave={t.chave} tamanho="xs" />
                  <span className={`min-w-0 flex-1 truncate ${t.pendentes === 0 ? 'text-slate-400' : ''}`}>
                    {rotuloTecnico(t.chave)}
                  </span>
                  {t.pendentes > 0 && (
                    <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-700">
                      {t.pendentes}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
          {selecionados.length > 0 && (
            <button
              type="button"
              onClick={() => aoMudar([])}
              className="mt-1 w-full border-t border-slate-100 px-2 py-1.5 text-left text-xs font-medium text-slate-500 hover:text-slate-700"
            >
              Limpar seleção (todos)
            </button>
          )}
        </div>
      )}
    </div>
  );
}
