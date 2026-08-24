import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { DadosPessoaisCampos } from '@/shared/ui/DadosPessoaisCampos';
import { SegurancaSecao } from '@/shared/ui/SegurancaSecao';
import { PermissoesSecao, matrizParaApi } from '@/shared/ui/PermissoesSecao';
import {
  enderecoVazio,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { consultarCpf } from '@/shared/api/integracoes';
import { consultarUsuarioPorCpf, obterUsuarioPorId } from '@/features/usuarios/api/usuariosApi';
import {
  useAtualizarMedico,
  useCadastrarMedico,
  useMedicoPorId,
} from '@/features/medicos/api/queries';
import {
  useAtualizarOverridesDoUsuario,
  useAtualizarPerfisDoUsuario,
  useUsuarioPermissoes,
} from '@/features/usuarios/api/queries';
import {
  atualizarMedicoSchema,
  cadastrarMedicoSchema,
} from '@/features/medicos/schemas/medicoSchema';
import { AssinaturaMedicoSecao } from '@/features/medicos/assinatura/AssinaturaMedicoSecao';
import { paraMatriz } from '@/features/perfis/lib/acoes';
import type { MatrizEdicao } from '@/features/perfis/types';
import { CONSELHOS } from '@/features/medicos/types';

type Props = {
  modo: 'criar' | 'editar';
  idMedico?: string | null;
  aoConcluir: () => void;
  /** Conselho pré-selecionado ao criar (Médicos = "CRM"; Profissionais = aba ativa). */
  conselhoInicial?: string;
  /** Se false, o conselho fica travado (menu Médicos). */
  permitirEscolherConselho?: boolean;
  /** Substantivo usado nos textos (médico / profissional). */
  substantivo?: string;
};

type Valores = {
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string;
  email: string;
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade: string;
  rqe: string;
  validadeRegistro: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  cpf: '',
  dataNascimento: '',
  email: '',
  conselho: 'CRM',
  registro: '',
  ufConselho: '',
  especialidade: '',
  rqe: '',
  validadeRegistro: '',
  telefone: '',
  endereco: enderecoVazio,
  fotoBase64: null,
};

type Erros = Partial<
  Record<
    | 'nomeCompleto'
    | 'cpf'
    | 'dataNascimento'
    | 'conselho'
    | 'registro'
    | 'ufConselho'
    | 'especialidade'
    | 'rqe'
    | 'validadeRegistro'
    | 'email'
    | 'telefone',
    string
  >
>;

export function FormularioMedico({
  modo,
  idMedico,
  aoConcluir,
  conselhoInicial = 'CRM',
  permitirEscolherConselho = false,
  substantivo = 'médico',
}: Props) {
  const [valores, setValores] = useState<Valores>({ ...INICIAL, conselho: conselhoInicial });
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const [passoCpfConcluido, setPassoCpfConcluido] = useState(modo === 'editar');
  const [consultandoCpf, setConsultandoCpf] = useState(false);
  // Nome do usuário (login) já existente para o CPF informado, sem papel definido.
  // Apenas informativo: ao cadastrar o médico, o vínculo é feito pelo CPF no back.
  const [usuarioExistenteNome, setUsuarioExistenteNome] = useState<string | null>(null);

  const [perfilIdsSelecionados, setPerfilIdsSelecionados] = useState<string[]>([]);
  const [overrides, setOverrides] = useState<MatrizEdicao>({});

  const cadastrar = useCadastrarMedico();
  const atualizar = useAtualizarMedico();
  const detalhe = useMedicoPorId(modo === 'editar' ? idMedico ?? null : null);
  const salvarPerfis = useAtualizarPerfisDoUsuario();
  const salvarOverrides = useAtualizarOverridesDoUsuario();
  const permissoesUsuario = useUsuarioPermissoes(
    modo === 'editar' && detalhe.data ? detalhe.data.usuarioId : null,
  );

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        cpf: detalhe.data.cpf,
        dataNascimento: detalhe.data.dataNascimento ?? '',
        email: '',
        conselho: detalhe.data.conselho || 'CRM',
        registro: detalhe.data.registro,
        ufConselho: detalhe.data.ufConselho,
        especialidade: detalhe.data.especialidade ?? '',
        rqe: detalhe.data.rqe ?? '',
        validadeRegistro: detalhe.data.validadeRegistro ?? '',
        telefone: detalhe.data.telefone ?? '',
        fotoBase64: detalhe.data.fotoBase64 ?? null,
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
      // Pega email e perfilIds do Usuario (só se houver usuário vinculado).
      if (detalhe.data.usuarioId) {
        obterUsuarioPorId(detalhe.data.usuarioId)
          .then((u) => {
            setValores((s) => ({ ...s, email: u.email ?? '' }));
            setPerfilIdsSelecionados(u.perfilIds ?? []);
          })
          .catch(() => {});
      }
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

  async function aoConfirmarPasso1() {
    setErros({});
    setErroGlobal(null);
    setUsuarioExistenteNome(null);

    const cpfLimpo = valores.cpf.replace(/\D/g, '');
    const ne: Erros = {};
    if (cpfLimpo.length !== 11) ne.cpf = 'CPF precisa ter 11 dígitos.';
    if (!valores.dataNascimento) ne.dataNascimento = 'Informe a data de nascimento.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }

    setConsultandoCpf(true);
    try {
      const existente = await consultarUsuarioPorCpf(cpfLimpo);
      if (existente) {
        if (existente.papelAtual === 'Medico') {
          setErroGlobal(`Já existe médico cadastrado com este CPF: ${existente.nomeCompleto}.`);
          return;
        }
        if (existente.papelAtual !== null) {
          setErroGlobal(
            `CPF já cadastrado como ${existente.papelAtual}: ${existente.nomeCompleto}.`,
          );
          return;
        }
        // Usuário (login) já existe sem papel. Não há mais "promover": cadastra-se o
        // médico normalmente (POST /medicos) e o vínculo usuário↔médico é resolvido
        // pelo CPF no back. Pré-preenche os dados dele e segue pelo fluxo padrão.
        setUsuarioExistenteNome(existente.nomeCompleto);
        setValores((s) => ({
          ...s,
          nomeCompleto: existente.nomeCompleto,
          cpf: cpfLimpo,
          email: existente.email ?? s.email,
          telefone: existente.telefone ?? s.telefone,
          fotoBase64: existente.fotoBase64 ?? null,
          endereco: existente.endereco
            ? {
                cep: existente.endereco.cep ?? '',
                logradouro: existente.endereco.logradouro ?? '',
                numero: existente.endereco.numero ?? '',
                complemento: existente.endereco.complemento ?? '',
                bairro: existente.endereco.bairro ?? '',
                cidade: existente.endereco.cidade ?? '',
                uf: existente.endereco.uf ?? '',
                pontoReferencia: existente.endereco.pontoReferencia ?? '',
              }
            : enderecoVazio,
        }));
        setPassoCpfConcluido(true);
        return;
      }

      const hub = await consultarCpf(cpfLimpo, valores.dataNascimento);
      setValores((s) => ({
        ...s,
        nomeCompleto: hub.nome.trim() || s.nomeCompleto,
        cpf: hub.cpf ? hub.cpf.replace(/\D/g, '') : cpfLimpo,
      }));
      setPassoCpfConcluido(true);
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    } finally {
      setConsultandoCpf(false);
    }
  }

  async function aplicarPermissoes(usuarioId: string) {
    const overridesParaApi = matrizParaApi(overrides);
    await salvarPerfis.mutateAsync({ id: usuarioId, perfilIds: perfilIdsSelecionados });
    await salvarOverrides.mutateAsync({ id: usuarioId, overrides: overridesParaApi });
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

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

    const base = {
      conselho: valores.conselho.trim().toUpperCase(),
      registro: valores.registro.trim(),
      ufConselho: valores.ufConselho.trim(),
      especialidade: valores.especialidade,
      rqe: valores.rqe,
      validadeRegistro: valores.validadeRegistro,
      telefone: valores.telefone,
      endereco: enderecoPayload,
      fotoBase64: valores.fotoBase64,
    };

    try {
      if (modo === 'criar') {
        const parsed = cadastrarMedicoSchema.safeParse({
          ...base,
          nomeCompleto: valores.nomeCompleto.trim(),
          cpf: valores.cpf,
          dataNascimento: valores.dataNascimento || undefined,
          email: valores.email,
        });
        if (!parsed.success) {
          const ne: Erros = {};
          for (const i of parsed.error.issues) {
            const k = i.path[0] as keyof Erros | undefined;
            if (k && !ne[k]) ne[k] = i.message;
          }
          setErros(ne);
          return;
        }
        const idMed = await cadastrar.mutateAsync(parsed.data);
        try {
          const novoMed = await import('@/features/medicos/api/medicosApi')
            .then((m) => m.obterMedicoPorId(idMed));
          if (novoMed.usuarioId) await aplicarPermissoes(novoMed.usuarioId);
        } catch (errPerm) {
          setErroGlobal(`Médico criado, mas falha ao aplicar permissões: ${extrairMensagemDeErro(errPerm)}`);
          return;
        }
      } else {
        if (!idMedico) throw new Error('ID ausente.');
        const parsed = atualizarMedicoSchema.safeParse(base);
        if (!parsed.success) {
          const ne: Erros = {};
          for (const i of parsed.error.issues) {
            const k = i.path[0] as keyof Erros | undefined;
            if (k && !ne[k]) ne[k] = i.message;
          }
          setErros(ne);
          return;
        }
        await atualizar.mutateAsync({ id: idMedico, payload: parsed.data });
        // Só aplica permissões se o médico tem usuário de acesso (login vinculado por CPF).
        if (detalhe.data?.usuarioId) await aplicarPermissoes(detalhe.data.usuarioId);
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente =
    cadastrar.isPending || atualizar.isPending
    || salvarPerfis.isPending || salvarOverrides.isPending;

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

          <Campo
            label="Data de nascimento"
            htmlFor="nascInicial"
            erro={erros.dataNascimento}
            required
          >
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
          <Button type="button" onClick={aoConfirmarPasso1} disabled={consultandoCpf}>
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

      {modo === 'criar' && usuarioExistenteNome ? (
        <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-800">
          <strong>{usuarioExistenteNome}</strong> já tem um usuário de acesso (login) com este
          CPF. Ao cadastrar, o médico será vinculado automaticamente a esse usuário — perfis e
          permissões definidos aqui já valem para o login dele.
        </div>
      ) : null}

      <DadosPessoaisCampos
        valores={dadosPessoaisValores}
        erros={erros}
        aoMudarCampo={(c, v) => {
          if (c === 'nomeCompleto' || c === 'cpf' || c === 'dataNascimento') return;
          setCampo(c as keyof Valores, v as Valores[keyof Valores]);
        }}
        identidadeReadOnly
        emailReadOnly={modo === 'editar'}
        desabilitado={pendente}
      />

      {modo === 'editar' && detalhe.data?.usuarioId ? (
        <SegurancaSecao
          usuarioId={detalhe.data.usuarioId}
          deveTrocarAtual={false}
        />
      ) : modo === 'editar' && detalhe.data ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Este profissional não tem usuário de acesso (login) vinculado. Senha e permissões só
          ficam disponíveis após criar um usuário com o mesmo CPF.
        </div>
      ) : null}
    </div>
  );

  const abaMedico = (
    <div className="space-y-5">
      <CamposMedicoEspecificos
        valores={valores}
        erros={erros}
        setCampo={setCampo}
        permitirEscolherConselho={permitirEscolherConselho}
      />
      {modo === 'editar' && idMedico ? <AssinaturaMedicoSecao medicoId={idMedico} /> : null}
    </div>
  );

  const semUsuario = modo === 'editar' && detalhe.data != null && !detalhe.data.usuarioId;
  const abaPermissoes = semUsuario ? (
    <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
      Este profissional não tem usuário de acesso (login) vinculado. Crie um usuário com o mesmo
      CPF para gerenciar perfis e permissões.
    </div>
  ) : (
    <PermissoesSecao
      perfilIdsSelecionados={perfilIdsSelecionados}
      aoMudarPerfilIds={setPerfilIdsSelecionados}
      overrides={overrides}
      aoMudarOverrides={setOverrides}
      desabilitado={pendente}
    />
  );

  const tituloAbaProfissional = substantivo.charAt(0).toUpperCase() + substantivo.slice(1);
  const abas: Aba[] = [
    { id: 'dados', rotulo: 'Dados pessoais', conteudo: abaDados },
    { id: 'medico', rotulo: tituloAbaProfissional, conteudo: abaMedico },
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

function CamposMedicoEspecificos({
  valores,
  erros,
  setCampo,
  autoFocusRegistro,
  permitirEscolherConselho,
}: {
  valores: Valores;
  erros: Erros;
  setCampo: <K extends keyof Valores>(k: K, v: Valores[K]) => void;
  autoFocusRegistro?: boolean;
  permitirEscolherConselho?: boolean;
}) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      <Campo label="Conselho" htmlFor="conselho" erro={erros.conselho} required>
        {permitirEscolherConselho ? (
          <select
            id="conselho"
            value={valores.conselho}
            onChange={(e) => setCampo('conselho', e.target.value)}
            className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-red-500 focus:ring-red-500"
          >
            {CONSELHOS.map((c) => (
              <option key={c.sigla} value={c.sigla}>
                {c.sigla} — {c.nome}
              </option>
            ))}
          </select>
        ) : (
          <Input id="conselho" value={valores.conselho} disabled readOnly />
        )}
      </Campo>

      <Campo label="Número do registro" htmlFor="registro" erro={erros.registro} required>
        <Input
          id="registro"
          value={valores.registro}
          onChange={(e) => setCampo('registro', e.target.value)}
          inputMode="numeric"
          required
          autoFocus={autoFocusRegistro}
        />
      </Campo>

      <Campo label="UF do conselho" htmlFor="ufConselho" erro={erros.ufConselho} required>
        <Input
          id="ufConselho"
          value={valores.ufConselho}
          onChange={(e) => setCampo('ufConselho', e.target.value.toUpperCase())}
          maxLength={2}
          placeholder="RJ"
          required
        />
      </Campo>

      <Campo label="Especialidade" htmlFor="especialidade" erro={erros.especialidade}>
        <Input
          id="especialidade"
          value={valores.especialidade}
          onChange={(e) => setCampo('especialidade', e.target.value)}
          placeholder="Clínica geral, Cardiologia…"
        />
      </Campo>

      <Campo label="RQE" htmlFor="rqe" erro={erros.rqe} dica="Registro de Qualificação de Especialista (médicos).">
        <Input id="rqe" value={valores.rqe} onChange={(e) => setCampo('rqe', e.target.value)} />
      </Campo>

      <Campo label="Validade do registro" htmlFor="validadeRegistro" erro={erros.validadeRegistro}>
        <Input
          id="validadeRegistro"
          type="date"
          value={valores.validadeRegistro}
          onChange={(e) => setCampo('validadeRegistro', e.target.value)}
        />
      </Campo>
    </div>
  );
}
