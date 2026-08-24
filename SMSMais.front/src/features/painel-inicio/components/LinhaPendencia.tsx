import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import type { CausaFalhaImportacao, ItemPendenciaImportacaoPainel } from '@/features/painel-inicio/types';

/**
 * O que cada causa significa para o operador — é isto que a coluna `causa` no banco existe para
 * permitir (ADR-0035 §2). Sem ela a tela só teria texto livre e não saberia que ação oferecer.
 */
export const ROTULO_CAUSA: Record<CausaFalhaImportacao, string> = {
  CpfNaoResolvido: 'CADSUS não devolveu o CPF',
  CadsusIndisponivel: 'CADSUS indisponível — tente revalidar',
  SemCns: 'Marcação sem CNS na origem',
  UnidadeNaoResolvida: 'Unidade executante não identificada',
  LinhaInvalida: 'Linha ilegível no arquivo',
  ArquivoIncompativel: 'Arquivo não é do SISREG',
  Outro: 'Não classificada',
};

type Props = {
  item: ItemPendenciaImportacaoPainel;
  aoInformarCpf: (item: ItemPendenciaImportacaoPainel) => void;
};

export function LinhaPendencia({ item, aoInformarCpf }: Props) {
  return (
    <li className="flex flex-wrap items-baseline gap-x-2 gap-y-1 py-2 text-sm">
      <span className="font-medium text-gray-900">{item.pacienteNome ?? 'Paciente não identificado'}</span>

      {item.procedimento && <span className="text-gray-600">{item.procedimento}</span>}

      {item.dataAgendada && (
        <span className="text-gray-500 tabular-nums">{formatarInstante(item.dataAgendada)}</span>
      )}

      {item.unidadeNome && <span className="text-gray-500">· {item.unidadeNome}</span>}

      <span className="text-xs text-gray-500" title={item.motivo}>
        · {ROTULO_CAUSA[item.causa]}
      </span>

      {item.podeInformarCpf && (
        <Button
          variante="outline"
          className="ml-auto h-7 px-2 text-xs"
          onClick={() => aoInformarCpf(item)}
        >
          Informar CPF e importar
        </Button>
      )}
    </li>
  );
}
