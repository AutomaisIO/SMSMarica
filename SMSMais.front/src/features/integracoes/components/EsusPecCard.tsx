import { useEffect, useState } from 'react';
import { ChevronDown, HeartPulse, Loader2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useLimparCredencial, useSalvarCredencial } from '@/features/integracoes/api';
import type { IntegracaoCredencial } from '@/features/integracoes/types';

export const PROVEDOR_ESUS_PEC = 'esuspec';

const BASE_URL_PADRAO = 'https://esus.marica.rj.gov.br';
const JANELA_INICIO_PADRAO = '02:00';
const JANELA_FIM_PADRAO = '05:00';

type ParametrosEsusPec = { baseUrl: string; acessoId: string; janelaInicio: string; janelaFim: string };

function lerParametros(json: string | null): ParametrosEsusPec {
  const padrao = { baseUrl: '', acessoId: '', janelaInicio: JANELA_INICIO_PADRAO, janelaFim: JANELA_FIM_PADRAO };
  if (!json) return padrao;
  try {
    const o = JSON.parse(json) as Record<string, unknown>;
    const texto = (v: unknown, d: string) => (typeof v === 'string' && v ? v : d);
    return {
      baseUrl: texto(o.baseUrl, ''),
      acessoId: texto(o.acessoId, ''),
      janelaInicio: texto(o.janelaInicio, JANELA_INICIO_PADRAO),
      janelaFim: texto(o.janelaFim, JANELA_FIM_PADRAO),
    };
  } catch {
    return padrao;
  }
}

/**
 * Credencial do e-SUS APS PEC do município (o e-SUS do GOVERNO) — consulta, SÓ LEITURA, do cadastro
 * do cidadão para achar outro celular quando a mensagem não chega (ADR-0067). Reaproveita o store
 * genérico cifrado: Usuário (CPF) → clientId, Senha → clientSecret; endereço, acesso (lotação) e a
 * janela da madrugada vão em parametrosJson.
 *
 * ⚠️ O PEC aceita UMA sessão por usuário e a credencial é cedida por uma servidora: a rotina só entra
 * dentro da janela da madrugada, para não derrubar a sessão dela durante o expediente.
 */
export function EsusPecCard({ cred }: { cred: IntegracaoCredencial }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const podeExcluir = usePermissao('IntegracoesConfig', 'Exclusao');
  const salvar = useSalvarCredencial();
  const limpar = useLimparCredencial();

  const [aberto, setAberto] = useState(false);
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [params, setParams] = useState<ParametrosEsusPec>(() => lerParametros(cred.parametrosJson));
  const [ativo, setAtivo] = useState(cred.ativo);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (aberto) return;
    setParams(lerParametros(cred.parametrosJson));
    setAtivo(cred.ativo);
  }, [aberto, cred.parametrosJson, cred.ativo]);

  const configurado = cred.clientIdDefinido && cred.clientSecretDefinido;
  const campo = (k: keyof ParametrosEsusPec) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setParams((p) => ({ ...p, [k]: e.target.value }));

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    if (!/^\d{2}:\d{2}$/.test(params.janelaInicio) || !/^\d{2}:\d{2}$/.test(params.janelaFim)) {
      setErro('Informe a janela no formato HH:MM (ex.: 02:00 e 05:00).');
      return;
    }
    const json = JSON.stringify({
      ...(params.baseUrl.trim() ? { baseUrl: params.baseUrl.trim() } : {}),
      ...(params.acessoId.trim() ? { acessoId: params.acessoId.trim() } : {}),
      janelaInicio: params.janelaInicio,
      janelaFim: params.janelaFim,
    });
    salvar.mutate(
      {
        provedor: cred.provedor,
        payload: {
          clientId: usuario.replace(/\D/g, '') || undefined,
          clientSecret: senha || undefined,
          redirectUri: null,
          parametrosJson: json,
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
    if (!window.confirm('Limpar a credencial do e-SUS PEC? A correção de telefone pelo e-SUS para de funcionar.')) {
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
          <HeartPulse className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">e-SUS APS PEC (cadastro do cidadão)</span>
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
            O e-SUS aceita apenas <b>uma sessão por usuário</b>. Esta credencial é usada só dentro da janela
            da madrugada abaixo — fora dela o sistema não entra, para não derrubar quem cedeu o acesso.
            A consulta é somente leitura: nada é gravado no e-SUS.
          </p>

          <Campo
            label="Usuário (CPF)"
            htmlFor="esuspec-usuario"
            dica={cred.clientIdDefinido ? 'Já definido — preencha apenas para substituir.' : 'Ainda não definido.'}
          >
            <Input
              id="esuspec-usuario"
              value={usuario}
              onChange={(e) => setUsuario(e.target.value)}
              placeholder={cred.clientIdDefinido ? '••••••••' : 'CPF de quem cedeu o acesso'}
              autoComplete="off"
              inputMode="numeric"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Senha"
            htmlFor="esuspec-senha"
            dica={cred.clientSecretDefinido ? 'Já definida — preencha apenas para substituir.' : 'Ainda não definida.'}
          >
            <Input
              id="esuspec-senha"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              placeholder={cred.clientSecretDefinido ? '••••••••••••' : 'Senha do e-SUS'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <Campo label="Endereço do e-SUS" htmlFor="esuspec-baseurl" dica={`Opcional — padrão: ${BASE_URL_PADRAO}`}>
            <Input
              id="esuspec-baseurl"
              value={params.baseUrl}
              onChange={campo('baseUrl')}
              placeholder={BASE_URL_PADRAO}
              disabled={!podeEditar}
            />
          </Campo>

          <Campo
            label="Acesso (lotação) usado na consulta"
            htmlFor="esuspec-acesso"
            dica="Código do acesso do usuário no e-SUS que pode ver o cadastro do cidadão (ex.: lotação de coordenação/direção). Vazio = o único acesso, se houver só um."
          >
            <Input
              id="esuspec-acesso"
              value={params.acessoId}
              onChange={campo('acessoId')}
              placeholder="Ex.: 22388"
              inputMode="numeric"
              disabled={!podeEditar}
            />
          </Campo>

          <div className="grid grid-cols-2 gap-3">
            <Campo label="Janela — início" htmlFor="esuspec-ini" dica="Horário de Brasília (HH:MM).">
              <Input id="esuspec-ini" value={params.janelaInicio} onChange={campo('janelaInicio')} placeholder="02:00" disabled={!podeEditar} />
            </Campo>
            <Campo label="Janela — fim" htmlFor="esuspec-fim" dica="Fora da janela o sistema não entra.">
              <Input id="esuspec-fim" value={params.janelaFim} onChange={campo('janelaFim')} placeholder="05:00" disabled={!podeEditar} />
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
