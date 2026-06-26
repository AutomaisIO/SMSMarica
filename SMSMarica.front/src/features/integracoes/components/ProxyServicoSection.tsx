import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useProxyMotores } from '@/features/integracoes/api';
import { ProxyMotorCard } from '@/features/integracoes/components/ProxyMotorCard';
import type { ServicoProxy } from '@/features/integracoes/types';

/**
 * Seção de um serviço de proxy (CPF ou CEP): lista os motores suportados como cards.
 * Cada motor pode ser ativado/desativado e ordenado na cadeia de fallback — o orquestrador
 * tenta os ativos por ordem e cai para o próximo quando um falha.
 */
export function ProxyServicoSection({
  servico,
  titulo,
  descricao,
}: {
  servico: ServicoProxy;
  titulo: string;
  descricao: string;
}) {
  const motores = useProxyMotores(servico);

  return (
    <section className="space-y-3">
      <div>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">{titulo}</h2>
        <p className="text-xs text-gray-500">{descricao}</p>
      </div>

      {motores.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(motores.error)}
        </div>
      ) : null}
      {motores.isLoading ? (
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {(motores.data ?? []).map((m) => (
          <ProxyMotorCard key={m.motor} servico={servico} motor={m} />
        ))}
      </div>
    </section>
  );
}
