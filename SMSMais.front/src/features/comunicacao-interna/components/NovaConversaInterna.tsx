import { useMemo, useState } from 'react';
import { ArrowLeft, Building2, ChevronRight, Loader2, Search, Users } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';
import {
  useAbrirConversaInterna,
  useContextoInterno,
  useUnidadesInternas,
  useUsuariosDaUnidadeInterna,
} from '@/features/comunicacao-interna/api/queries';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import { AvatarPessoa, PontoOnline } from '@/features/comunicacao-interna/components/Avatar';
import type { UnidadeInterna, UsuarioInterno } from '@/features/comunicacao-interna/types';

type Props = {
  aoAbrir: (conversaId: string) => void;
  aoVoltar: () => void;
};

function semAcento(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/**
 * Nova conversa: escolhe a unidade de destino e, AO LADO, aparecem as pessoas dela (com quem
 * está online) e, no topo, "Todos da unidade". Escrever para a mesma unidade/pessoa de novo cai
 * na mesma conversa — a sessão é única por par (ADR-0079 §2).
 */
export function NovaConversaInterna({ aoAbrir, aoVoltar }: Props) {
  const [filtro, setFiltro] = useState('');
  const [unidadeId, setUnidadeId] = useState<string | null>(null);
  const unidades = useUnidadesInternas(true);
  const contexto = useContextoInterno();

  const lista = useMemo(() => {
    const f = semAcento(filtro.trim());
    const todas = unidades.data ?? [];
    return f ? todas.filter((u) => semAcento(u.nome).includes(f)) : todas;
  }, [unidades.data, filtro]);

  const selecionada = unidades.data?.find((u) => u.id === unidadeId) ?? null;

  return (
    <div className="flex h-full min-h-0 flex-col bg-white">
      <div className="flex items-center gap-2 border-b border-gray-200 px-3 py-2">
        <button type="button" onClick={aoVoltar} className="rounded-md p-1 text-gray-500 hover:bg-gray-100" aria-label="Voltar">
          <ArrowLeft className="h-4 w-4" />
        </button>
        <p className="text-sm font-semibold text-gray-900">Nova conversa</p>
        <p className="hidden truncate text-xs text-gray-400 sm:block">— escolha a unidade e depois a pessoa (ou todos)</p>
      </div>

      <div className="flex min-h-0 flex-1">
        {/* Unidades */}
        <div className={cn('flex min-h-0 flex-col border-r border-gray-100 sm:w-64 sm:shrink-0', unidadeId ? 'hidden sm:flex' : 'flex flex-1')}>
          <div className="relative p-2">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
            <input
              value={filtro}
              onChange={(e) => setFiltro(e.target.value)}
              placeholder="Buscar unidade"
              autoFocus
              className="w-full rounded-full border border-gray-200 bg-gray-50 py-1.5 pl-8 pr-3 text-xs outline-none focus:border-orange-300 focus:bg-white"
              aria-label="Buscar unidade"
            />
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto">
            {unidades.isLoading ? (
              <p className="flex justify-center py-8 text-gray-400">
                <Loader2 className="h-5 w-5 animate-spin" />
              </p>
            ) : unidades.isError ? (
              <p className="px-4 py-8 text-center text-xs text-red-600">Não foi possível carregar as unidades.</p>
            ) : lista.length === 0 ? (
              <p className="px-4 py-8 text-center text-xs text-gray-500">Nenhuma unidade encontrada.</p>
            ) : (
              lista.map((u) => (
                <ItemUnidade key={u.id} u={u} ativa={u.id === unidadeId} aoClicar={() => setUnidadeId(u.id)} />
              ))
            )}
          </div>
        </div>

        {/* Pessoas da unidade escolhida */}
        <div className={cn('min-h-0 flex-1 flex-col', unidadeId ? 'flex' : 'hidden sm:flex')}>
          {selecionada ? (
            <PessoasDaUnidade
              key={selecionada.id}
              unidade={selecionada}
              minhasUnidades={contexto.data?.minhasUnidades ?? []}
              unidadeAtualId={contexto.data?.unidadeAtualId ?? null}
              aoAbrir={aoAbrir}
              aoVoltar={() => setUnidadeId(null)}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-2 p-6 text-center text-xs text-gray-400">
              <Building2 className="h-8 w-8 text-gray-300" />
              Escolha uma unidade para ver quem está nela.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function ItemUnidade({ u, ativa, aoClicar }: { u: UnidadeInterna; ativa: boolean; aoClicar: () => void }) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      className={cn('flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-orange-50/60', ativa && 'bg-orange-50')}
    >
      <Building2 className="h-4 w-4 shrink-0 text-orange-500" />
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-1.5">
          <span className="truncate text-sm text-gray-800">{u.nome}</span>
          {u.minha && (
            <span className="shrink-0 rounded-full bg-orange-100 px-1.5 text-[10px] font-medium text-orange-700">minha</span>
          )}
        </span>
        <span className="flex items-center gap-1 text-[11px] text-gray-500">
          {u.online > 0 && <PontoOnline online className="h-2 w-2 ring-0" />}
          {u.usuarios} {u.usuarios === 1 ? 'pessoa' : 'pessoas'}
          {u.online > 0 ? ` · ${u.online} online` : ''}
        </span>
      </span>
      <ChevronRight className="h-4 w-4 shrink-0 text-gray-300" />
    </button>
  );
}

function PessoasDaUnidade({
  unidade,
  minhasUnidades,
  unidadeAtualId,
  aoAbrir,
  aoVoltar,
}: {
  unidade: UnidadeInterna;
  minhasUnidades: { unidadeId: string; nome: string; principal: boolean }[];
  unidadeAtualId: string | null;
  aoAbrir: (conversaId: string) => void;
  aoVoltar: () => void;
}) {
  const usuarios = useUsuariosDaUnidadeInterna(unidade.id);
  const abrir = useAbrirConversaInterna();
  const presenca = useInterno((s) => s.presenca);
  const [erro, setErro] = useState<string | null>(null);

  // "Falando como": a unidade em que estou agora; senão a principal; senão a primeira.
  const padrao =
    minhasUnidades.find((u) => u.unidadeId === unidadeAtualId)?.unidadeId ??
    minhasUnidades.find((u) => u.principal)?.unidadeId ??
    minhasUnidades[0]?.unidadeId ??
    null;
  // O contexto pode chegar depois da montagem: só vira estado quando a pessoa escolhe outra.
  const [escolhida, setEscolhida] = useState<string | null>(null);
  const origemId = escolhida ?? padrao;
  const semVinculo = minhasUnidades.length === 0;
  const mesmaUnidade = origemId === unidade.id;

  const ordenados = useMemo(() => {
    const lista = (usuarios.data ?? []).map((p) => ({ ...p, online: presenca[p.id] ?? p.online }));
    return lista.sort((a, b) => (a.online === b.online ? a.nome.localeCompare(b.nome, 'pt-BR') : a.online ? -1 : 1));
  }, [usuarios.data, presenca]);

  async function abrirTodos() {
    setErro(null);
    try {
      const c = await abrir.mutateAsync({ tipo: 'Unidade', unidadeDestinoId: unidade.id, unidadeOrigemId: origemId });
      aoAbrir(c.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function abrirPessoa(p: UsuarioInterno) {
    setErro(null);
    try {
      const c = await abrir.mutateAsync({ tipo: 'Direta', usuarioId: p.id });
      aoAbrir(c.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-center gap-2 border-b border-gray-100 px-3 py-2">
        <button
          type="button"
          onClick={aoVoltar}
          className="rounded-md p-1 text-gray-500 hover:bg-gray-100 sm:hidden"
          aria-label="Voltar para as unidades"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <p className="min-w-0 flex-1 truncate text-sm font-semibold text-gray-900">{unidade.nome}</p>
        {minhasUnidades.length > 1 && (
          <label className="flex shrink-0 items-center gap-1 text-[11px] text-gray-500">
            Falando como
            <select
              value={origemId ?? ''}
              onChange={(e) => setEscolhida(e.target.value || null)}
              className="max-w-[9rem] rounded border border-gray-200 bg-white py-0.5 pl-1 pr-5 text-[11px] text-gray-700"
            >
              {minhasUnidades.map((u) => (
                <option key={u.unidadeId} value={u.unidadeId}>
                  {u.nome}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {erro && <p className="px-3 pt-2 text-xs text-red-600">{erro}</p>}

      <div className="min-h-0 flex-1 overflow-y-auto">
        <button
          type="button"
          onClick={() => void abrirTodos()}
          disabled={semVinculo || abrir.isPending}
          className="flex w-full items-center gap-2.5 border-b border-gray-100 px-3 py-2.5 text-left hover:bg-orange-50/60 disabled:cursor-not-allowed disabled:opacity-60"
          title={
            semVinculo
              ? 'Você não está vinculado a nenhuma unidade — peça ao administrador. Mensagem direta continua valendo.'
              : mesmaUnidade
                ? 'Canal da sua equipe: todos desta unidade participam'
                : 'Todos desta unidade e todos da sua unidade participam da mesma conversa'
          }
        >
          <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-orange-500 text-white">
            <Users className="h-4 w-4" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-semibold text-gray-900">
              {mesmaUnidade ? 'Todos da minha equipe' : 'Todos da unidade'}
            </span>
            <span className="block truncate text-xs text-gray-500">
              {semVinculo
                ? 'Indisponível: você não tem unidade vinculada'
                : mesmaUnidade
                  ? 'Conversa da equipe inteira desta unidade'
                  : `Conversa entre ${unidade.nome} e a sua unidade — todos interagem`}
            </span>
          </span>
          {abrir.isPending && <Loader2 className="h-4 w-4 animate-spin text-gray-400" />}
        </button>

        <p className="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
          Ou uma pessoa
        </p>
        {usuarios.isLoading ? (
          <p className="flex justify-center py-6 text-gray-400">
            <Loader2 className="h-5 w-5 animate-spin" />
          </p>
        ) : usuarios.isError ? (
          <p className="px-4 py-6 text-center text-xs text-red-600">Não foi possível carregar as pessoas.</p>
        ) : ordenados.length === 0 ? (
          <p className="px-4 py-6 text-center text-xs text-gray-500">Ninguém vinculado a esta unidade.</p>
        ) : (
          ordenados.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => void abrirPessoa(p)}
              disabled={p.souEu || abrir.isPending}
              className="flex w-full items-center gap-2.5 px-3 py-1.5 text-left hover:bg-orange-50/60 disabled:cursor-default disabled:hover:bg-transparent"
            >
              <AvatarPessoa id={p.id} nome={p.nome} online={p.online} tamanho="sm" />
              <span className="min-w-0 flex-1 truncate text-sm text-gray-800">
                {p.nome}
                {p.souEu && <span className="text-gray-400"> (você)</span>}
              </span>
              <span className={cn('shrink-0 text-[11px]', p.online ? 'text-emerald-600' : 'text-gray-400')}>
                {p.online ? 'online' : 'offline'}
              </span>
            </button>
          ))
        )}
      </div>
    </div>
  );
}
