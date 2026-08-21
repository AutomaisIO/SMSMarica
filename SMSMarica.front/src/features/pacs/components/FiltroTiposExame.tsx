import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { useListarTiposExame } from '@/features/tipos-exame/api/queries';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

type Props = {
  id?: string;
  /** Modalidades marcadas no filtro ao lado. Vazio = mostra os tipos de todas. */
  modalidades: ModalidadeDicom[];
  selecionados: string[];
  aoMudar: (ids: string[]) => void;
};

/**
 * Checkbox de TIPO de exame, agrupado por modalidade — o recorte fino de quem lauda MG e OT mas
 * não lauda tudo dentro delas.
 *
 * Diferente da modalidade, este filtro não existe no DICOM: quem sabe qual procedimento do SISREG
 * foi pedido é o pedido, do nosso lado. Por isso ele é aplicado pelo servidor, que pagina a lista
 * — e por isso, com tipo marcado, o exame ainda SEM associação sai da lista (não tem pedido, logo
 * não tem tipo). A tela avisa quantos ficaram de fora.
 */
export function FiltroTiposExame({ id, modalidades, selecionados, aoMudar }: Props) {
  const [aberto, setAberto] = useState(false);
  const [busca, setBusca] = useState('');
  const container = useRef<HTMLDivElement>(null);

  // Uma lista só, filtrada no cliente: são ~50 tipos ativos, não vale uma consulta por modalidade.
  const tipos = useListarTiposExame().data ?? [];

  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (container.current && !container.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  const grupos = useMemo(() => {
    const termo = busca
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .trim()
      .toLowerCase();
    const visiveis = tipos.filter((t) => {
      if (!t.ativo) return false;
      if (modalidades.length > 0 && !modalidades.includes(t.modalidadeDicom)) return false;
      if (!termo) return true;
      return t.nome
        .normalize('NFD')
        .replace(/[̀-ͯ]/g, '')
        .toLowerCase()
        .includes(termo);
    });
    const porModalidade = new Map<ModalidadeDicom, typeof visiveis>();
    for (const t of visiveis) {
      const lista = porModalidade.get(t.modalidadeDicom) ?? [];
      lista.push(t);
      porModalidade.set(t.modalidadeDicom, lista);
    }
    return [...porModalidade.entries()]
      .map(([mod, lista]) => [mod, [...lista].sort((a, b) => a.nome.localeCompare(b.nome))] as const)
      .sort((a, b) => a[0].localeCompare(b[0]));
  }, [tipos, modalidades, busca]);

  function alternar(idTipo: string) {
    aoMudar(
      selecionados.includes(idTipo)
        ? selecionados.filter((x) => x !== idTipo)
        : [...selecionados, idTipo],
    );
  }

  function alternarGrupo(ids: string[]) {
    const todosMarcados = ids.every((i) => selecionados.includes(i));
    aoMudar(
      todosMarcados
        ? selecionados.filter((i) => !ids.includes(i))
        : [...new Set([...selecionados, ...ids])],
    );
  }

  const rotulo = selecionados.length === 0 ? 'Todos' : `${selecionados.length} selecionado(s)`;

  return (
    <div className="relative" ref={container}>
      <button
        id={id}
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="input flex items-center justify-between gap-2 text-left"
      >
        <span className={selecionados.length === 0 ? 'text-gray-500' : 'truncate font-medium'}>{rotulo}</span>
        <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
      </button>

      {aberto ? (
        <div className="absolute right-0 z-20 mt-1 w-80 rounded-md border border-gray-200 bg-white p-2 shadow-lg">
          <div className="relative mb-2">
            <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
            <Input
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              placeholder="Filtrar tipos…"
              className="!py-1 !pl-7 text-xs"
            />
          </div>

          <div className="max-h-72 overflow-y-auto">
            {grupos.length === 0 ? (
              <p className="px-2 py-3 text-center text-xs text-gray-500">
                {modalidades.length > 0
                  ? 'Nenhum tipo nas modalidades marcadas.'
                  : 'Nenhum tipo encontrado.'}
              </p>
            ) : (
              grupos.map(([mod, lista]) => {
                const ids = lista.map((t) => t.id);
                const todos = ids.every((i) => selecionados.includes(i));
                return (
                  <div key={mod} className="mb-1">
                    <button
                      type="button"
                      onClick={() => alternarGrupo(ids)}
                      className="flex w-full items-center justify-between rounded px-2 py-1 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500 hover:bg-gray-50"
                    >
                      <span>{mod}</span>
                      <span className="text-[10px] font-medium normal-case text-gray-400">
                        {todos ? 'limpar' : 'marcar todos'}
                      </span>
                    </button>
                    {lista.map((t) => {
                      const marcado = selecionados.includes(t.id);
                      return (
                        <button
                          key={t.id}
                          type="button"
                          onClick={() => alternar(t.id)}
                          className="flex w-full items-start gap-2 rounded px-2 py-1 text-left text-sm text-gray-700 hover:bg-gray-50"
                        >
                          <span
                            className={`mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded border ${
                              marcado ? 'border-primary-600 bg-primary-600 text-white' : 'border-gray-300 bg-white'
                            }`}
                          >
                            {marcado ? <Check className="h-3 w-3" /> : null}
                          </span>
                          <span className="min-w-0 flex-1 break-words leading-snug">{t.nome}</span>
                        </button>
                      );
                    })}
                  </div>
                );
              })
            )}
          </div>

          {selecionados.length > 0 ? (
            <button
              type="button"
              onClick={() => aoMudar([])}
              className="mt-1 w-full border-t border-gray-100 px-2 py-1.5 text-left text-xs font-medium text-gray-500 hover:text-gray-700"
            >
              Limpar seleção
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
