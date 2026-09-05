import type { AgendaResumo } from '@/features/agenda/api/agendaApi';

/** Verde só até 100%: acima disso não é "muito bom", é encaixe além da vaga. */
function classeOcupacao(pct: number) {
  if (pct > 100) return 'text-red-700';
  if (pct >= 80) return 'text-emerald-700';
  if (pct >= 50) return 'text-amber-700';
  return 'text-gray-900';
}

type Props = { resumo: AgendaResumo | undefined; carregando: boolean };

/**
 * Os números do topo. Cada um responde a uma pergunta de gestão — não são enfeite:
 * quanto se ofertou, quanto se usou, onde sobrou e onde faltou.
 */
export function CartoesResumo({ resumo, carregando }: Props) {
  const c = (rotulo: string, valor: React.ReactNode, dica: string, classe = 'text-gray-900') => (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm" title={dica}>
      <p className="text-xs text-gray-500">{rotulo}</p>
      <p className={`mt-1 text-2xl font-semibold ${classe}`}>{carregando ? '…' : valor}</p>
      <p className="mt-1 text-[11px] leading-tight text-gray-400">{dica}</p>
    </div>
  );

  const r = resumo;
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-6">
      {c('Vagas ofertadas', r?.vagas ?? 0, 'Ocorrências de cada escala no período × vagas do bloco.')}
      {c('Agendados', r?.agendados ?? 0, 'Agendamentos importados do SISREG no mesmo recorte.')}
      {c(
        'Ocupação',
        `${r?.ocupacaoPercentual ?? 0}%`,
        'Agendados ÷ vagas. Acima de 100% é encaixe além da vaga publicada.',
        classeOcupacao(r?.ocupacaoPercentual ?? 0),
      )}
      {c(
        'Dias ociosos',
        r?.diasOciosos ?? 0,
        'Profissional com vaga publicada e NENHUM agendamento no dia — onde está sobrando.',
        (r?.diasOciosos ?? 0) > 0 ? 'text-amber-700' : 'text-gray-900',
      )}
      {c(
        'Dias sobrecarregados',
        r?.diasSobrecarregados ?? 0,
        'Mais agendamento que vaga no mesmo dia — onde está faltando.',
        (r?.diasSobrecarregados ?? 0) > 0 ? 'text-red-700' : 'text-gray-900',
      )}
      {c(
        'Agendados sem escala',
        r?.agendadosSemOferta ?? 0,
        'Agendamento sem escala do mesmo profissional naquele dia. Mede o quanto a grade do SISREG descreve a realidade.',
        (r?.agendadosSemOferta ?? 0) > 0 ? 'text-amber-700' : 'text-gray-900',
      )}
    </div>
  );
}
