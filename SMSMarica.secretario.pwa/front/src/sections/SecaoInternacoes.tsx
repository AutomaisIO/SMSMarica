import { memo } from 'react';
import type { Internacoes, MesInternacao } from '@/types/painel';
import { formatarDecimal, formatarInteiro, horaMinuto } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { SeloEscopo } from '@/components/SeloEscopo';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { GraficoSerieDiaria, FAIXAS_INTERNACAO } from '@/components/graficos/GraficoSerieDiaria';

/**
 * Composição da internação em três faixas exclusivas, com legenda — entidade
 * sempre nomeada. O corte é pela UNIDADE do leito e pela IDADE na entrada, não por
 * "urgência/eletiva": no HMCML o campo ID_INTERNACAO não sustenta essa leitura
 * (ver docs/consultas-oracle.md §Q5). A maternidade vem primeiro de propósito —
 * senão os recém-nascidos do berçário engoliriam a faixa infantil.
 */
function ComposicaoInternacao({ mes }: { mes: MesInternacao }) {
  const faixas = [
    { chave: 'maternidade', valor: mes.maternidade },
    { chave: 'ate17', valor: mes.ate17 },
    { chave: 'adultos', valor: mes.adultos },
  ] as const;
  const total = faixas.reduce((s, f) => s + f.valor, 0);
  if (total === 0) return null;

  return (
    <div className="mt-3">
      <div className="flex h-2 gap-[2px] overflow-hidden rounded-full">
        {FAIXAS_INTERNACAO.map((faixa, i) => {
          const valor = faixas[i].valor;
          if (valor === 0) return null;
          return (
            <div
              key={faixa.chave}
              className={i === 0 ? 'rounded-l-full' : i === FAIXAS_INTERNACAO.length - 1 ? 'rounded-r-full' : ''}
              style={{ width: `${(valor / total) * 100}%`, backgroundColor: faixa.cor }}
            />
          );
        })}
      </div>
      <div className="tnum mt-1.5 flex flex-wrap items-center gap-x-3.5 gap-y-0.5 text-[12.5px] text-grafite">
        {FAIXAS_INTERNACAO.map((faixa, i) => (
          <span key={faixa.chave} className="flex items-center gap-1.5">
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: faixa.cor }} />
            {faixa.rotulo}{' '}
            <span className="font-semibold text-tinta">{formatarInteiro(faixas[i].valor)}</span>
          </span>
        ))}
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
      <ComposicaoInternacao mes={mes} />
    </Cartao>
  );
}

/**
 * Internações: mês atual × anterior com a composição por faixa e série diária.
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
          <div className="flex flex-wrap items-center justify-end gap-x-3 gap-y-1.5">
            <SeloEscopo escopo={internacoes.escopo} />
            <p className="tnum text-[12px] text-grafite">
              atualizado às {horaMinuto(internacoes.atualizadoEm)}
            </p>
          </div>
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
