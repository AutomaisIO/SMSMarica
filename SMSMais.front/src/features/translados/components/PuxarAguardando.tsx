import { useState } from 'react';
import { Clock, Hourglass, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { useAguardandoRetorno, usePuxarParaRota } from '@/features/translados/api/queries';

function hhmm(t: string | null): string | null {
  return t ? t.slice(0, 5) : null;
}

/**
 * Volta (decisão de 16/10: depende do destino). Em Maricá a van não espera na unidade: quem desceu entra
 * aqui, com a hora em que deve estar pronto (chegada + tempo médio), e a central puxa para uma rota —
 * o tablet passa a mostrar a busca na unidade e a entrega em casa. Fora do município a van espera; só
 * aparece aqui quem ficou na unidade quando ela saiu.
 */
export function PuxarAguardando({ rotaId }: { rotaId: string }) {
  const podeVer = usePermissao('Rastreamento', 'Consulta');
  const podePuxar = usePermissao('Rastreamento', 'Edicao');
  const lista = useAguardandoRetorno(podeVer);
  const puxar = usePuxarParaRota();
  const [erro, setErro] = useState<string | null>(null);
  const [puxando, setPuxando] = useState<string | null>(null);

  if (!podeVer) return null;

  async function aoPuxar(sessaoId: string) {
    setErro(null);
    setPuxando(sessaoId);
    try {
      await puxar.mutateAsync({ rotaId, sessaoId });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setPuxando(null);
    }
  }

  const itens = lista.data ?? [];
  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <div className="mb-3 flex items-center gap-2">
        <Hourglass className="h-4 w-4 text-gray-500" />
        <h2 className="text-sm font-semibold text-gray-900">Aguardando a volta</h2>
      </div>
      <p className="mb-3 text-xs text-gray-500">
        Em Maricá a van não espera na unidade: quem desceu aparece aqui com a hora em que deve estar pronto. Puxe
        para esta rota e o tablet passa a mostrar a busca na unidade e a entrega em casa.
      </p>
      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {lista.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </p>
      ) : itens.length === 0 ? (
        <p className="text-sm text-gray-500">Ninguém aguardando a volta agora.</p>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-md border border-gray-100">
          {itens.map((p) => (
            <li key={p.sessaoId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
              <div className="min-w-0">
                <div className="font-medium text-gray-900">{p.pacienteNome || 'Paciente'}</div>
                <div className="text-xs text-gray-500">
                  {p.unidadeAtendimentoNome}
                  {p.foraDoMunicipio ? ' · fora do município (ficou na unidade)' : ''}
                </div>
              </div>
              <span className="flex items-center gap-1 text-xs text-gray-600">
                <Clock className="h-3.5 w-3.5" />
                {hhmm(p.prontoPrevisto) ? `pronto por volta de ${hhmm(p.prontoPrevisto)}` : 'aguardando'}
              </span>
              {podePuxar ? (
                <Button
                  variante="outline"
                  tamanho="sm"
                  onClick={() => aoPuxar(p.sessaoId)}
                  disabled={puxar.isPending}
                >
                  {puxando === p.sessaoId ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : null}
                  Puxar para esta rota
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
