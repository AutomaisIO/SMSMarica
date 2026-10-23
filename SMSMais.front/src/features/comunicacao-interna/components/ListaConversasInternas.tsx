import { useEffect, useState } from 'react';
import { BellOff, Loader2, Plus, Search, X } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { useBuscaInterna, useConversasInternas } from '@/features/comunicacao-interna/api/queries';
import { quandoFmt } from '@/features/comunicacao-interna/lib/formatar';
import { textoLegivel } from '@/features/comunicacao-interna/lib/selos';
import { useOnline } from '@/features/comunicacao-interna/store/internoStore';
import { AvatarPessoa, AvatarUnidade } from '@/features/comunicacao-interna/components/Avatar';
import type { ConversaInternaItem, ResultadoBuscaInterna } from '@/features/comunicacao-interna/types';

type Props = {
  conversaAtivaId: string | null;
  aoSelecionar: (conversaId: string, mensagemAlvoId?: string) => void;
  aoNova: () => void;
};

/**
 * Coluna da esquerda: busca (em tudo que eu enxergo — texto e nome de anexo) e a lista de
 * conversas pela última mensagem. Com termo de 2+ letras, a lista dá lugar aos resultados.
 */
export function ListaConversasInternas({ conversaAtivaId, aoSelecionar, aoNova }: Props) {
  const [termo, setTermo] = useState('');
  const [termoBusca, setTermoBusca] = useState('');
  useEffect(() => {
    const t = setTimeout(() => setTermoBusca(termo), 300);
    return () => clearTimeout(t);
  }, [termo]);

  const buscando = termoBusca.trim().length >= 2;
  const conversas = useConversasInternas();
  const busca = useBuscaInterna(termoBusca);

  return (
    <div className="flex h-full min-h-0 flex-col bg-white">
      <div className="flex items-center gap-1.5 border-b border-gray-100 p-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
          <input
            value={termo}
            onChange={(e) => setTermo(e.target.value)}
            placeholder="Buscar nas conversas"
            className="w-full rounded-full border border-gray-200 bg-gray-50 py-1.5 pl-8 pr-7 text-xs outline-none focus:border-orange-300 focus:bg-white"
            aria-label="Buscar nas conversas"
          />
          {termo && (
            <button
              type="button"
              onClick={() => setTermo('')}
              className="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-gray-400 hover:text-gray-600"
              aria-label="Limpar a busca"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
        <button
          type="button"
          onClick={aoNova}
          className="flex shrink-0 items-center gap-1 rounded-full bg-orange-500 px-2.5 py-1.5 text-xs font-medium text-white hover:bg-orange-600"
          title="Nova conversa — escolha a unidade e a pessoa (ou todos)"
        >
          <Plus className="h-3.5 w-3.5" /> Nova
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {buscando ? (
          <ResultadosBusca
            termo={termoBusca.trim()}
            carregando={busca.isLoading}
            erro={busca.isError}
            resultados={busca.data ?? []}
            aoAbrir={(r) => aoSelecionar(r.conversaId, r.mensagemId)}
          />
        ) : conversas.isLoading ? (
          <p className="flex justify-center py-8 text-gray-400">
            <Loader2 className="h-5 w-5 animate-spin" />
          </p>
        ) : conversas.isError ? (
          <p className="px-4 py-8 text-center text-xs text-red-600">Não foi possível carregar as conversas.</p>
        ) : (conversas.data ?? []).length === 0 ? (
          <div className="px-4 py-8 text-center text-xs text-gray-500">
            <p>Nenhuma conversa ainda.</p>
            <p className="mt-1">
              Clique em <b>Nova</b>, escolha a unidade e fale com uma pessoa ou com todos dela.
            </p>
          </div>
        ) : (
          (conversas.data ?? []).map((c) => (
            <ItemConversa key={c.id} c={c} ativo={c.id === conversaAtivaId} aoClicar={() => aoSelecionar(c.id)} />
          ))
        )}
      </div>
    </div>
  );
}

function ItemConversa({ c, ativo, aoClicar }: { c: ConversaInternaItem; ativo: boolean; aoClicar: () => void }) {
  const online = useOnline(c.outroUsuarioId, c.outroOnline);
  const previa = textoLegivel(c.ultimaMensagemPreview);
  return (
    <button
      type="button"
      onClick={aoClicar}
      className={cn(
        'flex w-full items-center gap-2.5 border-b border-gray-50 px-3 py-2 text-left hover:bg-orange-50/60',
        ativo && 'bg-orange-50',
      )}
    >
      {c.tipo === 'Direta' && c.outroUsuarioId ? (
        <AvatarPessoa id={c.outroUsuarioId} nome={c.titulo} online={online} />
      ) : (
        <AvatarUnidade equipe={c.unidadeAId === c.unidadeBId} />
      )}
      <span className="min-w-0 flex-1">
        <span className="flex items-baseline gap-1.5">
          <span className={cn('min-w-0 flex-1 truncate text-sm', c.naoLidas > 0 ? 'font-semibold text-gray-900' : 'text-gray-800')}>
            {c.titulo}
          </span>
          <span className={cn('shrink-0 text-[10px]', c.naoLidas > 0 ? 'font-semibold text-orange-600' : 'text-gray-400')}>
            {quandoFmt(c.ultimaMensagemEm)}
          </span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="min-w-0 flex-1 truncate text-xs text-gray-500">
            {previa
              ? c.tipo === 'Unidade' && c.ultimaMensagemAutorNome
                ? `${primeiroNome(c.ultimaMensagemAutorNome)}: ${previa}`
                : previa
              : (c.subtitulo ?? '')}
          </span>
          {c.silenciada && <BellOff className="h-3 w-3 shrink-0 text-gray-300" aria-label="Silenciada" />}
          {c.naoLidas > 0 && (
            <span
              className={cn(
                'inline-flex h-4 min-w-4 shrink-0 items-center justify-center rounded-full px-1 text-[10px] font-bold text-white',
                c.silenciada ? 'bg-gray-400' : 'bg-orange-500',
              )}
            >
              {c.naoLidas > 99 ? '99+' : c.naoLidas}
            </span>
          )}
        </span>
      </span>
    </button>
  );
}

function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}

function ResultadosBusca({
  termo,
  carregando,
  erro,
  resultados,
  aoAbrir,
}: {
  termo: string;
  carregando: boolean;
  erro: boolean;
  resultados: ResultadoBuscaInterna[];
  aoAbrir: (r: ResultadoBuscaInterna) => void;
}) {
  if (carregando) {
    return (
      <p className="flex justify-center py-8 text-gray-400">
        <Loader2 className="h-5 w-5 animate-spin" />
      </p>
    );
  }
  if (erro) return <p className="px-4 py-8 text-center text-xs text-red-600">Não foi possível buscar.</p>;
  if (resultados.length === 0) {
    return <p className="px-4 py-8 text-center text-xs text-gray-500">Nada encontrado para “{termo}”.</p>;
  }
  return (
    <>
      <p className="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        {resultados.length} {resultados.length === 1 ? 'mensagem' : 'mensagens'}
      </p>
      {resultados.map((r) => (
        <button
          key={r.mensagemId}
          type="button"
          onClick={() => aoAbrir(r)}
          className="block w-full border-b border-gray-50 px-3 py-2 text-left hover:bg-orange-50/60"
        >
          <span className="flex items-baseline gap-1.5">
            <span className="min-w-0 flex-1 truncate text-xs font-semibold text-gray-800">{r.conversaTitulo}</span>
            <span className="shrink-0 text-[10px] text-gray-400">{quandoFmt(r.enviadaEm)}</span>
          </span>
          <span className="block truncate text-[11px] text-gray-500">{r.autorNome}</span>
          <span className="mt-0.5 line-clamp-2 block text-xs text-gray-700">
            <Destaque texto={textoLegivel(r.trecho)} termo={termo} />
          </span>
        </button>
      ))}
    </>
  );
}

function semAcento(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/** Marca o termo no trecho, ignorando acento e maiúsculas (como a busca do servidor). */
function Destaque({ texto, termo }: { texto: string; termo: string }) {
  const alvo = semAcento(termo);
  const base = semAcento(texto);
  const i = alvo ? base.indexOf(alvo) : -1;
  // A normalização não muda o comprimento dos caracteres latinos comuns — dá para fatiar o original.
  if (i < 0 || base.length !== texto.length) return <>{texto}</>;
  return (
    <>
      {texto.slice(0, i)}
      <mark className="rounded bg-orange-200/70 px-0.5 text-gray-900">{texto.slice(i, i + alvo.length)}</mark>
      {texto.slice(i + alvo.length)}
    </>
  );
}
