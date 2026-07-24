import type { Agora, Internacoes } from '@/types/painel';
import { ordenarPorTriagem } from '@/lib/triagem';
import { formatarDecimal, formatarInteiro, horaMinuto } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { ChipCor } from '@/components/ChipCor';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { StatTile } from '@/components/StatTile';

interface Props {
  agora: Agora;
  /** Split do dia (seção internações) — pode ainda não existir no cold start. */
  internacoesHoje?: Internacoes['hoje'] | null;
}

/**
 * O herói do painel: quantas pessoas aguardam atendimento médico NESTE momento,
 * com a fila aberta por cor de classificação, e a fileira de indicadores vivos.
 */
export function SecaoAgora({ agora, internacoesHoje }: Props) {
  const filaPorCor = ordenarPorTriagem(agora.aguardandoPorCor);

  return (
    <section aria-labelledby="titulo-agora" className="space-y-3">
      <Cartao className="p-6 sm:p-8">
        <div className="flex items-baseline justify-between gap-4">
          <p className="eyebrow">Na emergência agora</p>
          <p className="tnum text-[12px] text-grafite">atualizado às {horaMinuto(agora.atualizadoEm)}</p>
        </div>
        <p className="mt-3 font-display text-[clamp(44px,9vw,72px)] font-extrabold leading-none tracking-tight text-tinta">
          <NumeroAnimado valor={agora.aguardandoMedico} />
        </p>
        <h2 id="titulo-agora" className="mt-2 text-[16px] font-medium text-grafite">
          Aguardando atendimento médico
        </h2>
        <div className="mt-5 border-t border-linha pt-5">
          <div className="flex flex-wrap gap-2">
            {filaPorCor.map((item) => (
              <ChipCor key={item.cor} item={item} />
            ))}
          </div>
        </div>
      </Cartao>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatTile
          rotulo="Em atendimento"
          valor={agora.emAtendimento}
          detalhes={['em atendimento ou observação']}
        />
        <StatTile
          rotulo="Internados neste momento"
          valor={agora.internadosAgora}
          detalhes={[
            `${formatarInteiro(agora.internadosUrgencia)} urgência · ${formatarInteiro(agora.internadosEletiva)} eletiva`,
            `média de ${agora.mediaDiasInternacao != null ? formatarDecimal(agora.mediaDiasInternacao) : '—'} dias de internação`,
          ]}
        />
        <StatTile
          rotulo="Atendimentos hoje"
          valor={agora.atendimentosHoje}
          detalhes={['boletins abertos desde a 0h']}
        />
        <StatTile
          rotulo="Internações hoje"
          valor={agora.internacoesHoje}
          detalhes={
            internacoesHoje
              ? [
                  `${formatarInteiro(internacoesHoje.urgencia)} urgência · ${formatarInteiro(internacoesHoje.eletiva)} eletiva`,
                ]
              : undefined
          }
        />
      </div>
    </section>
  );
}
