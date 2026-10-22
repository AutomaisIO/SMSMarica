import { useEffect, useState } from 'react';
import { BellRing, CheckCircle2, ChevronDown, Loader2, Trash2, Wifi, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { useLimparCredencial, useSalvarCredencial, useTestarFcm } from '@/features/integracoes/api';
import type { IntegracaoCredencial } from '@/features/integracoes/types';

export const PROVEDOR_FCM = 'fcm';

function lerProjectIdGravado(json: string | null): string | null {
  if (!json) return null;
  try {
    const o = JSON.parse(json) as Record<string, unknown>;
    return typeof o.projectId === 'string' && o.projectId ? o.projectId : null;
  } catch {
    return null;
  }
}

type LeituraContaServico =
  | { estado: 'vazio' }
  | { estado: 'invalido'; motivo: string }
  | { estado: 'ok'; projectId: string };

/**
 * Confere no navegador o JSON colado. O servidor confere de novo ao gravar; aqui é para a pessoa
 * saber, antes de salvar, se colou o arquivo certo — e para tirar dele o projeto, que é público.
 */
function lerContaServico(texto: string): LeituraContaServico {
  const t = texto.trim();
  if (!t) return { estado: 'vazio' };
  let o: unknown;
  try {
    o = JSON.parse(t);
  } catch {
    return { estado: 'invalido', motivo: 'Isto não é um JSON válido. Cole o arquivo inteiro, da primeira { à última }.' };
  }
  if (!o || typeof o !== 'object' || Array.isArray(o)) {
    return { estado: 'invalido', motivo: 'Isto não é o JSON de uma conta de serviço.' };
  }
  const r = o as Record<string, unknown>;
  if (r.type !== 'service_account') {
    return {
      estado: 'invalido',
      motivo:
        'Isto não é o JSON de uma conta de serviço (falta "type": "service_account"). Não é o google-services.json do app: é a chave privada gerada em Contas de serviço.',
    };
  }
  const faltando = ['project_id', 'client_email', 'private_key'].filter(
    (k) => typeof r[k] !== 'string' || !(r[k] as string).trim(),
  );
  if (faltando.length > 0) {
    return { estado: 'invalido', motivo: `Faltam campos no JSON: ${faltando.join(', ')}.` };
  }
  return { estado: 'ok', projectId: (r.project_id as string).trim() };
}

/**
 * Credencial do Firebase Cloud Messaging — envio de notificações ao app do cidadão. Reaproveita o
 * store genérico de credenciais: o JSON inteiro da conta de serviço vai cifrado no clientSecret
 * (nunca volta para a tela) e só o projectId, que é público, fica em parametrosJson.
 */
export function FirebaseCard({ cred }: { cred: IntegracaoCredencial }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const podeExcluir = usePermissao('IntegracoesConfig', 'Exclusao');
  const salvar = useSalvarCredencial();
  const limpar = useLimparCredencial();
  const testar = useTestarFcm();

  const [aberto, setAberto] = useState(false);
  const [contaServico, setContaServico] = useState('');
  const [ativo, setAtivo] = useState(cred.ativo);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  // Só com o card fechado, para um refetch não desfazer o que a pessoa está mexendo.
  useEffect(() => {
    if (aberto) return;
    setAtivo(cred.ativo);
  }, [aberto, cred.ativo]);

  const configurado = cred.clientSecretDefinido;
  // O Limpar apaga o segredo mas deixa o parametrosJson: sem segredo, o projeto antigo não vale.
  const projectIdGravado = configurado ? lerProjectIdGravado(cred.parametrosJson) : null;
  const leitura = lerContaServico(contaServico);

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    testar.reset();
    if (leitura.estado === 'invalido') {
      setErro(leitura.motivo);
      return;
    }
    // Sem JSON novo, o segredo gravado fica e o projeto também — o PUT sobrescreve parametrosJson.
    const parametrosJson =
      leitura.estado === 'ok' ? JSON.stringify({ projectId: leitura.projectId }) : cred.parametrosJson;
    salvar.mutate(
      {
        provedor: cred.provedor,
        payload: {
          clientSecret: leitura.estado === 'ok' ? contaServico.trim() : undefined,
          redirectUri: null,
          parametrosJson,
          ativo,
        },
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setContaServico('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoLimpar() {
    if (!window.confirm('Limpar a credencial do Firebase? O envio de notificações ao app do cidadão para de funcionar.')) {
      return;
    }
    setErro(null);
    setSalvo(false);
    testar.reset();
    limpar.mutate(cred.provedor, {
      // O servidor desliga o Ativo junto; com o card aberto, o efeito acima não ressincroniza.
      onSuccess: () => setAtivo(false),
      onError: (err) => setErro(extrairMensagemDeErro(err)),
    });
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <BellRing className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">Firebase — notificações do app do cidadão</span>
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
          <p className="text-sm text-gray-600">
            É o que permite mandar notificação, pela ficha do paciente, ao app do cidadão instalado no
            celular. No console do Firebase: Configurações do projeto → Contas de serviço → Gerar nova chave
            privada. Cole aqui o conteúdo do arquivo baixado.
          </p>

          {projectIdGravado ? (
            <p className="text-sm text-gray-700">
              Projeto gravado: <strong className="font-mono">{projectIdGravado}</strong>
            </p>
          ) : null}

          <Campo
            label="JSON da conta de serviço"
            htmlFor="fcm-conta-servico"
            dica={
              configurado
                ? 'Já definido — cole um novo apenas para substituir. O valor gravado nunca é exibido.'
                : 'Ainda não definido.'
            }
          >
            <textarea
              id="fcm-conta-servico"
              className="input min-h-[140px] font-mono text-xs"
              value={contaServico}
              onChange={(e) => setContaServico(e.target.value)}
              placeholder={configurado ? '••••••••••••' : '{ "type": "service_account", "project_id": "…", … }'}
              autoComplete="off"
              spellCheck={false}
              disabled={!podeEditar}
            />
          </Campo>

          {leitura.estado === 'ok' ? (
            <p className="flex items-center gap-1.5 text-sm text-green-700">
              <CheckCircle2 className="h-4 w-4 shrink-0" />
              Projeto lido do JSON: <strong className="font-mono">{leitura.projectId}</strong>
            </p>
          ) : leitura.estado === 'invalido' ? (
            <p className="flex items-start gap-1.5 text-sm text-red-700">
              <XCircle className="mt-0.5 h-4 w-4 shrink-0" />
              {leitura.motivo}
            </p>
          ) : null}

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Envio de notificações ativo
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {testar.isError ? (
            <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              <XCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <span>Não foi possível testar: {extrairMensagemDeErro(testar.error)}</span>
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
                <span>{testar.data.mensagem}</span>
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
                  // O teste usa o que está GRAVADO; com JSON novo colado, testaria o antigo.
                  disabled={testar.isPending || leitura.estado !== 'vazio'}
                  className="mr-auto"
                  title={
                    leitura.estado !== 'vazio'
                      ? 'Salve o JSON novo antes de testar — o teste usa a credencial gravada.'
                      : 'Autentica com a credencial gravada e faz um envio de validação ao Firebase, que não entrega nada a ninguém.'
                  }
                >
                  {testar.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Wifi className="mr-2 h-4 w-4" />
                  )}
                  Testar
                </Button>
              ) : null}
              {podeExcluir && configurado ? (
                <Button type="button" variante="outline" onClick={aoLimpar} disabled={limpar.isPending}>
                  <Trash2 className="mr-2 h-4 w-4" />
                  Limpar
                </Button>
              ) : null}
              <Button type="submit" disabled={salvar.isPending || leitura.estado === 'invalido'}>
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
