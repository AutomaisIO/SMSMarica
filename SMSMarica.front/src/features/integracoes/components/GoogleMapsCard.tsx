import { useEffect, useState } from 'react';
import { ChevronDown, Loader2, MapPin } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useSalvarTfdGoogle, useTfdGoogle } from '@/features/integracoes/api';

export function GoogleMapsCard() {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const config = useTfdGoogle();
  const salvar = useSalvarTfdGoogle();

  const [aberto, setAberto] = useState(false);
  const [baseUrl, setBaseUrl] = useState('https://maps.googleapis.com/');
  const [apiKey, setApiKey] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (config.data) {
      setBaseUrl(config.data.baseUrl);
      setAtivo(config.data.ativo);
    }
  }, [config.data]);

  const configurada = config.data?.chaveConfigurada ?? false;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      { baseUrl: baseUrl.trim(), apiKey: apiKey || undefined, ativo },
      {
        onSuccess: () => {
          setSalvo(true);
          setApiKey('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <MapPin className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">Google Maps (rotas/geocodificação)</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              configurada && (config.data?.ativo ?? false)
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {configurada ? (config.data?.ativo ? 'Configurado' : 'Inativo') : 'Não configurado'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-4 border-t border-gray-100 pt-4">
          <Campo label="URL base" htmlFor="gm-url">
            <Input id="gm-url" value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} disabled={!podeEditar} />
          </Campo>

          <Campo
            label="Chave da API (API Key)"
            htmlFor="gm-key"
            dica={configurada ? 'Já configurada — preencha apenas para substituir.' : 'Ainda não configurada.'}
          >
            <Input
              id="gm-key"
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={configurada ? '••••••••••••' : 'Cole a chave do Google aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Integração ativa
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              <Button type="submit" disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
            </div>
          ) : (
            <p className="text-xs text-gray-500">Você não tem permissão para editar.</p>
          )}
        </form>
      ) : null}
    </div>
  );
}
