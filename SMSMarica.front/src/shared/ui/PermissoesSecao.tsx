import { useMemo } from 'react';
import { useQueries } from '@tanstack/react-query';
import { obterPerfilPorId } from '@/features/perfis/api/perfisApi';
import { useListarPerfis } from '@/features/perfis/api/queries';
import { deMatriz, paraMatriz } from '@/features/perfis/lib/acoes';
import type { MatrizEdicao } from '@/features/perfis/types';
import { MatrizPermissoes } from '@/features/perfis/components/MatrizPermissoes';
import { cn } from '@/shared/lib/cn';
import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';

type Props = {
  perfilIdsSelecionados: string[];
  aoMudarPerfilIds: (ids: string[]) => void;
  overrides: MatrizEdicao;
  aoMudarOverrides: (m: MatrizEdicao) => void;
  desabilitado?: boolean;
};

function unirMatrizes(a: MatrizEdicao, b: MatrizEdicao): MatrizEdicao {
  const out: MatrizEdicao = {};
  const modulos = new Set<ModuloPermissao>([
    ...(Object.keys(a) as ModuloPermissao[]),
    ...(Object.keys(b) as ModuloPermissao[]),
  ]);
  for (const m of modulos) {
    const set = new Set<AcaoPermissao>([...(a[m] ?? []), ...(b[m] ?? [])]);
    out[m] = Array.from(set);
  }
  return out;
}

/**
 * Seção compartilhada para gestão de permissões RBAC de um usuário (qualquer
 * papel). Mostra a lista de perfis disponíveis e a matriz de overrides com
 * herdadas dos perfis selecionados. Reutilizada em Usuário, Médico e Motorista.
 *
 * Estado controlado pelo pai — perfil/overrides são apenas espelhados aqui.
 * O caller decide o que fazer com eles (chamar PUT após cadastro ou junto).
 */
export function PermissoesSecao({
  perfilIdsSelecionados,
  aoMudarPerfilIds,
  overrides,
  aoMudarOverrides,
  desabilitado,
}: Props) {
  const perfisDisponiveis = useListarPerfis();

  const perfisCompletos = useQueries({
    queries: perfilIdsSelecionados.map((id) => ({
      queryKey: ['perfis', 'detalhe', id],
      queryFn: () => obterPerfilPorId(id),
      enabled: Boolean(id),
    })),
  });

  const herdadas = useMemo<MatrizEdicao>(() => {
    let resultado: MatrizEdicao = {};
    for (const q of perfisCompletos) {
      if (q.data) {
        resultado = unirMatrizes(resultado, paraMatriz(q.data.permissoes));
      }
    }
    return resultado;
  }, [perfisCompletos]);

  function alternarPerfil(id: string) {
    aoMudarPerfilIds(
      perfilIdsSelecionados.includes(id)
        ? perfilIdsSelecionados.filter((x) => x !== id)
        : [...perfilIdsSelecionados, id],
    );
  }

  const perfisAtivos = (perfisDisponiveis.data ?? []).filter((p) => p.ativo);
  const carregandoHerdadas = perfisCompletos.some((q) => q.isLoading);

  return (
    <div className="space-y-6">
      <section>
        <h3 className="mb-2 text-sm font-semibold text-gray-900">Perfis</h3>
        <p className="mb-3 text-xs text-gray-500">
          Selecione um ou mais perfis. As permissões herdadas aparecem marcadas e bloqueadas na
          matriz abaixo.
        </p>
        {perfisDisponiveis.isLoading ? (
          <p className="text-sm text-gray-500">Carregando perfis...</p>
        ) : perfisAtivos.length === 0 ? (
          <p className="text-sm text-gray-500">Nenhum perfil cadastrado. Crie em "Cadastros → Perfis".</p>
        ) : (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {perfisAtivos.map((p) => {
              const selecionado = perfilIdsSelecionados.includes(p.id);
              return (
                <label
                  key={p.id}
                  className={cn(
                    'flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2 transition-colors',
                    selecionado ? 'border-primary-500 bg-primary-50' : 'border-gray-200 hover:bg-gray-50',
                  )}
                >
                  <input
                    type="checkbox"
                    checked={selecionado}
                    onChange={() => alternarPerfil(p.id)}
                    disabled={desabilitado}
                    className="mt-1"
                  />
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-gray-900">{p.nome}</div>
                    {p.descricao ? (
                      <div className="text-xs text-gray-600">{p.descricao}</div>
                    ) : null}
                    <div className="text-xs text-gray-400">{p.modulos} módulo(s)</div>
                  </div>
                </label>
              );
            })}
          </div>
        )}
      </section>

      <section>
        <div className="mb-2 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-900">Permissões resolvidas</h3>
          {carregandoHerdadas ? (
            <span className="text-xs text-gray-500">Recalculando herdadas…</span>
          ) : null}
        </div>
        <p className="mb-3 text-xs text-gray-500">
          Ações com fundo cinza são <strong>herdadas dos perfis</strong> selecionados (não podem
          ser desmarcadas). Marque ações adicionais como override individual.
        </p>
        <MatrizPermissoes
          herdadas={herdadas}
          editaveis={overrides}
          aoMudarEditaveis={aoMudarOverrides}
          desabilitado={desabilitado}
        />
      </section>
    </div>
  );
}

/** Helper para converter MatrizEdicao em formato da API. */
export const matrizParaApi = deMatriz;
