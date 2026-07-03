import { useListarUnidades } from '@/features/unidades/api/queries';
import { cn } from '@/shared/lib/cn';

export type UnidadeSelecionada = { unidadeId: string; principal: boolean };

type Props = {
  selecionadas: UnidadeSelecionada[];
  aoMudar: (unidades: UnidadeSelecionada[]) => void;
  desabilitado?: boolean;
};

/**
 * Seção de vínculo usuário↔unidades (multitenant): checkbox por unidade ativa +
 * marcação de "principal" (máx. 1 — vira a unidade default ao entrar no sistema).
 * Estado controlado pelo pai; o caller persiste via PUT /usuarios/{id}/unidades.
 */
export function UnidadesSecao({ selecionadas, aoMudar, desabilitado }: Props) {
  const unidades = useListarUnidades();
  const ativas = (unidades.data ?? []).filter((u) => u.ativo);

  function alternar(unidadeId: string) {
    const existente = selecionadas.find((s) => s.unidadeId === unidadeId);
    aoMudar(
      existente
        ? selecionadas.filter((s) => s.unidadeId !== unidadeId)
        : [...selecionadas, { unidadeId, principal: false }],
    );
  }

  function marcarPrincipal(unidadeId: string) {
    aoMudar(selecionadas.map((s) => ({ ...s, principal: s.unidadeId === unidadeId })));
  }

  return (
    <div className="space-y-3">
      <div>
        <h3 className="mb-1 text-sm font-semibold text-gray-900">Unidades do usuário</h3>
        <p className="text-xs text-gray-500">
          O usuário só enxerga os dados das unidades vinculadas (ex.: solicitações da unidade
          executora). Sem nenhum vínculo, vê todas. A unidade <strong>principal</strong> é a
          selecionada automaticamente ao entrar.
        </p>
      </div>

      {unidades.isLoading ? (
        <p className="text-sm text-gray-500">Carregando unidades...</p>
      ) : ativas.length === 0 ? (
        <p className="text-sm text-gray-500">Nenhuma unidade ativa cadastrada.</p>
      ) : (
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {ativas.map((u) => {
            const sel = selecionadas.find((s) => s.unidadeId === u.id);
            return (
              <label
                key={u.id}
                className={cn(
                  'flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2 transition-colors',
                  sel ? 'border-primary-500 bg-primary-50' : 'border-gray-200 hover:bg-gray-50',
                )}
              >
                <input
                  type="checkbox"
                  checked={Boolean(sel)}
                  onChange={() => alternar(u.id)}
                  disabled={desabilitado}
                  className="mt-1"
                />
                <div className="min-w-0 flex-1">
                  <div className="text-sm font-medium text-gray-900">{u.nome}</div>
                  {(u.cidade || u.uf) && (
                    <div className="text-xs text-gray-500">
                      {[u.cidade, u.uf].filter(Boolean).join(' / ')}
                    </div>
                  )}
                  {sel && (
                    <label
                      className="mt-1 flex w-fit cursor-pointer items-center gap-1.5 text-xs text-gray-600"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <input
                        type="radio"
                        name="unidade-principal"
                        checked={sel.principal}
                        onChange={() => marcarPrincipal(u.id)}
                        disabled={desabilitado}
                      />
                      Principal
                    </label>
                  )}
                </div>
              </label>
            );
          })}
        </div>
      )}
    </div>
  );
}
