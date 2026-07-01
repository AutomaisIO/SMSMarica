import { useEffect, useMemo, useState } from 'react';
import { Bug, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscarErros, useErroPorCodigo } from '@/features/erros/api/queries';
import type { ErroFiltro, RegistroErroListItem } from '@/features/erros/types';

function useDebounce<T>(valor: T, ms = 400): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR');
}

/** Detalhe de um erro (stack trace) buscado pelo código. */
function DetalheErro({ codigo, aoFechar }: { codigo: string; aoFechar: () => void }) {
  const q = useErroPorCodigo(codigo);
  const e = q.data;
  return (
    <Modal aberto aoFechar={aoFechar} titulo={`Erro ${codigo}`} largura="lg">
      {q.isLoading ? (
        <div className="text-sm text-gray-500">Carregando…</div>
      ) : q.isError || !e ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar o detalhe do erro.
        </div>
      ) : (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Info rotulo="Data/hora" valor={formatarDataHora(e.criadoEm)} />
            <Info rotulo="Status" valor={String(e.statusCode)} />
            <Info rotulo="Método" valor={e.metodo} />
            <Info rotulo="Usuário" valor={e.usuarioNome ?? '—'} />
            <Info rotulo="Caminho" valor={e.caminho + (e.queryString ?? '')} className="sm:col-span-2" />
            <Info rotulo="Trace ID" valor={e.traceId ?? '—'} className="sm:col-span-2" />
            <Info rotulo="Tipo da exceção" valor={e.tipoExcecao} className="sm:col-span-2" />
          </div>
          <div>
            <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Mensagem</span>
            <p className="mt-1 whitespace-pre-wrap text-sm text-gray-900">{e.mensagem}</p>
          </div>
          {e.interna ? (
            <div>
              <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Exceção interna</span>
              <p className="mt-1 whitespace-pre-wrap text-sm text-gray-900">{e.interna}</p>
            </div>
          ) : null}
          {e.stackTrace ? (
            <div>
              <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Stack trace</span>
              <pre className="mt-1 max-h-80 overflow-auto rounded-md bg-gray-900 p-3 text-xs leading-relaxed text-gray-100">
                {e.stackTrace}
              </pre>
            </div>
          ) : null}
          {e.userAgent ? <Info rotulo="User agent" valor={e.userAgent} /> : null}
        </div>
      )}
    </Modal>
  );
}

function Info({ rotulo, valor, className }: { rotulo: string; valor: string; className?: string }) {
  return (
    <div className={`flex flex-col gap-0.5 ${className ?? ''}`}>
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="break-words text-sm text-gray-900">{valor}</span>
    </div>
  );
}

export function ErrosPage() {
  const [texto, setTexto] = useState('');
  const [codigo, setCodigo] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [detalhe, setDetalhe] = useState<string | null>(null);
  const textoDebounced = useDebounce(texto, 400);
  const codigoDebounced = useDebounce(codigo, 400);

  const filtro = useMemo<ErroFiltro>(
    () => ({
      texto: textoDebounced.trim() || undefined,
      codigo: codigoDebounced.trim() || undefined,
      de: de ? `${de}T00:00:00` : undefined,
      ate: ate ? `${ate}T23:59:59` : undefined,
      tamanho: 100,
    }),
    [textoDebounced, codigoDebounced, de, ate],
  );

  const q = useBuscarErros(filtro);

  const colunas: Coluna<RegistroErroListItem>[] = useMemo(
    () => [
      { chave: 'data', cabecalho: 'Data/hora', render: (e) => formatarDataHora(e.criadoEm) },
      {
        chave: 'codigo',
        cabecalho: 'Código',
        render: (e) => (
          <button
            type="button"
            onClick={() => setDetalhe(e.codigoReferencia)}
            className="font-mono font-medium text-red-700 hover:underline"
          >
            {e.codigoReferencia}
          </button>
        ),
      },
      { chave: 'status', cabecalho: 'Status', render: (e) => e.statusCode },
      { chave: 'metodo', cabecalho: 'Método', render: (e) => e.metodo },
      {
        chave: 'caminho',
        cabecalho: 'Caminho',
        render: (e) => <span className="font-mono text-xs text-gray-600">{e.caminho}</span>,
      },
      { chave: 'usuario', cabecalho: 'Usuário', render: (e) => e.usuarioNome ?? '—' },
      {
        chave: 'mensagem',
        cabecalho: 'Mensagem',
        render: (e) => (
          <span className="block max-w-md truncate text-gray-700" title={e.mensagem}>
            {e.mensagem}
          </span>
        ),
      },
    ],
    [],
  );

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <Bug className="mt-1 h-6 w-6 text-red-600" />
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Erros do sistema</h1>
          <p className="mt-1 text-sm text-gray-600">
            Log de erros inesperados (500). Peça ao usuário o <strong>código</strong> mostrado na
            tela (ex.: <span className="font-mono">ERRO-XXXXXX</span>) e busque aqui para ver o trace.
          </p>
        </div>
      </header>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input
            autoFocus
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Buscar por mensagem, caminho, tipo ou usuário…"
            className="pl-9"
          />
        </div>
        <Input
          value={codigo}
          onChange={(e) => setCodigo(e.target.value)}
          placeholder="Código (ERRO-…)"
          className="font-mono"
        />
        <div className="flex items-center gap-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="De" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Até" />
        </div>
      </div>

      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={q.data?.itens ?? []}
        chaveLinha={(e) => e.id}
        carregando={q.isLoading}
        vazio="Nenhum erro registrado."
      />

      {q.data ? (
        <p className="text-xs text-gray-500">
          {q.data.total} erro(s){q.data.total > q.data.itens.length ? ` · exibindo os ${q.data.itens.length} mais recentes` : ''}.
        </p>
      ) : null}

      {detalhe ? <DetalheErro codigo={detalhe} aoFechar={() => setDetalhe(null)} /> : null}
    </div>
  );
}
