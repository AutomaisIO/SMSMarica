import { useEffect, useState } from 'react';
import { Edit2, Loader2, Plus, ScanLine, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useListarUnidades } from '@/features/unidades/api/queries';
import {
  useAtualizarEquipamento,
  useCadastrarEquipamento,
  useExcluirEquipamento,
  useListarEquipamentos,
} from '@/features/equipamentos/api/queries';
import {
  MODALIDADES,
  rotuloModalidade,
  type EquipamentoListItem,
  type ModalidadeDicom,
} from '@/features/equipamentos/types';

type FormEquip = {
  id: string | null;
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string;
  ativo: boolean;
};

const FORM_VAZIO: FormEquip = {
  id: null,
  nome: '',
  unidadeId: '',
  modalidadeDicom: 'US',
  identificadorDicom: '',
  ativo: true,
};

export function EquipamentosPage() {
  const [incluirInativos, setIncluirInativos] = useState(false);
  const lista = useListarEquipamentos(undefined, incluirInativos);
  const unidades = useListarUnidades();
  const cadastrar = useCadastrarEquipamento();
  const atualizar = useAtualizarEquipamento();
  const excluir = useExcluirEquipamento();

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState<FormEquip>(FORM_VAZIO);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!modalAberto) setErro(null);
  }, [modalAberto]);

  function abrirNovo() {
    setForm(FORM_VAZIO);
    setModalAberto(true);
  }

  function abrirEdicao(e: EquipamentoListItem) {
    setForm({
      id: e.id,
      nome: e.nome,
      unidadeId: e.unidadeId,
      modalidadeDicom: e.modalidadeDicom,
      identificadorDicom: '',
      ativo: e.ativo,
    });
    setModalAberto(true);
  }

  function aoSalvar(ev: React.FormEvent) {
    ev.preventDefault();
    setErro(null);
    if (!form.unidadeId) {
      setErro('Selecione a unidade.');
      return;
    }
    const payload = {
      nome: form.nome.trim(),
      unidadeId: form.unidadeId,
      modalidadeDicom: form.modalidadeDicom,
      identificadorDicom: form.identificadorDicom.trim() || null,
      ativo: form.ativo,
    };
    const opcoes = {
      onSuccess: () => setModalAberto(false),
      onError: (err: unknown) => setErro(extrairMensagemDeErro(err)),
    };
    if (form.id) atualizar.mutate({ id: form.id, payload }, opcoes);
    else cadastrar.mutate(payload, opcoes);
  }

  function aoExcluir(e: EquipamentoListItem) {
    if (!window.confirm(`Excluir o equipamento "${e.nome}"?`)) return;
    excluir.mutate(e.id, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<EquipamentoListItem>[] = [
    { chave: 'nome', cabecalho: 'Equipamento', render: (e) => <span className="font-medium text-gray-900">{e.nome}</span> },
    { chave: 'unidade', cabecalho: 'Unidade', render: (e) => e.unidadeNome },
    { chave: 'modalidade', cabecalho: 'Modalidade', render: (e) => rotuloModalidade(e.modalidadeDicom) },
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
            <ScanLine className="h-6 w-6 text-primary-600" />
            Equipamentos
          </h1>
          <p className="mt-1 text-sm text-gray-600">Equipamentos das unidades, agendáveis para exames de imagem.</p>
        </div>
        <Button onClick={abrirNovo}>
          <Plus className="mr-2 h-4 w-4" />
          Novo equipamento
        </Button>
      </header>

      <label className="flex items-center gap-2 text-sm text-gray-600">
        <input type="checkbox" checked={incluirInativos} onChange={(e) => setIncluirInativos(e.target.checked)} />
        Incluir inativos
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
        vazio="Nenhum equipamento cadastrado."
      />

      <Modal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        titulo={form.id ? 'Editar equipamento' : 'Novo equipamento'}
      >
        <form onSubmit={aoSalvar} className="space-y-4">
          <Campo label="Nome" htmlFor="eq-nome" required>
            <Input
              id="eq-nome"
              value={form.nome}
              onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))}
              placeholder="Ex.: Ultrassom Sala 2"
              autoFocus
              required
            />
          </Campo>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Unidade" htmlFor="eq-unidade" required>
              <Select
                id="eq-unidade"
                value={form.unidadeId}
                onChange={(e) => setForm((f) => ({ ...f, unidadeId: e.target.value }))}
              >
                <option value="">Selecione…</option>
                {(unidades.data ?? [])
                  .filter((u) => u.ativo)
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.nome}
                    </option>
                  ))}
              </Select>
            </Campo>

            <Campo label="Modalidade" htmlFor="eq-modalidade" required>
              <Select
                id="eq-modalidade"
                value={form.modalidadeDicom}
                onChange={(e) => setForm((f) => ({ ...f, modalidadeDicom: e.target.value as ModalidadeDicom }))}
              >
                {MODALIDADES.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.rotulo}
                  </option>
                ))}
              </Select>
            </Campo>
          </div>

          <Campo label="Identificador DICOM" htmlFor="eq-dicom" dica="Opcional — AE Title / Station para o worklist.">
            <Input
              id="eq-dicom"
              value={form.identificadorDicom}
              onChange={(e) => setForm((f) => ({ ...f, identificadorDicom: e.target.value }))}
              placeholder="Ex.: US_SALA2"
            />
          </Campo>

          {form.id ? (
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={form.ativo}
                onChange={(e) => setForm((f) => ({ ...f, ativo: e.target.checked }))}
              />
              Ativo
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
