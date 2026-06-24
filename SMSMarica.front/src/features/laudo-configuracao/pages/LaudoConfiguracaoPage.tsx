import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { EditorHtml } from '@/shared/ui/EditorHtml';
import { cn } from '@/shared/lib/cn';
import { useLaudoConfiguracao, useSalvarLaudoConfiguracao } from '../queries';

type Aba = 'cabecalho' | 'rodape';

export function LaudoConfiguracaoPage() {
  const { data, isLoading } = useLaudoConfiguracao();
  const salvar = useSalvarLaudoConfiguracao();
  const podeEditar = usePermissao('ConfiguracaoLaudo', 'Edicao');

  const [aba, setAba] = useState<Aba>('cabecalho');
  const [cabecalhoHtml, setCabecalhoHtml] = useState('');
  const [cabecalhoJson, setCabecalhoJson] = useState('{}');
  const [rodapeHtml, setRodapeHtml] = useState('');
  const [rodapeJson, setRodapeJson] = useState('{}');
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (data) {
      setCabecalhoHtml(data.cabecalhoHtml);
      setCabecalhoJson(data.cabecalhoJson);
      setRodapeHtml(data.rodapeHtml);
      setRodapeJson(data.rodapeJson);
    }
  }, [data]);

  async function aoSalvar() {
    setErro(null);
    setSalvo(false);
    try {
      await salvar.mutateAsync({ cabecalhoHtml, cabecalhoJson, rodapeHtml, rodapeJson });
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
        <EditorHtml
          valorHtml={cabecalhoHtml}
          aoMudar={(v) => {
            setCabecalhoHtml(v.html);
            setCabecalhoJson(v.json);
          }}
          somenteLeitura={!podeEditar}
          permitirImagem={podeEditar}
          categoriaImagem="laudo-cabecalho"
          placeholder="Monte o cabeçalho em HTML: logo, nome da instituição, endereço…"
          alturaMinima="320px"
        />
        <p className="mt-2 text-xs text-gray-500">
          Edite o HTML na aba <strong>HTML</strong> e veja o resultado fiel na aba <strong>Visual</strong>.
          Use <strong>Imagem</strong> para enviar o logotimbre.
        </p>
      </div>

      <div className={aba === 'rodape' ? 'block' : 'hidden'}>
        <EditorHtml
          valorHtml={rodapeHtml}
          aoMudar={(v) => {
            setRodapeHtml(v.html);
            setRodapeJson(v.json);
          }}
          somenteLeitura={!podeEditar}
          permitirImagem={podeEditar}
          categoriaImagem="laudo-rodape"
          placeholder="Monte o rodapé em HTML: endereço, contato, observações institucionais…"
          alturaMinima="240px"
        />
        <p className="mt-2 text-xs text-gray-500">
          O rodapé entra abaixo do bloco de assinatura do médico, em todas as páginas.
        </p>
      </div>
    </div>
  );
}
