import { useState } from 'react';
import { Check, Inbox, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useIgnorarPendencia,
  useListarPendencias,
  useResolverPendencia,
} from '@/features/pendencias-cadastro/api/queries';
import type {
  PendenciaCadastro,
  StatusPendenciaCadastro,
  VinculoContato,
} from '@/features/pendencias-cadastro/types';

const VINCULO_ROTULO: Record<VinculoContato, string> = {
  NaoInformado: 'Não informado',
  Parente: 'Parente',
  Responsavel: 'Responsável',
  SemVinculo: 'Sem vínculo (engano)',
};

const STATUS_ESTILO: Record<StatusPendenciaCadastro, string> = {
  Aberta: 'bg-amber-100 text-amber-700',
  Resolvida: 'bg-green-100 text-green-700',
  Ignorada: 'bg-gray-100 text-gray-600',
};

const FILTROS: { valor: StatusPendenciaCadastro | 'todas'; rotulo: string }[] = [
  { valor: 'Aberta', rotulo: 'Abertas' },
  { valor: 'Resolvida', rotulo: 'Resolvidas' },
  { valor: 'Ignorada', rotulo: 'Ignoradas' },
  { valor: 'todas', rotulo: 'Todas' },
];

export function PendenciasCadastroPage() {
  const [filtro, setFiltro] = useState<StatusPendenciaCadastro | 'todas'>('Aberta');
  const lista = useListarPendencias(filtro === 'todas' ? undefined : filtro);
  const resolver = useResolverPendencia();
  const ignorar = useIgnorarPendencia();

  function aoResolver(p: PendenciaCadastro) {
    const nota = window.prompt('Nota (opcional) sobre como o cadastro foi ajustado:', '');
    if (nota === null) return;
    resolver.mutate({ id: p.id, nota: nota.trim() || null }, { onError: (e) => window.alert(extrairMensagemDeErro(e)) });
  }

  function aoIgnorar(p: PendenciaCadastro) {
    const nota = window.prompt('Motivo de ignorar (opcional):', '');
    if (nota === null) return;
    ignorar.mutate({ id: p.id, nota: nota.trim() || null }, { onError: (e) => window.alert(extrairMensagemDeErro(e)) });
  }

  const colunas: Coluna<PendenciaCadastro>[] = [
    {
      chave: 'contato',
      cabecalho: 'Contato',
      render: (p) => <span className="font-medium text-gray-900">{p.telefoneCanonical}</span>,
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente do cadastro',
      render: (p) =>
        p.pacienteId ? (
          <div>
            <span className="text-gray-900">{p.pacienteNome ?? '—'}</span>
            {p.pacienteCpf ? <p className="text-xs text-gray-500">CPF {p.pacienteCpf}</p> : null}
          </div>
        ) : (
          <span className="text-gray-400">—</span>
        ),
    },
    { chave: 'vinculo', cabecalho: 'Vínculo', render: (p) => VINCULO_ROTULO[p.vinculo] },
    {
      chave: 'origem',
      cabecalho: 'Origem',
      render: (p) => (p.criadaPeloRobo ? '🤖 Robô' : 'Manual'),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (p) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_ESTILO[p.status]}`}>{p.status}</span>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (p) =>
        p.status === 'Aberta' ? (
          <div className="flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => aoResolver(p)}
              className="inline-flex items-center gap-1 rounded-md border border-green-300 bg-green-50 px-2.5 py-1 text-xs font-medium text-green-700 hover:bg-green-100"
            >
              <Check className="h-3.5 w-3.5" />
              Resolver
            </button>
            <button
              type="button"
              onClick={() => aoIgnorar(p)}
              className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50"
            >
              <X className="h-3.5 w-3.5" />
              Ignorar
            </button>
          </div>
        ) : p.resolucaoNota ? (
          <span className="text-xs text-gray-500">{p.resolucaoNota}</span>
        ) : null,
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Inbox className="h-6 w-6 text-primary-600" />
          Pendências de Cadastro
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Casos em que o cidadão avisou que o número não é dele. O robô só registra o vínculo — ajuste o cadastro aqui.
        </p>
      </header>

      <div className="flex flex-wrap gap-1.5">
        {FILTROS.map((f) => (
          <button
            key={f.valor}
            type="button"
            onClick={() => setFiltro(f.valor)}
            className={
              'rounded-md border px-3 py-1 text-sm font-medium ' +
              (filtro === f.valor
                ? 'border-primary-300 bg-primary-50 text-primary-700'
                : 'border-gray-300 bg-white text-gray-600 hover:bg-gray-50')
            }
          >
            {f.rotulo}
          </button>
        ))}
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(p) => p.id}
        carregando={lista.isPending}
        vazio="Nenhuma pendência."
      />
    </div>
  );
}
