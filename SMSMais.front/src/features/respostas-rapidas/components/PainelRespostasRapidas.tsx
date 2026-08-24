import { useMemo, useState } from 'react';
import { ArrowLeft, Loader2, Search, Zap } from 'lucide-react';
import { useRespostasRapidas } from '@/features/respostas-rapidas/api/queries';
import { resolverRespostaRapida } from '@/features/respostas-rapidas/api/respostasRapidasApi';
import { useChat } from '@/features/conversas/store/chatStore';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import type { RespostaRapida } from '@/features/respostas-rapidas/types';

/** Input do mini-form conforme o tipo declarado no cadastro. */
function CampoValor({
  campo,
  valor,
  onChange,
}: {
  campo: RespostaRapida['campos'][number];
  valor: string;
  onChange: (v: string) => void;
}) {
  const tipoInput = campo.tipo === 'Data' ? 'date' : campo.tipo === 'Numero' ? 'number' : 'text';
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-gray-600">
        {campo.rotulo || campo.nome}
      </label>
      <input
        type={tipoInput}
        value={valor}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
      />
    </div>
  );
}

/**
 * Atalhos de mensagens prontas, ao lado da conversa. Clicar joga o texto já resolvido no
 * campo de digitação — o envio continua sendo do operador (é ele quem lê e aperta enviar).
 * Quando a mensagem tem variáveis manuais, um mini-form aparece antes; as automáticas
 * ({{primeironome}}, {{saudacao}}…) o servidor resolve com o paciente da conversa.
 */
export function PainelRespostasRapidas({ conversaId }: { conversaId: string }) {
  const { data: respostas, isLoading } = useRespostasRapidas();
  const [busca, setBusca] = useState('');
  const [escolhida, setEscolhida] = useState<RespostaRapida | null>(null);
  const [valores, setValores] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [resolvendo, setResolvendo] = useState(false);

  const filtradas = useMemo(() => {
    const t = busca.trim().toLowerCase();
    if (!t) return respostas ?? [];
    return (respostas ?? []).filter(
      (r) =>
        r.titulo.toLowerCase().includes(t) ||
        r.corpo.toLowerCase().includes(t) ||
        (r.categoria ?? '').toLowerCase().includes(t),
    );
  }, [respostas, busca]);

  async function usar(r: RespostaRapida, valoresCampos: Record<string, string>) {
    setErro(null);
    setResolvendo(true);
    try {
      const { texto } = await resolverRespostaRapida(conversaId, r.id, valoresCampos);
      useChat.getState().inserirRascunho(conversaId, texto);
      setEscolhida(null);
      setValores({});
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setResolvendo(false);
    }
  }

  function aoClicar(r: RespostaRapida) {
    if (r.campos.length === 0) {
      void usar(r, {});
      return;
    }
    setEscolhida(r);
    setValores(Object.fromEntries(r.campos.map((c) => [c.nome, ''])));
  }

  if (escolhida) {
    const faltando = escolhida.campos.some((c) => !valores[c.nome]?.trim());
    return (
      <div className="flex h-full flex-col">
        <div className="flex items-center gap-2 border-b border-gray-200 px-3 py-2">
          <button
            type="button"
            onClick={() => setEscolhida(null)}
            className="rounded p-1 text-gray-400 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <span className="truncate text-sm font-semibold text-gray-900">{escolhida.titulo}</span>
        </div>

        <div className="flex-1 space-y-3 overflow-y-auto p-3">
          {escolhida.campos
            .slice()
            .sort((a, b) => a.ordem - b.ordem)
            .map((c) => (
              <CampoValor
                key={c.nome}
                campo={c}
                valor={valores[c.nome] ?? ''}
                onChange={(v) => setValores((atual) => ({ ...atual, [c.nome]: v }))}
              />
            ))}

          <p className="whitespace-pre-wrap rounded-md bg-gray-50 p-2 text-xs text-gray-500">
            {escolhida.corpo}
          </p>
          {erro ? <p className="text-xs text-red-600">{erro}</p> : null}
        </div>

        <div className="border-t border-gray-200 p-3">
          <button
            type="button"
            onClick={() => void usar(escolhida, valores)}
            disabled={resolvendo || faltando}
            className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {resolvendo ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Inserir na mensagem
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      <div className="border-b border-gray-200 px-3 py-2">
        <p className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-gray-900">
          <Zap className="h-4 w-4 text-amber-500" /> Mensagens prontas
        </p>
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-2 h-3.5 w-3.5 text-gray-400" />
          <input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar…"
            className="w-full rounded-md border border-gray-300 py-1.5 pl-8 pr-2 text-xs outline-none focus:border-primary-400"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-2">
        {isLoading ? <p className="p-2 text-xs text-gray-500">Carregando…</p> : null}

        {!isLoading && filtradas.length === 0 ? (
          <p className="p-2 text-xs text-gray-500">
            {busca.trim()
              ? 'Nenhuma mensagem encontrada.'
              : 'Nenhuma mensagem pronta cadastrada ainda.'}
          </p>
        ) : null}

        {filtradas.map((r) => (
          <button
            key={r.id}
            type="button"
            onClick={() => aoClicar(r)}
            disabled={resolvendo}
            className="mb-1 w-full rounded-md px-2 py-1.5 text-left hover:bg-gray-50 disabled:opacity-50"
          >
            <span className="flex items-center gap-1">
              <span className="truncate text-xs font-medium text-gray-900">{r.titulo}</span>
              {r.campos.length > 0 ? (
                <span className="shrink-0 rounded bg-gray-100 px-1 text-[10px] text-gray-500">
                  {r.campos.length} {r.campos.length === 1 ? 'campo' : 'campos'}
                </span>
              ) : null}
            </span>
            <span className="line-clamp-2 text-[11px] text-gray-500">{r.corpo}</span>
          </button>
        ))}

        {erro ? <p className="p-2 text-xs text-red-600">{erro}</p> : null}
      </div>
    </div>
  );
}
