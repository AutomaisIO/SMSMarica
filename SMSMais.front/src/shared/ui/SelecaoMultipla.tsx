import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';
import { Input } from '@/shared/ui/Input';

export type OpcaoSelecao = {
  valor: string;
  rotulo: string;
  /** Texto à direita, em cinza (ex.: quantidade). */
  detalhe?: string;
};

type Props = {
  id?: string;
  opcoes: OpcaoSelecao[];
  selecionados: string[];
  aoMudar: (valores: string[]) => void;
  /** O que o botão diz com nada marcado — nada marcado = sem filtro. */
  rotuloVazio?: string;
  /** Campo de busca no topo da lista. Liga sozinho com mais de 8 opções. */
  comBusca?: boolean;
  placeholderBusca?: string;
  /** Largura da lista aberta (classe Tailwind). */
  larguraLista?: string;
  desabilitado?: boolean;
};

function normalizar(s: string): string {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .toLowerCase();
}

/**
 * Dropdown de marcar várias opções: busca, "Marcar todas" / "Desmarcar todas" e o resumo no
 * botão. Nada marcado = sem filtro (o botão diz {@link Props.rotuloVazio}).
 *
 * "Marcar todas" com busca digitada marca só as VISÍVEIS — é o atalho para "todos os
 * procedimentos de fisioterapia" sem clicar um a um.
 */
export function SelecaoMultipla({
  id,
  opcoes,
  selecionados,
  aoMudar,
  rotuloVazio = 'Todas',
  comBusca,
  placeholderBusca = 'Filtrar…',
  larguraLista = 'w-96',
  desabilitado,
}: Props) {
  const [aberto, setAberto] = useState(false);
  const [busca, setBusca] = useState('');
  const container = useRef<HTMLDivElement>(null);
  const mostrarBusca = comBusca ?? opcoes.length > 8;

  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (container.current && !container.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  const visiveis = useMemo(() => {
    const termo = normalizar(busca);
    return termo ? opcoes.filter((o) => normalizar(o.rotulo).includes(termo)) : opcoes;
  }, [opcoes, busca]);

  const marcados = new Set(selecionados);

  function alternar(valor: string) {
    aoMudar(marcados.has(valor) ? selecionados.filter((v) => v !== valor) : [...selecionados, valor]);
  }

  function marcarVisiveis() {
    aoMudar([...new Set([...selecionados, ...visiveis.map((o) => o.valor)])]);
  }

  function desmarcarVisiveis() {
    const sair = new Set(visiveis.map((o) => o.valor));
    aoMudar(busca ? selecionados.filter((v) => !sair.has(v)) : []);
  }

  const rotulo =
    selecionados.length === 0
      ? rotuloVazio
      : selecionados.length === 1
        ? (opcoes.find((o) => o.valor === selecionados[0])?.rotulo ?? '1 selecionada')
        : `${selecionados.length} selecionadas`;

  return (
    <div className="relative" ref={container}>
      <button
        id={id}
        type="button"
        disabled={desabilitado}
        onClick={() => setAberto((a) => !a)}
        className="input flex items-center justify-between gap-2 text-left disabled:opacity-60"
      >
        <span className={selecionados.length === 0 ? 'truncate text-gray-500' : 'truncate font-medium'}>
          {rotulo}
        </span>
        <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
      </button>

      {aberto ? (
        <div
          className={`absolute left-0 z-20 mt-1 max-w-[calc(100vw-2rem)] rounded-md border border-gray-200 bg-white p-2 shadow-lg ${larguraLista}`}
        >
          {mostrarBusca ? (
            <div className="relative mb-2">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
              <Input
                autoFocus
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                placeholder={placeholderBusca}
                className="!py-1 !pl-7 text-xs"
              />
            </div>
          ) : null}

          <div className="mb-1 flex items-center justify-between gap-2 border-b border-gray-100 px-1 pb-1.5 text-xs">
            <span className="text-gray-500">
              {selecionados.length} de {opcoes.length} marcada(s)
            </span>
            <span className="flex gap-3">
              <button
                type="button"
                onClick={marcarVisiveis}
                disabled={visiveis.length === 0}
                className="font-medium text-primary-700 hover:underline disabled:opacity-50"
              >
                {busca ? 'Marcar as filtradas' : 'Marcar todas'}
              </button>
              <button
                type="button"
                onClick={desmarcarVisiveis}
                disabled={selecionados.length === 0}
                className="font-medium text-gray-600 hover:underline disabled:opacity-50"
              >
                {busca ? 'Desmarcar as filtradas' : 'Desmarcar todas'}
              </button>
            </span>
          </div>

          <div className="max-h-72 overflow-y-auto">
            {visiveis.length === 0 ? (
              <p className="px-2 py-3 text-center text-xs text-gray-500">Nada encontrado.</p>
            ) : (
              visiveis.map((o) => {
                const marcado = marcados.has(o.valor);
                return (
                  <button
                    key={o.valor}
                    type="button"
                    onClick={() => alternar(o.valor)}
                    className="flex w-full items-start gap-2 rounded px-2 py-1 text-left text-sm text-gray-700 hover:bg-gray-50"
                  >
                    <span
                      className={`mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded border ${
                        marcado ? 'border-primary-600 bg-primary-600 text-white' : 'border-gray-300 bg-white'
                      }`}
                    >
                      {marcado ? <Check className="h-3 w-3" /> : null}
                    </span>
                    <span className="min-w-0 flex-1 break-words leading-snug">{o.rotulo}</span>
                    {o.detalhe ? <span className="shrink-0 text-xs tabular-nums text-gray-400">{o.detalhe}</span> : null}
                  </button>
                );
              })
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
