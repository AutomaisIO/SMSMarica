import type { ReactNode } from 'react';
import { Ban, CircleCheck, CircleHelp, TriangleAlert } from 'lucide-react';

import { useRespostasRegras } from '../api/solicitacoesQueries';
import type { RespostaRegraRegistrada, ResultadoRegraRegulacao } from '../tiposSolicitacao';

const ICONE: Record<ResultadoRegraRegulacao, ReactNode> = {
  Atende: <CircleCheck className="size-4 shrink-0 text-emerald-600" />,
  Bloqueia: <Ban className="size-4 shrink-0 text-red-600" />,
  Ressalva: <TriangleAlert className="size-4 shrink-0 text-amber-600" />,
  Indefinido: <CircleHelp className="size-4 shrink-0 text-slate-400" />,
};

function textoResposta(r: RespostaRegraRegistrada): string | null {
  if (r.resposta === 'Deduzido') return null;
  if (r.opcoes.length > 0) {
    if (r.resposta === 'Nao') return 'Nenhuma destas';
    if (r.resposta === 'NaoSei') return 'Não sei';
    return null; // as marcadas aparecem logo abaixo
  }
  return r.resposta === 'Sim' ? 'Sim' : r.resposta === 'Nao' ? 'Não' : 'Não sei';
}

/**
 * As regras do manual como ficaram respondidas na solicitação — a leitura do regulador.
 *
 * <p><b>Não reavalia nada.</b> O GET de elegibilidade reavalia e regrava; aqui é só o que a
 * unidade respondeu e o que o sistema deduziu. Na pergunta de lista aparecem as condições
 * marcadas, que é o que diz ao regulador por que o pedido cabe naquele procedimento.</p>
 *
 * <p>Regra informativa não entra: ela não é respondida, só lida na hora de pedir.</p>
 */
export function RespostasRegras({ solicitacaoId }: { solicitacaoId: string }) {
  const respostas = useRespostasRegras(solicitacaoId);
  const lista = (respostas.data ?? []).filter((r) => r.tipo !== 'Informativa');
  const vigentes = lista.filter((r) => r.vigente);
  const substituidas = lista.filter((r) => !r.vigente);

  if (respostas.isLoading || lista.length === 0) return null;

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h2 className="mb-3 text-sm font-semibold text-slate-900">Regras do manual</h2>
      {vigentes.length > 0 ? (
        <ListaRespostas itens={vigentes} />
      ) : (
        <p className="text-sm text-slate-500">As regras respondidas foram substituídas depois.</p>
      )}
      {/* Regra trocada depois de respondida: a resposta é história — não decide mais nada. */}
      {substituidas.length > 0 && (
        <details className="mt-3 text-slate-500">
          <summary className="cursor-pointer text-xs">
            Respostas a regras que foram substituídas depois ({substituidas.length})
          </summary>
          <div className="mt-2 opacity-70">
            <ListaRespostas itens={substituidas} />
          </div>
        </details>
      )}
    </section>
  );
}

function ListaRespostas({ itens }: { itens: RespostaRegraRegistrada[] }) {
  return (
    <ul className="space-y-2">
      {itens.map((r) => {
        const resposta = textoResposta(r);
        return (
          <li key={r.regraId} className="flex items-start gap-2 text-sm">
            {ICONE[r.resultado]}
            <div className="min-w-0">
              <p className="text-slate-800">{r.pergunta ?? r.descricao}</p>
              {resposta && <p className="text-xs text-slate-600">Resposta: {resposta}</p>}
              {r.opcoesMarcadas.length > 0 && (
                <ul className="mt-0.5 list-inside list-disc text-xs text-slate-700">
                  {r.opcoesMarcadas.map((o) => (
                    <li key={o.id}>{o.texto}</li>
                  ))}
                </ul>
              )}
              {r.motivo && <p className="text-xs text-slate-500">{r.motivo}</p>}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
