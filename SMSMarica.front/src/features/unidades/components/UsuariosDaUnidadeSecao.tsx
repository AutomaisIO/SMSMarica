import { useMemo, useState } from 'react';
import { Search, Star, Trash2, UserPlus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Input } from '@/shared/ui/Input';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useListarUsuarios } from '@/features/usuarios/api/queries';
import {
  useAdicionarUsuarioNaUnidade,
  useRemoverUsuarioDaUnidade,
  useUsuariosDaUnidade,
} from '@/features/unidades/api/queries';
import type { UsuarioDaUnidade } from '@/features/unidades/api/unidadesApi';

type Props = { unidadeId: string };

/**
 * Aba "Usuários" do detalhe da unidade: quem tem vínculo com a unidade
 * (multitenant), com busca para incluir novos usuários e remoção do vínculo.
 */
export function UsuariosDaUnidadeSecao({ unidadeId }: Props) {
  const vinculados = useUsuariosDaUnidade(unidadeId);
  const todos = useListarUsuarios();
  const adicionar = useAdicionarUsuarioNaUnidade();
  const remover = useRemoverUsuarioDaUnidade();
  const [busca, setBusca] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const idsVinculados = useMemo(
    () => new Set((vinculados.data ?? []).map((v) => v.usuarioId)),
    [vinculados.data],
  );

  // Candidatos à inclusão: usuários ativos, fora da unidade, casando com a busca.
  const termo = busca.trim().toLowerCase();
  const candidatos = useMemo(() => {
    if (termo.length < 2) return [];
    return (todos.data ?? [])
      .filter((u) => u.ativo && !idsVinculados.has(u.id))
      .filter(
        (u) =>
          u.nomeCompleto.toLowerCase().includes(termo) ||
          (u.email ?? '').toLowerCase().includes(termo),
      )
      .slice(0, 8);
  }, [todos.data, idsVinculados, termo]);

  async function aoAdicionar(usuarioId: string) {
    setErro(null);
    try {
      await adicionar.mutateAsync({ unidadeId, usuarioId });
      setBusca('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoRemover(usuarioId: string) {
    setErro(null);
    try {
      await remover.mutateAsync({ unidadeId, usuarioId });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const colunas: Coluna<UsuarioDaUnidade>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (v) => (
        <span className="flex items-center gap-1.5 font-medium text-gray-900">
          {v.nomeCompleto}
          {v.principal ? (
            <span title="Unidade principal deste usuário">
              <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" />
            </span>
          ) : null}
        </span>
      ),
    },
    { chave: 'email', cabecalho: 'E-mail', render: (v) => v.email ?? '—' },
    { chave: 'status', cabecalho: 'Status', render: (v) => <StatusBadge ativo={v.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (v) => (
        <div className="flex justify-end">
          <BotaoLinhaAcao tom="perigo" onClick={() => aoRemover(v.usuarioId)}>
            <Trash2 className="w-3.5 h-3.5" /> Remover
          </BotaoLinhaAcao>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div>
        <label htmlFor="buscaUsuarioUnidade" className="label">
          Incluir usuário na unidade
        </label>
        <div className="relative max-w-md">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
          <Input
            id="buscaUsuarioUnidade"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Digite nome ou e-mail (mín. 2 letras)…"
            className="pl-9"
          />
        </div>
        {termo.length >= 2 ? (
          <div className="mt-2 max-w-md divide-y divide-gray-100 rounded-md border border-gray-200 bg-white shadow-sm">
            {todos.isLoading ? (
              <p className="px-3 py-2 text-sm text-gray-500">Carregando usuários…</p>
            ) : candidatos.length === 0 ? (
              <p className="px-3 py-2 text-sm text-gray-500">
                Nenhum usuário encontrado fora da unidade.
              </p>
            ) : (
              candidatos.map((u) => (
                <button
                  key={u.id}
                  type="button"
                  onClick={() => aoAdicionar(u.id)}
                  disabled={adicionar.isPending}
                  className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-gray-50"
                >
                  <UserPlus className="h-4 w-4 shrink-0 text-primary-600" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium text-gray-900">{u.nomeCompleto}</span>
                    {u.email ? (
                      <span className="block truncate text-xs text-gray-500">{u.email}</span>
                    ) : null}
                  </span>
                  <span className="text-xs text-primary-600">Incluir</span>
                </button>
              ))
            )}
          </div>
        ) : null}
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={vinculados.data ?? []}
        chaveLinha={(v) => v.usuarioId}
        carregando={vinculados.isLoading}
        vazio={
          !vinculados.isLoading && (vinculados.data?.length ?? 0) === 0
            ? 'Nenhum usuário vinculado a esta unidade.'
            : undefined
        }
      />
    </div>
  );
}
