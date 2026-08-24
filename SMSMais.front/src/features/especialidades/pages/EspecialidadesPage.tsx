import { useEffect, useState } from 'react';
import { Edit2, Loader2, Plus, Stethoscope, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useAtualizarEspecialidade,
  useCadastrarEspecialidade,
  useExcluirEspecialidade,
  useListarEspecialidades,
} from '@/features/especialidades/api/queries';
import type { EspecialidadeListItem } from '@/features/especialidades/types';

type FormEspecialidade = { id: string | null; nome: string; codigoCbo: string; ativo: boolean };

const FORM_VAZIO: FormEspecialidade = { id: null, nome: '', codigoCbo: '', ativo: true };

export function EspecialidadesPage() {
  const [incluirInativas, setIncluirInativas] = useState(false);
  const lista = useListarEspecialidades(incluirInativas);
  const cadastrar = useCadastrarEspecialidade();
  const atualizar = useAtualizarEspecialidade();
  const excluir = useExcluirEspecialidade();

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState<FormEspecialidade>(FORM_VAZIO);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!modalAberto) setErro(null);
  }, [modalAberto]);

  function abrirNova() {
    setForm(FORM_VAZIO);
    setModalAberto(true);
  }

  function abrirEdicao(e: EspecialidadeListItem) {
    setForm({ id: e.id, nome: e.nome, codigoCbo: e.codigoCbo ?? '', ativo: e.ativo });
    setModalAberto(true);
  }

  function aoSalvar(ev: React.FormEvent) {
    ev.preventDefault();
    setErro(null);
    const payload = { nome: form.nome.trim(), codigoCbo: form.codigoCbo.trim() || null, ativo: form.ativo };
    const opcoes = {
      onSuccess: () => setModalAberto(false),
      onError: (err: unknown) => setErro(extrairMensagemDeErro(err)),
    };
    if (form.id) atualizar.mutate({ id: form.id, payload }, opcoes);
    else cadastrar.mutate(payload, opcoes);
  }

  function aoExcluir(e: EspecialidadeListItem) {
    if (!window.confirm(`Excluir a especialidade "${e.nome}"?`)) return;
    excluir.mutate(e.id, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<EspecialidadeListItem>[] = [
    { chave: 'nome', cabecalho: 'Especialidade', render: (e) => <span className="font-medium text-gray-900">{e.nome}</span> },
    { chave: 'cbo', cabecalho: 'CBO', render: (e) => e.codigoCbo ?? '—' },
    { chave: 'status', cabecalho: 'Status', render: (e) => <StatusBadge ativo={e.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (e) => (
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => abrirEdicao(e)}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Edit2 className="h-3.5 w-3.5" />
            Editar
          </button>
          <button
            type="button"
            onClick={() => aoExcluir(e)}
            className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
          >
            <Trash2 className="h-3.5 w-3.5" />
            Excluir
          </button>
        </div>
      ),
    },
  ];

  const salvando = cadastrar.isPending || atualizar.isPending;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <Stethoscope className="h-6 w-6 text-primary-600" />
            Especialidades
          </h1>
          <p className="mt-1 text-sm text-gray-600">Lista controlada de especialidades médicas usada nas agendas.</p>
        </div>
        <Button onClick={abrirNova}>
          <Plus className="mr-2 h-4 w-4" />
          Nova especialidade
        </Button>
      </header>

      <label className="flex items-center gap-2 text-sm text-gray-600">
        <input type="checkbox" checked={incluirInativas} onChange={(e) => setIncluirInativas(e.target.checked)} />
        Incluir inativas
      </label>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(e) => e.id}
        carregando={lista.isPending}
        vazio="Nenhuma especialidade cadastrada."
      />

      <Modal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        titulo={form.id ? 'Editar especialidade' : 'Nova especialidade'}
      >
        <form onSubmit={aoSalvar} className="space-y-4">
          <Campo label="Nome" htmlFor="esp-nome" required>
            <Input
              id="esp-nome"
              value={form.nome}
              onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))}
              placeholder="Ex.: Cardiologia"
              autoFocus
              required
            />
          </Campo>
          <Campo label="Código CBO" htmlFor="esp-cbo" dica="Opcional.">
            <Input
              id="esp-cbo"
              value={form.codigoCbo}
              onChange={(e) => setForm((f) => ({ ...f, codigoCbo: e.target.value }))}
              placeholder="Ex.: 225125"
            />
          </Campo>
          {form.id ? (
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={form.ativo}
                onChange={(e) => setForm((f) => ({ ...f, ativo: e.target.checked }))}
              />
              Ativa
            </label>
          ) : null}

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex items-center justify-end gap-3 pt-2">
            <Button type="button" variante="secundaria" onClick={() => setModalAberto(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={salvando || !form.nome.trim()}>
              {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
