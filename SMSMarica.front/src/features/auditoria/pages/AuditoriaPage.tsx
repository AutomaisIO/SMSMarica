import { useEffect, useMemo, useState } from 'react';
import { ScrollText, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { paraUtcDeLocal } from '@/shared/lib/datas';
import { Input } from '@/shared/ui/Input';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscarAuditoria } from '@/features/auditoria/api/queries';
import type { AuditoriaFiltro, RegistroAuditoria } from '@/features/auditoria/types';

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

/** Rótulos amigáveis das ações auditadas. Cai no valor cru quando desconhecido. */
const ACAO_LABEL: Record<string, string> = {
  AlteracaoNome: 'Alteração de nome',
};

/** Entidades que podem ser filtradas (cresce conforme a auditoria se espalha). */
const ENTIDADES = [
  { valor: '', rotulo: 'Todas as entidades' },
  { valor: 'Paciente', rotulo: 'Paciente' },
];

export function AuditoriaPage() {
  const [texto, setTexto] = useState('');
  const [entidade, setEntidade] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const textoDebounced = useDebounce(texto, 400);

  const filtro = useMemo<AuditoriaFiltro>(
    () => ({
      texto: textoDebounced.trim() || undefined,
      entidade: entidade || undefined,
      // Limites do dia em Brasília → UTC (as datas de auditoria são timestamptz/UTC).
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
      { chave: 'usuario', cabecalho: 'Usuário', render: (r) => r.usuarioNome ?? '—' },
      { chave: 'entidade', cabecalho: 'Entidade', render: (r) => r.entidade },
      { chave: 'acao', cabecalho: 'Ação', render: (r) => ACAO_LABEL[r.acao] ?? r.acao },
      {
        chave: 'de',
        cabecalho: 'De',
        render: (r) => <span className="text-gray-500">{r.valorAnterior ?? '—'}</span>,
      },
      {
        chave: 'para',
        cabecalho: 'Para',
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
            Trilha de alterações do sistema (somente leitura). Registra quem alterou o quê,
            de qual valor para qual, e quando.
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
        <select
          value={entidade}
          onChange={(e) => setEntidade(e.target.value)}
          className="input"
          aria-label="Entidade"
        >
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
      />

      {q.data ? (
        <p className="text-xs text-gray-500">
          {q.data.total} registro(s){q.data.total > q.data.itens.length ? ` · exibindo os ${q.data.itens.length} mais recentes` : ''}.
        </p>
      ) : null}
    </div>
  );
}
