import { memo, useState } from 'react';
import type { Atendimentos, CorTriagem, EsperaPorCor, PeriodoPainel } from '@/types/painel';
import { apenasCoresDaUnidade, ordenarPorTriagem } from '@/lib/triagem';
import { horaMinuto } from '@/lib/formatos';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { Pulseira } from '@/components/Pulseira';
import { SegmentedControl } from '@/components/SegmentedControl';
import { opcoesDePeriodo } from '@/lib/periodos';

interface Props {
  espera: EsperaPorCor;
  /** Cores do protocolo da unidade — o Conde não usa laranja, as UPAs usam. */
  coresUsadas: CorTriagem[];
  /** Fonte dos rótulos de mês do seletor de período. */
  atendimentos?: Atendimentos | null;
}

/**
 * As pulseiras de classificação, com os DOIS tempos da jornada: chegada →
 * classificação e classificação → médico.
 *
 * A meta agora é a mesma nas três unidades (protocolo de Manchester, fixada no back em
 * MetasTriagem), então o consolidado da rede também mostra meta — antes ela sumia na aba
 * "geral" porque cada base cadastrava um alvo diferente para a mesma cor.
 *
 * memo: só re-renderiza quando os dados mudam (painel aberto em TV).
 */
export const SecaoEmergencia = memo(function SecaoEmergencia({
  espera,
  coresUsadas,
  atendimentos,
}: Props) {
  const [periodo, setPeriodo] = useState<PeriodoPainel>('hoje');
  const opcoes = opcoesDePeriodo(atendimentos);

  const pulseiras = ordenarPorTriagem(apenasCoresDaUnidade(espera.periodos[periodo], coresUsadas));

  return (
    <section aria-labelledby="titulo-emergencia">
      <CabecalhoSecao
        eyebrow="Emergência"
        titulo="Tempo até o atendimento médico"
        tituloId="titulo-emergencia"
        sub={`Chegada → classificação → atendimento · atualizado às ${horaMinuto(espera.atualizadoEm)}`}
        direita={
          <SegmentedControl
            opcoes={opcoes}
            valor={periodo}
            aoMudar={setPeriodo}
            ariaLabel="Período do tempo de espera"
          />
        }
      />
      <div className="space-y-3">
        {pulseiras.map((item) => (
          <Pulseira key={item.cor} item={item} />
        ))}
      </div>
      {/* A ressalva anda colada ao número: limitação escondida vira decisão errada. */}
      <p className="mt-3 text-[12.5px] leading-relaxed text-grafite">
        Boletins sem cor registrada aparecem como “Sem classificação” — nunca são
        descartados. No <strong>vermelho</strong> o tempo é contado da chegada até a
        primeira interação de qualquer natureza: ali o atendimento vem antes do registro,
        e o alvo é imediato.
      </p>
    </section>
  );
});
