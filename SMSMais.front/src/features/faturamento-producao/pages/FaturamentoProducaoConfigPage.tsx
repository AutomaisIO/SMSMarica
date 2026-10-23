import { Link } from 'react-router-dom';
import { Loader2, Settings2, ShieldX } from 'lucide-react';

import {
  useConfiguracaoFaturamento,
  useDefinirUnidadeHabilitada,
} from '@/features/faturamento-producao/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAuth } from '@/shared/auth/authStore';
import { notificar } from '@/shared/ui/Notificacoes';

export function FaturamentoProducaoConfigPage() {
  const podeEditar = useAuth((s) => (s.permissoes.FaturamentoProducao ?? []).includes('Edicao'));
  const config = useConfiguracaoFaturamento();
  const definir = useDefinirUnidadeHabilitada();

  if (!podeEditar) {
    return (
      <div className="mx-auto max-w-lg py-16 text-center">
        <ShieldX className="mx-auto size-10 text-slate-400" />
        <h1 className="mt-3 text-lg font-semibold text-slate-900">Sem acesso às Configurações</h1>
        <p className="mt-2 text-sm text-slate-600">
          Habilitar unidades para o BPA depende da ação <strong>Edição</strong> no módulo Faturamento —
          Produção. Peça a quem administra os perfis.
        </p>
        <Link to="/app/faturamento-producao" className="mt-5 inline-block rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700">
          Voltar ao Faturamento
        </Link>
      </div>
    );
  }

  async function alternar(unidadeId: string, habilitada: boolean) {
    try {
      await definir.mutateAsync({ unidadeId, habilitada });
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <div className="mx-auto max-w-[900px] space-y-4 p-4">
      <header className="flex items-center gap-2">
        <Settings2 className="size-6 text-red-600" />
        <div>
          <h1 className="text-lg font-semibold text-slate-900">Faturamento — Configurações</h1>
          <p className="text-sm text-slate-500">
            Marque as unidades que ficam liberadas para o usuário final gerar o BPA de produção.
          </p>
        </div>
      </header>

      {config.isPending && (
        <div className="flex items-center justify-center gap-2 p-8 text-slate-500">
          <Loader2 className="size-5 animate-spin" /> Carregando unidades…
        </div>
      )}

      {config.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {extrairMensagemDeErro(config.error)}
        </div>
      )}

      {config.data && (
        <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-3 py-2">Unidade</th>
                <th className="px-3 py-2">CNES</th>
                <th className="px-3 py-2 text-center">Habilitada</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {config.data.map((u) => (
                <tr key={u.id}>
                  <td className="px-3 py-2">{u.nome}</td>
                  <td className="px-3 py-2 tabular-nums text-slate-600">{u.cnes ?? '—'}</td>
                  <td className="px-3 py-2 text-center">
                    <input
                      type="checkbox"
                      className="size-4 accent-red-600"
                      checked={u.habilitada}
                      disabled={definir.isPending}
                      onChange={(e) => void alternar(u.id, e.target.checked)}
                    />
                  </td>
                </tr>
              ))}
              {config.data.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-3 py-8 text-center text-slate-500">
                    Nenhuma unidade no seu escopo.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
