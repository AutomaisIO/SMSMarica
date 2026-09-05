import { useEffect, useLayoutEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react';
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { LARGURA_MIN, clampLargura, useTabelaPreferencias } from '@/shared/ui/tabelaPreferencias';

export type Coluna<T> = {
  chave: string;
  cabecalho: string;
  render: (item: T) => ReactNode;
  className?: string;
  /**
   * Valor de ordenação da linha para esta coluna — presente = cabeçalho clicável.
   * Clique cicla crescente → decrescente → ordem original. Ordena só a página
   * carregada (client-side); vazios (null/undefined) ficam sempre no fim.
   */
  ordenar?: (item: T) => string | number | null | undefined;
};

type Props<T> = {
  colunas: Coluna<T>[];
  dados: T[];
  chaveLinha: (item: T) => string;
  vazio?: ReactNode;
  carregando?: boolean;
  /**
   * Barra de rolagem horizontal FLUTUANTE: quando a tabela é mais larga que a
   * área visível, uma barra fica grudada na base da viewport (enquanto a tabela
   * estiver à vista), para o operador não precisar rolar até o fim para arrastá-la.
   */
  scrollXFlutuante?: boolean;
  /**
   * Torna a LINHA inteira clicável (cursor "dedinho" + tooltip). Cliques em
   * botões/links/inputs dentro da linha são ignorados (as ações da linha seguem
   * funcionando), então não é preciso stopPropagation em cada botão.
   */
  aoClicarLinha?: (item: T) => void;
  /** Tooltip da linha clicável. Default: "Clique para visualizar". */
  dicaLinha?: string;
  /**
   * Classe extra por linha (ex.: destacar urgentes em vermelho claro). Aplicada
   * depois das classes base, então vence conflitos via tailwind-merge — para
   * sobrepor o hover padrão, devolva também o `hover:` correspondente.
   */
  classeLinha?: (item: T) => string | undefined;
  /**
   * Layout fixo (`table-fixed`): as larguras das colunas passam a valer pelas classes
   * `w-*` do <c>className</c> de cada coluna, e o conteúdo trunca em vez de esticar a
   * tabela. Colunas SEM largura definida dividem o espaço restante igualmente. Use para
   * evitar rolagem horizontal quando algumas colunas devem ser fixas e outras "flex".
   */
  layoutFixo?: boolean;
  /**
   * Habilita o redimensionamento de colunas por um separador arrastável no cabeçalho
   * (ticket #99). Exige <c>idTabela</c> para saber sob qual chave salvar as larguras no
   * perfil do usuário. Opt-in: sem esta prop, a tabela renderiza como antes. Ativar isto
   * força <c>table-fixed</c> (as larguras salvas passam a valer em px).
   */
  redimensionavel?: boolean;
  /** Identificador estável desta tela/tabela — chave sob a qual as larguras de coluna são salvas no perfil. */
  idTabela?: string;
};

export function Tabela<T>({ colunas, dados, chaveLinha, vazio, carregando, scrollXFlutuante, aoClicarLinha, dicaLinha, classeLinha, layoutFixo, redimensionavel, idTabela }: Props<T>) {
  // Defensivo: se a API retornar algo não-array (HTML por URL errada, erro
  // serializado, etc.), renderiza vazio em vez de derrubar a tela toda.
  const dadosSeguros: T[] = Array.isArray(dados) ? dados : [];

  const [ordem, setOrdem] = useState<{ chave: string; desc: boolean } | null>(null);
  const colunaOrdenada = ordem ? colunas.find((c) => c.chave === ordem.chave && c.ordenar) : undefined;
  const dadosExibidos = colunaOrdenada
    ? [...dadosSeguros].sort((a, b) => {
        const va = colunaOrdenada.ordenar!(a);
        const vb = colunaOrdenada.ordenar!(b);
        if (va == null && vb == null) return 0;
        if (va == null) return 1; // vazios sempre no fim, independente da direção
        if (vb == null) return -1;
        const r =
          typeof va === 'number' && typeof vb === 'number'
            ? va - vb
            : String(va).localeCompare(String(vb), 'pt-BR', { numeric: true, sensitivity: 'base' });
        return ordem!.desc ? -r : r;
      })
    : dadosSeguros;

  function aoClicarCabecalho(chave: string) {
    setOrdem((atual) =>
      atual?.chave !== chave ? { chave, desc: false } : atual.desc ? null : { chave, desc: true },
    );
  }

  // ── Redimensionamento de coluna (opt-in via `redimensionavel` + `idTabela`) ──────────
  const redim = Boolean(redimensionavel && idTabela);
  const largurasSalvas = useTabelaPreferencias((s) => (idTabela ? s.larguras[idTabela] : undefined));
  const definirVarias = useTabelaPreferencias((s) => s.definirVarias);
  // Estado "ao vivo" durante o arrasto; re-semeado quando o perfil hidrata/muda.
  const [larguras, setLarguras] = useState<Record<string, number>>(() => ({ ...(largurasSalvas ?? {}) }));
  useEffect(() => {
    setLarguras({ ...(largurasSalvas ?? {}) });
  }, [largurasSalvas]);
  /**
   * Arrasto de uma FRONTEIRA entre duas colunas vizinhas.
   *
   * A alça não estica uma coluna sozinha: ela move o limite entre a coluna dona e a seguinte, e
   * o que uma ganha a outra cede. Duas consequências, e são as que importam:
   *
   * - a SOMA das larguras não muda, então `w-full table-fixed` não tem sobra para redistribuir e
   *   a largura que você define gruda (antes ela voltava um pouco sozinha);
   * - a primeira coluna fica colada no início e a ÚLTIMA colada no fim da tabela, sempre. A
   *   última não tem alça própria porque não tem vizinha à direita — quem a ajusta é a alça da
   *   penúltima, que é exatamente o mesmo gesto.
   */
  const arrasto = useRef<{
    chave: string;
    chaveVizinha: string;
    startX: number;
    startWidth: number;
    startWidthVizinha: number;
  } | null>(null);
  const [emArrasto, setEmArrasto] = useState(false);
  // Só vira table-fixed quando há largura (salva ou em edição): antes disso a tabela
  // mantém o layout automático (colunas ajustam ao conteúdo), sem regressão visual.
  const temLarguras = redim && Object.keys(larguras).length > 0;
  const larguraFixa = layoutFixo || temLarguras;
  const colunasNoCabecalho = colunas.length;

  function iniciarArrasto(e: ReactPointerEvent<HTMLDivElement>, chave: string, chaveVizinha: string) {
    const th = (e.currentTarget as HTMLElement).closest('th');
    const thVizinha = th?.nextElementSibling as HTMLElement | null;
    if (!th || !thVizinha || !chaveVizinha) return;
    // Primeiro arrasto sem larguras salvas: semeia TODAS as colunas com a largura
    // renderizada atual, para congelar o visual exato antes de entrar em table-fixed.
    if (Object.keys(larguras).length === 0) {
      const tr = th.parentElement;
      const ths = tr ? Array.from(tr.querySelectorAll('th')) : [];
      if (ths.length === colunas.length) {
        const semente: Record<string, number> = {};
        colunas.forEach((c, idx) => {
          semente[c.chave] = clampLargura(ths[idx].getBoundingClientRect().width);
        });
        setLarguras(semente);
      }
    }
    arrasto.current = {
      chave,
      chaveVizinha,
      startX: e.clientX,
      startWidth: th.getBoundingClientRect().width,
      startWidthVizinha: thVizinha.getBoundingClientRect().width,
    };
    setEmArrasto(true);
    e.currentTarget.setPointerCapture(e.pointerId);
    e.preventDefault();
    e.stopPropagation();
  }

  /**
   * Quanto a fronteira pode andar sem esmagar nenhuma das duas colunas: para a direita, o que a
   * vizinha tem acima do mínimo; para a esquerda, o que esta coluna tem acima do mínimo.
   */
  function deltaPermitido(st: NonNullable<typeof arrasto.current>, clientX: number): number {
    const bruto = clientX - st.startX;
    const paraDireita = Math.max(0, st.startWidthVizinha - LARGURA_MIN);
    const paraEsquerda = Math.max(0, st.startWidth - LARGURA_MIN);
    return Math.round(Math.min(paraDireita, Math.max(-paraEsquerda, bruto)));
  }

  function aplicarArrasto(st: NonNullable<typeof arrasto.current>, clientX: number) {
    const delta = deltaPermitido(st, clientX);
    return {
      [st.chave]: st.startWidth + delta,
      [st.chaveVizinha]: st.startWidthVizinha - delta,
    };
  }

  function moverArrasto(e: ReactPointerEvent<HTMLDivElement>) {
    const st = arrasto.current;
    if (!st) return;
    setLarguras((m) => ({ ...m, ...aplicarArrasto(st, e.clientX) }));
  }

  function terminarArrasto(e: ReactPointerEvent<HTMLDivElement>) {
    const st = arrasto.current;
    if (!st) return;
    arrasto.current = null;
    setEmArrasto(false);
    const ajuste = aplicarArrasto(st, e.clientX);
    setLarguras((m) => {
      const proximo = { ...m, ...ajuste };
      // Persiste o mapa inteiro (inclui as colunas semeadas no 1º arrasto) em 1 PUT.
      if (idTabela) definirVarias(idTabela, proximo);
      return proximo;
    });
  }

  const scrollerRef = useRef<HTMLDivElement>(null);
  const barraRef = useRef<HTMLDivElement>(null);
  const [larguraConteudo, setLarguraConteudo] = useState(0);
  const [precisaBarra, setPrecisaBarra] = useState(false);
  // Largura visível do scroller — usada para NÃO deixar a soma das colunas salvas (em px, de uma
  // tela mais larga) estourar num monitor/janela menor, empurrando a última coluna (ações) para
  // fora da borda. Como o arrasto preserva a soma, sem isto não há como "recolher para caber".
  const [larguraDisponivel, setLarguraDisponivel] = useState(0);

  useLayoutEffect(() => {
    if (!scrollXFlutuante) return;
    const el = scrollerRef.current;
    if (!el) return;
    const medir = () => {
      setLarguraConteudo(el.scrollWidth);
      setPrecisaBarra(el.scrollWidth > el.clientWidth + 1);
    };
    medir();
    const ro = new ResizeObserver(medir);
    ro.observe(el);
    window.addEventListener('resize', medir);
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', medir);
    };
  }, [scrollXFlutuante, dadosSeguros.length, colunas.length]);

  const sincronizarDaBarra = () => {
    if (scrollerRef.current && barraRef.current) scrollerRef.current.scrollLeft = barraRef.current.scrollLeft;
  };
  const sincronizarDoScroller = () => {
    if (scrollerRef.current && barraRef.current) barraRef.current.scrollLeft = scrollerRef.current.scrollLeft;
  };

  const escondeBarraNativa = scrollXFlutuante && precisaBarra;

  // Mede a largura visível do scroller enquanto a tabela é redimensionável (independe de
  // `scrollXFlutuante`), para reescalar as colunas quando a soma salva não couber.
  useLayoutEffect(() => {
    if (!redim) return;
    const el = scrollerRef.current;
    if (!el) return;
    const medir = () => setLarguraDisponivel(el.clientWidth);
    medir();
    const ro = new ResizeObserver(medir);
    ro.observe(el);
    window.addEventListener('resize', medir);
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', medir);
    };
  }, [redim]);

  // Só reescala quando TODAS as colunas têm largura (estado de arrasto/perfil, nunca parcial) e a
  // soma passa da área visível. O fator vale só na renderização do <colgroup>; a preferência
  // salva continua intacta, então em telas largas as larguras originais voltam a valer (fator 1).
  const somaLarguras = redim ? colunas.reduce((s, c) => s + (larguras[c.chave] ?? 0), 0) : 0;
  const todasComLargura = redim && colunas.every((c) => (larguras[c.chave] ?? 0) > 0);
  const fatorEscala =
    todasComLargura && larguraDisponivel > 0 && somaLarguras > larguraDisponivel
      ? larguraDisponivel / somaLarguras
      : 1;

  return (
    <div className="rounded-lg border border-gray-200 bg-white shadow-sm">
      <div
        ref={scrollerRef}
        onScroll={scrollXFlutuante ? sincronizarDoScroller : undefined}
        className={cn(
          'overflow-x-auto rounded-lg',
          // Esconde a barra nativa (embaixo, longe) — a flutuante assume o papel.
          escondeBarraNativa && '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
        )}
      >
        <table
          className={cn(
            'divide-y divide-gray-200',
            larguraFixa ? 'w-full table-fixed' : 'min-w-full',
            emArrasto && 'select-none',
          )}
        >
          {redim ? (
            <colgroup>
              {colunas.map((c) => (
                <col
                  key={c.chave}
                  style={larguras[c.chave] ? { width: Math.floor(larguras[c.chave] * fatorEscala) } : undefined}
                />
              ))}
            </colgroup>
          ) : null}
          <thead className="bg-gray-50">
            <tr>
              {colunas.map((c, i) => {
                const ativa = ordem?.chave === c.chave && Boolean(c.ordenar);
                // A alça move a FRONTEIRA entre esta coluna e a seguinte, então a última não tem
                // uma: não há vizinha à direita para ceder/receber. Ela é ajustada pela alça da
                // penúltima, que é o mesmo gesto.
                const podeRedimensionar = redim && i < colunas.length - 1;
                const chaveVizinha = colunas[i + 1]?.chave;
                return (
                  <th
                    key={c.chave}
                    className={cn(
                      'px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500',
                      redim && 'relative',
                      c.className,
                    )}
                  >
                    {c.ordenar ? (
                      <button
                        type="button"
                        onClick={() => aoClicarCabecalho(c.chave)}
                        title={
                          !ativa
                            ? 'Ordenar por esta coluna'
                            : ordem!.desc
                              ? 'Ordenado ↓ — clique para voltar à ordem original'
                              : 'Ordenado ↑ — clique para inverter'
                        }
                        className={cn(
                          'inline-flex items-center gap-1 uppercase tracking-wide',
                          ativa ? 'text-gray-800' : 'hover:text-gray-700',
                        )}
                      >
                        {c.cabecalho}
                        {ativa ? (
                          ordem!.desc ? (
                            <ChevronDown className="h-3.5 w-3.5 shrink-0" />
                          ) : (
                            <ChevronUp className="h-3.5 w-3.5 shrink-0" />
                          )
                        ) : (
                          <ChevronsUpDown className="h-3 w-3 shrink-0 text-gray-300" />
                        )}
                      </button>
                    ) : (
                      c.cabecalho
                    )}
                    {podeRedimensionar ? (
                      <div
                        role="separator"
                        aria-orientation="vertical"
                        title="Arraste para ajustar a largura da coluna"
                        onPointerDown={(e) => iniciarArrasto(e, c.chave, chaveVizinha!)}
                        onPointerMove={moverArrasto}
                        onPointerUp={terminarArrasto}
                        onPointerCancel={terminarArrasto}
                        onClick={(e) => e.stopPropagation()}
                        className="absolute right-0 top-0 z-10 flex h-full w-3 cursor-col-resize touch-none items-center justify-center hover:bg-primary-100/60"
                      >
                        <span className="h-4 w-px bg-gray-300" />
                      </div>
                    ) : null}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {carregando ? (
              <tr>
                <td colSpan={colunasNoCabecalho} className="px-4 py-8 text-center text-sm text-gray-500">
                  Carregando…
                </td>
              </tr>
            ) : dadosSeguros.length === 0 ? (
              <tr>
                <td colSpan={colunasNoCabecalho} className="px-4 py-8 text-center text-sm text-gray-500">
                  {vazio ?? 'Nenhum registro encontrado.'}
                </td>
              </tr>
            ) : (
              dadosExibidos.map((item) => (
                <tr
                  key={chaveLinha(item)}
                  className={cn('hover:bg-gray-50', aoClicarLinha && 'cursor-pointer', classeLinha?.(item))}
                  title={aoClicarLinha ? (dicaLinha ?? 'Clique para visualizar') : undefined}
                  onClick={
                    aoClicarLinha
                      ? (e) => {
                          // Ignora cliques em elementos interativos (botões/links/inputs).
                          if ((e.target as HTMLElement).closest('button, a, input, select, [role="button"]')) return;
                          aoClicarLinha(item);
                        }
                      : undefined
                  }
                >
                  {colunas.map((c) => (
                    <td
                      key={c.chave}
                      className={cn('px-4 py-3 text-sm text-gray-700', c.className)}
                    >
                      {c.render(item)}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {escondeBarraNativa ? (
        <div
          ref={barraRef}
          onScroll={sincronizarDaBarra}
          aria-hidden="true"
          className={cn(
            'sticky bottom-0 z-10 overflow-x-auto border-t border-primary-100 bg-white/90 backdrop-blur',
            // Barra VERMELHA (tema Maricá) e mais visível — a cinza padrão do SO ficava discreta.
            '[scrollbar-width:auto] [scrollbar-color:#C8102E_transparent]',
            '[&::-webkit-scrollbar]:h-3',
            '[&::-webkit-scrollbar-track]:rounded-full [&::-webkit-scrollbar-track]:bg-primary-50',
            '[&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-primary-600',
            '[&::-webkit-scrollbar-thumb:hover]:bg-primary-700',
          )}
        >
          <div style={{ width: larguraConteudo }} className="h-3" />
        </div>
      ) : null}
    </div>
  );
}
