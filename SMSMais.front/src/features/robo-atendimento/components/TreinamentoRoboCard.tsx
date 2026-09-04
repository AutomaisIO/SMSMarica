import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { GraduationCap, Loader2, Plus } from 'lucide-react';
import {
  useAbrirTreinamento,
  useListarTreinamento,
} from '@/features/robo-atendimento/api/queries';
import { TreinamentoItemDetalhe } from '@/features/robo-atendimento/components/TreinamentoItemDetalhe';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import type { StatusTreinamento, TreinamentoItemResumo } from '@/features/robo-atendimento/types';

const FILTROS: { rotulo: string; valor?: StatusTreinamento }[] = [
  { rotulo: 'A tratar', valor: 'Aberto' },
  { rotulo: 'Aguardando você', valor: 'AguardandoHumano' },
  { rotulo: 'Simulação pendente', valor: 'SimulacaoPendente' },
  { rotulo: 'Concluídos', valor: 'Concluido' },
  { rotulo: 'Todos', valor: undefined },
];

/** Cada situação tem uma cor e, mais importante, uma frase que diz de quem é a vez. */
export const ESTILO_STATUS: Record<StatusTreinamento, { rotulo: string; classe: string; dica: string }> = {
  Aberto: {
    rotulo: 'A tratar',
    classe: 'bg-gray-100 text-gray-700',
    dica: 'Crítica registrada. Mande treinar quando quiser.',
  },
  Analisando: {
    rotulo: 'Analisando',
    classe: 'bg-blue-50 text-blue-700',
    dica: 'O agente está propondo, atacando e julgando a correção. Leva alguns minutos.',
  },
  AguardandoHumano: {
    rotulo: 'Aguardando você',
    classe: 'bg-amber-100 text-amber-800',
    dica: 'A análise parou numa decisão que não é do agente. Responda a pendência para destravar.',
  },
  SimulacaoPendente: {
    rotulo: 'Falta simular',
    classe: 'bg-purple-50 text-purple-700',
    dica: 'A correção foi aplicada, mas ninguém viu o robô respondendo com ela ainda.',
  },
  Concluido: {
    rotulo: 'Concluído',
    classe: 'bg-emerald-50 text-emerald-700',
    dica: 'Analisado, aplicado e verificado por simulação.',
  },
  Descartado: {
    rotulo: 'Descartado',
    classe: 'bg-gray-100 text-gray-500',
    dica: 'A crítica não gerou mudança.',
  },
  Falhou: {
    rotulo: 'Falhou',
    classe: 'bg-red-50 text-red-700',
    dica: 'A análise não completou. Dá para mandar treinar de novo.',
  },
};

function dataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit',
  });
}

function Linha({ item, onAbrir }: { item: TreinamentoItemResumo; onAbrir: () => void }) {
  const estilo = ESTILO_STATUS[item.status];
  return (
    <li>
      <button
        type="button"
        onClick={onAbrir}
        className="w-full space-y-1.5 rounded-lg border border-gray-200 bg-white p-3 text-left hover:border-primary-300 hover:bg-primary-50/30"
      >
        <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
          <span className={`rounded-full px-2 py-0.5 font-medium ${estilo.classe}`}>
            {item.status === 'Analisando' && <Loader2 className="mr-1 inline h-3 w-3 animate-spin" />}
            {estilo.rotulo}
          </span>
          <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-medium text-indigo-700">
            {item.assuntoNome ?? 'sem assunto'}
          </span>
          <span>{dataHora(item.criadoEm)}</span>
          {item.criadoPorNome ? <span>· por {item.criadoPorNome}</span> : null}
          {item.pendenciasAbertas > 0 && (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 font-medium text-amber-800">
              {item.pendenciasAbertas} pendência{item.pendenciasAbertas > 1 ? 's' : ''}
            </span>
          )}
          {item.alteracoesAplicadas > 0 && (
            <span className="rounded-full bg-emerald-50 px-2 py-0.5 font-medium text-emerald-700">
              {item.alteracoesAplicadas} alteração{item.alteracoesAplicadas > 1 ? 'ões' : ''}
            </span>
          )}
          {item.ultimoVeredito && (
            <span
              className={`rounded-full px-2 py-0.5 font-medium ${
                item.ultimoVeredito === 'Passou'
                  ? 'bg-emerald-50 text-emerald-700'
                  : item.ultimoVeredito === 'Falhou'
                    ? 'bg-red-50 text-red-700'
                    : 'bg-yellow-50 text-yellow-800'
              }`}
            >
              simulação: {item.ultimoVeredito.toLowerCase()}
            </span>
          )}
        </div>
        <p className="line-clamp-2 text-sm text-gray-800">{item.critica}</p>
        {item.trecho ? (
          <p className="line-clamp-1 text-xs italic text-gray-400">robô: “{item.trecho}”</p>
        ) : null}
      </button>
    </li>
  );
}

/**
 * Treinamento do robô: a fila de críticas que os atendentes escreveram nas bolhas, o que o agente
 * fez com cada uma e o que ainda depende de uma decisão humana.
 */
export function TreinamentoRoboCard() {
  const [params, setParams] = useSearchParams();
  const [status, setStatus] = useState<StatusTreinamento | undefined>(undefined);
  const [aberto, setAberto] = useState<string | null>(null);
  const [novoAberto, setNovoAberto] = useState(false);
  const [novaCritica, setNovaCritica] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const { data, isLoading } = useListarTreinamento(status);
  const abrir = useAbrirTreinamento();

  // A tela de conversas manda o operador direto para o item que ele acabou de criar.
  const alvoDaUrl = params.get('treinamento');
  useEffect(() => {
    if (alvoDaUrl) setAberto(alvoDaUrl);
  }, [alvoDaUrl]);

  function fechar() {
    setAberto(null);
    if (params.has('treinamento')) {
      params.delete('treinamento');
      setParams(params, { replace: true });
    }
  }

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="flex items-center gap-2 text-base font-semibold text-gray-900">
            <GraduationCap className="h-4 w-4 text-amber-500" /> Treinamento
          </h2>
          <p className="text-xs text-gray-500">
            As críticas dos atendentes viram correção no material do robô — analisadas contra as
            regras que já existem antes de entrar.
          </p>
        </div>
        <button
          type="button"
          onClick={() => { setNovoAberto((v) => !v); setNovaCritica(''); setErro(null); }}
          className="flex items-center gap-1 rounded-md border border-gray-200 px-2.5 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        >
          <Plus className="h-3.5 w-3.5" /> Ensinar algo
        </button>
      </div>

      {novoAberto && (
        <div className="rounded-lg border border-gray-200 bg-white p-3">
          <p className="text-xs text-gray-500">
            Sem partir de uma conversa: descreva o comportamento que quer corrigir ou ensinar.
          </p>
          <textarea
            value={novaCritica}
            onChange={(e) => setNovaCritica(e.target.value)}
            rows={3}
            maxLength={4000}
            placeholder="Ex.: quando perguntarem por atestado, orientar a procurar a unidade onde foi atendido — o robô não trata atestado."
            className="mt-2 w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
          />
          {erro && <p className="mt-2 text-xs text-red-600">{erro}</p>}
          <div className="mt-2 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setNovoAberto(false)}
              className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={abrir.isPending || novaCritica.trim().length === 0}
              onClick={() => {
                setErro(null);
                abrir.mutate(
                  { critica: novaCritica.trim() },
                  {
                    onSuccess: (id) => { setNovoAberto(false); setAberto(id); },
                    onError: (e) => setErro(extrairMensagemDeErro(e)),
                  },
                );
              }}
              className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
            >
              {abrir.isPending ? 'Abrindo…' : 'Abrir item'}
            </button>
          </div>
        </div>
      )}

      <div className="flex flex-wrap gap-1.5">
        {FILTROS.map((f) => (
          <button
            key={f.rotulo}
            type="button"
            onClick={() => setStatus(f.valor)}
            className={`rounded-full px-2.5 py-1 text-xs font-medium ${
              status === f.valor
                ? 'bg-primary-600 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            {f.rotulo}
          </button>
        ))}
      </div>

      {isLoading && <p className="text-sm text-gray-500">Carregando…</p>}
      {!isLoading && (data?.length ?? 0) === 0 && (
        <p className="rounded-lg border border-dashed border-gray-200 p-4 text-sm text-gray-500">
          Nada aqui. Os atendentes criam itens pelo botão “Treinar” abaixo de cada resposta do robô,
          na Central de Atendimento.
        </p>
      )}

      <ul className="space-y-2">
        {data?.map((i) => (
          <Linha key={i.id} item={i} onAbrir={() => setAberto(i.id)} />
        ))}
      </ul>

      {aberto && <TreinamentoItemDetalhe id={aberto} onFechar={fechar} />}
    </section>
  );
}
