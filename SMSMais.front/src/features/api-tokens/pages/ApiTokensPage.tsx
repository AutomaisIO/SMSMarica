import { useEffect, useState } from 'react';
import { Check, Copy, KeySquare, Loader2, Plus, ShieldAlert, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useCriarApiToken,
  useListarApiTokens,
  useRevogarApiToken,
} from '@/features/api-tokens/api/queries';
import type { ApiTokenCriado, ApiTokenListItem } from '@/features/api-tokens/types';

function formatarData(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR');
}

export function ApiTokensPage() {
  const lista = useListarApiTokens();
  const criar = useCriarApiToken();
  const revogar = useRevogarApiToken();

  const podeIncluir = usePermissao('ApiTokens', 'Inclusao');
  const podeExcluir = usePermissao('ApiTokens', 'Exclusao');

  const [modalNovo, setModalNovo] = useState(false);
  const [nome, setNome] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  // Token recém-criado, mostrado uma única vez.
  const [tokenCriado, setTokenCriado] = useState<ApiTokenCriado | null>(null);
  const [copiado, setCopiado] = useState(false);

  useEffect(() => {
    if (!modalNovo) {
      setErro(null);
      setNome('');
    }
  }, [modalNovo]);

  function aoCriar(ev: React.FormEvent) {
    ev.preventDefault();
    setErro(null);
    criar.mutate(nome.trim(), {
      onSuccess: (data) => {
        setModalNovo(false);
        setTokenCriado(data);
        setCopiado(false);
      },
      onError: (err) => setErro(extrairMensagemDeErro(err)),
    });
  }

  function aoRevogar(t: ApiTokenListItem) {
    if (!window.confirm(`Revogar o token "${t.nome}"? Integrações que o usam vão parar de funcionar.`)) {
      return;
    }
    revogar.mutate(t.id, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
  }

  async function copiarToken() {
    if (!tokenCriado) return;
    try {
      await navigator.clipboard.writeText(tokenCriado.token);
      setCopiado(true);
      setTimeout(() => setCopiado(false), 2000);
    } catch {
      window.alert('Não foi possível copiar. Selecione e copie manualmente.');
    }
  }

  const colunas: Coluna<ApiTokenListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (t) => <span className="font-medium text-gray-900">{t.nome}</span>,
    },
    {
      chave: 'prefixo',
      cabecalho: 'Token',
      render: (t) => <code className="rounded bg-gray-100 px-1.5 py-0.5 text-xs">{t.prefixo}…</code>,
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (t) => (
        <span className={cn('badge', t.ativo ? 'badge-success' : 'badge-gray')}>
          {t.ativo ? 'Ativo' : 'Revogado'}
        </span>
      ),
    },
    { chave: 'criadoEm', cabecalho: 'Criado em', render: (t) => formatarData(t.criadoEm) },
    { chave: 'ultimoUso', cabecalho: 'Último uso', render: (t) => formatarData(t.ultimoUsoEm) },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (t) =>
        t.ativo && podeExcluir ? (
          <div className="flex items-center justify-end">
            <button
              type="button"
              onClick={() => aoRevogar(t)}
              className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
            >
              <Trash2 className="h-3.5 w-3.5" />
              Revogar
            </button>
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        ),
    },
  ];

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <KeySquare className="h-6 w-6 text-primary-600" />
            API Tokens
          </h1>
          <p className="mt-1 max-w-2xl text-sm text-gray-600">
            Chaves de serviço para integrações externas (ex.: CentralIA) chamarem a API sem login de
            usuário. Use o token no header <code className="rounded bg-gray-100 px-1 py-0.5 text-xs">X-API-Key</code>.
          </p>
        </div>
        {podeIncluir ? (
          <Button onClick={() => setModalNovo(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Novo token
          </Button>
        ) : null}
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(t) => t.id}
        carregando={lista.isPending}
        vazio="Nenhum token de API criado."
      />

      {/* Modal: criar token (pede só o nome) */}
      <Modal aberto={modalNovo} aoFechar={() => setModalNovo(false)} titulo="Novo token de API">
        <form onSubmit={aoCriar} className="space-y-4">
          <Campo label="Nome / origem" htmlFor="token-nome" required dica="Identifica quem usa o token (ex.: CentralIA).">
            <Input
              id="token-nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder="Ex.: CentralIA"
              autoFocus
              required
            />
          </Campo>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex items-center justify-end gap-3 pt-2">
            <Button type="button" variante="secundaria" onClick={() => setModalNovo(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={criar.isPending || !nome.trim()}>
              {criar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Gerar token
            </Button>
          </div>
        </form>
      </Modal>

      {/* Modal: token gerado — mostrado UMA única vez */}
      <Modal
        aberto={tokenCriado !== null}
        aoFechar={() => setTokenCriado(null)}
        titulo="Token gerado"
      >
        {tokenCriado ? (
          <div className="space-y-4">
            <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <ShieldAlert className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <span>
                Copie agora — este é o <strong>único</strong> momento em que o token aparece. Depois
                de fechar, não há como recuperá-lo (só revogar e gerar outro).
              </span>
            </div>

            <Campo label="Token" htmlFor="token-valor">
              <div className="flex items-stretch gap-2">
                <input
                  id="token-valor"
                  readOnly
                  value={tokenCriado.token}
                  className="flex-1 rounded-md border border-gray-300 bg-gray-50 px-3 py-2 font-mono text-xs text-gray-800"
                  onFocus={(e) => e.target.select()}
                />
                <Button type="button" variante="outline" onClick={copiarToken}>
                  {copiado ? <Check className="h-4 w-4 text-green-600" /> : <Copy className="h-4 w-4" />}
                </Button>
              </div>
            </Campo>

            <div className="flex items-center justify-end pt-2">
              <Button type="button" onClick={() => setTokenCriado(null)}>
                Já copiei, fechar
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
