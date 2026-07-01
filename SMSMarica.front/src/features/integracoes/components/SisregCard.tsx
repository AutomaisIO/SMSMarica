import { useEffect, useState } from 'react';
import { ChevronDown, Stethoscope, Loader2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useLimparCredencial, useSalvarCredencial } from '@/features/integracoes/api';
import type { IntegracaoCredencial } from '@/features/integracoes/types';

export const PROVEDOR_SISREG = 'sisreg';

type ParametrosSisreg = { baseUrl: string; autoLogin: boolean };

function lerParametros(json: string | null): ParametrosSisreg {
  // autoLogin ausente = ligado (compatível com credenciais já salvas).
  if (!json) return { baseUrl: '', autoLogin: true };
  try {
    const o = JSON.parse(json) as Record<string, unknown>;
    return {
      baseUrl: typeof o.baseUrl === 'string' ? o.baseUrl : '',
      autoLogin: o.autoLogin === false ? false : true,
    };
  } catch {
    return { baseUrl: '', autoLogin: true };
  }
}

/**
 * Credencial do SISREG III (web scraping) — usada na consulta de paciente por CNS
 * (CADSUS). Reaproveita o store genérico cifrado: Usuário → clientId, Senha →
 * clientSecret, e a Base URL é serializada em parametrosJson ({ baseUrl }).
 *
 * ⚠️ O SISREG é sessão única por operador: use, de preferência, um operador
 * dedicado ao sistema para não derrubar a sessão dos atendentes humanos.
 */
export function SisregCard({ cred }: { cred: IntegracaoCredencial }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const podeExcluir = usePermissao('IntegracoesConfig', 'Exclusao');
  const salvar = useSalvarCredencial();
  const limpar = useLimparCredencial();

  const [aberto, setAberto] = useState(false);
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [baseUrl, setBaseUrl] = useState(() => lerParametros(cred.parametrosJson).baseUrl);
  const [autoLogin, setAutoLogin] = useState(() => lerParametros(cred.parametrosJson).autoLogin);
  const [ativo, setAtivo] = useState(cred.ativo);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (aberto) return;
    const p = lerParametros(cred.parametrosJson);
    setBaseUrl(p.baseUrl);
    setAutoLogin(p.autoLogin);
    setAtivo(cred.ativo);
  }, [aberto, cred.parametrosJson, cred.ativo]);

  const configurado = cred.clientIdDefinido && cred.clientSecretDefinido;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    const params = JSON.stringify({ ...(baseUrl.trim() ? { baseUrl: baseUrl.trim() } : {}), autoLogin });
    salvar.mutate(
      {
        provedor: cred.provedor,
        payload: {
          clientId: usuario || undefined,
          clientSecret: senha || undefined,
          redirectUri: null,
          parametrosJson: params,
          ativo,
        },
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setUsuario('');
          setSenha('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoLimpar() {
    if (!window.confirm('Limpar as credenciais do SISREG? A busca de paciente por CNS para de funcionar.')) {
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
          <Stethoscope className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">SISREG (consulta por CNS)</span>
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
          <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800">
            O SISREG aceita apenas uma sessão por operador. Prefira um operador dedicado ao sistema —
            senão o robô e os atendentes derrubam a sessão um do outro.
          </p>

          <Campo
            label="Operador (usuário)"
            htmlFor="sisreg-usuario"
            dica={cred.clientIdDefinido ? 'Já definido — preencha apenas para substituir.' : 'Ainda não definido.'}
          >
            <Input
              id="sisreg-usuario"
              value={usuario}
              onChange={(e) => setUsuario(e.target.value)}
              placeholder={cred.clientIdDefinido ? '••••••••' : 'Ex.: 022-FULANO'}
              autoComplete="off"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Senha"
            htmlFor="sisreg-senha"
            dica={cred.clientSecretDefinido ? 'Já definida — preencha apenas para substituir.' : 'Ainda não definida.'}
          >
            <Input
              id="sisreg-senha"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              placeholder={cred.clientSecretDefinido ? '••••••••••••' : 'Senha do operador'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo label="Base URL" htmlFor="sisreg-baseurl" dica="Opcional — padrão: https://sisregiii.saude.gov.br">
            <Input
              id="sisreg-baseurl"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
              placeholder="https://sisregiii.saude.gov.br"
              disabled={!podeEditar}
            />
          </Campo>

          <div className="rounded-md border border-gray-200 p-3">
            <label className="flex items-center gap-2 text-sm font-medium text-gray-800">
              <input
                type="checkbox"
                checked={autoLogin}
                onChange={(e) => setAutoLogin(e.target.checked)}
                disabled={!podeEditar}
              />
              Reautenticação automática
            </label>
            <p className="mt-1 text-xs text-gray-500">
              Ligada: se a sessão cair, o robô refaz o login sozinho (pode derrubar a sessão de um
              atendente logado com o mesmo operador). Desligada: o robô não reconecta e a consulta
              retorna “Falha no SISREG” — use durante o expediente para não brigar com os atendentes.
            </p>
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
