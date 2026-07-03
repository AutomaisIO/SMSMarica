import { useState, type FormEvent } from 'react';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Avatar } from '@/shared/ui/Avatar';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarUsuario,
  useListarUsuarios,
} from '@/features/usuarios/api/queries';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { FormularioUsuario } from '@/features/usuarios/components/FormularioUsuario';
import { type FiltroUsuarios, type UsuarioListItem } from '@/features/usuarios/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

const LIMITE = 50;

function formatarCpf(cpf: string | null): string {
  if (!cpf) return '—';
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

export function UsuariosPage() {
  const [filtroDigitado, setFiltroDigitado] = useState<FiltroUsuarios>({ limite: LIMITE });
  const [filtroAplicado, setFiltroAplicado] = useState<FiltroUsuarios>({ limite: LIMITE });
  const lista = useListarUsuarios(filtroAplicado);
  const unidades = useListarUnidades();
  const desativar = useDesativarUsuario();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<UsuarioListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    setFiltroAplicado(filtroDigitado);
  }

  function limpar() {
    const vazio = { limite: LIMITE };
    setFiltroDigitado(vazio);
    setFiltroAplicado(vazio);
  }

  const resultados = lista.data ?? [];
  const atingiuLimite = resultados.length >= (filtroAplicado.limite ?? LIMITE);

  const colunas: Coluna<UsuarioListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (u) => (
        <div className="flex items-center gap-2">
          <Avatar src={u.fotoBase64} nome={u.nomeCompleto} tamanho="sm" />
          <span className="text-gray-900">{u.nomeCompleto}</span>
        </div>
      ),
    },
    { chave: 'cpf', cabecalho: 'CPF', render: (u) => formatarCpf(u.cpf) },
    { chave: 'email', cabecalho: 'E-mail', render: (u) => u.email ?? '—' },
    { chave: 'status', cabecalho: 'Status', render: (u) => <StatusBadge ativo={u.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (u) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: u.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {u.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(u)}>
              <Trash2 className="w-3.5 h-3.5" /> Excluir
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function confirmarDesativar() {
    if (!paraDesativar) return;
    setErroAcao(null);
    try {
      await desativar.mutateAsync(paraDesativar.id);
      setParaDesativar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Usuários</h1>
          <p className="mt-1 text-sm text-gray-600">Contas com acesso ao ecossistema SMS Maricá.</p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo usuário
        </Button>
      </header>

      <form
        onSubmit={aoBuscar}
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-[1fr_16rem_auto]"
      >
        <Campo label="Buscar" htmlFor="busca">
          <Input
            id="busca"
            value={filtroDigitado.busca ?? ''}
            onChange={(e) => setFiltroDigitado((f) => ({ ...f, busca: e.target.value }))}
            placeholder="Nome ou CPF"
          />
        </Campo>
        <Campo label="Unidade" htmlFor="unidade">
          <Select
            id="unidade"
            value={filtroDigitado.unidadeId ?? ''}
            onChange={(e) =>
              setFiltroDigitado((f) => ({ ...f, unidadeId: e.target.value || undefined }))
            }
          >
            <option value="">Todas</option>
            {(unidades.data ?? [])
              .filter((u) => u.ativo)
              .map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                </option>
              ))}
          </Select>
        </Campo>
        <div className="flex items-end gap-2">
          <Button type="submit">
            <Search className="h-4 w-4" /> Buscar
          </Button>
          {(filtroAplicado.busca || filtroAplicado.unidadeId) ? (
            <Button type="button" variante="ghost" onClick={limpar}>
              Limpar
            </Button>
          ) : null}
        </div>
      </form>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={resultados}
        chaveLinha={(u) => u.id}
        carregando={lista.isLoading}
      />

      {atingiuLimite ? (
        <p className="text-center text-xs text-gray-500">
          Mostrando os primeiros {filtroAplicado.limite ?? LIMITE} resultados. Refine a busca ou
          filtre por unidade para encontrar um usuário específico.
        </p>
      ) : null}

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo usuário' : 'Editar usuário'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioUsuario
            modo={estado.tipo}
            idUsuario={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir usuário"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nomeCompleto}"? A conta some das listagens e deixa de poder autenticar.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Excluir"
        carregando={desativar.isPending}
        aoConfirmar={confirmarDesativar}
        aoCancelar={() => {
          setParaDesativar(null);
          setErroAcao(null);
        }}
      />

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}
    </div>
  );
}
