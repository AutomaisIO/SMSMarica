import { useState } from 'react';
import { ChevronDown, Network } from 'lucide-react';
import { formatarInstante } from '@/shared/lib/datas';
import { useEnderecoSisreg } from '@/features/integracoes/api';
import type { SituacaoEnderecoSisreg } from '@/features/integracoes/types';

const SITUACAO: Record<SituacaoEnderecoSisreg, { texto: string; classe: string }> = {
  NoTunel: { texto: 'Saindo pelo túnel', classe: 'bg-green-100 text-green-700' },
  SaidaDireta: { texto: 'Saída direta (servidor no Brasil)', classe: 'bg-green-100 text-green-700' },
  ForaDoTunel: { texto: 'Fora do túnel — o SISREG não responde', classe: 'bg-red-100 text-red-700' },
  SemDns: { texto: 'Endereço não resolvido', classe: 'bg-amber-100 text-amber-800' },
  RotaDesconhecida: { texto: 'Rota não verificada', classe: 'bg-gray-100 text-gray-600' },
  NaoVerificado: { texto: 'Aguardando a primeira verificação', classe: 'bg-gray-100 text-gray-600' },
};

/**
 * IP atual do SISREG, desde quando, se sai pelo túnel e o histórico das trocas. O SISREG troca de IP
 * sem aviso (09/10/2026: foi para trás do F5) e a produção só o alcança pelo túnel; o servidor põe o
 * IP novo no túnel sozinho e este bloco mostra o resultado — ver docs/sisreg-egress.md.
 */
export function EnderecoSisregStatus() {
  const { data, isLoading, isError } = useEnderecoSisreg();
  const [historicoAberto, setHistoricoAberto] = useState(false);

  if (isLoading) return null;
  if (isError || !data) {
    return <p className="mt-3 text-xs text-gray-400">Não foi possível ler o endereço do SISREG agora.</p>;
  }

  const situacao = SITUACAO[data.situacao];
  const ips = data.atuais.map((a) => a.ip).join(', ');
  const desde = data.atuais
    .map((a) => a.desdeEm)
    .filter((d): d is string => !!d)
    .sort()
    .at(-1);

  return (
    <div className="mt-3 rounded-lg border border-gray-100 bg-gray-50 px-3 py-2 text-xs text-gray-700">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Network className="h-4 w-4 text-gray-400" />
        <span className="font-medium text-gray-800">Endereço do SISREG</span>
        {ips ? <span className="font-mono">{ips}</span> : null}
        {desde ? <span className="text-gray-500">desde {formatarInstante(desde)}</span> : null}
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${situacao.classe}`}>
          {situacao.texto}
          {data.situacao === 'NoTunel' && data.tunelEsperado ? ` (${data.tunelEsperado})` : ''}
        </span>
        {data.historico.length > 0 ? (
          <button
            type="button"
            onClick={() => setHistoricoAberto((a) => !a)}
            className="ml-auto flex items-center gap-1 text-primary-700 hover:underline"
          >
            Histórico ({data.historico.length})
            <ChevronDown className={`h-3.5 w-3.5 transition-transform ${historicoAberto ? 'rotate-180' : ''}`} />
          </button>
        ) : null}
      </div>

      {data.situacao === 'ForaDoTunel' ? (
        <p className="mt-2 rounded-md bg-red-50 px-2 py-1.5 text-red-700">
          A rota até o SISREG está saindo por{' '}
          {data.atuais
            .filter((a) => a.noTunel === false)
            .map((a) => `${a.ip} → ${a.interfaceRota}`)
            .join(', ')}
          , não pelo {data.tunelEsperado}. De fora do Brasil o SISREG não responde e o login falha. O servidor
          costuma corrigir sozinho em até 1 minuto; se persistir, avise o suporte técnico.
        </p>
      ) : null}
      {data.erro ? <p className="mt-2 rounded-md bg-amber-50 px-2 py-1.5 text-amber-800">{data.erro}</p> : null}

      <p className="mt-1 text-[11px] text-gray-400">
        {data.verificadoEm ? `Verificado em ${formatarInstante(data.verificadoEm)} · ` : ''}
        {data.host} · o servidor confere a cada 2 minutos
      </p>

      {historicoAberto ? (
        <table className="mt-2 w-full text-left text-[11px]">
          <thead className="text-gray-500">
            <tr>
              <th className="py-1 pr-2 font-medium">IP</th>
              <th className="py-1 pr-2 font-medium">Desde</th>
              <th className="py-1 pr-2 font-medium">Até</th>
              <th className="py-1 font-medium">Saída</th>
            </tr>
          </thead>
          <tbody>
            {data.historico.map((p) => (
              <tr key={`${p.ip}-${p.desdeEm}`} className="border-t border-gray-100">
                <td className="py-1 pr-2 font-mono">{p.ip}</td>
                <td className="py-1 pr-2">{formatarInstante(p.desdeEm)}</td>
                <td className="py-1 pr-2">
                  {p.ateEm ? (
                    formatarInstante(p.ateEm)
                  ) : (
                    <span className="font-medium text-green-700">atual</span>
                  )}
                </td>
                <td className="py-1 font-mono">{p.interfaceRota ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
