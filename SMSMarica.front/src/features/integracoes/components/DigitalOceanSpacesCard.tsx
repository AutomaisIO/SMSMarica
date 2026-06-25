import { useEffect, useState } from 'react';
import { CheckCircle2, ChevronDown, Cloud, Loader2, Trash2, Wifi, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useLimparCredencial, useSalvarCredencial, useTestarSpaces } from '@/features/integracoes/api';
import type { EtapaTesteSpaces, IntegracaoCredencial } from '@/features/integracoes/types';

export const PROVEDOR_SPACES = 'digitalocean_spaces';

const ROTULO_ETAPA: Record<EtapaTesteSpaces, string> = {
  credencial: 'credencial',
  conexao: 'conexão',
  escrita: 'escrita',
  leitura: 'leitura',
  exclusao: 'exclusão',
  ok: 'conexão',
};

type ParametrosSpaces = { endpoint: string; region: string; bucket: string };

function lerParametros(json: string | null): ParametrosSpaces {
  if (!json) return { endpoint: '', region: '', bucket: '' };
  try {
    const o = JSON.parse(json) as Record<string, unknown>;
    return {
      endpoint: typeof o.endpoint === 'string' ? o.endpoint : '',
      region: typeof o.region === 'string' ? o.region : '',
      bucket: typeof o.bucket === 'string' ? o.bucket : '',
    };
  } catch {
    return { endpoint: '', region: '', bucket: '' };
  }
}

/**
 * Credencial do DigitalOcean Spaces (compatível com S3) — armazenamento dos PDFs
 * de exames digitalizados. Reaproveita o store genérico de credenciais:
 * Access Key → clientId, Secret Key → clientSecret, e endpoint/region/bucket
 * são serializados em parametrosJson.
 */
export function DigitalOceanSpacesCard({ cred }: { cred: IntegracaoCredencial }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const podeExcluir = usePermissao('IntegracoesConfig', 'Exclusao');
  const salvar = useSalvarCredencial();
  const limpar = useLimparCredencial();
  const testar = useTestarSpaces();

  const [aberto, setAberto] = useState(false);
  const [accessKey, setAccessKey] = useState('');
  const [secretKey, setSecretKey] = useState('');
  const [params, setParams] = useState<ParametrosSpaces>(lerParametros(cred.parametrosJson));
  const [ativo, setAtivo] = useState(cred.ativo);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  // Sincroniza os campos públicos com a lista recarregada, mas só com o card
  // fechado — evita resetar o que o usuário está digitando após um refetch.
  useEffect(() => {
    if (aberto) return;
    setParams(lerParametros(cred.parametrosJson));
    setAtivo(cred.ativo);
  }, [aberto, cred.parametrosJson, cred.ativo]);

  const configurado = cred.clientIdDefinido && cred.clientSecretDefinido;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    testar.reset();
    const obj: Record<string, string> = {};
    if (params.endpoint.trim()) obj.endpoint = params.endpoint.trim();
    if (params.region.trim()) obj.region = params.region.trim();
    if (params.bucket.trim()) obj.bucket = params.bucket.trim();
    salvar.mutate(
      {
        provedor: cred.provedor,
        payload: {
          clientId: accessKey || undefined,
          clientSecret: secretKey || undefined,
          redirectUri: null,
          parametrosJson: Object.keys(obj).length ? JSON.stringify(obj) : null,
          ativo,
        },
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setAccessKey('');
          setSecretKey('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoLimpar() {
    if (!window.confirm('Limpar as credenciais do DigitalOcean Spaces? O upload de exames para o bucket para de funcionar.')) {
      return;
    }
    setErro(null);
    testar.reset();
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
          <Cloud className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">DigitalOcean Spaces (S3)</span>
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
            label="Access Key"
            htmlFor="spaces-access"
            dica={cred.clientIdDefinido ? 'Já definida — preencha apenas para substituir.' : 'Ainda não definida.'}
          >
            <Input
              id="spaces-access"
              type="password"
              value={accessKey}
              onChange={(e) => setAccessKey(e.target.value)}
              placeholder={cred.clientIdDefinido ? '••••••••••••' : 'Cole a Access Key aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Secret Key"
            htmlFor="spaces-secret"
            dica={cred.clientSecretDefinido ? 'Já definida — preencha apenas para substituir.' : 'Ainda não definida.'}
          >
            <Input
              id="spaces-secret"
              type="password"
              value={secretKey}
              onChange={(e) => setSecretKey(e.target.value)}
              placeholder={cred.clientSecretDefinido ? '••••••••••••' : 'Cole a Secret Key aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo label="Endpoint" htmlFor="spaces-endpoint" dica="Ex.: https://nyc3.digitaloceanspaces.com">
            <Input
              id="spaces-endpoint"
              value={params.endpoint}
              onChange={(e) => setParams((p) => ({ ...p, endpoint: e.target.value }))}
              placeholder="https://nyc3.digitaloceanspaces.com"
              disabled={!podeEditar}
            />
          </Campo>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Region" htmlFor="spaces-region" dica="Ex.: nyc3">
              <Input
                id="spaces-region"
                value={params.region}
                onChange={(e) => setParams((p) => ({ ...p, region: e.target.value }))}
                placeholder="nyc3"
                disabled={!podeEditar}
              />
            </Campo>
            <Campo label="Bucket" htmlFor="spaces-bucket" dica="Nome do Space (bucket).">
              <Input
                id="spaces-bucket"
                value={params.bucket}
                onChange={(e) => setParams((p) => ({ ...p, bucket: e.target.value }))}
                placeholder="smsmarica-exames"
                disabled={!podeEditar}
              />
            </Campo>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Armazenamento ativo
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {testar.isError ? (
            <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              <XCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <span>Não foi possível testar a conexão: {extrairMensagemDeErro(testar.error)}</span>
            </div>
          ) : testar.data ? (
            testar.data.ok ? (
              <div className="flex items-start gap-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0" />
                <span>{testar.data.mensagem}</span>
              </div>
            ) : (
              <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                <XCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                <span>
                  Falha na {ROTULO_ETAPA[testar.data.etapa]}: {testar.data.mensagem}
                </span>
              </div>
            )
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              {configurado ? (
                <Button
                  type="button"
                  variante="outline"
                  onClick={() => testar.mutate()}
                  disabled={testar.isPending}
                  className="mr-auto"
                >
                  {testar.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Wifi className="mr-2 h-4 w-4" />
                  )}
                  Testar conexão
                </Button>
              ) : null}
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
