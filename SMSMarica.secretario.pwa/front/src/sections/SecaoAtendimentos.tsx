import { memo } from 'react';
import { ArrowDownRight, ArrowUpRight } from 'lucide-react';
import type { Atendimentos } from '@/types/painel';
import { formatarDecimal, formatarInteiro, horaMinuto, nomeDoMes } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { GraficoSerieDiaria } from '@/components/graficos/GraficoSerieDiaria';
import { GraficoPorHora } from '@/components/graficos/GraficoPorHora';

/**
 * Atendimentos: mês atual × anterior, série diária de 35 dias e o dia hora a hora.
 * memo: painel fica aberto em TV — a seção (pesada, gráficos) só re-renderiza
 * quando os dados mudam, não a cada tick do relógio.
 */
export const SecaoAtendimentos = memo(function SecaoAtendimentos({
  atendimentos,
}: {
  atendimentos: Atendimentos;
}) {
  const { mesAtual, mesAnterior } = atendimentos;
  const deltaPct =
    mesAnterior.mediaDiaria > 0
      ? ((mesAtual.mediaDiaria - mesAnterior.mediaDiaria) / mesAnterior.mediaDiaria) * 100
      : null;

  return (
    <section aria-labelledby="titulo-atendimentos">
      <CabecalhoSecao
        eyebrow="Movimento"
        titulo="Atendimentos"
        tituloId="titulo-atendimentos"
        direita={
          <p className="tnum text-[12px] text-grafite">
            atualizado às {horaMinuto(atendimentos.atualizadoEm)}
          </p>
        }
      />
      <div className="space-y-4">
        <div className="grid gap-3 lg:grid-cols-2">
          {/* mês atual — em destaque */}
          <Cartao className="p-5">
            <p className="eyebrow !text-vermelho-marica">{mesAtual.rotulo}</p>
            <div className="mt-1.5 flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <p className="font-display text-[32px] font-bold leading-none tracking-tight text-tinta sm:text-[36px]">
                <NumeroAnimado valor={mesAtual.mediaDiaria} formatar={formatarDecimal} />
                <span className="ml-1.5 font-corpo text-[14px] font-medium text-grafite">por dia</span>
              </p>
              {deltaPct != null && (
                <p className="tnum flex items-center gap-0.5 text-[13px] font-medium text-grafite">
                  {deltaPct < 0 ? (
                    <ArrowDownRight className="h-3.5 w-3.5" aria-hidden="true" />
                  ) : (
                    <ArrowUpRight className="h-3.5 w-3.5" aria-hidden="true" />
                  )}
                  {formatarDecimal(Math.abs(deltaPct))}% vs {nomeDoMes(mesAnterior.rotulo)}
                </p>
              )}
            </div>
            <p className="tnum mt-2.5 text-[13px] text-grafite">
              {formatarInteiro(mesAtual.total)} atendimentos no mês · média sobre{' '}
              {formatarInteiro(mesAtual.diasCompletos)} dias completos (
              {formatarInteiro(mesAtual.totalDiasCompletos)})
            </p>
          </Cartao>

          {/* mês anterior — referência */}
          <Cartao className="p-5">
            <p className="eyebrow">{mesAnterior.rotulo}</p>
            <p className="mt-1.5 font-display text-[32px] font-bold leading-none tracking-tight text-tinta sm:text-[36px]">
              <NumeroAnimado valor={mesAnterior.mediaDiaria} formatar={formatarDecimal} />
              <span className="ml-1.5 font-corpo text-[14px] font-medium text-grafite">por dia</span>
            </p>
            <p className="tnum mt-2.5 text-[13px] text-grafite">
              {formatarInteiro(mesAnterior.total)} atendimentos em {formatarInteiro(mesAnterior.dias)}{' '}
              dias
            </p>
          </Cartao>
        </div>

        <Cartao className="p-5">
          <p className="text-[15px] font-semibold text-tinta">Movimento diário</p>
          <p className="mb-3 text-[12.5px] text-grafite">
            últimos {atendimentos.serieDiaria.length} dias · o dia de hoje ainda está em andamento
          </p>
          <GraficoSerieDiaria dados={atendimentos.serieDiaria} rotuloUnidade="atendimentos" />
        </Cartao>

        <Cartao className="p-5">
          <p className="text-[15px] font-semibold text-tinta">Hoje, hora a hora</p>
          <p className="mb-3 text-[12.5px] text-grafite">
            {formatarInteiro(atendimentos.hoje.total)} atendimentos até agora
          </p>
          <GraficoPorHora dados={atendimentos.porHoraHoje} />
        </Cartao>
      </div>
    </section>
  );
});
