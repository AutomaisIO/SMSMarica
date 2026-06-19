import { useEffect, useState } from 'react';
import { ChevronDown, KeyRound, Loader2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useLimparCredencial, useSalvarCredencial } from '@/features/integracoes/api';
import type { IntegracaoCredencial } from '@/features/integracoes/types';

/** Dica do campo "Parâmetros" por provedor (extras não-sensíveis em JSON). */
const DICA_PARAMS: Record<string, string> = {
  microsoft: 'Ex.: {"tenant_id":"common"}',
  facebook: 'Ex.: {"client_token":"..."} (opcional)',
  google: 'Ex.: {"hosted_domain":"..."} (opcional)',
};

export function CredencialOAuthCard({ cred }: { cred: IntegracaoCredencial }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const podeExcluir = usePermissao('IntegracoesConfig', 'Exclusao');
  const salvar = useSalvarCredencial();
  const limpar = useLimparCredencial();

  const [aberto, setAberto] = useState(false);
  const [clientId, setClientId] = useState('');
  const [clientSecret, setClientSecret] = useState('');
  const [redirectUri, setRedirectUri] = useState(cred.redirectUri ?? '');
  const [parametros, setParametros] = useState(cred.parametrosJson ?? '');
  const [ativo, setAtivo] = useState(cred.ativo);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  // Sincroniza os campos públicos quando a lista é recarregada (ex.: após salvar).
  useEffect(() => {
    setRedirectUri(cred.redirectUri ?? '');
    setParametros(cred.parametrosJson ?? '');
    setAtivo(cred.ativo);
  }, [cred.redirectUri, cred.parametrosJson, cred.ativo]);

  const configurado = cred.clientIdDefinido && cred.clientSecretDefinido;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      {
        provedor: cred.provedor,
        payload: {
          clientId: clientId || undefined,
          clientSecret: clientSecret || undefined,
          redirectUri: redirectUri.trim() || null,
          parametrosJson: parametros.trim() || null,
          ativo,
        },
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setClientId('');
          setClientSecret('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoLimpar() {
    if (!window.confirm(`Limpar as credenciais de ${cred.rotulo}? O login por este provedor para de funcionar.`)) {
      return;
    }
    setErro(null);
    limpar.mutate(cred.provedor, { onError: (err) => setErro(extrairMensagemDeErro(err)) });
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <KeyRound className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">{cred.rotulo}</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              configurado && cred.ativo
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {configurado ? (cred.ativo ? 'Configurado' : 'Inativo') : 'Não configurado'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-4 border-t border-gray-100 pt-4">
          <Campo
            label="Client ID / App ID"
            htmlFor={`${cred.provedor}-cid`}
            dica={cred.clientIdDefinido ? 'Já definido — preencha apenas para substituir.' : 'Ainda não definido.'}
          >
            <Input
              id={`${cred.provedor}-cid`}
              type="password"
              value={clientId}
              onChange={(e) => setClientId(e.target.value)}
              placeholder={cred.clientIdDefinido ? '••••••••••••' : 'Cole o Client ID aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Client Secret / App Secret"
            htmlFor={`${cred.provedor}-secret`}
            dica={cred.clientSecretDefinido ? 'Já definido — preencha apenas para substituir.' : 'Ainda não definido.'}
          >
            <Input
              id={`${cred.provedor}-secret`}
              type="password"
              value={clientSecret}
              onChange={(e) => setClientSecret(e.target.value)}
              placeholder={cred.clientSecretDefinido ? '••••••••••••' : 'Cole o Client Secret aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo label="Redirect URI" htmlFor={`${cred.provedor}-redirect`} dica="URL de retorno do OAuth (pública).">
            <Input
              id={`${cred.provedor}-redirect`}
              value={redirectUri}
              onChange={(e) => setRedirectUri(e.target.value)}
              placeholder="https://app.smsmarica.online/auth/callback"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Parâmetros adicionais (JSON)"
            htmlFor={`${cred.provedor}-params`}
            dica={DICA_PARAMS[cred.provedor] ?? 'Opcional.'}
          >
            <Input
              id={`${cred.provedor}-params`}
              value={parametros}
              onChange={(e) => setParametros(e.target.value)}
              placeholder={DICA_PARAMS[cred.provedor] ?? '{}'}
              disabled={!podeEditar}
            />
          </Campo>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Provedor ativo
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              {podeExcluir && configurado ? (
                <Button type="button" variante="outline" onClick={aoLimpar} disabled={limpar.isPending}>
                  <Trash2 className="mr-2 h-4 w-4" />
                  Limpar
                </Button>
              ) : null}
              <Button type="submit" disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
            </div>
          ) : (
            <p className="text-xs text-gray-500">Você não tem permissão para editar credenciais.</p>
          )}
        </form>
      ) : null}
    </div>
  );
}
