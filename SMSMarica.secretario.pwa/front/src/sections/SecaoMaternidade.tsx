import { memo } from 'react';
import type { Maternidade, MaternidadePeriodo } from '@/types/painel';
import { formatarDecimal, formatarInteiro, horaMinuto } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { SeloEscopo } from '@/components/SeloEscopo';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { StatTile } from '@/components/StatTile';
import { GraficoSerieDiaria } from '@/components/graficos/GraficoSerieDiaria';

const VINHO = '#9E1B32';
const NEUTRO_SERIE = '#C9CED6';

/** Cesárea × parto normal: barra proporcional com as duas parcelas nomeadas. */
function SplitParto({ periodo }: { periodo: MaternidadePeriodo }) {
  const total = periodo.cesareas + periodo.vaginais;
  if (total === 0) return null;
  return (
    <div className="mt-3">
      <div className="flex h-2 gap-[2px] overflow-hidden rounded-full">
        <div
          className="rounded-l-full"
          style={{ width: `${(periodo.cesareas / total) * 100}%`, backgroundColor: VINHO }}
        />
        <div
          className="rounded-r-full"
          style={{ width: `${(periodo.vaginais / total) * 100}%`, backgroundColor: NEUTRO_SERIE }}
        />
      </div>
      <div className="tnum mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-0.5 text-[12.5px] text-grafite">
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: VINHO }} />
          Cesárea <span className="font-semibold text-tinta">{formatarInteiro(periodo.cesareas)}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: NEUTRO_SERIE }} />
          Normal <span className="font-semibold text-tinta">{formatarInteiro(periodo.vaginais)}</span>
        </span>
      </div>
    </div>
  );
}

function CartaoMesMaternidade({
  periodo,
  destaque,
}: {
  periodo: MaternidadePeriodo;
  destaque?: boolean;
}) {
  return (
    <Cartao className="p-5">
      <p className={destaque ? 'eyebrow !text-vermelho-marica' : 'eyebrow'}>{periodo.rotulo}</p>
      <p className="mt-1.5 font-display text-[32px] font-bold leading-none tracking-tight text-tinta sm:text-[36px]">
        <NumeroAnimado valor={periodo.partos} />
        <span className="ml-1.5 font-corpo text-[14px] font-medium text-grafite">
          {periodo.partos === 1 ? 'parto' : 'partos'}
        </span>
      </p>
      <p className="tnum mt-2.5 text-[13px] text-grafite">
        {periodo.pctCesarea != null
          ? `${formatarDecimal(periodo.pctCesarea)}% por cesárea`
          : 'sem partos no período'}
        {periodo.mediaDiaria != null && ` · ${formatarDecimal(periodo.mediaDiaria)} por dia`}
      </p>
      <SplitParto periodo={periodo} />
    </Cartao>
  );
}

/**
 * Maternidade: o livro de partos do hospital (INFOSAUDE.NASCIMENTO), registro
 * consistente e com peso/prematuridade/APGAR preenchidos em 100% do mês.
 * memo: só re-renderiza quando os dados mudam (painel aberto em TV).
 */
export const SecaoMaternidade = memo(function SecaoMaternidade({
  maternidade,
}: {
  maternidade: Maternidade;
}) {
  const mes = maternidade.mesAtual;
  const hoje = maternidade.hoje;

  return (
    <section aria-labelledby="titulo-maternidade">
      <CabecalhoSecao
        eyebrow="Maternidade"
        titulo="Nascimentos"
        tituloId="titulo-maternidade"
        sub={`Partos registrados no hospital · atualizado às ${horaMinuto(maternidade.atualizadoEm)}`}
        direita={<SeloEscopo escopo={maternidade.escopo} />}
      />
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatTile
            rotulo="Nascimentos hoje"
            valor={hoje.partos}
            detalhes={[
              `${formatarInteiro(hoje.cesareas)} cesárea · ${formatarInteiro(hoje.vaginais)} normal`,
            ]}
          />
          <StatTile
            rotulo="Nascimentos no mês"
            valor={mes.partos}
            detalhes={[
              `${formatarInteiro(mes.meninas)} meninas · ${formatarInteiro(mes.meninos)} meninos`,
            ]}
          />
          <StatTile
            rotulo="Peso médio ao nascer"
            valor={mes.pesoMedioKg ?? 0}
            formatar={(n) => (mes.pesoMedioKg == null ? '—' : `${formatarDecimal(n)} kg`)}
            detalhes={[`${formatarInteiro(mes.baixoPeso)} abaixo de 2,5 kg no mês`]}
          />
          <StatTile
            rotulo="Prematuros no mês"
            valor={mes.prematuros}
            detalhes={[
              `${formatarInteiro(mes.apgar5Abaixo7)} com Apgar abaixo de 7 no 5º minuto`,
            ]}
          />
        </div>

        <div className="grid gap-3 lg:grid-cols-2">
          <CartaoMesMaternidade periodo={maternidade.mesAtual} destaque />
          <CartaoMesMaternidade periodo={maternidade.mesAnterior} />
        </div>

        <Cartao className="p-5">
          <p className="text-[15px] font-semibold text-tinta">Nascimentos por dia</p>
          <p className="mb-3 text-[12.5px] text-grafite">
            últimos 35 dias · o dia de hoje ainda está em andamento
          </p>
          <GraficoSerieDiaria
            dados={maternidade.serieDiaria.map((p) => ({ dia: p.dia, qtd: p.qtd }))}
            rotuloUnidade="nascimentos"
            altura={200}
          />
        </Cartao>
      </div>
    </section>
  );
});
