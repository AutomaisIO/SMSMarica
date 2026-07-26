import type { EsperaCor } from '@/types/painel';
import { TRIAGEM } from '@/lib/triagem';
import { formatarInteiro, formatarPct, minutosLegiveis } from '@/lib/formatos';
import { NumeroAnimado } from '@/components/NumeroAnimado';

/**
 * A assinatura do painel: cada cor de classificação é uma PULSEIRA de hospital —
 * banda horizontal de raio alto com a ponta na cor da triagem (3 furinhos de
 * snap, como a pulseira real), tempo médio em número grande e uma barra fina de
 * tempo vs meta (tick vertical na meta; o excedente ganha o tom forte).
 */

function BarraMeta({
  media,
  meta,
  cor,
  corForte,
}: {
  media: number;
  meta: number | null;
  cor: string;
  corForte: string;
}) {
  // Escala mínima 1 — anti-NaN: com media 0 (e sem meta) a divisão seria 0/0.
  const escala = Math.max(meta != null ? Math.max(media, meta) * 1.15 : media * 1.25, 1);
  const pctMedia = Math.min(100, (media / escala) * 100);
  const pctMeta = meta != null ? (meta / escala) * 100 : null;
  const pctDentro = pctMeta != null ? Math.min(pctMedia, pctMeta) : pctMedia;
  const excede = pctMeta != null && pctMedia > pctMeta;

  return (
    <div className="relative h-1.5 w-full rounded-full bg-grade">
      <div
        className="absolute inset-y-0 left-0 rounded-full"
        style={{ width: `${pctDentro}%`, backgroundColor: cor }}
      />
      {excede && (
        <div
          className="absolute inset-y-0 rounded-r-full"
          style={{
            left: `${pctMeta}%`,
            width: `${pctMedia - (pctMeta as number)}%`,
            backgroundColor: corForte,
          }}
        />
      )}
      {pctMeta != null && (
        <span
          className="absolute -top-[3px] h-3 w-[2px] rounded-full bg-tinta/60 shadow-[0_0_0_1.5px_rgba(255,255,255,0.9)]"
          style={{ left: `calc(${pctMeta}% - 1px)` }}
          aria-hidden="true"
        />
      )}
    </div>
  );
}

interface Props {
  item: EsperaCor;
}

export function Pulseira({ item }: Props) {
  const estilo = TRIAGEM[item.cor];
  const temEspera = item.mediaEspera != null;
  // Boletim que nunca passou pela triagem não tem de onde contar o tempo — e é
  // diferente de "ninguém foi atendido", que é o que "0 de N" dá a entender.
  const semTriagem = item.cor === 'SEM_CLASSIFICACAO' && item.comAtendimento === 0;

  // No VERMELHO o relógio é outro: conta da CHEGADA até a primeira interação de
  // qualquer natureza (a classificação já vale), porque ali o médico assiste antes de
  // registrar — medir "classificação → documento" mediria o papel, não o cuidado.
  const vermelho = item.cor === 'VERMELHO';
  const rotuloEspera = vermelho ? 'chegada → 1ª interação' : 'classificação → médico';

  // Meta zero é alvo IMEDIATO, não prazo: não vira tick na barra nem percentual.
  const metaEhPrazo = item.metaMin != null && item.metaMin > 0;

  return (
    <div className="flex items-stretch overflow-hidden rounded-[28px] border border-linha bg-papel shadow-cartao transition-colors duration-200 hover:border-vermelho-marica/30 sm:rounded-full">
      {/* ponta da pulseira: cor da triagem + 3 furinhos de snap */}
      <div
        className="flex w-11 shrink-0 items-center justify-center gap-[5px] sm:w-14"
        style={{ backgroundColor: estilo.cor }}
        aria-hidden="true"
      >
        {[0, 1, 2].map((furo) => (
          <span
            key={furo}
            className="h-[5px] w-[5px] rounded-full bg-white/45 shadow-[inset_0_1px_1.5px_rgba(0,0,0,0.3)]"
          />
        ))}
      </div>

      <div className="grid flex-1 grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 py-3.5 pl-4 pr-5 sm:grid-cols-[150px_148px_1fr_auto] sm:gap-x-6 sm:py-3 sm:pr-7">
        {/* nome + N de pacientes + cobertura visível (alvo é celular/touch — nada só em title) */}
        <div className="min-w-0">
          <p className="text-[15px] font-semibold leading-tight text-tinta">{estilo.nome}</p>
          <p className="tnum text-[12.5px] text-grafite">
            {formatarInteiro(item.pacientes)} {item.pacientes === 1 ? 'paciente' : 'pacientes'}
          </p>
          <p className="tnum text-[11.5px] leading-snug text-grafite/80">
            {semTriagem ? (
              'sem triagem registrada'
            ) : (
              <>
                {formatarInteiro(item.comAtendimento)} de {formatarInteiro(item.pacientes)} com
                atendimento registrado
              </>
            )}
          </p>
        </div>

        {/*
          Os DOIS tempos da jornada, um sobre o outro e nomeados. Separados porque cobram
          donos diferentes: o primeiro é da enfermagem da porta, o segundo é do plantão
          médico. Só o segundo é o número-herói (é o que vai contra a meta), mas o
          primeiro deixou de ser invisível — ele vinha no contrato desde sempre e nunca
          chegava à tela.
        */}
        <div className="text-right sm:text-left">
          <p className="text-[10.5px] uppercase leading-tight tracking-wide text-grafite/85">
            chegada → classificação
          </p>
          <p className="tnum font-display text-[17px] font-semibold leading-none text-tinta sm:text-[19px]">
            {item.mediaAteTriagem != null ? minutosLegiveis(item.mediaAteTriagem) : '—'}
          </p>

          <p className="mt-2 text-[10.5px] uppercase leading-tight tracking-wide text-grafite/85">
            {rotuloEspera}
          </p>
          {temEspera ? (
            <p className="font-display text-[26px] font-bold leading-none tracking-tight text-tinta sm:text-[30px]">
              <NumeroAnimado valor={item.mediaEspera as number} formatar={minutosLegiveis} />
            </p>
          ) : (
            <p className="font-display text-[26px] font-bold leading-none text-grafite sm:text-[30px]">—</p>
          )}
        </div>

        {/* barra tempo vs meta */}
        <div className="col-span-2 sm:col-span-1">
          {temEspera && (
            <>
              {/*
                Alvo imediato (meta 0) não desenha barra: sem prazo não há régua, e uma
                barra cheia da cor da triagem lê como alarme — no vermelho, 4 minutos
                apareciam como uma faixa vermelha de ponta a ponta.
              */}
              {item.metaMin !== 0 && (
                <BarraMeta
                  media={item.mediaEspera as number}
                  meta={metaEhPrazo ? item.metaMin : null}
                  cor={estilo.cor}
                  corForte={estilo.corForte}
                />
              )}
              <div className="mt-1.5 flex items-baseline justify-between gap-3 text-[11.5px] text-grafite">
                <span>
                  {metaEhPrazo
                    ? `meta ${minutosLegiveis(item.metaMin as number)}`
                    : item.metaMin === 0
                      ? 'alvo: atendimento imediato'
                      : 'sem meta definida'}
                </span>
                {item.pctNaMeta != null && (
                  <span className="tnum">
                    <span className="font-semibold text-tinta">{formatarPct(item.pctNaMeta)}</span> na meta
                  </span>
                )}
              </div>
            </>
          )}
          {!temEspera && (
            <p className="text-[12.5px] text-grafite">
              {semTriagem
                ? 'tempo não medido — sem classificação de risco no boletim'
                : 'sem atendimento médico registrado no período'}
            </p>
          )}
        </div>

        {/* mediana e p90 */}
        {temEspera && (
          <div className="col-span-2 tnum text-[12px] leading-snug text-grafite sm:col-span-1 sm:text-right">
            {item.medianaEspera != null && <p>mediana {minutosLegiveis(item.medianaEspera)}</p>}
            {item.p90Espera != null && <p>p90 {minutosLegiveis(item.p90Espera)}</p>}
          </div>
        )}
      </div>
    </div>
  );
}
