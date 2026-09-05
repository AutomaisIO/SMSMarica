import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useOpcoesAgenda } from '@/features/agenda/api/queries';
import type { AgendaFiltro } from '@/features/agenda/api/agendaApi';

type Props = { filtro: AgendaFiltro; aoMudar: (f: AgendaFiltro) => void };

/**
 * Filtros da Agenda. As opções vêm do que existe de fato na grade vigente — oferecer
 * profissional que saiu da escala só produziria consulta vazia e a impressão de dado perdido.
 */
export function FiltroAgenda({ filtro, aoMudar }: Props) {
  const opcoes = useOpcoesAgenda();
  const set = <K extends keyof AgendaFiltro>(k: K, v: AgendaFiltro[K]) =>
    aoMudar({ ...filtro, [k]: v });

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-6">
        <Campo label="De" htmlFor="ag-de">
          <Input id="ag-de" type="date" value={filtro.de} onChange={(e) => set('de', e.target.value)} />
        </Campo>
        <Campo label="Até" htmlFor="ag-ate">
          <Input id="ag-ate" type="date" value={filtro.ate} onChange={(e) => set('ate', e.target.value)} />
        </Campo>
        <Campo label="Unidade" htmlFor="ag-un">
          <Select id="ag-un" value={filtro.unidadeId ?? ''} onChange={(e) => set('unidadeId', e.target.value || null)}>
            <option value="">Todas</option>
            {opcoes.data?.unidades.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Especialidade (CBO)" htmlFor="ag-cbo">
          <Select id="ag-cbo" value={filtro.cbo ?? ''} onChange={(e) => set('cbo', e.target.value || null)}>
            <option value="">Todas</option>
            {opcoes.data?.cbos.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Profissional" htmlFor="ag-prof">
          <Select
            id="ag-prof"
            value={filtro.profissionalCpf ?? ''}
            onChange={(e) => set('profissionalCpf', e.target.value || null)}
          >
            <option value="">Todos</option>
            {opcoes.data?.profissionais.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Procedimento" htmlFor="ag-proc">
          <Select
            id="ag-proc"
            value={filtro.procedimentoCodigo ?? ''}
            onChange={(e) => set('procedimentoCodigo', e.target.value || null)}
          >
            <option value="">Todos</option>
            {opcoes.data?.procedimentos.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
      </div>
    </section>
  );
}
