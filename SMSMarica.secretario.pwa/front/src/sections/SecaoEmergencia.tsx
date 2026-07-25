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
  /** Aba "geral": some com a meta, que é régua de cada unidade (ver Pulseira). */
  consolidado?: boolean;
  /** Fonte dos rótulos de mês do seletor de período. */
  atendimentos?: Atendimentos | null;
}

/**
 * As pulseiras de classificação — tempo da triagem até o primeiro boletim médico.
 * memo: só re-renderiza quando os dados mudam (painel aberto em TV).
 */
export const SecaoEmergencia = memo(function SecaoEmergencia({
  espera,
  coresUsadas,
  consolidado = false,
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
        sub={`Da classificação de risco ao primeiro boletim médico · atualizado às ${horaMinuto(espera.atualizadoEm)}`}
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
          <Pulseira key={item.cor} item={item} consolidado={consolidado} />
        ))}
      </div>
      {/* A ressalva anda colada ao número: limitação escondida vira decisão errada. */}
      <p className="mt-3 text-[12.5px] leading-relaxed text-grafite">
        Boletins sem cor registrada aparecem como “Sem classificação” — nunca são
        descartados.
      </p>
    </section>
  );
});
