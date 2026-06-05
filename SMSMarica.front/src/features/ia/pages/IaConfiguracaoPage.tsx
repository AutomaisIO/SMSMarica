import { useEffect, useState } from 'react';
import { Database, Edit2, Loader2, Plus, Settings2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { FonteConfigModal } from '@/features/ia/components/FonteConfigModal';
import {
  useAtualizarConfiguracaoIa,
  useConfiguracaoIa,
  useFontesConfig,
  useRemoverFonteConfig,
} from '@/features/ia/api/queries';
import type { AtualizarConfiguracaoPayload, FonteConfig } from '@/features/ia/types';

type FormConfig = {
  provedor: string;
  modelo: string;
  provedorEmbeddings: string;
  modeloEmbeddings: string;
  token: string;
  tokenEmbeddings: string;
};

export function IaConfiguracaoPage() {
  const config = useConfiguracaoIa();
  const salvar = useAtualizarConfiguracaoIa();
  const fontes = useFontesConfig();
  const remover = useRemoverFonteConfig();

  const [form, setForm] = useState<FormConfig>({
    provedor: '',
    modelo: '',
    provedorEmbeddings: '',
    modeloEmbeddings: '',
    token: '',
    tokenEmbeddings: '',
  });
  const [erroConfig, setErroConfig] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<FonteConfig | undefined>(undefined);
  const [erroFonte, setErroFonte] = useState<string | null>(null);

  useEffect(() => {
    if (config.data) {
      setForm((f) => ({
        ...f,
        provedor: config.data.provedor,
        modelo: config.data.modelo,
        provedorEmbeddings: config.data.provedorEmbeddings,
        modeloEmbeddings: config.data.modeloEmbeddings,
      }));
    }
  }, [config.data]);

  function set<K extends keyof FormConfig>(chave: K, valor: FormConfig[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
    setSalvo(false);
  }

  function aoSalvarConfig(e: React.FormEvent) {
    e.preventDefault();
    setErroConfig(null);
    const payload: AtualizarConfiguracaoPayload = {
      provedor: form.provedor.trim(),
      modelo: form.modelo.trim(),
      provedorEmbeddings: form.provedorEmbeddings.trim(),
      modeloEmbeddings: form.modeloEmbeddings.trim(),
      token: form.token ? form.token : undefined,
      tokenEmbeddings: form.tokenEmbeddings ? form.tokenEmbeddings : undefined,
    };
    salvar.mutate(payload, {
      onSuccess: () => {
        setSalvo(true);
        setForm((f) => ({ ...f, token: '', tokenEmbeddings: '' }));
      },
      onError: (err) => setErroConfig(extrairMensagemDeErro(err)),
    });
  }

  function abrirNova() {
    setEmEdicao(undefined);
    setModalAberto(true);
  }

  function abrirEdicao(fonte: FonteConfig) {
    setEmEdicao(fonte);
    setModalAberto(true);
  }

  function aoRemover(fonte: FonteConfig) {
    if (!window.confirm(`Remover a base "${fonte.nome}"?`)) return;
    setErroFonte(null);
    remover.mutate(fonte.id, { onError: (err) => setErroFonte(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<FonteConfig>[] = [
    {
      chave: 'nome',
      cabecalho: 'Base',
      render: (f) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{f.nome}</div>
          <div className="truncate text-xs text-gray-500">
            {f.tipo}
            {f.host ? ` · ${f.host}${f.porta ? `:${f.porta}` : ''}` : ''}
            {f.servico ? ` · ${f.servico}` : ''}
          </div>
        </div>
      ),
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
    <div className="space-y-8">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Settings2 className="h-6 w-6 text-primary-600" />
          Configuração IA
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Provedor de IA, modelos e bases de dados consultáveis.
        </p>
      </header>

      {/* ── Provedor de IA ───────────────────────────────────────────────── */}
      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-gray-900">Provedor de IA</h2>
        <p className="mt-0.5 text-sm text-gray-500">
          Credenciais e modelos usados para interpretar perguntas e gerar respostas. O token é
          gravado de forma segura e nunca exibido.
        </p>

        {config.isError ? (
          <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(config.error)}
          </div>
        ) : null}

        <form onSubmit={aoSalvarConfig} className="mt-4 space-y-5">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Provedor de IA" htmlFor="cfg-provedor">
              <Input
                id="cfg-provedor"
                value={form.provedor}
                onChange={(e) => set('provedor', e.target.value)}
                placeholder="Provedor"
              />
            </Campo>
            <Campo label="Modelo" htmlFor="cfg-modelo">
              <Input
                id="cfg-modelo"
                value={form.modelo}
                onChange={(e) => set('modelo', e.target.value)}
              />
            </Campo>
            <Campo
              label="Token de IA"
              htmlFor="cfg-token"
              className="sm:col-span-2"
              dica={
                config.data?.tokenDefinido
                  ? 'Já definido — preencha apenas para substituir.'
                  : 'Ainda não definido.'
              }
            >
              <Input
                id="cfg-token"
                type="password"
                value={form.token}
                onChange={(e) => set('token', e.target.value)}
                placeholder={config.data?.tokenDefinido ? '••••••••••••' : 'Cole o token aqui'}
                autoComplete="new-password"
              />
            </Campo>
          </div>

          {erroConfig ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroConfig}
            </div>
          ) : null}

          <div className="flex items-center justify-end gap-3">
            {salvo ? <span className="text-sm text-green-600">Configuração salva.</span> : null}
            <Button type="submit" disabled={salvar.isPending || config.isPending}>
              {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar configuração
            </Button>
          </div>
        </form>
      </section>

      {/* ── Bases de dados ───────────────────────────────────────────────── */}
      <section className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
              <Database className="h-5 w-5 text-primary-600" />
              Bases de dados
            </h2>
            <p className="mt-0.5 text-sm text-gray-500">
              Conexões consultáveis pela IA. A senha é write-only e nunca é exibida.
            </p>
          </div>
          <Button onClick={abrirNova}>
            <Plus className="mr-2 h-4 w-4" />
            Nova base
          </Button>
        </div>

        {erroFonte ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroFonte}
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
          vazio="Nenhuma base cadastrada."
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
