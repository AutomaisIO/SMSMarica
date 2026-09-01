import { useState } from 'react';
import { AlertTriangle, Check, Loader2, Tags } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import {
  usePendenciasSigtap,
  useReprocessarPendenciasSigtap,
} from '@/features/importacao-sisreg/api/queries';
import { useConfirmarDeParaSigtap } from '@/features/sisreg-mapeamento/api/queries';
import { useListarProcedimentos } from '@/features/procedimentos-sigtap/api/queries';
import type { PendenciaSigtapAgrupada } from '@/features/importacao-sisreg/types';

/**
 * Fila de mapeamento: procedimentos que a varredura trouxe e não conseguiu classificar.
 *
 * <p>Existe agrupada porque a correção é POR PROCEDIMENTO, não por solicitação. Uma mamografia
 * com 200 agendamentos e sem mapeamento vira 200 pendências idênticas — listadas uma a uma,
 * afogam as pendências que realmente exigem olhar caso a caso (paciente sem CNS, CPF não
 * resolvido). Aqui é 1 linha, 1 mapeamento, 200 solicitações liberadas.</p>
 */
export function PendenciasSigtapSecao() {
  const pendencias = usePendenciasSigtap();
  const [alvo, setAlvo] = useState<PendenciaSigtapAgrupada | null>(null);

  const lista = pendencias.data ?? [];
  if (pendencias.isLoading || lista.length === 0) return null;

  const total = lista.reduce((soma, p) => soma + p.solicitacoes, 0);

  return (
    <>
      <section className="mb-4 rounded-lg border border-amber-200 bg-amber-50">
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-amber-200 px-4 py-3">
          <h3 className="flex items-center gap-2 font-medium text-amber-900">
            <Tags className="h-5 w-5" />
            Procedimentos aguardando código SIGTAP
          </h3>
          <span className="text-sm text-amber-800">
            {lista.length} procedimento{lista.length > 1 ? 's' : ''} · {total} solicitaç
            {total > 1 ? 'ões' : 'ão'}
          </span>
        </header>

        <p className="px-4 pt-3 text-sm text-amber-800">
          O SISREG não informa o código SIGTAP na agenda. Sem ele a solicitação não teria categoria
          nem worklist, então ela fica aqui em vez de entrar errada. Mapeie o procedimento uma vez e
          todas as solicitações dele entram de uma vez.
        </p>

        <ul className="divide-y divide-amber-200 p-2">
          {lista.map((p) => (
            <li
              key={p.procedimentoTexto}
              className="flex flex-wrap items-center gap-3 px-2 py-2 text-sm"
            >
              <span className="flex-1 font-medium text-gray-900">{p.procedimentoTexto}</span>
              {p.codigoSisreg && (
                <span className="font-mono text-xs text-gray-500">{p.codigoSisreg}</span>
              )}
              <span className="rounded bg-white px-2 py-0.5 text-xs font-medium text-amber-800">
                {p.solicitacoes} solicitaç{p.solicitacoes > 1 ? 'ões' : 'ão'}
              </span>
              <Button variante="outline" tamanho="sm" onClick={() => setAlvo(p)}>
                Mapear e importar
              </Button>
            </li>
          ))}
        </ul>
      </section>

      <ModalMapear alvo={alvo} aoFechar={() => setAlvo(null)} />
    </>
  );
}

function ModalMapear({
  alvo,
  aoFechar,
}: {
  alvo: PendenciaSigtapAgrupada | null;
  aoFechar: () => void;
}) {
  const [busca, setBusca] = useState('');
  const [escolhido, setEscolhido] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const confirmar = useConfirmarDeParaSigtap();
  const reprocessar = useReprocessarPendenciasSigtap();
  // Abre já buscando pelo nome que veio do SISREG: na maioria das vezes o certo é o primeiro.
  const catalogo = useListarProcedimentos(busca || (alvo?.procedimentoTexto ?? ''), undefined, 30);

  const pendente = confirmar.isPending || reprocessar.isPending;

  async function aplicar() {
    if (!alvo || !escolhido) return;
    setErro(null);

    try {
      // Duas etapas encadeadas: grava o de-para (para as PRÓXIMAS varreduras nem gerarem
      // pendência) e revalida o que já está preso.
      if (alvo.deParaId) {
        await confirmar.mutateAsync({ id: alvo.deParaId, procedimentoSigtapId: escolhido });
      }
      const r = await reprocessar.mutateAsync(alvo.procedimentoTexto);
      aoFechar();
      setEscolhido(null);
      setBusca('');
      if (r.continuam > 0) setErro(r.mensagem);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal
      aberto={alvo !== null}
      aoFechar={aoFechar}
      titulo="Mapear procedimento para o SIGTAP"
      descricao={
        alvo
          ? `${alvo.procedimentoTexto} — ${alvo.solicitacoes} solicitações aguardando.`
          : undefined
      }
      largura="lg"
    >
      <div className="space-y-3">
        {alvo && !alvo.deParaId && (
          <p className="flex items-start gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            Este procedimento não está no catálogo da unidade — o mapeamento não fica guardado para
            as próximas varreduras. O catálogo se preenche sozinho na próxima importação da agenda
            desta unidade; siga assim mesmo para liberar as solicitações já presas.
          </p>
        )}

        <input
          className="input w-full"
          placeholder="Buscar no catálogo SIGTAP por nome ou código…"
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
        />

        <ul className="max-h-72 divide-y divide-gray-100 overflow-y-auto rounded-md border border-gray-200">
          {catalogo.isLoading ? (
            <li className="p-3 text-sm text-gray-500">Buscando…</li>
          ) : (catalogo.data?.length ?? 0) === 0 ? (
            <li className="p-3 text-sm text-gray-600">Nenhum procedimento encontrado.</li>
          ) : (
            catalogo.data!.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  onClick={() => setEscolhido(p.id)}
                  className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-gray-50 ${
                    escolhido === p.id ? 'bg-emerald-50' : ''
                  }`}
                >
                  {escolhido === p.id ? (
                    <Check className="h-4 w-4 shrink-0 text-emerald-600" />
                  ) : (
                    <span className="h-4 w-4 shrink-0" />
                  )}
                  <span className="font-mono text-xs text-gray-500">{p.codigo}</span>
                  <span className="flex-1 text-gray-800">{p.nome}</span>
                </button>
              </li>
            ))
          )}
        </ul>

        {erro && (
          <p className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            {erro}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <Button variante="ghost" onClick={aoFechar} disabled={pendente}>
            Cancelar
          </Button>
          <Button onClick={aplicar} disabled={!escolhido || pendente}>
            {pendente ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Mapear e importar {alvo?.solicitacoes ?? 0}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
