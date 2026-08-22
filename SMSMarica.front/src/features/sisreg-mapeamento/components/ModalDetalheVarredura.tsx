import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { useVarreduraItens } from '@/features/sisreg-mapeamento/api/queries';
import type { VarreduraExecucao } from '@/features/sisreg-mapeamento/types';

type Props = {
  unidadeId: string;
  execucao: VarreduraExecucao;
  aoFechar: () => void;
};

function data(iso: string | null | undefined): string {
  return iso ? new Date(iso).toLocaleString('pt-BR') : '—';
}

function dataCurta(yyyymmdd: string): string {
  // "2026-08-01" → "01/08/2026", sem passar por Date (que jogaria para o fuso e poderia recuar 1 dia).
  const [a, m, d] = yyyymmdd.split('-');
  return a && m && d ? `${d}/${m}/${a}` : yyyymmdd;
}

/**
 * Detalhe de UMA varredura: o que o agregado da linha do histórico não conta — quantos
 * agendamentos por profissional e procedimento, e quantos viraram solicitação nova. A observação
 * por linha aparece quando algo fugiu do trivial (pendências geradas).
 */
export function ModalDetalheVarredura({ unidadeId, execucao: e, aoFechar }: Props) {
  const itens = useVarreduraItens(unidadeId, e.id);
  const linhas = itens.data ?? [];

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Detalhes da sincronização"
      descricao={`${dataCurta(e.janelaInicio)} a ${dataCurta(e.janelaFim)} · ${
        e.disparo === 'Agendado' ? 'agendada' : 'manual'
      }${e.criadoPorNome ? ` · ${e.criadoPorNome}` : ''}`}
      largura="lg"
    >
      <dl className="mb-4 grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
        <div>
          <dt className="text-xs text-gray-500">Quando</dt>
          <dd className="text-gray-900">{data(e.iniciadoEm)}</dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Cobertura</dt>
          <dd className="text-gray-900">
            {e.combinacoesFeitas}/{e.combinacoesTotal} combinações
          </dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Requisições</dt>
          <dd className="text-gray-900">{e.requisicoes}</dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Novas solicitações</dt>
          <dd className="font-medium text-emerald-700">{Math.max(0, e.validos - e.jaExistiam)}</dd>
        </div>
      </dl>

      {e.mensagemErro && (
        <p
          className={`mb-4 rounded-md border px-3 py-2 text-xs ${
            e.status === 'Erro'
              ? 'border-red-200 bg-red-50 text-red-700'
              : 'border-amber-200 bg-amber-50 text-amber-800'
          }`}
        >
          {e.mensagemErro}
        </p>
      )}

      {itens.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando detalhe…
        </p>
      ) : linhas.length === 0 ? (
        <p className="text-sm text-gray-500">
          Sem detalhe por combinação — esta varredura é anterior ao registro detalhado, ou não chegou
          a rodar nenhuma combinação.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-gray-500">
                <th className="py-1 pr-3 font-medium">Profissional</th>
                <th className="py-1 pr-3 font-medium">Procedimento</th>
                <th className="py-1 pr-3 font-medium">Lidas</th>
                <th className="py-1 pr-3 font-medium">Novas</th>
                <th className="py-1 pr-3 font-medium">Já existiam</th>
                <th className="py-1 pr-3 font-medium">Pendências</th>
                <th className="py-1 pr-3 font-medium">Req.</th>
                <th className="py-1 pr-3 font-medium">Observação</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {linhas.map((i) => {
                const novas = Math.max(0, i.validos - i.jaExistiam);
                return (
                  <tr key={i.id} className="text-gray-700 align-top">
                    <td className="py-1.5 pr-3">{i.profissionalNome}</td>
                    <td className="py-1.5 pr-3">
                      {i.procedimentoNome}
                      <span className="ml-1 text-xs text-gray-400">{i.procedimentoCodigo}</span>
                    </td>
                    <td className="py-1.5 pr-3">{i.registrosEncontrados}</td>
                    <td
                      className={`py-1.5 pr-3 ${
                        novas > 0 ? 'font-semibold text-emerald-700' : 'text-gray-400'
                      }`}
                    >
                      {novas > 0 ? novas : '—'}
                    </td>
                    <td className="py-1.5 pr-3 text-gray-500">
                      {i.jaExistiam > 0 ? i.jaExistiam : '—'}
                    </td>
                    <td className={`py-1.5 pr-3 ${i.invalidos > 0 ? 'text-amber-700' : 'text-gray-400'}`}>
                      {i.invalidos > 0 ? i.invalidos : '—'}
                    </td>
                    <td className="py-1.5 pr-3 text-gray-500">{i.requisicoes}</td>
                    <td className="py-1.5 pr-3 text-xs text-gray-500">{i.observacao ?? '—'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}
