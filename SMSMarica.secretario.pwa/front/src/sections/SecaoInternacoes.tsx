import { memo } from 'react';
import type { Internacoes, MesInternacao } from '@/types/painel';
import { formatarDecimal, formatarInteiro, horaMinuto } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { GraficoSerieDiaria } from '@/components/graficos/GraficoSerieDiaria';

const VINHO = '#9E1B32';
const NEUTRO_SERIE = '#C9CED6';

/**
 * Proporção maternidade × demais unidades, com legenda — entidade sempre nomeada.
 * O corte é pela UNIDADE do leito, não por "urgência/eletiva": no HMCML o campo
 * ID_INTERNACAO não sustenta essa leitura (ver docs/consultas-oracle.md §Q5).
 */
function SplitPorUnidade({ maternidade, demais }: { maternidade: number; demais: number }) {
  const total = maternidade + demais;
  if (total === 0) return null;
  return (
    <div className="mt-3">
      <div className="flex h-2 gap-[2px] overflow-hidden rounded-full">
        <div
          className="rounded-l-full"
          style={{ width: `${(maternidade / total) * 100}%`, backgroundColor: VINHO }}
        />
        <div
          className="rounded-r-full"
          style={{ width: `${(demais / total) * 100}%`, backgroundColor: NEUTRO_SERIE }}
        />
      </div>
      <div className="tnum mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-0.5 text-[12.5px] text-grafite">
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: VINHO }} />
          Maternidade <span className="font-semibold text-tinta">{formatarInteiro(maternidade)}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: NEUTRO_SERIE }} />
          Demais <span className="font-semibold text-tinta">{formatarInteiro(demais)}</span>
        </span>
      </div>
    </div>
  );
}

function CartaoMesInternacao({ mes, destaque }: { mes: MesInternacao; destaque?: boolean }) {
  return (
    <Cartao className="p-5">
      <p className={destaque ? 'eyebrow !text-vermelho-marica' : 'eyebrow'}>{mes.rotulo}</p>
      <p className="mt-1.5 font-display text-[32px] font-bold leading-none tracking-tight text-tinta sm:text-[36px]">
        <NumeroAnimado valor={mes.mediaDiaria} formatar={formatarDecimal} />
        <span className="ml-1.5 font-corpo text-[14px] font-medium text-grafite">por dia</span>
      </p>
      <p className="tnum mt-2.5 text-[13px] text-grafite">
        {formatarInteiro(mes.total)} internações no mês
      </p>
      <SplitPorUnidade maternidade={mes.maternidade} demais={mes.demais} />
    </Cartao>
  );
}

/**
 * Internações: mês atual × anterior com split por unidade e série diária.
 * memo: só re-renderiza quando os dados mudam (painel aberto em TV).
 */
export const SecaoInternacoes = memo(function SecaoInternacoes({
  internacoes,
}: {
  internacoes: Internacoes;
}) {
  return (
    <section aria-labelledby="titulo-internacoes">
      <CabecalhoSecao
        eyebrow="Leitos"
        titulo="Internações"
        tituloId="titulo-internacoes"
        direita={
          <p className="tnum text-[12px] text-grafite">
            atualizado às {horaMinuto(internacoes.atualizadoEm)}
          </p>
        }
      />
      <div className="space-y-4">
        <div className="grid gap-3 lg:grid-cols-2">
          <CartaoMesInternacao mes={internacoes.mesAtual} destaque />
          <CartaoMesInternacao mes={internacoes.mesAnterior} />
        </div>
        <Cartao className="p-5">
          <p className="text-[15px] font-semibold text-tinta">Internações por dia</p>
          <p className="mb-3 text-[12.5px] text-grafite">
            por data de entrada · o dia de hoje ainda está em andamento
          </p>
          <GraficoSerieDiaria dados={internacoes.serieDiaria} rotuloUnidade="internações" />
        </Cartao>
      </div>
    </section>
  );
});
