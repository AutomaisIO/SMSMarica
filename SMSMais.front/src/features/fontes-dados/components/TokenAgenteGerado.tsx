import { useState } from 'react';
import { Check, Copy, KeyRound } from 'lucide-react';
import type { TokenAgente } from '@/features/fontes-dados/types';

/**
 * Token do agente recém-gerado. Aparece UMA vez — o servidor guarda só o hash. Fica dentro do
 * modal, não num aviso no canto: é o desfecho de um ato que derruba o agente antigo.
 */
export function TokenAgenteGerado({ token }: { token: TokenAgente }) {
  const [copiado, setCopiado] = useState<'token' | 'url' | null>(null);

  async function copiar(texto: string, qual: 'token' | 'url') {
    try {
      await navigator.clipboard.writeText(texto);
      setCopiado(qual);
    } catch {
      setCopiado(null);
    }
  }

  return (
    <div className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
      <p className="flex items-center gap-2 font-medium">
        <KeyRound className="h-4 w-4" />
        Token do agente <code className="rounded bg-white px-1">{token.slug}</code> — copie agora, ele não aparece de novo.
      </p>
      <Linha rotulo="Token" valor={token.token} copiado={copiado === 'token'} aoCopiar={() => copiar(token.token, 'token')} />
      <Linha rotulo="Endereço" valor={token.wssUrl} copiado={copiado === 'url'} aoCopiar={() => copiar(token.wssUrl, 'url')} />
      <p className="text-xs">
        Cole os dois no <code>.env</code> do agente, no servidor da unidade, e reinicie o serviço dele. Até
        isso, o agente fica desconectado.
      </p>
    </div>
  );
}

function Linha({ rotulo, valor, copiado, aoCopiar }: { rotulo: string; valor: string; copiado: boolean; aoCopiar: () => void }) {
  return (
    <div className="flex items-center gap-2">
      <span className="w-16 shrink-0 text-xs font-medium">{rotulo}</span>
      <code className="min-w-0 flex-1 truncate rounded bg-white px-2 py-1 font-mono text-xs">{valor}</code>
      <button
        type="button"
        onClick={aoCopiar}
        className="inline-flex items-center gap-1 rounded border border-amber-300 bg-white px-2 py-1 text-xs hover:bg-amber-100"
      >
        {copiado ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
        {copiado ? 'Copiado' : 'Copiar'}
      </button>
    </div>
  );
}
