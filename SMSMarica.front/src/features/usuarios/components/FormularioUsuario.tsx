import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { DadosPessoaisCampos } from '@/shared/ui/DadosPessoaisCampos';
import { PermissoesSecao, matrizParaApi } from '@/shared/ui/PermissoesSecao';
import { SegurancaSecao } from '@/shared/ui/SegurancaSecao';
import { consultarCpf } from '@/shared/api/integracoes';
import { consultarUsuarioPorCpf } from '@/features/usuarios/api/usuariosApi';
import { buscarMedicos } from '@/features/medicos/api/medicosApi';
import { obterPacientePorCpf } from '@/features/pacientes/api/pacientesApi';
import {
  enderecoVazio,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarOverridesDoUsuario,
  useAtualizarPerfisDoUsuario,
  useAtualizarUsuario,
  useCadastrarUsuario,
  useUsuarioPermissoes,
  useUsuarioPorId,
} from '@/features/usuarios/api/queries';
import { paraMatriz } from '@/features/perfis/lib/acoes';
import type { MatrizEdicao } from '@/features/perfis/types';

type Props = { modo: 'criar' | 'editar'; idUsuario?: string | null; aoConcluir: () => void };

type Valores = {
  nomeCompleto: string;
  email: string;
  senha: string;
  cpf: string;
  dataNascimento: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
  deveTrocarSenha: boolean;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  email: '',
  senha: '',
  cpf: '',
  dataNascimento: '',
  telefone: '',
  endereco: enderecoVazio,
  fotoBase64: null,
  deveTrocarSenha: false,
};

type Erros = Partial<Record<'nomeCompleto' | 'email' | 'senha' | 'cpf' | 'dataNascimento' | 'telefone', string>>;

function formatarCpfDigitos(cpf: string): string {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

export function FormularioUsuario({ modo, idUsuario, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [perfilIdsSelecionados, setPerfilIdsSelecionados] = useState<string[]>([]);
  const [overrides, setOverrides] = useState<MatrizEdicao>({});
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);

  // Gate inicial em modo criar: CPF + nascimento + consulta Hub.
  const [passoCpfConcluido, setPassoCpfConcluido] = useState(modo === 'editar');
  const [consultandoCpf, setConsultandoCpf] = useState(false);

  const cadastrar = useCadastrarUsuario();
  const atualizar = useAtualizarUsuario();
  const salvarPerfis = useAtualizarPerfisDoUsuario();
  const salvarOverrides = useAtualizarOverridesDoUsuario();
  const detalhe = useUsuarioPorId(modo === 'editar' ? idUsuario ?? null : null);
  const permissoesUsuario = useUsuarioPermissoes(modo === 'editar' ? idUsuario ?? null : null);

  const deveTrocarAtual = detalhe.data?.deveTrocarSenha ?? false;

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        email: detalhe.data.email ?? '',
        senha: '',
        cpf: formatarCpfDigitos(detalhe.data.cpf ?? ''),
        dataNascimento: detalhe.data.dataNascimento ?? '',
        telefone: detalhe.data.telefone ?? '',
        fotoBase64: detalhe.data.fotoBase64 ?? null,
        deveTrocarSenha: false,
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
      setPerfilIdsSelecionados(detalhe.data.perfilIds ?? []);
    }
  }, [modo, detalhe.data]);

  useEffect(() => {
    if (modo === 'editar' && permissoesUsuario.data) {
      setOverrides(paraMatriz(permissoesUsuario.data.overrides));
    }
  }, [modo, permissoesUsuario.data]);

  function setCampo<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  async function consultarCpfHub() {
    setErros({});
    setErroGlobal(null);
    const cpfLimpo = valores.cpf.replace(/\D/g, '');
    if (cpfLimpo.length !== 11) {
      setErros({ cpf: 'CPF deve ter 11 dígitos.' });
      return;
    }
    setConsultandoCpf(true);
    try {
      const existente = await consultarUsuarioPorCpf(cpfLimpo);
      if (existente) {
        setErroGlobal(
          existente.ativo
            ? `Já existe usuário com este CPF: ${existente.nomeCompleto}${existente.email ? ` (${existente.email})` : ''}.`
            : `CPF pertence ao usuário inativo "${existente.nomeCompleto}". Peça para um administrador reativá-lo.`,
        );
        return;
      }

      // Com data de nascimento: valida o CPF na Receita (via hub) e usa o nome de lá.
      if (valores.dataNascimento) {
        const hub = await consultarCpf(cpfLimpo, valores.dataNascimento);
        setValores((s) => ({
          ...s,
          nomeCompleto: hub.nome.trim() || s.nomeCompleto,
          cpf: hub.cpf || cpfLimpo,
        }));
        setPassoCpfConcluido(true);
        return;
      }

      // Sem data de nascimento: procura na base (médico → paciente). Muito médico
      // importado não tem data de nascimento; se já existe cadastro, seguimos com ele.
      const medicos = await buscarMedicos(cpfLimpo);
      const medico = medicos.find((m) => m.cpf.replace(/\D/g, '') === cpfLimpo);
      if (medico) {
        setValores((s) => ({ ...s, nomeCompleto: medico.nomeCompleto.trim() || s.nomeCompleto, cpf: cpfLimpo }));
        setPassoCpfConcluido(true);
        return;
      }
      const paciente = await obterPacientePorCpf(cpfLimpo);
      if (paciente) {
        setValores((s) => ({ ...s, nomeCompleto: paciente.nomeCompleto.trim() || s.nomeCompleto, cpf: cpfLimpo }));
        setPassoCpfConcluido(true);
        return;
      }

      // Nada na base e sem data de nascimento → não há como validar a identidade.
      setErros({
        dataNascimento:
          'Sem cadastro de médico/paciente com este CPF. Informe a data de nascimento para validar na Receita.',
      });
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    } finally {
      setConsultandoCpf(false);
    }
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const ne: Erros = {};
    const nome = valores.nomeCompleto.trim();
    const email = valores.email.trim();
    const cpf = valores.cpf.trim();
    const senha = valores.senha;

    if (nome.length < 3) ne.nomeCompleto = 'Nome obrigatório (mínimo 3 caracteres).';
    // E-mail é opcional (médico importado pode não ter); valida formato só se preenchido.
    if (email && !/^\S+@\S+\.\S+$/.test(email)) ne.email = 'E-mail inválido.';
    if (modo === 'criar') {
      if (senha && senha.length < 8) ne.senha = 'Mínimo 8 caracteres.';
      // Data de nascimento é opcional: o gate já resolveu a identidade (Receita
      // com nascimento, ou cadastro de médico/paciente sem nascimento).
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
          pontoReferencia: null,
        }
      : null;

    const overridesParaApi = matrizParaApi(overrides);

    try {
      if (modo === 'criar') {
        const novoId = await cadastrar.mutateAsync({
          nomeCompleto: nome,
          email: email || undefined,
          cpf: cpf.replace(/\D/g, '') || undefined,
          dataNascimento: valores.dataNascimento || undefined,
          telefone: valores.telefone || undefined,
          endereco: enderecoPayload,
          fotoBase64: valores.fotoBase64,
          senha: senha || undefined,
          deveTrocarSenha: senha ? valores.deveTrocarSenha : undefined,
          perfilIds: perfilIdsSelecionados,
        });
        if (overridesParaApi.length > 0) {
          await salvarOverrides.mutateAsync({ id: novoId, overrides: overridesParaApi });
        }
      } else {
        if (!idUsuario) throw new Error('ID ausente.');
        // Nome, CPF e data de nascimento são imutáveis. E-mail PODE ser editado/inserido
        // (médicos importados vêm sem e-mail). Em branco = backend não altera.
        await atualizar.mutateAsync({
          id: idUsuario,
          payload: {
            telefone: valores.telefone || undefined,
            endereco: enderecoPayload,
            fotoBase64: valores.fotoBase64,
            email: email || undefined,
          },
        });
        await salvarPerfis.mutateAsync({ id: idUsuario, perfilIds: perfilIdsSelecionados });
        await salvarOverrides.mutateAsync({ id: idUsuario, overrides: overridesParaApi });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente =
    cadastrar.isPending || atualizar.isPending || salvarPerfis.isPending || salvarOverrides.isPending;

  // Modo criar exige consulta CPF+nascimento antes de abrir o restante do form.
  if (modo === 'criar' && !passoCpfConcluido) {
    return (
      <div className="space-y-5">
        <p className="text-sm text-gray-600">
          Informe o CPF e a data de nascimento. Esses dados não poderão ser editados depois.
        </p>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Campo label="CPF" htmlFor="cpfInicial" erro={erros.cpf} required>
            <Input
              id="cpfInicial"
              value={valores.cpf}
              onChange={(e) => setCampo('cpf', e.target.value)}
              inputMode="numeric"
              placeholder="00000000000"
              autoFocus
              disabled={consultandoCpf}
            />
          </Campo>

          <Campo label="Data de nascimento" htmlFor="nascInicial" erro={erros.dataNascimento} dica="Opcional se já houver cadastro de médico/paciente com este CPF.">
            <Input
              id="nascInicial"
              type="date"
              value={valores.dataNascimento}
              onChange={(e) => setCampo('dataNascimento', e.target.value)}
              disabled={consultandoCpf}
            />
          </Campo>
        </div>

        {erroGlobal ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroGlobal}
          </div>
        ) : null}

        <div className="flex items-center justify-end gap-3 pt-2">
          <Button type="button" variante="ghost" onClick={aoConcluir} disabled={consultandoCpf}>
            Cancelar
          </Button>
          <Button type="button" onClick={consultarCpfHub} disabled={consultandoCpf}>
            {consultandoCpf ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" /> Consultando…
              </>
            ) : (
              <>
                <Search className="h-4 w-4" /> Continuar
              </>
            )}
          </Button>
        </div>
      </div>
    );
  }

  const dadosPessoaisValores = {
    nomeCompleto: valores.nomeCompleto,
    cpf: valores.cpf,
    dataNascimento: valores.dataNascimento,
    email: valores.email,
    telefone: valores.telefone,
    endereco: valores.endereco,
    fotoBase64: valores.fotoBase64,
  };

  const abaDados = (
    <div className="space-y-5">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <DadosPessoaisCampos
        valores={dadosPessoaisValores}
        erros={erros}
        aoMudarCampo={(c, v) => {
          if (c === 'nomeCompleto' || c === 'cpf' || c === 'dataNascimento') return;
          setCampo(c as keyof Valores, v as Valores[keyof Valores]);
        }}
        identidadeReadOnly
        emailReadOnly={false}
        desabilitado={pendente}
        mostrarPontoReferencia={false}
      />

      {modo === 'criar' ? (
        <div className="space-y-3">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="Senha inicial" htmlFor="senha" erro={erros.senha} dica="Opcional. Mínimo 8 caracteres.">
              <Input
                id="senha"
                type="password"
                value={valores.senha}
                onChange={(e) => setCampo('senha', e.target.value)}
                autoComplete="new-password"
              />
            </Campo>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={valores.deveTrocarSenha && !!valores.senha.trim()}
              onChange={(e) => setCampo('deveTrocarSenha', e.target.checked)}
              disabled={!valores.senha.trim()}
            />
            <span>
              Exigir troca de senha no próximo login
              {!valores.senha.trim() ? ' (defina a senha inicial acima)' : ''}
            </span>
          </label>
        </div>
      ) : idUsuario ? (
        <SegurancaSecao usuarioId={idUsuario} deveTrocarAtual={deveTrocarAtual} />
      ) : null}
    </div>
  );

  const abaPermissoes = (
    <PermissoesSecao
      perfilIdsSelecionados={perfilIdsSelecionados}
      aoMudarPerfilIds={setPerfilIdsSelecionados}
      overrides={overrides}
      aoMudarOverrides={setOverrides}
      desabilitado={pendente}
    />
  );

  const abas: Aba[] = [
    { id: 'dados', rotulo: 'Dados', conteudo: abaDados },
    {
      id: 'permissoes',
      rotulo: 'Permissões',
      conteudo: abaPermissoes,
      badge: perfilIdsSelecionados.length || undefined,
    },
  ];

  return (
    <form onSubmit={aoEnviar} className="space-y-5">
      <Tabs abas={abas} inicial="dados" />

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
