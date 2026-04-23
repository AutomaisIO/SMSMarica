import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarUsuario,
  useCadastrarUsuario,
  useUsuarioPorId,
} from '@/features/usuarios/api/queries';
import {
  PerfilUsuario,
  rotulosPerfil,
  type PerfilUsuarioValor,
} from '@/features/usuarios/types';

type Props = { modo: 'criar' | 'editar'; idUsuario?: string | null; aoConcluir: () => void };

type Valores = {
  nomeCompleto: string;
  email: string;
  cpf: string;
  perfil: PerfilUsuarioValor;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  email: '',
  cpf: '',
  perfil: PerfilUsuario.Operador,
};

type Erros = Partial<Record<keyof Valores, string>>;

const perfisDisponiveis: PerfilUsuarioValor[] = [
  PerfilUsuario.Operador,
  PerfilUsuario.Gestor,
  PerfilUsuario.Paciente,
  PerfilUsuario.Motorista,
];

export function FormularioUsuario({ modo, idUsuario, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarUsuario();
  const atualizar = useAtualizarUsuario();
  const detalhe = useUsuarioPorId(modo === 'editar' ? idUsuario ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        email: detalhe.data.email,
        cpf: detalhe.data.cpf ?? '',
        perfil: detalhe.data.perfil,
      });
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const ne: Erros = {};
    const nome = valores.nomeCompleto.trim();
    const email = valores.email.trim();
    const cpf = valores.cpf.trim();

    if (nome.length < 3) ne.nomeCompleto = 'Nome obrigatório (mínimo 3 caracteres).';
    if (modo === 'criar') {
      if (!/^\S+@\S+\.\S+$/.test(email)) ne.email = 'E-mail inválido.';
    }
    if (cpf && cpf.replace(/\D/g, '').length !== 11) ne.cpf = 'CPF precisa ter 11 dígitos.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }

    try {
      if (modo === 'criar') {
        await cadastrar.mutateAsync({
          nomeCompleto: nome,
          email,
          cpf: cpf || undefined,
          perfil: valores.perfil,
        });
      } else {
        if (!idUsuario) throw new Error('ID ausente.');
        await atualizar.mutateAsync({
          id: idUsuario,
          payload: { nomeCompleto: nome, cpf: cpf || undefined, perfil: valores.perfil },
        });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-4">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo
          label="Nome completo"
          htmlFor="nomeCompleto"
          erro={erros.nomeCompleto}
          className="md:col-span-2"
        >
          <Input
            id="nomeCompleto"
            value={valores.nomeCompleto}
            onChange={(e) => set('nomeCompleto', e.target.value)}
            required
          />
        </Campo>

        <Campo
          label="E-mail"
          htmlFor="email"
          erro={erros.email}
          dica={modo === 'editar' ? 'E-mail não pode ser alterado.' : undefined}
        >
          <Input
            id="email"
            type="email"
            value={valores.email}
            onChange={(e) => set('email', e.target.value)}
            required={modo === 'criar'}
            disabled={modo === 'editar'}
          />
        </Campo>

        <Campo label="CPF (opcional)" htmlFor="cpf" erro={erros.cpf}>
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => set('cpf', e.target.value)}
            inputMode="numeric"
            placeholder="00000000000"
          />
        </Campo>

        <Campo label="Perfil" htmlFor="perfil" erro={erros.perfil} className="md:col-span-2">
          <Select
            id="perfil"
            value={valores.perfil}
            onChange={(e) => set('perfil', Number(e.target.value) as PerfilUsuarioValor)}
          >
            {perfisDisponiveis.map((p) => (
              <option key={p} value={p}>
                {rotulosPerfil[p]}
              </option>
            ))}
          </Select>
        </Campo>
      </div>

      {erroGlobal ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroGlobal}
        </div>
      ) : null}

      <div className="flex items-center justify-end gap-3 pt-2">
        <Button type="button" variante="ghost" onClick={aoConcluir} disabled={pendente}>
          Cancelar
        </Button>
        <Button type="submit" disabled={pendente}>
          {pendente ? 'Salvando…' : modo === 'criar' ? 'Cadastrar' : 'Salvar alterações'}
        </Button>
      </div>
    </form>
  );
}
