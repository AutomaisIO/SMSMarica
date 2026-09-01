import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { useItensMapeamentoLote } from '@/features/sisreg/api/queries';
import type {
  MapeamentoLoteExecucao,
  MapeamentoLoteExecucaoItem,
  ResultadoUnidadeLote,
} from '@/features/sisreg/types';

type Props = { execucao: MapeamentoLoteExecucao; aoFechar: () => void };

const ROTULO_RESULTADO: Record<ResultadoUnidadeLote, string> = {
  Mapeada: 'Mapeada',
  PuladaPorTtl: 'Já atualizada',
  PuladaPorOrcamento: 'Ficou para a próxima',
  Erro: 'Erro',
  SomenteDescoberta: 'Só descoberta',
  SemProfissionais: 'Sem executante',
};

const CLASSE_RESULTADO: Record<ResultadoUnidadeLote, string> = {
  Mapeada: 'bg-emerald-50 text-emerald-700',
  PuladaPorTtl: 'bg-gray-100 text-gray-600',
  PuladaPorOrcamento: 'bg-amber-50 text-amber-800',
  Erro: 'bg-red-50 text-red-700',
  SomenteDescoberta: 'bg-blue-50 text-blue-700',
  SemProfissionais: 'bg-gray-100 text-gray-600',
};

function dataHora(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString('pt-BR') : '—';
}

/**
 * Detalhe de UMA sincronização, unidade a unidade: quantos médicos e quantos procedimentos vieram
 * de cada uma — a pergunta que o agregado da linha não responde — e, para quem não foi ao SISREG,
 * o motivo por extenso. "Pulada" sozinho não diz nada; o operador precisa saber se foi porque já
 * estava atualizada (normal) ou porque o orçamento acabou (volta na próxima).
 */
export function ModalDetalheMapeamentoLote({ execucao: e, aoFechar }: Props) {
  const itens = useItensMapeamentoLote(e.id);
  const linhas = itens.data ?? [];

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Detalhes da sincronização"
      descricao={`${dataHora(e.iniciadoEm)} · ${e.disparo === 'Agendado' ? 'agendada' : 'manual'}${
        e.duracaoSegundos != null ? ` · ${duracao(e.duracaoSegundos)}` : ''
      }`}
      largura="lg"
    >
      <dl className="mb-4 grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
        <Dado rotulo="Unidades no SISREG" valor={e.unidadesNoSisreg || '—'} />
        <Dado
          rotulo="Criadas aqui"
          valor={e.unidadesCriadas || '—'}
          destaque={e.unidadesCriadas > 0}
        />
        <Dado rotulo="Mapeadas" valor={`${e.unidadesMapeadas}/${e.unidadesTotal}`} />
        <Dado rotulo="Requisições" valor={e.requisicoes} />
        <Dado rotulo="Médicos encontrados" valor={e.profissionaisEncontrados} />
        <Dado
          rotulo="Médicos novos"
          valor={e.profissionaisNovos || '—'}
          destaque={e.profissionaisNovos > 0}
        />
        <Dado rotulo="Procedimentos" valor={e.procedimentosEncontrados} />
        <Dado
          rotulo="Procedimentos novos"
          valor={e.procedimentosNovos || '—'}
          destaque={e.procedimentosNovos > 0}
        />
      </dl>

      {e.unidadesComCnesPreenchido > 0 && (
        <p className="mb-3 rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-xs text-blue-800">
          {e.unidadesComCnesPreenchido} unidade(s) que já existiam aqui sem CNES foram casadas pelo
          nome e ganharam o código do SISREG.
        </p>
      )}

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
          Sem detalhe por unidade — esta sincronização é anterior ao registro detalhado.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-gray-500">
                <th className="py-1 pr-3 font-medium">Unidade</th>
                <th className="py-1 pr-3 font-medium">Situação</th>
                <th className="py-1 pr-3 font-medium">Médicos</th>
                <th className="py-1 pr-3 font-medium">Novos</th>
                <th className="py-1 pr-3 font-medium">Procedimentos</th>
                <th className="py-1 pr-3 font-medium">Novos</th>
                <th className="py-1 pr-3 font-medium">FHIR</th>
                <th className="py-1 pr-3 font-medium">Req.</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {linhas.map((i) => (
                <LinhaUnidade key={i.id} item={i} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}

function LinhaUnidade({ item: i }: { item: MapeamentoLoteExecucaoItem }) {
  const mapeada = i.resultado === 'Mapeada';

  return (
    <>
      <tr className="align-top text-gray-700">
        <td className="py-1.5 pr-3">
          {i.unidadeNome}
          {i.cnes && <span className="ml-1 font-mono text-xs text-gray-400">{i.cnes}</span>}
          {i.unidadeCriada && (
            <span className="ml-1 rounded bg-emerald-50 px-1.5 py-0.5 text-xs text-emerald-700">
              nova
            </span>
          )}
        </td>
        <td className="py-1.5 pr-3">
          <span className={`rounded px-1.5 py-0.5 text-xs ${CLASSE_RESULTADO[i.resultado]}`}>
            {ROTULO_RESULTADO[i.resultado]}
          </span>
        </td>
        <td className="py-1.5 pr-3">{mapeada ? i.profissionaisEncontrados : '—'}</td>
        <td
          className={`py-1.5 pr-3 ${
            i.profissionaisNovos > 0 ? 'font-semibold text-emerald-700' : 'text-gray-400'
          }`}
        >
          {i.profissionaisNovos > 0 ? i.profissionaisNovos : '—'}
        </td>
        <td className="py-1.5 pr-3">{mapeada ? i.procedimentosEncontrados : '—'}</td>
        <td
          className={`py-1.5 pr-3 ${
            i.procedimentosNovos > 0 ? 'font-semibold text-emerald-700' : 'text-gray-400'
          }`}
        >
          {i.procedimentosNovos > 0 ? i.procedimentosNovos : '—'}
        </td>
        <td className="py-1.5 pr-3 text-xs text-gray-500">
          {i.practitionersCriados + i.practitionersVinculados > 0
            ? `${i.practitionersCriados} criados / ${i.practitionersVinculados} vinculados`
            : '—'}
        </td>
        <td className="py-1.5 pr-3 text-gray-500">{i.requisicoes || '—'}</td>
      </tr>

      {/* O motivo fica VISÍVEL, não num tooltip: é justamente quando a unidade NÃO foi mapeada
          que o operador precisa saber o porquê. */}
      {i.observacao && (
        <tr>
          <td colSpan={8} className="pb-2 pr-3">
            <p
              className={`rounded-md border px-3 py-1.5 text-xs ${
                i.resultado === 'Erro'
                  ? 'border-red-200 bg-red-50 text-red-700'
                  : 'border-gray-200 bg-gray-50 text-gray-600'
              }`}
            >
              {i.observacao}
            </p>
          </td>
        </tr>
      )}
    </>
  );
}

function Dado({
  rotulo,
  valor,
  destaque,
}: {
  rotulo: string;
  valor: string | number;
  destaque?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs text-gray-500">{rotulo}</dt>
      <dd className={destaque ? 'font-semibold text-emerald-700' : 'text-gray-900'}>{valor}</dd>
    </div>
  );
}

function duracao(segundos: number): string {
  if (segundos < 60) return `${segundos}s`;
  const min = Math.floor(segundos / 60);
  const seg = segundos % 60;
  return seg > 0 ? `${min}min ${seg}s` : `${min}min`;
}
