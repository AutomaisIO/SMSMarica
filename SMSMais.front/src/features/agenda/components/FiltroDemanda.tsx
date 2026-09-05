import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useOpcoesDemanda } from '@/features/agenda/api/queries';
import type { DemandaFiltro } from '@/features/agenda/api/demandaApi';

type Props = { filtro: DemandaFiltro; aoMudar: (f: DemandaFiltro) => void };

/**
 * Filtros da demanda. O seletor de eixo é o mais importante da tela e por isso vem primeiro,
 * com a explicação do que ele muda — trocar de eixo troca a pergunta, não a ordenação.
 */
export function FiltroDemanda({ filtro, aoMudar }: Props) {
  const opcoes = useOpcoesDemanda();
  const set = <K extends keyof DemandaFiltro>(k: K, v: DemandaFiltro[K]) =>
    aoMudar({ ...filtro, [k]: v });

  return (
    <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Campo label="Contar pela data de" htmlFor="dm-eixo">
          <Select
            id="dm-eixo"
            value={filtro.eixoData}
            onChange={(e) => set('eixoData', e.target.value as DemandaFiltro['eixoData'])}
          >
            <option value="agendada">Agendamento (quem foi atendido)</option>
            <option value="solicitada">Solicitação (quem pediu)</option>
          </Select>
        </Campo>
        <Campo label="De" htmlFor="dm-de">
          <Input id="dm-de" type="date" value={filtro.de} onChange={(e) => set('de', e.target.value)} />
        </Campo>
        <Campo label="Até" htmlFor="dm-ate">
          <Input id="dm-ate" type="date" value={filtro.ate} onChange={(e) => set('ate', e.target.value)} />
        </Campo>
        <Campo label="Prioridade" htmlFor="dm-prio">
          <Select
            id="dm-prio"
            value={filtro.prioridade ?? ''}
            onChange={(e) => set('prioridade', e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Todas</option>
            <option value="1">Eletiva</option>
            <option value="2">Prioritária</option>
            <option value="3">Urgente</option>
          </Select>
        </Campo>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Campo label="Unidade executante" htmlFor="dm-ux">
          <Select
            id="dm-ux"
            value={filtro.unidadeExecutanteId ?? ''}
            onChange={(e) => set('unidadeExecutanteId', e.target.value || null)}
          >
            <option value="">Todas</option>
            {opcoes.data?.unidadesExecutantes.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Unidade solicitante" htmlFor="dm-us">
          <Select
            id="dm-us"
            value={filtro.unidadeSolicitanteId ?? ''}
            onChange={(e) => set('unidadeSolicitanteId', e.target.value || null)}
          >
            <option value="">Todas</option>
            {opcoes.data?.unidadesSolicitantes.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Procedimento" htmlFor="dm-proc">
          <Select
            id="dm-proc"
            value={filtro.procedimento ?? ''}
            onChange={(e) => set('procedimento', e.target.value || null)}
          >
            <option value="">Todos</option>
            {opcoes.data?.procedimentos.map((o) => (
              <option key={o.valor} value={o.valor}>{o.rotulo}</option>
            ))}
          </Select>
        </Campo>
      </div>

      {filtro.eixoData === 'solicitada' ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-[11px] leading-snug text-amber-900">
          <strong>Leia com cuidado neste eixo.</strong> Só entram na base solicitações que já têm
          agendamento importado. Um pedido antigo só aparece se demorou o bastante para cair na
          janela varrida, e um pedido recente só se foi rápido — a espera fica exagerada nos meses
          antigos e subestimada nos recentes. Para afirmação exata use o eixo{' '}
          <em>Agendamento</em>: ali "quem foi atendido nestes dias esperou X" é verdade sem ressalva.
        </p>
      ) : null}
    </section>
  );
}
