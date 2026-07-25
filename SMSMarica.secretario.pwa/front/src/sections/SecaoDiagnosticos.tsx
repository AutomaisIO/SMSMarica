import { memo, useState } from 'react';
import type { CorTriagem, Diagnosticos } from '@/types/painel';
import { TRIAGEM, ORDEM_TRIAGEM } from '@/lib/triagem';
import { formatarInteiro, formatarPct } from '@/lib/formatos';
import { Cartao } from '@/components/Cartao';
import { CabecalhoSecao } from '@/components/CabecalhoSecao';
import { SeloEscopo } from '@/components/SeloEscopo';
import { SegmentedControl, type OpcaoSegmento } from '@/components/SegmentedControl';

/**
 * Os diagnósticos mais registrados em cada cor da triagem.
 *
 * Abre no AMARELO por pedido da gerência do contrato: é a cor onde a pergunta
 * "por que essas pessoas estão vindo?" tem mais consequência — volume alto o
 * bastante para significar alguma coisa e gravidade alta o bastante para importar.
 */
const COR_PADRAO: CorTriagem = 'AMARELO';

export const SecaoDiagnosticos = memo(function SecaoDiagnosticos({
  diagnosticos,
}: {
  diagnosticos: Diagnosticos;
}) {
  const disponiveis = ORDEM_TRIAGEM.filter((cor) =>
    diagnosticos.porCor.some((c) => c.cor === cor && c.cids.length > 0),
  );
  const [cor, setCor] = useState<CorTriagem>(
    disponiveis.includes(COR_PADRAO) ? COR_PADRAO : (disponiveis[0] ?? COR_PADRAO),
  );

  const escolhida = diagnosticos.porCor.find((c) => c.cor === cor) ?? diagnosticos.porCor[0];
  if (!escolhida) return null;

  const estilo = TRIAGEM[escolhida.cor];
  const opcoes: OpcaoSegmento<CorTriagem>[] = disponiveis.map((c) => ({
    valor: c,
    rotulo: TRIAGEM[c].nome,
  }));
  const maior = Math.max(...escolhida.cids.map((c) => c.qtd), 1);

  return (
    <section aria-labelledby="titulo-diagnosticos">
      <CabecalhoSecao
        eyebrow="Diagnósticos"
        titulo="Por que estão procurando"
        tituloId="titulo-diagnosticos"
        sub={`CIDs mais registrados em ${diagnosticos.rotulo} · escolha a cor da classificação`}
        direita={<SeloEscopo escopo={diagnosticos.escopo} />}
      />

      {opcoes.length > 1 && (
        <div className="mb-3 overflow-x-auto pb-1">
          <SegmentedControl
            opcoes={opcoes}
            valor={escolhida.cor}
            aoMudar={setCor}
            ariaLabel="Cor da classificação de risco"
          />
        </div>
      )}

      <Cartao className="p-5">
        <div className="mb-4 flex items-baseline gap-2.5">
          <span
            className="h-3 w-3 shrink-0 rounded-full"
            style={{ backgroundColor: estilo.cor }}
            aria-hidden="true"
          />
          <p className="text-[15px] font-semibold text-tinta">{estilo.nome}</p>
          <p className="tnum text-[12.5px] text-grafite">
            {formatarInteiro(escolhida.boletins)} boletins com diagnóstico registrado
          </p>
        </div>

        <ol className="space-y-3">
          {escolhida.cids.map((cid, i) => (
            <li key={cid.codigo} className="grid grid-cols-[auto_1fr_auto] items-center gap-x-3 gap-y-1">
              <span className="tnum w-5 text-right font-display text-[15px] font-bold text-grafite/70">
                {i + 1}
              </span>
              <div className="min-w-0">
                <p className="truncate text-[14px] font-medium leading-tight text-tinta">
                  {cid.descricao}
                </p>
                <p className="tnum text-[11.5px] text-grafite">CID {cid.codigo}</p>
              </div>
              <p className="tnum text-right text-[13px] text-grafite">
                <span className="font-display text-[16px] font-bold text-tinta">
                  {formatarInteiro(cid.qtd)}
                </span>
                {cid.pct != null && <> · {formatarPct(cid.pct)}</>}
              </p>
              <div className="col-span-3 ml-8 h-1.5 overflow-hidden rounded-full bg-grade">
                <div
                  className="h-full rounded-full"
                  style={{ width: `${(cid.qtd / maior) * 100}%`, backgroundColor: estilo.cor }}
                />
              </div>
            </li>
          ))}
        </ol>
      </Cartao>

      {/* A ressalva anda colada ao número — o percentual é sobre quem TEM CID. */}
      <p className="mt-3 text-[12.5px] leading-relaxed text-grafite">
        O percentual é sobre os boletins da cor que têm diagnóstico registrado, não sobre
        o total de atendimentos.
      </p>
    </section>
  );
});
