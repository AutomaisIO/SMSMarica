import { useState } from 'react';
import { FlaskConical, Loader2, Play } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { useListarAssuntos, useSimularRobo } from '@/features/robo-atendimento/api/queries';
import type { RoboSimulacao } from '@/features/robo-atendimento/types';

const CAMPO =
  'w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400';

function ms(v: number): string {
  return v < 1000 ? `${v} ms` : `${(v / 1000).toFixed(1)} s`;
}

function usd(v: number | null): string {
  if (v === null) return '—';
  return `US$ ${v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 6 })}`;
}

function Resultado({ r }: { r: RoboSimulacao }) {
  return (
    <div className="space-y-3">
      {/* O que o cidadão receberia */}
      <div>
        <p className="mb-1 text-[11px] font-medium uppercase tracking-wide text-gray-500">
          Resposta ao cidadão
        </p>
        <div className="whitespace-pre-wrap rounded-2xl bg-indigo-50 px-3 py-2 text-sm text-indigo-900 ring-1 ring-indigo-200">
          {r.texto}
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2 text-[11px]">
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">
          {r.assunto ?? 'sem assunto identificado'}
        </span>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">{r.modelo}</span>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">{ms(r.duracaoMs)}</span>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">
          {r.tokensEntrada ?? 0} ent / {r.tokensSaida ?? 0} saí · {usd(r.custoUsd)}
        </span>
        {r.confianca !== null && (
          <span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">
            confiança {(r.confianca * 100).toFixed(0)}%
          </span>
        )}
        {!r.dentroDoHorario && (
          <span className="rounded-full bg-amber-50 px-2 py-0.5 text-amber-700">fora do horário</span>
        )}
        {r.handOff && (
          <span className="rounded-full bg-amber-50 px-2 py-0.5 font-medium text-amber-700">
            passaria para atendente{r.motivoHandOff ? ` (${r.motivoHandOff})` : ''}
          </span>
        )}
      </div>

      {r.chamadas.length > 0 && (
        <div>
          <p className="mb-1 text-[11px] font-medium uppercase tracking-wide text-gray-500">
            Comandos que o robô usou
          </p>
          <ul className="space-y-1.5">
            {r.chamadas.map((c, i) => (
              <li key={`${c.comando}-${i}`} className="rounded-md border border-gray-200 p-2 text-xs">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-gray-800">{c.comando}</span>
                  {c.simulado ? (
                    <span className="rounded-full bg-amber-50 px-2 py-0.5 text-amber-700">
                      escrita — não executado
                    </span>
                  ) : (
                    <span
                      className={`rounded-full px-2 py-0.5 ${
                        c.sucesso ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'
                      }`}
                    >
                      {c.sucesso ? 'executado' : 'falhou'}
                    </span>
                  )}
                </div>
                {c.entradaJson && (
                  <p className="mt-1 break-all font-mono text-[11px] text-gray-500">{c.entradaJson}</p>
                )}
                <p className="mt-1 text-gray-700">{c.resultado}</p>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/**
 * Ensaia um turno do robô sem falar com ninguém: mesma persona, mesmos treinos, mesmo modelo e as
 * mesmas ferramentas do atendimento real. Comandos de leitura rodam de verdade; os de ESCRITA são
 * apenas registrados. É o passo antes de religar o robô.
 */
export function SimuladorRoboCard() {
  const assuntos = useListarAssuntos(false);
  const simular = useSimularRobo();
  const [mensagem, setMensagem] = useState('');
  const [assuntoId, setAssuntoId] = useState('');
  const [resultado, setResultado] = useState<RoboSimulacao | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  function executar() {
    if (!mensagem.trim()) return;
    setErro(null);
    simular.mutate(
      { mensagem: mensagem.trim(), assuntoId: assuntoId || null },
      { onSuccess: setResultado, onError: (e) => setErro(extrairMensagemDeErro(e)) },
    );
  }

  return (
    <section className="space-y-3 rounded-lg border border-gray-200 bg-white p-4">
      <div>
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <FlaskConical className="h-4 w-4 text-primary-600" /> Simular atendimento
        </h2>
        <p className="mt-1 text-xs text-gray-500">
          Escreva o que um cidadão mandaria e veja o que o robô responderia. <strong>Nada é enviado</strong> —
          comandos que alteram dados não são executados.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
        <textarea
          value={mensagem}
          onChange={(e) => setMensagem(e.target.value)}
          rows={3}
          maxLength={1000}
          placeholder="Ex.: fiz um exame semana passada, cadê o resultado?"
          className={CAMPO}
        />
        <div className="flex flex-col gap-2">
          <select value={assuntoId} onChange={(e) => setAssuntoId(e.target.value)} className={CAMPO}>
            <option value="">Assunto: automático</option>
            {(assuntos.data ?? []).map((a) => (
              <option key={a.id} value={a.id}>
                {a.nome}
              </option>
            ))}
          </select>
          <Button type="button" onClick={executar} disabled={!mensagem.trim() || simular.isPending}>
            {simular.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Simular
          </Button>
        </div>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {resultado ? <Resultado r={resultado} /> : null}
    </section>
  );
}
