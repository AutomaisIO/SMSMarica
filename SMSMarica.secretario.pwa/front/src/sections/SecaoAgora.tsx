import type { Agora, CorTriagem, Internacoes } from '@/types/painel';
import { apenasCoresDaUnidade, ordenarPorTriagem } from '@/lib/triagem';
import { formatarDecimal, formatarInteiro, horaMinuto } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { ChipCor } from '@/components/ChipCor';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { StatTile } from '@/components/StatTile';

interface Props {
  agora: Agora;
  /** Cores do protocolo da unidade — as outras nem aparecem na fila. */
  coresUsadas: CorTriagem[];
  /** Split do dia (seção internações) — pode ainda não existir no cold start. */
  internacoesHoje?: Internacoes['hoje'] | null;
}

/**
 * O herói do painel: quantas pessoas aguardam atendimento médico NESTE momento,
 * com a fila aberta por cor de classificação, e a fileira de indicadores vivos.
 *
 * Os dois quadros de internação só existem onde há internação (o Conde, e o
 * "geral" que a repassa etiquetada). Na UPA a fileira fica com dois quadros em vez
 * de quatro — melhor do que dois zeros que sugerem hospital vazio.
 */
export function SecaoAgora({ agora, coresUsadas, internacoesHoje }: Props) {
  const filaPorCor = ordenarPorTriagem(apenasCoresDaUnidade(agora.aguardandoPorCor, coresUsadas));
  const internados = agora.internados;

  return (
    <section aria-labelledby="titulo-agora" className="space-y-3">
      <Cartao className="p-6 sm:p-8">
        <div className="flex items-baseline justify-between gap-4">
          <p className="eyebrow">Na emergência agora</p>
          <p className="tnum text-[12px] text-grafite">atualizado às {horaMinuto(agora.atualizadoEm)}</p>
        </div>
        <p className="mt-3 font-display text-[clamp(44px,9vw,72px)] font-extrabold leading-none tracking-tight text-vermelho-marica">
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
        {internados && (
          <StatTile
            rotulo="Internados neste momento"
            valor={internados.total}
            detalhes={[
              `${formatarInteiro(internados.maternidade)} maternidade · ${formatarInteiro(internados.ate17)} até 17 anos · ${formatarInteiro(internados.adultos)} adultos`,
              `média de ${internados.mediaDiasInternacao != null ? formatarDecimal(internados.mediaDiasInternacao) : '—'} dias de internação`,
            ]}
            escopo={internados.escopo}
          />
        )}
        <StatTile
          rotulo="Atendimentos hoje"
          valor={agora.atendimentosHoje}
          // "abertos" lia como "ainda em aberto"; é a CRIAÇÃO do boletim que conta.
          // Metade dos de ontem já estava encerrada (260 de 523).
          detalhes={['boletins registrados desde a 0h']}
        />
        {internados && (
          <StatTile
            rotulo="Internações hoje"
            valor={internados.internacoesHoje}
            detalhes={
              internacoesHoje
                ? [
                    `${formatarInteiro(internacoesHoje.maternidade)} maternidade · ${formatarInteiro(internacoesHoje.ate17)} até 17 anos · ${formatarInteiro(internacoesHoje.adultos)} adultos`,
                  ]
                : undefined
            }
            escopo={internados.escopo}
          />
        )}
      </div>
    </section>
  );
}
