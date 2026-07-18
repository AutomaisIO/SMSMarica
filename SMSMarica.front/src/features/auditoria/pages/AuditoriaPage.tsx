import { useEffect, useMemo, useState } from 'react';
import { Building2, ScrollText, Search, User, UserRound, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { paraUtcDeLocal } from '@/shared/lib/datas';
import { Input } from '@/shared/ui/Input';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscarAuditoria } from '@/features/auditoria/api/queries';
import type { RegistroAuditoria, TipoAtorAuditoria, AuditoriaFiltro } from '@/features/auditoria/types';

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
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

const ACAO_LABEL: Record<string, string> = {
  AlteracaoNome: 'Alteração de nome',
};

const ENTIDADES = [
  { valor: '', rotulo: 'Todas as entidades' },
  { valor: 'Paciente', rotulo: 'Paciente' },
];

const ATOR: Record<TipoAtorAuditoria, { rotulo: string; cor: string; Icone: typeof User }> = {
  UsuarioSistema: { rotulo: 'Usuário do sistema', cor: 'bg-indigo-50 text-indigo-700 ring-indigo-200', Icone: User },
  Paciente: { rotulo: 'Paciente (app)', cor: 'bg-emerald-50 text-emerald-700 ring-emerald-200', Icone: UserRound },
  Desconhecido: { rotulo: 'Não identificado', cor: 'bg-gray-100 text-gray-600 ring-gray-200', Icone: User },
};

function BadgeAtor({ tipo }: { tipo: TipoAtorAuditoria }) {
  const a = ATOR[tipo];
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${a.cor}`}>
      <a.Icone className="h-3 w-3" /> {a.rotulo}
    </span>
  );
}

export function AuditoriaPage() {
  const [texto, setTexto] = useState('');
  const [entidade, setEntidade] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [selecionado, setSelecionado] = useState<RegistroAuditoria | null>(null);
  const textoDebounced = useDebounce(texto, 400);

  const filtro = useMemo<AuditoriaFiltro>(
    () => ({
      texto: textoDebounced.trim() || undefined,
      entidade: entidade || undefined,
      de: de ? paraUtcDeLocal(`${de}T00:00`) ?? undefined : undefined,
      ate: ate ? paraUtcDeLocal(`${ate}T23:59`) ?? undefined : undefined,
      tamanho: 100,
    }),
    [textoDebounced, entidade, de, ate],
  );

  const q = useBuscarAuditoria(filtro);

  const colunas: Coluna<RegistroAuditoria>[] = useMemo(
    () => [
      { chave: 'data', cabecalho: 'Data/hora', render: (r) => formatarDataHora(r.criadoEm) },
      {
        chave: 'usuario',
        cabecalho: 'Usuário',
        render: (r) => (
          <span className={r.usuarioNome ? 'font-medium text-gray-900' : 'text-gray-400'}>
            {r.usuarioNome ?? 'Não identificado'}
          </span>
        ),
      },
      { chave: 'ator', cabecalho: 'Origem', render: (r) => <BadgeAtor tipo={r.atorTipo} /> },
      {
        chave: 'entidade',
        cabecalho: 'Registro afetado',
        render: (r) => (
          <div>
            <div className="text-gray-900">{r.entidadeNome ?? r.entidade}</div>
            {r.entidadeNome ? <div className="text-xs text-gray-400">{r.entidade}</div> : null}
          </div>
        ),
      },
      { chave: 'acao', cabecalho: 'Ação', render: (r) => ACAO_LABEL[r.acao] ?? r.acao },
      {
        chave: 'para',
        cabecalho: 'Novo valor',
        render: (r) => <span className="font-medium text-gray-900">{r.valorNovo ?? '—'}</span>,
      },
    ],
    [],
  );

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <ScrollText className="mt-1 h-6 w-6 text-red-600" />
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Auditoria</h1>
          <p className="mt-1 text-sm text-gray-600">
            Trilha de alterações (somente leitura). Quem alterou, o que mudou, de onde (IP) e quando.
            Clique numa linha para os detalhes.
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
            placeholder="Buscar por usuário, valor ou ação…"
            className="pl-9"
          />
        </div>
        <select value={entidade} onChange={(e) => setEntidade(e.target.value)} className="input" aria-label="Entidade">
          {ENTIDADES.map((o) => (
            <option key={o.valor} value={o.valor}>
              {o.rotulo}
            </option>
          ))}
        </select>
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
        chaveLinha={(r) => r.id}
        carregando={q.isLoading}
        vazio="Nenhum registro de auditoria encontrado."
        aoClicarLinha={(r) => setSelecionado(r)}
      />

      {q.data ? (
        <p className="text-xs text-gray-500">
          {q.data.total} registro(s)
          {q.data.total > q.data.itens.length ? ` · exibindo os ${q.data.itens.length} mais recentes` : ''}.
        </p>
      ) : null}

      {selecionado ? <ModalDetalhe registro={selecionado} aoFechar={() => setSelecionado(null)} /> : null}
    </div>
  );
}

function ModalDetalhe({ registro, aoFechar }: { registro: RegistroAuditoria; aoFechar: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={aoFechar}>
      <div className="w-full max-w-lg rounded-lg bg-white shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
          <h3 className="text-sm font-semibold text-gray-900">Detalhe da alteração</h3>
          <button type="button" onClick={aoFechar} className="rounded p-1 text-gray-500 hover:bg-gray-100">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="space-y-3 px-4 py-4 text-sm">
          <div className="flex items-center gap-2">
            <BadgeAtor tipo={registro.atorTipo} />
            <span className="font-medium text-gray-900">{registro.usuarioNome ?? 'Não identificado'}</span>
          </div>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2">
            <Campo rotulo="Quando" valor={formatarDataHora(registro.criadoEm)} />
            <Campo rotulo="IP de origem" valor={registro.ip} />
            <Campo rotulo="Ação" valor={ACAO_LABEL[registro.acao] ?? registro.acao} />
            <Campo rotulo="Tipo de registro" valor={registro.entidade} />
            <div className="col-span-2 flex items-start gap-1.5">
              {registro.entidade === 'Paciente' ? (
                <Building2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-gray-400" />
              ) : null}
              <Campo rotulo="Registro afetado" valor={registro.entidadeNome ?? registro.entidadeId} />
            </div>
          </dl>
          <div className="rounded-md border border-gray-200">
            <div className="border-b border-gray-100 px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-gray-400">Valor anterior</div>
              <div className="break-words text-gray-500">{registro.valorAnterior ?? '—'}</div>
            </div>
            <div className="px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-gray-400">Novo valor</div>
              <div className="break-words font-medium text-gray-900">{registro.valorNovo ?? '—'}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function Campo({ rotulo, valor }: { rotulo: string; valor: string | null | undefined }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-400">{rotulo}</dt>
      <dd className="break-words text-gray-900">{valor || '—'}</dd>
    </div>
  );
}
