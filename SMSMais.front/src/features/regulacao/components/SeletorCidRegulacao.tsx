import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Check, ChevronDown, Loader2, Search, X } from 'lucide-react';

import { Campo } from '@/shared/ui/Campo';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

import { useCidsRegulacao, type CidRegulacao } from '../api/cidsQueries';
import { ROTULO_SISTEMA_REGULACAO, type SistemaRegulacao } from '../types';

/**
 * A Hipótese do fluxo Externo — que no SER/SERNIT **não é texto livre**: é a caixa de CID.
 *
 * <p>O destino guarda a hipótese pelo CID CLICADO na sugestão e descarta texto solto (em
 * 10/08/2026 uma edição respondeu "salva com sucesso" e o campo voltou vazio). Por isso aqui só
 * se escolhe da lista.</p>
 *
 * <p><b>A lista é do RECURSO</b>, não do CID-10: o oncológico aceita 136 códigos, a cardiologia
 * os 14.226. A pergunta vai com o procedimento e o sistema de destino; o servidor chega ao
 * recurso, busca no espelho e, quando o recurso ainda não foi copiado, consulta o SER/SERNIT ao
 * vivo (docs/ser-criar-solicitacao.md §2.1.3). Mesmo desenho de `SeletorCidSer`.</p>
 */
export function SeletorCidRegulacao({
  rotulo,
  procedimentoId,
  sistema,
  valor,
  onChange,
  desabilitado,
}: {
  rotulo: string;
  procedimentoId: string | null;
  /** O destino escolhido. ESUS SG ou vazio: o servidor usa o SER, e na falta dele o SERNIT. */
  sistema: SistemaRegulacao | null;
  valor: string;
  onChange: (texto: string) => void;
  desabilitado?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [termo, setTermo] = useState('');
  const [buscado, setBuscado] = useState('');
  const caixa = useRef<HTMLDivElement>(null);

  // Atraso antes de buscar: recurso ainda não copiado cai no autocomplete ao vivo, e lá cada
  // busca abre uma conversa no SER, na sessão única que a varredura também usa.
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

  const sugestoes = useCidsRegulacao(aberto ? procedimentoId : null, sistema, buscado);
  const itens = sugestoes.data?.itens ?? [];
  const nomeSistema = sugestoes.data ? ROTULO_SISTEMA_REGULACAO[sugestoes.data.sistema] : 'sistema';
  const escolhido = partes(valor);

  function escolher(c: CidRegulacao) {
    onChange(c.texto);
    setAberto(false);
    setTermo('');
  }

  return (
    <Campo label={rotulo} htmlFor="hipotese-cid" className="min-w-96 flex-1">
      <div ref={caixa} className="relative">
        <button
          id="hipotese-cid"
          type="button"
          disabled={desabilitado || !procedimentoId}
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
            <span className="flex min-w-0 items-center gap-2">
              <AlertTriangle className="size-4 shrink-0 text-amber-600" />
              <span className="truncate text-amber-800">{valor}</span>
            </span>
          ) : (
            <span className="text-slate-400">Procure o CID...</span>
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
            Este texto não veio da lista de CID. O destino guarda a hipótese pelo CID escolhido e
            descarta texto digitado — procure o CID de novo.
          </p>
        )}

        {aberto && !desabilitado && procedimentoId && (
          <div className="absolute z-20 mt-1 w-full rounded-md border border-slate-300 bg-white shadow-lg">
            <div className="flex items-center gap-2 border-b border-slate-200 px-3 py-2">
              <Search className="size-4 shrink-0 text-slate-400" />
              <input
                autoFocus
                value={termo}
                onChange={(e) => setTermo(e.target.value)}
                placeholder="código ou nome do CID — vazio lista todos"
                className="w-full text-sm outline-none"
              />
              {sugestoes.isFetching && <Loader2 className="size-4 shrink-0 animate-spin text-slate-400" />}
            </div>

            <ul className="max-h-72 overflow-y-auto py-1">
              {sugestoes.isError && (
                <li className="px-3 py-4 text-center text-sm">
                  <p className="text-red-700">Não consegui buscar os CID agora.</p>
                  <p className="mt-1 text-xs text-slate-500">{extrairMensagemDeErro(sugestoes.error)}</p>
                </li>
              )}

              {!sugestoes.isError && (
                <>
                  {sugestoes.isFetching && itens.length === 0 && (
                    <li className="px-3 py-4 text-center text-sm text-slate-500">Procurando...</li>
                  )}

                  {!sugestoes.isFetching && sugestoes.data && itens.length === 0 && (
                    <li className="px-3 py-4 text-center text-sm text-slate-500">
                      {buscado
                        ? `Não há CID com “${buscado}” entre os que o ${nomeSistema} aceita para este procedimento.`
                        : `O ${nomeSistema} não devolveu CID nenhum para este procedimento.`}
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
                        <span className="mt-0.5 w-14 shrink-0 font-mono text-xs text-slate-600">{c.codigo}</span>
                        <span className="min-w-0">{c.descricao}</span>
                      </button>
                    </li>
                  ))}

                  {sugestoes.data?.truncado && (
                    <li className="border-t border-slate-100 px-3 py-2 text-xs text-amber-700">
                      Lista cortada em {itens.length} — o mesmo teto do {nomeSistema}. Refine o
                      termo: há mais CID que casam.
                    </li>
                  )}
                </>
              )}
            </ul>
          </div>
        )}
      </div>
    </Campo>
  );
}

/**
 * Separa `(A09 ) Diarréia...` em código e descrição. Nulo quando o texto não tem essa forma — é
 * como se reconhece o que veio da lista e o que foi digitado à mão.
 */
function partes(texto: string): { codigo: string; descricao: string } | null {
  const m = /^\(([A-Za-z]\d{2,3})\s*\)\s*(.*)$/.exec(texto.trim());
  return m ? { codigo: m[1].toUpperCase(), descricao: m[2].trim() } : null;
}
