import { FileText, ListChecks, Loader2, MessageCircleQuestion, RefreshCw } from 'lucide-react';

import { ROTULO_SISTEMA_REGULACAO } from '@/features/regulacao/types';
import type {
  ResultadoRegraRegulacao,
  SeveridadeRegraRegulacao,
  TipoRegraRegulacao,
} from '@/features/regulacao/tiposSolicitacao';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';
import { formatarInstante } from '@/shared/lib/datas';
import { useReanalisarAnaliseRegras } from '@/shared/regulacao/analiseRegras/analiseRegrasApi';
import { SeloVeredito } from '@/shared/regulacao/analiseRegras/BadgeAnaliseRegras';
import {
  DESCRICAO_VEREDITO,
  type AnaliseRegrasDetalhe,
  type SistemaAnaliseEspelho,
} from '@/shared/regulacao/analiseRegras/tipos';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';

/** Onde ficam os anexos do pedido — é lá que o documento se confere. */
const NOME_SISTEMA_ESPELHO: Record<SistemaAnaliseEspelho, string> = {
  ser: 'SER',
  sernit: 'SERNIT',
  esussg: 'ESUS',
};

const ROTULO_RESULTADO: Record<ResultadoRegraRegulacao, string> = {
  Atende: 'Atende',
  Bloqueia: 'Bloqueia',
  Ressalva: 'Ressalva',
  Indefinido: 'Indefinido',
};

const DICA_RESULTADO: Record<ResultadoRegraRegulacao, string> = {
  Atende: 'O pedido cumpre a regra.',
  Bloqueia: 'A regra barra o pedido para este sistema.',
  Ressalva: 'A regra passa, mas marcada — quem regula decide.',
  Indefinido: 'Falta dado para decidir (sem nascimento, sem CID, pergunta sem resposta…).',
};

const CLASSE_RESULTADO: Record<ResultadoRegraRegulacao, string> = {
  Atende: 'border-green-200 bg-green-50 text-green-700',
  Bloqueia: 'border-red-200 bg-red-50 text-red-700',
  Ressalva: 'border-yellow-300 bg-yellow-50 text-yellow-800',
  Indefinido: 'border-slate-200 bg-slate-100 text-slate-600',
};

const ROTULO_TIPO: Record<TipoRegraRegulacao, string> = {
  Dedutivel: 'deduzida do cadastro',
  NaoDedutivel: 'pergunta',
  Documental: 'documento',
  Informativa: 'informativa',
};

const ROTULO_SEVERIDADE: Record<SeveridadeRegraRegulacao, string> = {
  Bloqueia: 'bloqueia',
  Ressalva: 'ressalva',
  Aviso: 'aviso',
};

/**
 * Painel da análise automática das regras de elegibilidade, no detalhe de um pedido do espelho
 * (SER, SERNIT ou ESUS SG).
 *
 * <p>O mesmo avaliador do assistente de Nova Solicitação, rodando com o que o espelho sabe.
 * Pergunta não tem resposta aqui — vira "a conferir" quando pode travar. Documento não decide o
 * parecer (09/10/2026): os anexos ficam no sistema de origem e a análise não os enxerga, então ele
 * só aparece listado para quem regula conferir lá. É um parecer ao lado do pedido: nada é escrito
 * no sistema externo, por isso "Reanalisar" pede só a Consulta do módulo.</p>
 */
export function PainelAnaliseRegras({
  sistema,
  espelhoId,
  analise,
  className,
}: {
  sistema: SistemaAnaliseEspelho;
  espelhoId: string;
  analise: AnaliseRegrasDetalhe | null | undefined;
  className?: string;
}) {
  const reanalisar = useReanalisarAnaliseRegras(sistema, espelhoId);
  const r = analise?.resumo;

  return (
    <section className={cn('rounded-lg border border-slate-200 bg-white p-4', className)}>
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <ListChecks className="size-4 text-red-600" /> Análise das regras de elegibilidade
        </h2>
        <AjudaManual artigo="analise-regras-espelho" />
        {r && <SeloVeredito veredito={r.veredito} />}
        <Button
          variante="secundaria"
          tamanho="sm"
          className="ml-auto"
          onClick={() => reanalisar.mutate()}
          disabled={reanalisar.isPending}
          title="Refaz a análise agora, com as regras e o pedido como estão"
        >
          {reanalisar.isPending ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <RefreshCw className="size-4" />
          )}
          Reanalisar
        </Button>
      </div>

      <p className="mb-3 text-xs text-slate-500">
        Usa as mesmas regras do assistente de Nova Solicitação, com o que o espelho sabe (idade,
        sexo, CPF, CID). Pergunta não se responde sozinha — quando pode travar, o pedido fica “a
        conferir”. Os anexos ficam no sistema de origem e a análise não os vê: os documentos que o
        manual exige aparecem listados para você conferir lá, sem mudar o parecer. É um parecer ao lado
        do pedido: nada é escrito no sistema externo.
      </p>

      {reanalisar.isError && (
        <p className="mb-3 rounded bg-red-50 p-2 text-sm text-red-700">
          {extrairMensagemDeErro(reanalisar.error)}
        </p>
      )}

      {!analise || !r ? (
        <p className="rounded border border-dashed border-slate-300 p-3 text-sm text-slate-500">
          Este pedido ainda não foi analisado. A análise passa sozinha a cada 10 minutos nos pedidos
          em aberto (em fila ou pendentes) — e de novo quando o pedido ou as regras mudam. Use{' '}
          <strong>Reanalisar</strong> para rodar agora.
        </p>
      ) : (
        <div className="space-y-4">
          <div className="rounded bg-slate-50 p-3 text-sm text-slate-800">
            {r.resumo ?? DESCRICAO_VEREDITO[r.veredito]}
          </div>

          <dl className="grid gap-3 text-sm sm:grid-cols-4">
            <Contador rotulo="Bloqueios" valor={r.bloqueios} destaque={r.bloqueios > 0 ? 'text-red-700' : undefined} />
            <Contador rotulo="Ressalvas" valor={r.ressalvas} destaque={r.ressalvas > 0 ? 'text-yellow-700' : undefined} />
            <Contador
              rotulo="Perguntas em aberto"
              valor={r.perguntasPendentes}
              destaque={r.perguntasPendentes > 0 ? 'text-amber-700' : undefined}
            />
            <Contador rotulo="Documentos para conferir" valor={r.documentosPendentes} />
          </dl>

          <div className="text-sm">
            <span className="text-xs text-slate-500">Procedimento do catálogo canônico: </span>
            {analise.procedimentoNome ? (
              <span className="font-medium">{analise.procedimentoNome}</span>
            ) : (
              <span className="text-slate-500">
                não ligado — o procedimento do pedido ainda não foi pareado no catálogo canônico
                (Regulação → SER → Configuração, aba Catálogo de procedimentos).
              </span>
            )}
          </div>

          {analise.regras.length > 0 && (
            <div>
              <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                Regras avaliadas ({analise.regras.length})
              </h3>
              <ul className="space-y-2">
                {analise.regras.map((regra) => (
                  <li
                    key={`${regra.regraId}-${regra.versao}`}
                    className="rounded border border-slate-200 p-2"
                  >
                    <div className="flex flex-wrap items-start gap-2">
                      <span
                        title={DICA_RESULTADO[regra.resultado]}
                        className={cn(
                          'shrink-0 rounded-full border px-2 py-0.5 text-xs font-medium',
                          CLASSE_RESULTADO[regra.resultado],
                        )}
                      >
                        {ROTULO_RESULTADO[regra.resultado]}
                      </span>
                      <span className="min-w-0 flex-1 text-sm text-slate-800">{regra.descricao}</span>
                    </div>
                    {regra.motivo && (
                      <p className="mt-1 text-xs text-slate-600">{regra.motivo}</p>
                    )}
                    <p className="mt-1 text-[11px] text-slate-400">
                      {ROTULO_TIPO[regra.tipo]} · {ROTULO_SEVERIDADE[regra.severidade]}
                      {regra.sistema ? ` · só para ${ROTULO_SISTEMA_REGULACAO[regra.sistema]}` : ''} · versão {regra.versao}
                    </p>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {analise.perguntas.length > 0 && (
            <div>
              <h3 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500">
                <MessageCircleQuestion className="size-3.5" /> Perguntas que só uma pessoa responde
              </h3>
              <ul className="space-y-1 text-sm">
                {analise.perguntas.map((p) => (
                  <li key={p.regraId} className="flex flex-wrap items-center gap-2">
                    <span className="text-slate-800">{p.pergunta}</span>
                    {p.severidade === 'Bloqueia' && (
                      <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-800">
                        pode travar
                      </span>
                    )}
                    {/* Lista do manual: basta o paciente ter uma das condições. */}
                    {p.opcoes && p.opcoes.length > 0 && (
                      <ul className="w-full list-inside list-disc pl-1 text-xs text-slate-600">
                        <li className="list-none text-slate-500">Basta uma destas:</li>
                        {p.opcoes.map((o) => (
                          <li key={o.id}>{o.texto}</li>
                        ))}
                      </ul>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {analise.documentos.length > 0 && (
            <div>
              <h3 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500">
                <FileText className="size-3.5" /> Documentos que o manual exige — confira no{' '}
                {NOME_SISTEMA_ESPELHO[sistema]}
              </h3>
              <ul className="list-inside list-disc space-y-0.5 text-sm text-slate-800">
                {analise.documentos.map((d) => (
                  <li key={d}>{d}</li>
                ))}
              </ul>
            </div>
          )}

          <p className="text-xs text-slate-400">Analisado em {formatarInstante(r.analisadoEm)}.</p>
        </div>
      )}
    </section>
  );
}

function Contador({ rotulo, valor, destaque }: { rotulo: string; valor: number; destaque?: string }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className={cn('text-lg font-semibold', destaque ?? 'text-slate-800')}>{valor}</dd>
    </div>
  );
}
