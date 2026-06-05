import { useState } from 'react';
import { Loader2, Send, Sparkles } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { SeletorFontes } from '@/features/ia/components/SeletorFontes';
import { RespostaRenderer } from '@/features/ia/components/RespostaRenderer';
import {
  useFontes,
  usePerguntar,
  useReportarRespostaErrada,
} from '@/features/ia/api/queries';
import type { RespostaIa } from '@/features/ia/types';

export function IaPage() {
  const fontes = useFontes();
  const perguntar = usePerguntar();
  const reportar = useReportarRespostaErrada();

  const [selecionadas, setSelecionadas] = useState<string[]>([]);
  const [pergunta, setPergunta] = useState('');
  const [respostas, setRespostas] = useState<RespostaIa[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [reportadas, setReportadas] = useState<Record<string, boolean>>({});

  const podeEnviar =
    pergunta.trim().length > 0 && selecionadas.length > 0 && !perguntar.isPending;

  function aoEnviar(e: React.FormEvent) {
    e.preventDefault();
    if (!podeEnviar) return;
    setErro(null);
    perguntar.mutate(
      { pergunta: pergunta.trim(), fonteIds: selecionadas },
      {
        onSuccess: (data) => setRespostas(data.respostas),
        onError: (err) => {
          setRespostas(null);
          setErro(extrairMensagemDeErro(err));
        },
      },
    );
  }

  function aoReportarErro(resposta: RespostaIa) {
    reportar.mutate({ consultaId: resposta.consultaId });
    setReportadas((r) => ({ ...r, [resposta.consultaId]: true }));
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
            selecionadas={selecionadas}
            onChange={setSelecionadas}
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

      {respostas ? (
        respostas.length === 0 ? (
          <p className="text-sm text-gray-500">Nenhuma resposta retornada.</p>
        ) : (
          <div className="space-y-4">
            {respostas.map((r) => (
              <div key={r.consultaId} className="space-y-1">
                <RespostaRenderer resposta={r} onReportarErro={aoReportarErro} />
                {reportadas[r.consultaId] ? (
                  <p className="px-1 text-xs text-gray-500">
                    Obrigado pelo retorno — sinalizamos esta resposta para revisão.
                  </p>
                ) : null}
              </div>
            ))}
          </div>
        )
      ) : null}
    </div>
  );
}
