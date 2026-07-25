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

/** Linha rótulo → número, com alerta discreto para o que exige atenção. */
function LinhaIndicador({
  rotulo,
  valor,
  formatar,
  destaque,
  alerta,
  nota,
}: {
  rotulo: string;
  valor: number;
  formatar?: (n: number) => string;
  destaque?: boolean;
  alerta?: boolean;
  nota?: string;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <div className="min-w-0">
        <p className="text-[13px] leading-snug text-grafite">{rotulo}</p>
        {nota && <p className="text-[11.5px] leading-snug text-grafite/75">{nota}</p>}
      </div>
      <p
        className={
          'tnum shrink-0 font-display font-bold leading-none ' +
          (destaque ? 'text-[20px] ' : 'text-[17px] ') +
          (alerta ? 'text-vermelho-marica' : 'text-tinta')
        }
      >
        {formatar ? formatar(valor) : formatarInteiro(valor)}
      </p>
    </div>
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
          {/* Prematuridade sai da IDADE GESTACIONAL, não do campo IN_PREMATURO: o flag
              é marcado à mão e discorda do próprio registro (em julho marcava 1, quando
              3 nasceram entre 32 e 36 semanas). Mostrar os dois lado a lado fazia a tela
              se contradizer. */}
          <StatTile
            rotulo="Prematuros no mês"
            valor={mes.prematuroTardio}
            detalhes={[
              'nascidos entre 32 e 36 semanas',
              `${formatarInteiro(mes.apgar5Abaixo7)} com Apgar abaixo de 7 no 5º minuto`,
            ]}
          />
        </div>

        <div className="grid gap-3 lg:grid-cols-2">
          <CartaoMesMaternidade periodo={maternidade.mesAtual} destaque />
          <CartaoMesMaternidade periodo={maternidade.mesAnterior} />
        </div>

        {/* Enriquecimento pedido pela gerência do contrato: o que o livro de partos
            tem além da contagem — desfecho, idade gestacional e perfil da mãe. */}
        <div className="grid gap-3 lg:grid-cols-3">
          <Cartao className="p-5">
            <p className="eyebrow">Desfecho do nascimento</p>
            <div className="mt-3 space-y-2">
              <LinhaIndicador rotulo="Nascidos vivos" valor={mes.partos - mes.natimortos} destaque />
              <LinhaIndicador rotulo="Natimortos" valor={mes.natimortos} alerta={mes.natimortos > 0} />
              <LinhaIndicador rotulo="Apgar < 7 no 1º minuto" valor={mes.apgar1Abaixo7} />
              <LinhaIndicador rotulo="Apgar < 7 no 5º minuto" valor={mes.apgar5Abaixo7} alerta={mes.apgar5Abaixo7 > 0} />
              <LinhaIndicador
                rotulo="Com malformação"
                valor={mes.comMalformacao}
                nota={mes.malformacaoSemInfo > 0 ? `${formatarInteiro(mes.malformacaoSemInfo)} sem informação` : undefined}
              />
            </div>
          </Cartao>

          <Cartao className="p-5">
            <p className="eyebrow">Idade gestacional</p>
            <div className="mt-3 space-y-2">
              <LinhaIndicador rotulo="A termo (37–41 sem.)" valor={mes.aTermo} destaque />
              <LinhaIndicador rotulo="Prematuro tardio (32–36)" valor={mes.prematuroTardio} />
              <LinhaIndicador rotulo="Pós-termo (42+)" valor={mes.posTermo} />
              <LinhaIndicador rotulo="Gravidez múltipla" valor={mes.gravidezMultipla} />
              {mes.gestacaoSemInfo > 0 && (
                <LinhaIndicador rotulo="Sem informação" valor={mes.gestacaoSemInfo} />
              )}
            </div>
          </Cartao>

          <Cartao className="p-5">
            <p className="eyebrow">Perfil da mãe</p>
            <div className="mt-3 space-y-2">
              <LinhaIndicador
                rotulo="Idade média"
                valor={mes.idadeMediaMae ?? 0}
                formatar={(n) => (mes.idadeMediaMae == null ? '—' : `${formatarDecimal(n)} anos`)}
                destaque
              />
              <LinhaIndicador rotulo="Mães com até 17 anos" valor={mes.maeAte17} alerta={mes.maeAte17 > 0} />
              <LinhaIndicador rotulo="Mães com menos de 20" valor={mes.maeMenor20} />
              <LinhaIndicador rotulo="Mães com 35 anos ou mais" valor={mes.mae35Mais} />
            </div>
          </Cartao>
        </div>

        <Cartao className="p-5">
          <p className="text-[15px] font-semibold text-tinta">Medidas ao nascer</p>
          <p className="mb-3 text-[12.5px] text-grafite">média dos nascidos em {mes.rotulo}</p>
          <div className="grid grid-cols-3 gap-4">
            {[
              { rotulo: 'Peso', valor: mes.pesoMedioKg, unidade: 'kg' },
              { rotulo: 'Estatura', valor: mes.estaturaMedia, unidade: 'cm' },
              { rotulo: 'Perímetro cefálico', valor: mes.perimetroCefalicoMedio, unidade: 'cm' },
            ].map((m) => (
              <div key={m.rotulo}>
                <p className="text-[12.5px] text-grafite">{m.rotulo}</p>
                <p className="font-display text-[24px] font-bold leading-tight text-tinta">
                  {m.valor != null ? formatarDecimal(m.valor) : '—'}
                  <span className="ml-1 font-corpo text-[13px] font-medium text-grafite">{m.unidade}</span>
                </p>
              </div>
            ))}
          </div>
        </Cartao>

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
