import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { UploadFoto } from '@/shared/ui/UploadFoto';
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
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
  perfil: PerfilUsuarioValor;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  email: '',
  cpf: '',
  telefone: '',
  endereco: enderecoVazio,
  fotoBase64: null,
  perfil: PerfilUsuario.Operador,
};

type Erros = Partial<Record<'nomeCompleto' | 'email' | 'cpf' | 'telefone' | 'perfil', string>>;

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
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        email: detalhe.data.email,
        cpf: detalhe.data.cpf ?? '',
        telefone: detalhe.data.telefone ?? '',
        fotoBase64: detalhe.data.fotoBase64 ?? null,
        perfil: detalhe.data.perfil,
        endereco: e
          ? {
              cep: e.cep ?? '',
              logradouro: e.logradouro ?? '',
              numero: e.numero ?? '',
              complemento: e.complemento ?? '',
              bairro: e.bairro ?? '',
              cidade: e.cidade ?? '',
              uf: e.uf ?? '',
              pontoReferencia: e.pontoReferencia ?? '',
            }
          : enderecoVazio,
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

    const enderecoForm = paraPayload(valores.endereco);
    const enderecoPayload = enderecoForm
      ? {
          cep: enderecoForm.cep,
          logradouro: enderecoForm.logradouro,
          numero: enderecoForm.numero || null,
          complemento: enderecoForm.complemento || null,
          bairro: enderecoForm.bairro,
          cidade: enderecoForm.cidade,
          uf: enderecoForm.uf,
          pontoReferencia: enderecoForm.pontoReferencia || null,
        }
      : null;

    try {
      if (modo === 'criar') {
        await cadastrar.mutateAsync({
          nomeCompleto: nome,
          email,
          cpf: cpf || undefined,
          telefone: valores.telefone || undefined,
          endereco: enderecoPayload,
          fotoBase64: valores.fotoBase64,
          perfil: valores.perfil,
        });
      } else {
        if (!idUsuario) throw new Error('ID ausente.');
        await atualizar.mutateAsync({
          id: idUsuario,
          payload: {
            nomeCompleto: nome,
            cpf: cpf || undefined,
            telefone: valores.telefone || undefined,
            endereco: enderecoPayload,
            fotoBase64: valores.fotoBase64,
            perfil: valores.perfil,
          },
        });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-5">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <UploadFoto
        valor={valores.fotoBase64}
        aoMudar={(v) => set('fotoBase64', v)}
        nome={valores.nomeCompleto || undefined}
        desabilitado={pendente}
      />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo
          label="Nome completo"
          htmlFor="nomeCompleto"
          erro={erros.nomeCompleto}
          required
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
          required={modo === 'criar'}
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

        <Campo label="CPF" htmlFor="cpf" erro={erros.cpf}>
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => set('cpf', e.target.value)}
            inputMode="numeric"
            placeholder="00000000000"
          />
        </Campo>

        <Campo label="Telefone" htmlFor="telefone" erro={erros.telefone}>
          <Input
            id="telefone"
            value={valores.telefone}
            onChange={(e) => set('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
          />
        </Campo>

        <Campo label="Perfil" htmlFor="perfil" erro={erros.perfil} required>
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

      <section>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Endereço</h3>
        <FormularioEndereco
          valor={valores.endereco}
          aoMudar={(e) => set('endereco', e)}
          desabilitado={pendente}
        />
      </section>

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
