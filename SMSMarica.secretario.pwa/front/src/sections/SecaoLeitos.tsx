import { memo } from 'react';
import { AlertTriangle } from 'lucide-react';
import type { Leitos, PerfilInternados, Permanencia, SetorOcupacao } from '@/types/painel';
import {
  formatarDecimal,
  formatarInteiro,
  formatarPct,
  formatarPctFino,
  horaMinuto,
} from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { NumeroAnimado } from '@/components/NumeroAnimado';
import { SeloEscopo } from '@/components/SeloEscopo';
import { StatTile } from '@/components/StatTile';

/**
 * Faixa de ocupação: verde folgado, âmbar apertado, vermelho no limite. É a única
 * cor semântica desta tela — e vem sempre com o número ao lado, nunca sozinha.
 * Os cortes (85% e 95%) são os usados em gestão de leitos para "operação tensa" e
 * "sem folga".
 */
function corDaTaxa(taxa: number): { cor: string; rotulo: string } {
  // Decide pelo valor JÁ ARREDONDADO, o mesmo que aparece escrito: com 84,6 a tela
  // mostrava "85%" pintado de verde, e o leitor via a régua se contradizer.
  const exibida = Math.round(taxa);
  if (exibida >= 95) return { cor: '#D62828', rotulo: 'sem folga' };
  if (exibida >= 85) return { cor: '#E9A400', rotulo: 'operação tensa' };
  return { cor: '#2E9E5B', rotulo: 'com folga' };
}

function BarraSetor({ setor }: { setor: SetorOcupacao }) {
  const disponiveis = setor.leitos - setor.bloqueados;
  const taxa = setor.taxa;
  const largura = taxa != null ? Math.min(100, taxa) : 0;
  const estilo = taxa != null ? corDaTaxa(taxa) : null;

  return (
    <div className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-1 py-2.5">
      <div className="min-w-0">
        <p className="truncate text-[14px] font-semibold leading-tight text-tinta">{setor.setor}</p>
        <p className="tnum text-[12px] text-grafite">
          {formatarInteiro(setor.ocupados)} de {formatarInteiro(disponiveis)} leitos
          {setor.bloqueados > 0 && (
            <span className="text-grafite/80"> · {formatarInteiro(setor.bloqueados)} bloqueados</span>
          )}
        </p>
      </div>
      <p className="tnum text-right font-display text-[17px] font-bold leading-none text-tinta">
        {taxa != null ? formatarPct(taxa) : '—'}
      </p>
      <div className="col-span-2 h-2 w-full overflow-hidden rounded-full bg-grade">
        {estilo && (
          <div
            className="h-full rounded-full transition-[width] duration-500"
            style={{ width: `${largura}%`, backgroundColor: estilo.cor }}
          />
        )}
      </div>
    </div>
  );
}

function Composicao({ perfil }: { perfil: PerfilInternados }) {
  const faixas = [
    { rotulo: 'Até 17 anos', valor: perfil.ate17, cor: '#C97B86' },
    { rotulo: '18 a 59 anos', valor: perfil.adultos, cor: '#2F6FDE' },
    { rotulo: '60 anos ou mais', valor: perfil.idosos, cor: '#7A5AF8' },
  ];
  const sexos = [
    { rotulo: 'Mulheres', valor: perfil.mulheres, cor: '#C2589A' },
    { rotulo: 'Homens', valor: perfil.homens, cor: '#2F8FA8' },
    ...(perfil.semSexo > 0
      ? [{ rotulo: 'Sem registro', valor: perfil.semSexo, cor: '#8494A8' }]
      : []),
  ];

  return (
    <div className="grid gap-5 sm:grid-cols-2">
      {[
        { titulo: 'Por faixa etária', itens: faixas },
        { titulo: 'Por sexo', itens: sexos },
      ].map((grupo) => {
        const total = grupo.itens.reduce((s, i) => s + i.valor, 0);
        return (
          <div key={grupo.titulo}>
            <p className="mb-2 text-[13px] font-semibold text-tinta">{grupo.titulo}</p>
            {total > 0 && (
              <div className="flex h-2 gap-[2px] overflow-hidden rounded-full">
                {grupo.itens.map((item, i) =>
                  item.valor === 0 ? null : (
                    <div
                      key={item.rotulo}
                      className={
                        i === 0 ? 'rounded-l-full' : i === grupo.itens.length - 1 ? 'rounded-r-full' : ''
                      }
                      style={{ width: `${(item.valor / total) * 100}%`, backgroundColor: item.cor }}
                    />
                  ),
                )}
              </div>
            )}
            <div className="tnum mt-2 space-y-0.5">
              {grupo.itens.map((item) => (
                <p key={item.rotulo} className="flex items-center gap-2 text-[12.5px] text-grafite">
                  <span className="h-2 w-2 shrink-0 rounded-full" style={{ backgroundColor: item.cor }} />
                  {item.rotulo}
                  <span className="ml-auto font-semibold text-tinta">{formatarInteiro(item.valor)}</span>
                </p>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

const ROTULO_SEGMENTO: Record<string, string> = {
  HOMENS: 'Homens',
  MULHERES: 'Mulheres',
  ATE17: 'Até 17 anos',
  ADULTOS: '18 a 59 anos',
  IDOSOS: '60 anos ou mais',
};

function TabelaPermanencia({ permanencia }: { permanencia: Permanencia }) {
  const maior = Math.max(
    ...permanencia.segmentos.map((s) => s.mediaDias ?? 0),
    permanencia.mediaDias ?? 0,
    1,
  );

  return (
    <div className="space-y-2.5">
      {permanencia.segmentos.map((s) => (
        <div key={s.segmento} className="grid grid-cols-[1fr_auto] items-center gap-x-3 gap-y-1">
          <p className="text-[13.5px] text-tinta">{ROTULO_SEGMENTO[s.segmento] ?? s.segmento}</p>
          <p className="tnum text-right text-[13px] text-grafite">
            <span className="font-display text-[15px] font-bold text-tinta">
              {s.mediaDias != null ? formatarDecimal(s.mediaDias) : '—'}
            </span>{' '}
            dias · {formatarInteiro(s.altas)} altas
          </p>
          <div className="col-span-2 h-1.5 w-full overflow-hidden rounded-full bg-grade">
            <div
              className="h-full rounded-full bg-vermelho-marica/70"
              style={{ width: `${((s.mediaDias ?? 0) / maior) * 100}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

/**
 * Leitos e internação. O Conde tem internação de verdade — setores, perfil de quem
 * está no leito e permanência das altas. As UPAs têm leitos de OBSERVAÇÃO, que é
 * outra coisa, e a tela nomeia isso em vez de fingir simetria.
 *
 * memo: só re-renderiza quando os dados mudam (painel aberto em TV).
 */
export const SecaoLeitos = memo(function SecaoLeitos({ leitos }: { leitos: Leitos }) {
  const { ocupacao, perfil, permanencia, observacao } = leitos;
  const estiloTaxa = ocupacao?.taxa != null ? corDaTaxa(ocupacao.taxa) : null;

  return (
    <div className="space-y-10 sm:space-y-12">
      <section aria-labelledby="titulo-ocupacao" className="anima-entrada">
        <CabecalhoSecao
          eyebrow="Leitos"
          titulo="Ocupação"
          tituloId="titulo-ocupacao"
          sub={`Atualizado às ${horaMinuto(leitos.atualizadoEm)}`}
          direita={<SeloEscopo escopo={leitos.escopo} />}
        />

        {/* Sem cadastro utilizável, a tela DIZ o que falta. Publicar "0 de 2 leitos"
            faria o Secretário concluir que a unidade está vazia. */}
        {leitos.indisponivel && (
          <Cartao className="p-5">
            <div className="flex gap-3">
              <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-triagem-amarelo/15 text-triagem-amarelo-apoio">
                <AlertTriangle className="h-4 w-4" aria-hidden="true" />
              </span>
              <div>
                <p className="text-[15px] font-semibold text-tinta">Cadastro de leitos indisponível</p>
                <p className="mt-1 max-w-prose text-[13.5px] leading-relaxed text-grafite">
                  {leitos.indisponivel}
                </p>
              </div>
            </div>
          </Cartao>
        )}

        {ocupacao && (
          <>
            <Cartao className="p-6 sm:p-8">
              <div className="flex flex-wrap items-end justify-between gap-4">
                <div>
                  <p className="eyebrow">Taxa de ocupação</p>
                  <p
                    className="mt-2 font-display text-[clamp(44px,9vw,72px)] font-extrabold leading-none tracking-tight"
                    style={{ color: estiloTaxa?.cor }}
                  >
                    {ocupacao.taxa != null ? (
                      <NumeroAnimado valor={ocupacao.taxa} formatar={formatarPct} />
                    ) : (
                      '—'
                    )}
                  </p>
                  {estiloTaxa && (
                    <p className="mt-2 text-[15px] font-medium text-grafite">{estiloTaxa.rotulo}</p>
                  )}
                </div>
                <div className="tnum text-right text-[13.5px] leading-relaxed text-grafite">
                  <p>
                    <span className="font-display text-[22px] font-bold text-tinta">
                      {formatarInteiro(ocupacao.ocupados)}
                    </span>{' '}
                    ocupados
                  </p>
                  <p>{formatarInteiro(ocupacao.livres)} livres</p>
                  <p>{formatarInteiro(ocupacao.leitos)} leitos cadastrados</p>
                </div>
              </div>
              <p className="mt-4 border-t border-linha pt-3 text-[12.5px] leading-relaxed text-grafite">
                A taxa considera os leitos disponíveis
                {ocupacao.bloqueados > 0 && (
                  <>
                    {' '}
                    — os{' '}
                    <span className="font-semibold text-tinta">
                      {formatarInteiro(ocupacao.bloqueados)} leitos bloqueados
                    </span>{' '}
                    ficam de fora, porque leito interditado não é capacidade
                  </>
                )}
                .
              </p>
            </Cartao>

            {leitos.setores.length > 0 && (
              <Cartao className="mt-3 p-5">
                <p className="text-[15px] font-semibold text-tinta">Ocupação por setor</p>
                <p className="mb-2 text-[12.5px] text-grafite">
                  do mais cheio para o mais vazio · percentual sobre os leitos disponíveis
                </p>
                <div className="divide-y divide-linha">
                  {leitos.setores.map((setor) => (
                    <BarraSetor key={setor.setor} setor={setor} />
                  ))}
                </div>
              </Cartao>
            )}
          </>
        )}

        {observacao && (
          <div className="mt-3 grid grid-cols-2 gap-3">
            <StatTile
              rotulo={`Encaminhados à observação · ${observacao.rotulo}`}
              valor={observacao.encaminhados}
              detalhes={[`de ${formatarInteiro(observacao.classificados)} pacientes classificados`]}
            />
            <StatTile
              rotulo="Fatia da triagem"
              valor={
                observacao.classificados > 0
                  ? (observacao.encaminhados / observacao.classificados) * 100
                  : 0
              }
              formatar={formatarPctFino}
              detalhes={['dos classificados foram para leito de observação']}
            />
          </div>
        )}
      </section>

      {perfil && perfil.total > 0 && (
        <section aria-labelledby="titulo-perfil" className="anima-entrada" style={{ animationDelay: '70ms' }}>
          <CabecalhoSecao
            eyebrow="Internados"
            titulo="Quem está no leito agora"
            tituloId="titulo-perfil"
            sub={`${formatarInteiro(perfil.total)} pessoas internadas neste momento`}
          />
          <Cartao className="p-5 sm:p-6">
            <Composicao perfil={perfil} />
            <div className="mt-5 grid grid-cols-2 gap-x-6 gap-y-2 border-t border-linha pt-4 text-[13px] text-grafite">
              <p>
                Idade média{' '}
                <span className="font-display text-[16px] font-bold text-tinta">
                  {perfil.idadeMedia != null ? formatarDecimal(perfil.idadeMedia) : '—'}
                </span>{' '}
                anos
              </p>
              <p>
                Já internados há{' '}
                <span className="font-display text-[16px] font-bold text-tinta">
                  {perfil.diasMedios != null ? formatarDecimal(perfil.diasMedios) : '—'}
                </span>{' '}
                dias em média
              </p>
            </div>
          </Cartao>
        </section>
      )}

      {permanencia && permanencia.altas > 0 && (
        <section
          aria-labelledby="titulo-permanencia"
          className="anima-entrada"
          style={{ animationDelay: '140ms' }}
        >
          <CabecalhoSecao
            eyebrow="Permanência"
            titulo="Tempo médio de internação"
            tituloId="titulo-permanencia"
            sub={`Altas de ${permanencia.rotulo} · da entrada no leito até a alta`}
          />
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <StatTile
                rotulo="Média"
                valor={permanencia.mediaDias ?? 0}
                formatar={(n) => `${formatarDecimal(n)} dias`}
                detalhes={[`${formatarInteiro(permanencia.altas)} altas no período`]}
              />
              <StatTile
                rotulo="Mediana"
                valor={permanencia.medianaDias ?? 0}
                formatar={(n) => `${formatarDecimal(n)} dias`}
                detalhes={['metade das altas saiu antes disso']}
              />
              <StatTile
                rotulo="P90"
                valor={permanencia.p90Dias ?? 0}
                formatar={(n) => `${formatarDecimal(n)} dias`}
                detalhes={['9 em cada 10 saíram antes disso']}
              />
              <StatTile
                rotulo="Internados agora"
                valor={perfil?.diasMedios ?? 0}
                formatar={(n) => `${formatarDecimal(n)} dias`}
                detalhes={['tempo já decorrido de quem ainda está no leito']}
              />
            </div>
            <Cartao className="p-5">
              <p className="text-[15px] font-semibold text-tinta">Por segmento</p>
              <p className="mb-3 text-[12.5px] text-grafite">
                média de dias por alta · sexo e faixa etária se sobrepõem, cada um é o total
              </p>
              <TabelaPermanencia permanencia={permanencia} />
            </Cartao>
          </div>
        </section>
      )}
    </div>
  );
});
