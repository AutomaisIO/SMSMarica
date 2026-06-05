import { useEffect, useState } from 'react';
import { Loader2, Send, Sparkles, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { SeletorFontes } from '@/features/ia/components/SeletorFontes';
import { RespostaRenderer } from '@/features/ia/components/RespostaRenderer';
import { useFontes, usePerguntar, useReportarRespostaErrada } from '@/features/ia/api/queries';
import { useHistoricoIa } from '@/features/ia/store/historicoIa';
import type { RespostaIa } from '@/features/ia/types';

function novoId(): string {
  try {
    return crypto.randomUUID();
  } catch {
    return `${Date.now()}-${Math.round(Math.random() * 1e9)}`;
  }
}

function formatarQuando(ms: number): string {
  const d = new Date(ms);
  return d.toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

export function IaPage() {
  const fontes = useFontes();
  const perguntar = usePerguntar();
  const reportar = useReportarRespostaErrada();

  const {
    interacoes,
    fontesSelecionadas,
    reportadas,
    adicionar,
    limpar,
    marcarReportada,
    setFontesSelecionadas,
  } = useHistoricoIa();

  const [pergunta, setPergunta] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  // Remove da seleção persistida bases que não existem mais (ex.: removidas).
  useEffect(() => {
    if (!fontes.data) return;
    const validas = fontesSelecionadas.filter((id) => fontes.data.some((f) => f.id === id));
    if (validas.length !== fontesSelecionadas.length) setFontesSelecionadas(validas);
  }, [fontes.data, fontesSelecionadas, setFontesSelecionadas]);

  const podeEnviar =
    pergunta.trim().length > 0 && fontesSelecionadas.length > 0 && !perguntar.isPending;

  function aoEnviar(e: React.FormEvent) {
    e.preventDefault();
    if (!podeEnviar) return;
    setErro(null);
    const texto = pergunta.trim();
    perguntar.mutate(
      { pergunta: texto, fonteIds: fontesSelecionadas },
      {
        onSuccess: (data) => {
          adicionar({
            id: novoId(),
            pergunta: texto,
            fonteIds: fontesSelecionadas,
            criadoEm: Date.now(),
            respostas: data.respostas,
          });
          setPergunta('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoReportarErro(resposta: RespostaIa) {
    reportar.mutate({ consultaId: resposta.consultaId });
    marcarReportada(resposta.consultaId);
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Sparkles className="h-6 w-6 text-primary-600" />
          IA
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Pergunte em linguagem natural sobre os dados das bases conectadas. Cada base responde
          separadamente.
        </p>
      </header>

      <form onSubmit={aoEnviar} className="space-y-4 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <Campo label="Bases consultadas" htmlFor="ia-fontes" required>
          <SeletorFontes
            fontes={fontes.data ?? []}
            selecionadas={fontesSelecionadas}
            onChange={setFontesSelecionadas}
            carregando={fontes.isPending}
          />
        </Campo>

        <Campo label="Pergunta" htmlFor="ia-pergunta" required>
          <textarea
            id="ia-pergunta"
            value={pergunta}
            onChange={(e) => setPergunta(e.target.value)}
            placeholder="Ex.: Quantos atendimentos foram feitos no último mês?"
            rows={3}
            className="input resize-y"
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) aoEnviar(e);
            }}
          />
        </Campo>

        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-gray-400">Ctrl+Enter para enviar.</span>
          <Button type="submit" disabled={!podeEnviar}>
            {perguntar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            Perguntar
          </Button>
        </div>
      </form>

      {fontes.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar as bases: {extrairMensagemDeErro(fontes.error)}
        </div>
      ) : null}

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {interacoes.length > 0 ? (
        <div className="space-y-5">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">Perguntas recentes</h2>
            <button
              type="button"
              onClick={limpar}
              className="inline-flex items-center gap-1 text-xs text-gray-500 hover:text-red-700"
            >
              <Trash2 className="h-3.5 w-3.5" /> Limpar histórico
            </button>
          </div>

          {interacoes.map((it) => (
            <section key={it.id} className="space-y-2">
              <div className="flex flex-wrap items-baseline justify-between gap-2 border-l-2 border-primary-200 pl-3">
                <p className="text-sm font-medium text-gray-900">{it.pergunta}</p>
                <span className="text-xs text-gray-400">{formatarQuando(it.criadoEm)}</span>
              </div>
              <div className="space-y-3">
                {it.respostas.map((r) => (
                  <div key={`${it.id}-${r.consultaId}`} className="space-y-1">
                    <RespostaRenderer resposta={r} onReportarErro={aoReportarErro} />
                    {reportadas[r.consultaId] ? (
                      <p className="px-1 text-xs text-gray-500">
                        Obrigado pelo retorno — sinalizamos esta resposta para revisão.
                      </p>
                    ) : null}
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
      ) : null}
    </div>
  );
}
