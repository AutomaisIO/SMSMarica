import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Check, ChevronDown, Loader2, Search, X } from 'lucide-react';

import { useSugestoesCidSer } from '@/features/ser/api/queries';
import type { CidSer, TipoRecursoSer } from '@/features/ser/types';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/**
 * A Hipótese diagnóstica — que no SER **não é texto livre**: é a caixa de CID.
 *
 * <p>Digitar e sair não preenche nada. O SER guarda a hipótese pelo CID que foi CLICADO na
 * sugestão e descarta o texto solto: em 10/08/2026 uma edição respondeu "salva com sucesso" e o
 * campo voltou vazio. Por isso aqui só se escolhe da lista.</p>
 *
 * <p><b>A lista é do RECURSO, não do CID-10.</b> Sem recurso escolhido o SER responde "Nenhum CID
 * encontrado" para qualquer termo; com ele, a relação muda — "Cirurgia Geral (Oncologia)" só
 * aceita neoplasia, cardiologia aceita quase tudo. É por isso que a busca vai ao SER a cada termo
 * em vez de filtrar uma tabela nossa: uma lista única ofereceria código que o SER recusa na hora
 * de gravar.</p>
 */
export function SeletorCidSer({
  tipo,
  recurso,
  ambulatorioEstadual,
  valor,
  onChange,
  desabilitado,
}: {
  tipo?: TipoRecursoSer;
  recurso?: string;
  ambulatorioEstadual?: boolean;
  /** O texto do jeito do SER: `(A09 ) Diarréia e gastroenterite...`. */
  valor: string;
  onChange: (texto: string) => void;
  desabilitado?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [termo, setTermo] = useState('');
  const [buscado, setBuscado] = useState('');
  const caixa = useRef<HTMLDivElement>(null);

  const semRecurso = !recurso || !tipo || ambulatorioEstadual === undefined;

  // Atraso antes de perguntar ao SER: cada busca abre uma conversa lá, na sessão única que a
  // varredura também usa. Sem isto, uma palavra digitada vira meia dúzia de idas ao Estado.
  useEffect(() => {
    const t = setTimeout(() => setBuscado(termo.trim()), 450);
    return () => clearTimeout(t);
  }, [termo]);

  useEffect(() => {
    function fora(e: MouseEvent) {
      if (caixa.current && !caixa.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', fora);
    return () => document.removeEventListener('mousedown', fora);
  }, []);

  const sugestoes = useSugestoesCidSer(tipo, recurso, ambulatorioEstadual, buscado);
  const itens = sugestoes.data?.itens ?? [];
  const escolhido = partes(valor);
  const curto = termo.trim().length < 2;

  function escolher(c: CidSer) {
    onChange(c.texto);
    setAberto(false);
    setTermo('');
  }

  return (
    <div ref={caixa} className="relative">
      <button
        type="button"
        disabled={desabilitado || semRecurso}
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 text-left text-sm disabled:bg-slate-100 disabled:text-slate-400"
      >
        {escolhido ? (
          <span className="flex min-w-0 items-center gap-2">
            <span className="shrink-0 rounded bg-slate-100 px-1.5 py-0.5 font-mono text-xs text-slate-700">
              {escolhido.codigo}
            </span>
            <span className="truncate text-slate-900">{escolhido.descricao}</span>
          </span>
        ) : valor ? (
          /* Rascunho antigo, de quando o campo era texto livre: o SER não vai aceitar isso — o
             operador precisa escolher da lista antes de o pedido sair. */
          <span className="flex min-w-0 items-center gap-2">
            <AlertTriangle className="size-4 shrink-0 text-amber-600" />
            <span className="truncate text-amber-800">{valor}</span>
          </span>
        ) : (
          <span className="text-slate-400">
            {semRecurso ? 'Escolha o recurso primeiro...' : 'Procure o CID...'}
          </span>
        )}

        <span className="flex shrink-0 items-center gap-1">
          {valor && !desabilitado && (
            <X
              className="size-4 text-slate-400 hover:text-slate-700"
              onClick={(e) => {
                e.stopPropagation();
                onChange('');
              }}
            />
          )}
          <ChevronDown className="size-4 text-slate-400" />
        </span>
      </button>

      {valor && !escolhido && (
        <p className="mt-1 text-xs text-amber-700">
          Este texto não veio da lista do SER. Lá a hipótese é guardada pelo CID escolhido, e texto
          digitado é descartado — procure o CID de novo.
        </p>
      )}

      {aberto && !desabilitado && !semRecurso && (
        <div className="absolute z-20 mt-1 w-full rounded-md border border-slate-300 bg-white shadow-lg">
          <div className="flex items-center gap-2 border-b border-slate-200 px-3 py-2">
            <Search className="size-4 shrink-0 text-slate-400" />
            <input
              autoFocus
              value={termo}
              onChange={(e) => setTermo(e.target.value)}
              placeholder="código ou nome do CID..."
              className="w-full text-sm outline-none"
            />
            {sugestoes.isFetching && (
              <Loader2 className="size-4 shrink-0 animate-spin text-slate-400" />
            )}
          </div>

          <ul className="max-h-72 overflow-y-auto py-1">
            {curto && (
              <li className="px-3 py-4 text-center text-sm text-slate-500">
                Digite ao menos 2 caracteres. A busca é a do próprio SER: vale o código
                (<span className="font-mono">I10</span>) ou parte do nome.
              </li>
            )}

            {!curto && sugestoes.isError && (
              <li className="px-3 py-4 text-center text-sm text-red-700">
                {extrairMensagemDeErro(sugestoes.error)}
              </li>
            )}

            {!curto && !sugestoes.isError && (
              <>
                {sugestoes.isFetching && itens.length === 0 && (
                  <li className="px-3 py-4 text-center text-sm text-slate-500">
                    Perguntando ao SER...
                  </li>
                )}

                {!sugestoes.isFetching && itens.length === 0 && (
                  <li className="px-3 py-4 text-center text-sm text-slate-500">
                    O SER não tem CID com &ldquo;{buscado}&rdquo; para este recurso.
                  </li>
                )}

                {itens.map((c) => (
                  <li key={c.codigo}>
                    <button
                      type="button"
                      onClick={() => escolher(c)}
                      className="flex w-full items-start gap-2 px-3 py-1.5 text-left text-sm hover:bg-slate-50"
                    >
                      {escolhido?.codigo === c.codigo ? (
                        <Check className="mt-0.5 size-4 shrink-0 text-red-700" />
                      ) : (
                        <span className="w-4 shrink-0" />
                      )}
                      <span className="mt-0.5 w-14 shrink-0 font-mono text-xs text-slate-600">
                        {c.codigo}
                      </span>
                      <span className="min-w-0">{c.descricao}</span>
                    </button>
                  </li>
                ))}

                {sugestoes.data?.truncado && (
                  <li className="border-t border-slate-100 px-3 py-2 text-xs text-amber-700">
                    O SER cortou a lista em {itens.length}. Refine o termo — há mais CID que casam.
                  </li>
                )}
              </>
            )}
          </ul>
        </div>
      )}
    </div>
  );
}

/**
 * Separa `(A09 ) Diarréia...` em código e descrição. Nulo quando o texto não tem essa forma — é
 * como se reconhece o que veio da lista do SER e o que foi digitado à mão.
 */
function partes(texto: string): { codigo: string; descricao: string } | null {
  const m = /^\(([A-Za-z]\d{2,3})\s*\)\s*(.*)$/.exec(texto.trim());
  return m ? { codigo: m[1].toUpperCase(), descricao: m[2].trim() } : null;
}
