import { useEffect, useState } from 'react';
import { ChevronDown, Loader2, MessageCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useSalvarTfdWhatsApp, useTfdWhatsApp } from '@/features/integracoes/api';

export function WhatsAppCard() {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const config = useTfdWhatsApp();
  const salvar = useSalvarTfdWhatsApp();

  const [aberto, setAberto] = useState(false);
  const [baseUrl, setBaseUrl] = useState('https://graph.facebook.com/v21.0/');
  const [phoneNumberId, setPhoneNumberId] = useState('');
  const [wabaId, setWabaId] = useState('');
  const [token, setToken] = useState('');
  const [verifyToken, setVerifyToken] = useState('');
  const [appSecret, setAppSecret] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (config.data) {
      setBaseUrl(config.data.baseUrl);
      setPhoneNumberId(config.data.phoneNumberId ?? '');
      setWabaId(config.data.wabaId ?? '');
      setAtivo(config.data.ativo);
    }
  }, [config.data]);

  const configurado = config.data?.tokenConfigurado ?? false;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      {
        baseUrl: baseUrl.trim(),
        phoneNumberId: phoneNumberId.trim() || null,
        wabaId: wabaId.trim() || null,
        token: token || undefined,
        verifyToken: verifyToken || undefined,
        appSecret: appSecret || undefined,
        ativo,
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setToken('');
          setVerifyToken('');
          setAppSecret('');
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
          <MessageCircle className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">WhatsApp / Meta Cloud API</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              configurado && (config.data?.ativo ?? false)
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {configurado ? (config.data?.ativo ? 'Configurado' : 'Inativo') : 'Não configurado'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-4 border-t border-gray-100 pt-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="URL base" htmlFor="wa-url" className="sm:col-span-2">
              <Input id="wa-url" value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} disabled={!podeEditar} />
            </Campo>
            <Campo label="Phone Number ID" htmlFor="wa-phone">
              <Input id="wa-phone" value={phoneNumberId} onChange={(e) => setPhoneNumberId(e.target.value)} disabled={!podeEditar} />
            </Campo>
            <Campo label="WABA ID" htmlFor="wa-waba">
              <Input id="wa-waba" value={wabaId} onChange={(e) => setWabaId(e.target.value)} disabled={!podeEditar} />
            </Campo>
          </div>

          <Campo
            label="Token (System User)"
            htmlFor="wa-token"
            dica={config.data?.tokenConfigurado ? 'Já configurado — preencha apenas para substituir.' : 'Ainda não configurado.'}
          >
            <Input
              id="wa-token"
              type="password"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              placeholder={config.data?.tokenConfigurado ? '••••••••••••' : 'Cole o token aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo
              label="Verify Token (webhook)"
              htmlFor="wa-verify"
              dica={config.data?.verifyTokenConfigurado ? 'Já configurado.' : 'Ainda não configurado.'}
            >
              <Input
                id="wa-verify"
                type="password"
                value={verifyToken}
                onChange={(e) => setVerifyToken(e.target.value)}
                placeholder={config.data?.verifyTokenConfigurado ? '••••••••' : 'Verify token'}
                autoComplete="new-password"
                disabled={!podeEditar}
              />
            </Campo>
            <Campo
              label="App Secret"
              htmlFor="wa-secret"
              dica={config.data?.appSecretConfigurado ? 'Já configurado.' : 'Ainda não configurado.'}
            >
              <Input
                id="wa-secret"
                type="password"
                value={appSecret}
                onChange={(e) => setAppSecret(e.target.value)}
                placeholder={config.data?.appSecretConfigurado ? '••••••••' : 'App secret'}
                autoComplete="new-password"
                disabled={!podeEditar}
              />
            </Campo>
          </div>

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
