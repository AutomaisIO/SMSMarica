import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { ConfiguracaoImagem } from '@/features/laudo-configuracao/components/ConfiguracaoImagem';
import { cn } from '@/shared/lib/cn';
import { useLaudoConfiguracao, useSalvarLaudoConfiguracao } from '../queries';

type Aba = 'cabecalho' | 'rodape' | 'regras';

export function LaudoConfiguracaoPage() {
  const { data, isLoading } = useLaudoConfiguracao();
  const salvar = useSalvarLaudoConfiguracao();
  const podeEditar = usePermissao('ConfiguracaoLaudo', 'Edicao');

  const [aba, setAba] = useState<Aba>('cabecalho');
  const [cabecalhoHtml, setCabecalhoHtml] = useState('');
  const [cabecalhoJson, setCabecalhoJson] = useState('{}');
  const [rodapeHtml, setRodapeHtml] = useState('');
  const [rodapeJson, setRodapeJson] = useState('{}');
  const [permitirSemAssociacao, setPermitirSemAssociacao] = useState(false);
  const [permitirSemAnamnese, setPermitirSemAnamnese] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (data) {
      setCabecalhoHtml(data.cabecalhoHtml);
      setCabecalhoJson(data.cabecalhoJson);
      setRodapeHtml(data.rodapeHtml);
      setRodapeJson(data.rodapeJson);
      setPermitirSemAssociacao(data.permitirLaudarSemAssociacao);
      setPermitirSemAnamnese(data.permitirLaudarSemAnamnese);
    }
  }, [data]);

  async function aoSalvar() {
    setErro(null);
    setSalvo(false);
    try {
      await salvar.mutateAsync({
        cabecalhoHtml,
        cabecalhoJson,
        rodapeHtml,
        rodapeJson,
        permitirLaudarSemAssociacao: permitirSemAssociacao,
        permitirLaudarSemAnamnese: permitirSemAnamnese,
      });
      setSalvo(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" /> Carregando configuração…
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Configuração de laudo</h1>
          <p className="mt-1 text-sm text-gray-600">
            Cabeçalho e rodapé institucionais aplicados a <strong>todos</strong> os laudos em PDF.
          </p>
        </div>
        {podeEditar ? (
          <Button onClick={aoSalvar} disabled={salvar.isPending}>
            {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Salvar
          </Button>
        ) : null}
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {salvo ? (
        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
          Configuração salva. Os próximos PDFs já usam o novo cabeçalho/rodapé.
        </div>
      ) : null}

      <div className="flex gap-1 border-b border-gray-200">
        {(
          [
            ['cabecalho', 'Cabeçalho'],
            ['rodape', 'Rodapé'],
            ['regras', 'Regras para laudar'],
          ] as [Aba, string][]
        ).map(([id, rotulo]) => (
          <button
            key={id}
            type="button"
            onClick={() => setAba(id)}
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-sm font-medium',
              aba === id
                ? 'border-primary-600 text-primary-700'
                : 'border-transparent text-gray-500 hover:text-gray-800',
            )}
          >
            {rotulo}
          </button>
        ))}
      </div>

      {/* Mantém ambos montados (display none) para não perder estado ao trocar de aba. */}
      <div className={aba === 'cabecalho' ? 'block' : 'hidden'}>
        <ConfiguracaoImagem
          valorHtml={cabecalhoHtml}
          aoMudar={(v) => {
            setCabecalhoHtml(v.html);
            setCabecalhoJson(v.json);
          }}
          categoria="laudo-cabecalho"
          rotulo="cabeçalho"
          somenteLeitura={!podeEditar}
        />
      </div>

      <div className={aba === 'rodape' ? 'block' : 'hidden'}>
        <ConfiguracaoImagem
          valorHtml={rodapeHtml}
          aoMudar={(v) => {
            setRodapeHtml(v.html);
            setRodapeJson(v.json);
          }}
          categoria="laudo-rodape"
          rotulo="rodapé"
          somenteLeitura={!podeEditar}
        />
        <p className="mt-2 text-xs text-gray-500">
          O rodapé entra abaixo do bloco de assinatura do médico, em todas as páginas.
        </p>
      </div>

      <div className={aba === 'regras' ? 'block' : 'hidden'}>
        <div className="space-y-3 rounded-lg border border-gray-200 bg-white p-4">
          <p className="text-sm text-gray-600">
            Controla o que é exigido para <strong>iniciar</strong> um laudo. Por segurança, o padrão
            exige associação e anamnese.
          </p>

          <label className="flex items-start gap-3">
            <input
              type="checkbox"
              className="mt-1 h-4 w-4 rounded border-gray-300"
              checked={permitirSemAssociacao}
              disabled={!podeEditar}
              onChange={(e) => setPermitirSemAssociacao(e.target.checked)}
            />
            <span className="text-sm">
              <span className="font-medium text-gray-900">Permitir iniciar laudo sem associação</span>
              <span className="block text-gray-500">
                Por padrão (desligado), o exame precisa estar associado a um pedido para laudar.
                <strong> Assinar</strong> sempre exige associação — isso não muda.
              </span>
            </span>
          </label>

          <label className="flex items-start gap-3">
            <input
              type="checkbox"
              className="mt-1 h-4 w-4 rounded border-gray-300"
              checked={permitirSemAnamnese}
              disabled={!podeEditar}
              onChange={(e) => setPermitirSemAnamnese(e.target.checked)}
            />
            <span className="text-sm">
              <span className="font-medium text-gray-900">Permitir iniciar laudo sem anamnese</span>
              <span className="block text-gray-500">
                Por padrão (desligado), a anamnese da solicitação precisa estar preenchida para laudar.
              </span>
            </span>
          </label>
        </div>
      </div>
    </div>
  );
}
