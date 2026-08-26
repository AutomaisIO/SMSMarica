import { useState } from 'react';
import { Bot, ChevronDown, ChevronRight, Loader2, MessageCircle, Users } from 'lucide-react';
import { useMensagensSessaoPaciente, useSessoesConversaPaciente } from '@/features/pacientes/api/queries';
import { formatarInstante } from '@/shared/lib/datas';
import type { Mensagem } from '@/features/conversas/types';
import type { SessaoConversaPaciente } from '@/features/pacientes/types';

function telefoneFmt(fone: string): string {
  const d = fone.replace(/\D/g, '').replace(/^55/, '');
  if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
  if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return fone;
}

/** Bolha somente-leitura — mesmo visual da Central (ThreadMensagens), sem composer/ações. */
function BolhaLeitura({ m }: { m: Mensagem }) {
  const saida = m.direcao === 'Saida';
  const nota = m.tipoMensagem === 'NotaInterna';
  const robo = m.tipoMensagem === 'Robo';
  return (
    <div className={`flex ${saida ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[80%] rounded-2xl px-3 py-2 text-sm shadow-sm ${
          nota
            ? 'bg-amber-50 text-amber-900'
            : robo
              ? 'bg-indigo-50 text-indigo-900 ring-1 ring-indigo-200'
              : saida
                ? 'bg-primary-600 text-white'
                : 'bg-white text-gray-800 ring-1 ring-gray-200'
        }`}
      >
        {saida && (
          <p className="mb-0.5 text-[11px] font-semibold opacity-80">
            {robo ? '🤖 ' : ''}
            {m.autorNomeExibicao ?? 'Sistema'}
          </p>
        )}
        {m.template && !m.conteudo && <p className="italic opacity-90">[modelo: {m.template}]</p>}
        {m.conteudo && <p className="whitespace-pre-wrap break-words">{m.conteudo}</p>}
        <p className={`mt-1 text-[10px] ${saida && !nota && !robo ? 'text-white/70' : 'text-gray-400'}`}>
          {formatarInstante(m.ocorridoEm)}
        </p>
      </div>
    </div>
  );
}

function MensagensDaSessao({ pacienteId, sessao }: { pacienteId: string; sessao: SessaoConversaPaciente }) {
  const q = useMensagensSessaoPaciente(pacienteId, sessao);
  if (q.isLoading) {
    return (
      <p className="flex items-center gap-2 p-3 text-sm text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" /> Carregando mensagens…
      </p>
    );
  }
  if ((q.data?.length ?? 0) === 0) {
    return <p className="p-3 text-sm text-gray-500">Nenhuma mensagem nesta sessão.</p>;
  }
  return (
    <div className="space-y-2 rounded-b-md bg-gray-50 p-3">
      {q.data!.map((m) => (
        <BolhaLeitura key={m.id} m={m} />
      ))}
    </div>
  );
}

/**
 * Aba "Conversas" da ficha do paciente: sessões de WhatsApp (blocos separados por 24h+ de
 * silêncio), mais recentes primeiro — inclui as mensagens automáticas do sistema e, quando o
 * telefone é compartilhado (celular de família), as sessões marcadas "pelo telefone".
 */
export function SecaoConversasWhatsApp({ pacienteId }: { pacienteId: string }) {
  const q = useSessoesConversaPaciente(pacienteId);
  const [aberta, setAberta] = useState<string | null>(null);

  const chave = (s: SessaoConversaPaciente) => `${s.telefone}:${s.inicio}`;

  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <MessageCircle className="h-4 w-4" /> Conversas de WhatsApp
        {q.data && (
          <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
            {q.data.length}
          </span>
        )}
      </div>

      {q.isLoading && (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando conversas…
        </p>
      )}
      {!q.isLoading && (q.data?.length ?? 0) === 0 && (
        <p className="text-sm text-gray-500">Nenhuma conversa de WhatsApp com este paciente.</p>
      )}

      <ul className="space-y-2">
        {q.data?.map((s) => {
          const expandida = aberta === chave(s);
          return (
            <li key={chave(s)} className="overflow-hidden rounded-md ring-1 ring-gray-200">
              <button
                type="button"
                onClick={() => setAberta(expandida ? null : chave(s))}
                className="flex w-full items-center gap-2 bg-white px-3 py-2.5 text-left hover:bg-gray-50"
                aria-expanded={expandida}
              >
                {expandida ? (
                  <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
                ) : (
                  <ChevronRight className="h-4 w-4 shrink-0 text-gray-400" />
                )}
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                    <span className="text-sm font-medium text-gray-900">
                      {formatarInstante(s.inicio)}
                    </span>
                    <span className="text-xs text-gray-500">{telefoneFmt(s.telefone)}</span>
                    <span className="text-xs text-gray-400">
                      {s.qtdMensagens} msg ({s.qtdRecebidas} recebidas · {s.qtdEnviadas} enviadas)
                    </span>
                  </div>
                  <div className="mt-0.5 flex flex-wrap items-center gap-1">
                    {s.operadores.map((op) => (
                      <span
                        key={op}
                        className="inline-flex items-center gap-1 rounded bg-sky-50 px-1.5 py-0.5 text-[10px] text-sky-700"
                      >
                        <Users className="h-3 w-3" /> {op}
                      </span>
                    ))}
                    {s.temAutomaticas && (
                      <span className="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-600">
                        <Bot className="h-3 w-3" /> Sistema
                      </span>
                    )}
                    {s.peloTelefone && (
                      <span
                        className="rounded bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700"
                        title="Sessão do telefone do cadastro, sem mensagem vinculada a este paciente — pode ser diálogo de outra pessoa da casa (celular de família)."
                      >
                        pelo telefone
                      </span>
                    )}
                  </div>
                </div>
              </button>
              {expandida && <MensagensDaSessao pacienteId={pacienteId} sessao={s} />}
            </li>
          );
        })}
      </ul>
    </>
  );
}
