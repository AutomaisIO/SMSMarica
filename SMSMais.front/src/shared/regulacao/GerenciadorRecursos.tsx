import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronDown, Stethoscope } from 'lucide-react';

import { Modal } from '@/shared/ui/Modal';
import type { RecursoNotificacao } from '@/shared/regulacao/recursos';

type Props = {
  id?: string;
  recursos: RecursoNotificacao[];
  selecionados: string[];
  aoMudar: (recursos: string[]) => void;
};

/** Cor do chip de tipo dentro do modal, só para distinguir Consulta de Exame na lista. */
const COR_CHIP: Record<string, string> = {
  Consulta: 'bg-sky-100 text-sky-700',
  Exame: 'bg-violet-100 text-violet-700',
};
const corChip = (tipo: string) => COR_CHIP[tipo] ?? 'bg-slate-100 text-slate-600';

/**
 * Filtro por recurso (procedimento/especialidade) das Notificações da regulação.
 *
 * <p>São centenas de recursos — um dropdown não serve. O botão de fora (no estilo do filtro por
 * técnico) abre um <b>modal</b> com busca, checkboxes e um atalho "só os selecionados", para a
 * pessoa gerenciar a própria seleção sem se perder; a seleção só vale ao <b>Salvar</b>.</p>
 *
 * <p>O botão mostra só o <b>total</b> de procedimentos marcados (ou "Todos" quando nada está). O
 * tipo (Consulta/Exame) aparece como chip ao lado de cada item dentro do modal, para distinguir —
 * não vira badge colorido do lado de fora.</p>
 */
export function GerenciadorRecursos({ id, recursos, selecionados, aoMudar }: Props) {
  const [aberto, setAberto] = useState(false);
  const [rascunho, setRascunho] = useState<string[]>(selecionados);
  const [busca, setBusca] = useState('');
  const [soSelecionados, setSoSelecionados] = useState(false);

  // Ao abrir, o rascunho parte da seleção vigente; cancelar/fechar descarta, Salvar aplica.
  useEffect(() => {
    if (aberto) {
      setRascunho(selecionados);
      setBusca('');
      setSoSelecionados(false);
    }
  }, [aberto, selecionados]);

  // Lista do modal: catálogo + os órfãos marcados (selecionados que sumiram do catálogo), para
  // dar para desmarcar. Filtra por busca e pelo atalho "só selecionados".
  const opcoes = useMemo(() => {
    const conhecidos = new Set(recursos.map((r) => r.recurso));
    const orfaos: RecursoNotificacao[] = rascunho
      .filter((r) => !conhecidos.has(r))
      .map((recurso) => ({ recurso, tipo: 'Outros', pendentes: 0 }));
    const termo = busca.trim().toUpperCase();
    return [...orfaos, ...recursos]
      .filter((r) => !soSelecionados || rascunho.includes(r.recurso))
      .filter((r) => !termo || r.recurso.toUpperCase().includes(termo));
  }, [recursos, rascunho, busca, soSelecionados]);

  function alternar(recurso: string) {
    setRascunho((atual) =>
      atual.includes(recurso) ? atual.filter((r) => r !== recurso) : [...atual, recurso],
    );
  }

  function salvar() {
    aoMudar(rascunho);
    setAberto(false);
  }

  return (
    <>
      <button
        id={id}
        type="button"
        onClick={() => setAberto(true)}
        className={`flex items-center gap-2 rounded border px-2 py-1 text-sm ${
          selecionados.length > 0
            ? 'border-red-700 bg-red-50 text-red-800'
            : 'border-slate-300 bg-white text-slate-700'
        }`}
      >
        <Stethoscope className={`size-4 ${selecionados.length > 0 ? 'text-red-700' : 'text-slate-400'}`} />
        {selecionados.length === 0
          ? 'Todos'
          : `${selecionados.length} ${selecionados.length === 1 ? 'procedimento' : 'procedimentos'}`}
        <ChevronDown className="size-4 shrink-0 text-slate-400" />
      </button>

      <Modal
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        titulo="Recursos do filtro"
        descricao="Marque os procedimentos que a tela deve mostrar. Nada marcado = todos."
        largura="lg"
      >
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <input
              type="search"
              autoFocus
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              placeholder="Buscar procedimento… (ex.: cardio)"
              className="min-w-0 flex-1 rounded border border-slate-300 px-3 py-2 text-sm"
            />
            <label className="flex items-center gap-1.5 whitespace-nowrap text-sm text-slate-600">
              <input
                type="checkbox"
                checked={soSelecionados}
                onChange={(e) => setSoSelecionados(e.target.checked)}
                className="size-4 accent-red-700"
              />
              Só os selecionados
            </label>
          </div>

          <div className="max-h-[52vh] overflow-y-auto rounded border border-slate-200">
            {opcoes.length === 0 ? (
              <p className="px-3 py-6 text-center text-sm text-slate-400">
                {soSelecionados ? 'Nenhum procedimento selecionado.' : 'Nenhum procedimento encontrado.'}
              </p>
            ) : (
              opcoes.map((r) => {
                const marcado = rascunho.includes(r.recurso);
                return (
                  <button
                    key={r.recurso}
                    type="button"
                    onClick={() => alternar(r.recurso)}
                    className="flex w-full items-center gap-2 border-b border-slate-100 px-3 py-2 text-left text-sm text-slate-700 last:border-b-0 hover:bg-slate-50"
                  >
                    <span
                      className={`flex size-4 shrink-0 items-center justify-center rounded border ${
                        marcado ? 'border-red-700 bg-red-700 text-white' : 'border-slate-300 bg-white'
                      }`}
                    >
                      {marcado && <Check className="size-3" />}
                    </span>
                    <span className="min-w-0 flex-1 truncate">{r.recurso}</span>
                    <span className={`shrink-0 rounded px-1.5 py-0.5 text-xs ${corChip(r.tipo)}`}>
                      {r.tipo}
                    </span>
                    {r.pendentes > 0 && (
                      <span className="shrink-0 rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-700">
                        {r.pendentes}
                      </span>
                    )}
                  </button>
                );
              })
            )}
          </div>

          <div className="flex items-center justify-between gap-2 border-t border-slate-100 pt-3">
            <div className="flex items-center gap-3 text-sm text-slate-600">
              <span>{rascunho.length} selecionado(s)</span>
              {rascunho.length > 0 && (
                <button
                  type="button"
                  onClick={() => setRascunho([])}
                  className="font-medium text-slate-500 hover:text-slate-700"
                >
                  Limpar
                </button>
              )}
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setAberto(false)}
                className="rounded border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={salvar}
                className="rounded bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-800"
              >
                Salvar
              </button>
            </div>
          </div>
        </div>
      </Modal>
    </>
  );
}
