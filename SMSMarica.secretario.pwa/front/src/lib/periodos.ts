import type { Atendimentos, PeriodoPainel } from '@/types/painel';
import { nomeDoMes } from '@/lib/formatos';
import type { OpcaoSegmento } from '@/components/SegmentedControl';

/**
 * Os quatro períodos do painel, na mesma ordem em toda seção que tem seletor.
 *
 * Os rótulos dos meses vêm do contrato (o back manda "julho/2026"), não de uma
 * conta de data no navegador: se o relógio do aparelho estiver errado — e em TV de
 * corredor costuma estar — o rótulo continua batendo com o número que ele nomeia.
 */
export function opcoesDePeriodo(atendimentos?: Atendimentos | null): OpcaoSegmento<PeriodoPainel>[] {
  return [
    { valor: 'hoje', rotulo: 'Hoje' },
    { valor: 'ontem', rotulo: 'Ontem' },
    { valor: 'mesAtual', rotulo: atendimentos ? nomeDoMes(atendimentos.mesAtual.rotulo) : 'mês atual' },
    {
      valor: 'mesAnterior',
      rotulo: atendimentos ? nomeDoMes(atendimentos.mesAnterior.rotulo) : 'mês anterior',
    },
  ];
}
