import { useMemo, useState } from 'react';
import { Building2, ChevronDown, ChevronRight, KeyRound } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { CredencialSisregSecao } from '@/features/sisreg-mapeamento/components/CredencialSisregSecao';

/**
 * Lista todas as unidades com um ponto de entrada para cadastrar/configurar a senha do
 * SISREG de cada uma. Expandir a unidade revela o cartão de credencial compartilhado
 * ({@link CredencialSisregSecao}), que autentica no SISREG e confere a unidade antes de
 * gravar. A credencial por unidade pertence ao módulo SisregMapeamento — sem essa
 * permissão o back recusaria; por isso a seção só aparece com ela.
 */
export function SisregSenhasPorUnidadeSecao() {
  const podeGerir = useTemConsulta('SisregMapeamento');
  const unidades = useListarUnidades();
  const [filtro, setFiltro] = useState('');
  const [expandida, setExpandida] = useState<string | null>(null);

  const lista = useMemo(() => {
    const termo = filtro.trim().toLowerCase();
    const dados = unidades.data ?? [];
    if (!termo) return dados;
    return dados.filter((u) => u.nome.toLowerCase().includes(termo));
  }, [unidades.data, filtro]);

  if (!podeGerir) return null;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <KeyRound className="h-5 w-5 text-primary-600" />
          Senhas do SISREG por unidade
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          Cada unidade tem a credencial do operador do SISREG dela. Ao salvar, autenticamos no
          SISREG e conferimos se a credencial pertence mesmo à unidade escolhida — se divergir, a
          gravação é barrada. Sem credencial própria, a unidade usa a credencial global.
        </p>
      </header>

      {unidades.data && unidades.data.length > 8 ? (
        <input
          className="input mb-3 w-full"
          placeholder="Filtrar unidade pelo nome…"
          value={filtro}
          onChange={(e) => setFiltro(e.target.value)}
        />
      ) : null}

      {unidades.isLoading ? (
        <p className="text-sm text-gray-500">Carregando unidades…</p>
      ) : unidades.isError ? (
        <p className="text-sm text-red-600">{extrairMensagemDeErro(unidades.error)}</p>
      ) : lista.length === 0 ? (
        <p className="text-sm text-gray-600">Nenhuma unidade encontrada.</p>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
          {lista.map((u) => {
            const aberta = expandida === u.id;
            return (
              <li key={u.id}>
                <button
                  type="button"
                  onClick={() => setExpandida((atual) => (atual === u.id ? null : u.id))}
                  className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-gray-50"
                  aria-expanded={aberta}
                >
                  {aberta ? (
                    <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
                  ) : (
                    <ChevronRight className="h-4 w-4 shrink-0 text-gray-400" />
                  )}
                  <Building2 className="h-4 w-4 shrink-0 text-gray-400" />
                  <span className="flex-1 font-medium text-gray-900">{u.nome}</span>
                  {!u.ativo && (
                    <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">
                      inativa
                    </span>
                  )}
                  <span className="text-xs text-gray-500">
                    {aberta ? 'ocultar' : 'configurar senha'}
                  </span>
                </button>
                {aberta && (
                  <div className="border-t border-gray-100 bg-gray-50 p-3">
                    <CredencialSisregSecao unidadeId={u.id} nomeUnidade={u.nome} />
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
