import { CircleDot, PauseCircle, PowerOff } from 'lucide-react';
import { useAgendasPep, useBasesPep } from '@/features/pep-sincronizacao/api/queries';
import type { AgendaPep, BasePep, StatusImportacao } from '@/features/pep-sincronizacao/types';

function dataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('pt-BR');
}

type Situacao = {
  rotulo: string;
  detalhe: string;
  cor: string;
  Icone: typeof CircleDot;
};

/**
 * Situação do motor de UMA base. A distinção que importa e que não existia em lugar nenhum:
 * "desligado" e "pausado" não são a mesma coisa — desligado é decisão de configuração,
 * pausado é interrupção temporária que volta sozinha.
 */
function situacao(base: BasePep, agenda: AgendaPep | undefined, rodando: boolean): Situacao {
  if (rodando) {
    return { rotulo: 'Sincronizando agora', detalhe: 'run em andamento', cor: 'text-blue-700 bg-blue-50 border-blue-200', Icone: CircleDot };
  }
  if (!base.suportada) {
    return { rotulo: 'Sem conector', detalhe: 'nenhuma estratégia atende esta base', cor: 'text-gray-600 bg-gray-50 border-gray-200', Icone: PowerOff };
  }
  if (!agenda) {
    return { rotulo: 'Nunca ligado', detalhe: 'sem agenda — não roda sozinho', cor: 'text-amber-800 bg-amber-50 border-amber-200', Icone: PowerOff };
  }
  if (agenda.pausadoAte && new Date(agenda.pausadoAte) > new Date()) {
    return { rotulo: 'Pausado', detalhe: `até ${dataHora(agenda.pausadoAte)}`, cor: 'text-amber-800 bg-amber-50 border-amber-200', Icone: PauseCircle };
  }
  if (!agenda.ativo) {
    return { rotulo: 'Desligado', detalhe: 'agenda existe, mas está desmarcada', cor: 'text-gray-600 bg-gray-50 border-gray-200', Icone: PowerOff };
  }
  return {
    rotulo: 'Ligado',
    detalhe: `a cada ${agenda.intervaloMinutos} min`,
    cor: 'text-green-800 bg-green-50 border-green-200',
    Icone: CircleDot,
  };
}

/**
 * Estado dos motores de TODAS as bases, lado a lado.
 *
 * <p>Existe porque o painel era todo escopado à base selecionada: para saber se o Klinikos
 * estava sincronizando era preciso trocar o seletor e ler a caixa da agenda. Com uma base só
 * isso não fazia falta; com três — Salux, UPA e Santa Rita — a pergunta "o que está rodando?"
 * não tinha resposta em lugar nenhum da tela.</p>
 */
export function QuadroMotores({
  status,
  fonteSelecionada,
  aoSelecionar,
}: {
  status: StatusImportacao | undefined;
  fonteSelecionada: string;
  aoSelecionar: (fonteId: string) => void;
}) {
  const bases = useBasesPep();
  const agendas = useAgendasPep();

  if (!bases.data?.length) return null;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="mb-3 text-lg font-semibold text-gray-900">Motores por base</h2>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
        {bases.data.map((b) => {
          const agenda = agendas.data?.find((a) => a.fonteId === b.id);
          const rodando = !!status?.emExecucao && status.fonteId === b.id;
          const s = situacao(b, agenda, rodando);
          const selecionada = b.id === fonteSelecionada;

          return (
            <button
              key={b.id}
              type="button"
              onClick={() => aoSelecionar(b.id)}
              className={`rounded-lg border p-3 text-left transition hover:border-primary-400 ${
                selecionada ? 'border-primary-500 ring-1 ring-primary-200' : 'border-gray-200'
              }`}
            >
              <div className="flex items-start justify-between gap-2">
                <span className="truncate text-sm font-medium text-gray-900" title={b.nome}>
                  {b.nome}
                </span>
                <span className={`shrink-0 rounded-full border px-2 py-0.5 text-xs font-medium ${s.cor}`}>
                  <s.Icone className="mr-1 inline h-3 w-3" />
                  {s.rotulo}
                </span>
              </div>

              <p className="mt-1 text-xs text-gray-500">{s.detalhe}</p>

              <dl className="mt-2 space-y-0.5 text-xs text-gray-600">
                <div className="flex justify-between gap-2">
                  <dt className="text-gray-500">Último sincronismo</dt>
                  <dd>{dataHora(b.ultimaSincronizacaoEm)}</dd>
                </div>
                {agenda?.ativo ? (
                  <div className="flex justify-between gap-2">
                    <dt className="text-gray-500">Próximo</dt>
                    <dd>{dataHora(agenda.proximoRunEm)}</dd>
                  </div>
                ) : null}
                {agenda && agenda.falhasConsecutivas > 0 ? (
                  <div className="flex justify-between gap-2 font-medium text-red-700">
                    <dt>Falhas seguidas</dt>
                    <dd>{agenda.falhasConsecutivas}</dd>
                  </div>
                ) : null}
              </dl>
            </button>
          );
        })}
      </div>

      <p className="mt-3 text-xs text-gray-500">
        As agendas são independentes por base, mas <strong>roda uma importação por vez</strong>: enquanto
        um motor está sincronizando, os outros aguardam o próximo tique.
      </p>
    </section>
  );
}
