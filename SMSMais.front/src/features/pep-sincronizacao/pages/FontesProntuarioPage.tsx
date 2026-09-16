import { useState } from 'react';
import { Database, Edit2, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { FonteConfigModal } from '@/features/ia/components/FonteConfigModal';
import { useFontesConfig, useRemoverFonteConfig } from '@/features/ia/api/queries';
import type { FonteConfig } from '@/features/ia/types';

/**
 * Fontes / Conectores de prontuário — sob "Importar Prontuários".
 *
 * Reúne, num único lugar, TODAS as formas de trazer prontuário para o hub: bases por banco
 * direto/agente (Salux, Klinikos-SQL) e o caminho novo "como usuário" via web (KlinikosWeb).
 * Tudo é uma `IaFonte`; o conector web NÃO é uma "integração de serviço" (não fica em
 * Integrações → Credenciais). Reusa o mesmo modal/endpoint de fontes do módulo de IA.
 */
export function FontesProntuarioPage() {
  const fontes = useFontesConfig();
  const remover = useRemoverFonteConfig();

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<FonteConfig | undefined>(undefined);
  const [erro, setErro] = useState<string | null>(null);

  function abrirNova() {
    setEmEdicao(undefined);
    setErro(null);
    setModalAberto(true);
  }

  function abrirEdicao(fonte: FonteConfig) {
    setEmEdicao(fonte);
    setErro(null);
    setModalAberto(true);
  }

  function aoRemover(fonte: FonteConfig) {
    if (!window.confirm(`Remover a fonte "${fonte.nome}"?`)) return;
    setErro(null);
    remover.mutate(fonte.id, { onError: (err) => setErro(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<FonteConfig>[] = [
    {
      chave: 'nome',
      cabecalho: 'Fonte',
      render: (f) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{f.nome}</div>
          <div className="truncate text-xs text-gray-500">
            {f.tipo}
            {f.slug ? ` · ${f.slug}` : ''}
            {f.familia ? ` · família ${f.familia}` : ''}
            {f.viaAgente ? ' · via agente' : ''}
          </div>
        </div>
      ),
    },
    {
      chave: 'acesso',
      cabecalho: 'Acesso',
      render: (f) => {
        const web = f.tipo === 'KlinikosWeb';
        return (
          <span
            className={`rounded px-2 py-0.5 text-xs font-medium uppercase ${
              web ? 'bg-emerald-100 text-emerald-700' : 'bg-gray-100 text-gray-700'
            }`}
          >
            {web ? 'web (como usuário)' : f.viaAgente ? 'agente' : 'banco'}
          </span>
        );
      },
    },
    {
      chave: 'ambiente',
      cabecalho: 'Ambiente',
      render: (f) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium uppercase text-gray-700">
          {f.ambiente}
        </span>
      ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (f) => <StatusBadge ativo={f.ativo} />,
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (f) => (
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => abrirEdicao(f)}
            title="Editar"
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Edit2 className="h-3.5 w-3.5" />
            Editar
          </button>
          <button
            type="button"
            onClick={() => aoRemover(f)}
            title="Remover"
            className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
          >
            <Trash2 className="h-3.5 w-3.5" />
            Remover
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <section className="space-y-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
              <Database className="h-5 w-5 text-primary-600" />
              Fontes / Conectores de prontuário
            </h2>
            <p className="mt-0.5 text-sm text-gray-500">
              De onde os prontuários são trazidos para o hub: banco direto, agente, ou o caminho
              novo "como usuário" via web (Klinikos). A senha é write-only e nunca é exibida.
            </p>
          </div>
          <Button onClick={abrirNova}>
            <Plus className="mr-2 h-4 w-4" />
            Nova fonte
          </Button>
        </div>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        {fontes.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(fontes.error)}
          </div>
        ) : null}

        <Tabela
          colunas={colunas}
          dados={fontes.data ?? []}
          chaveLinha={(f) => f.id}
          carregando={fontes.isPending}
          vazio="Nenhuma fonte cadastrada."
        />
      </section>

      <FonteConfigModal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        fonte={emEdicao}
      />
    </div>
  );
}
