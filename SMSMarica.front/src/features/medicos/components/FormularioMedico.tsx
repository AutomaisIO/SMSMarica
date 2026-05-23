import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, Lock, Search, UserPlus } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { consultarCpf } from '@/shared/api/integracoes';
import { consultarUsuarioPorCpf } from '@/features/usuarios/api/usuariosApi';
import {
  useAtualizarMedico,
  useCadastrarMedico,
  useMedicoPorId,
  usePromoverMedico,
} from '@/features/medicos/api/queries';
import {
  atualizarMedicoSchema,
  cadastrarMedicoSchema,
} from '@/features/medicos/schemas/medicoSchema';

type Props = { modo: 'criar' | 'editar'; idMedico?: string | null; aoConcluir: () => void };

type Valores = {
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string;
  crm: string;
  ufCrm: string;
  especialidade: string;
  rqe: string;
  validadeCrm: string;
  email: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  cpf: '',
  dataNascimento: '',
  crm: '',
  ufCrm: '',
  especialidade: '',
  rqe: '',
  validadeCrm: '',
  email: '',
  telefone: '',
  endereco: enderecoVazio,
  fotoBase64: null,
};

type Erros = Partial<
  Record<
    | 'nomeCompleto'
    | 'cpf'
    | 'dataNascimento'
    | 'crm'
    | 'ufCrm'
    | 'especialidade'
    | 'rqe'
    | 'validadeCrm'
    | 'email'
    | 'telefone'
    | 'endereco',
    string
  >
>;

type PromocaoPendente = {
  usuarioId: string;
  nome: string;
  email: string;
  fotoBase64: string | null;
};

export function FormularioMedico({ modo, idMedico, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const [passoCpfConcluido, setPassoCpfConcluido] = useState(modo === 'editar');
  const [consultandoCpf, setConsultandoCpf] = useState(false);
  const [promocao, setPromocao] = useState<PromocaoPendente | null>(null);

  const cadastrar = useCadastrarMedico();
  const atualizar = useAtualizarMedico();
  const promover = usePromoverMedico();
  const detalhe = useMedicoPorId(modo === 'editar' ? idMedico ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        cpf: detalhe.data.cpf,
        dataNascimento: '',
        crm: detalhe.data.crm,
        ufCrm: detalhe.data.ufCrm,
        especialidade: detalhe.data.especialidade ?? '',
        rqe: detalhe.data.rqe ?? '',
        validadeCrm: detalhe.data.validadeCrm ?? '',
        email: '',
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
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  async function aoConfirmarPasso1() {
    setErros({});
    setErroGlobal(null);
    setPromocao(null);

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
        if (existente.tipoPapel === 'Medico') {
          setErroGlobal(`Já existe médico cadastrado com este CPF: ${existente.nomeCompleto}.`);
          return;
        }
        if (existente.tipoPapel !== null) {
          setErroGlobal(
            `CPF já cadastrado como ${existente.tipoPapel}: ${existente.nomeCompleto}.`,
          );
          return;
        }
        setPromocao({
          usuarioId: existente.id,
          nome: existente.nomeCompleto,
          email: existente.email,
          fotoBase64: existente.fotoBase64 ?? null,
        });
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

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    // Modo promoção: só dados do papel (CRM/UF/Especialidade/RQE/Validade) importam.
    if (modo === 'criar' && promocao) {
      const ne: Erros = {};
      const crm = valores.crm.trim();
      const uf = valores.ufCrm.trim().toUpperCase();
      if (crm.length < 3) ne.crm = 'CRM obrigatório.';
      if (uf.length !== 2) ne.ufCrm = 'UF do CRM deve ter 2 letras.';
      if (Object.keys(ne).length > 0) {
        setErros(ne);
        return;
      }
      try {
        await promover.mutateAsync({
          usuarioId: promocao.usuarioId,
          crm,
          ufCrm: uf,
          especialidade: valores.especialidade.trim() || undefined,
          rqe: valores.rqe.trim() || undefined,
          validadeCrm: valores.validadeCrm || undefined,
        });
        aoConcluir();
      } catch (erro) {
        setErroGlobal(extrairMensagemDeErro(erro));
      }
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

    const base = {
      nomeCompleto: valores.nomeCompleto.trim(),
      crm: valores.crm.trim(),
      ufCrm: valores.ufCrm.trim(),
      especialidade: valores.especialidade,
      rqe: valores.rqe,
      validadeCrm: valores.validadeCrm,
      telefone: valores.telefone,
      endereco: enderecoPayload,
      fotoBase64: valores.fotoBase64,
    };

    try {
      if (modo === 'criar') {
        const parsed = cadastrarMedicoSchema.safeParse({
          ...base,
          cpf: valores.cpf,
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
        await cadastrar.mutateAsync(parsed.data);
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
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending || promover.isPending;
  const travarIdentidade = modo === 'criar';

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
              onChange={(e) => set('cpf', e.target.value)}
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
              onChange={(e) => set('dataNascimento', e.target.value)}
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

  // Modo promoção: usuário existente sem papel → só pede dados do papel médico.
  if (modo === 'criar' && promocao) {
    return (
      <form onSubmit={aoEnviar} className="space-y-5">
        <div className="rounded-md border border-amber-300 bg-amber-50 p-4">
          <div className="flex items-start gap-3">
            <UserPlus className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-700" />
            <div className="min-w-0">
              <p className="text-sm font-medium text-amber-900">
                Promover usuário existente a médico
              </p>
              <p className="mt-1 text-sm text-amber-800">
                <strong>{promocao.nome}</strong> ({promocao.email}) já está cadastrado como
                usuário e ainda não tem papel definido. Ao continuar, será promovido a médico.
                Os dados pessoais (nome, CPF, endereço, telefone, foto) permanecem como estão e
                podem ser ajustados depois em <em>Editar médico</em>.
              </p>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Campo label="CRM" htmlFor="crm" erro={erros.crm} required>
            <Input
              id="crm"
              value={valores.crm}
              onChange={(e) => set('crm', e.target.value)}
              inputMode="numeric"
              required
              autoFocus
            />
          </Campo>

          <Campo label="UF do CRM" htmlFor="ufCrm" erro={erros.ufCrm} required>
            <Input
              id="ufCrm"
              value={valores.ufCrm}
              onChange={(e) => set('ufCrm', e.target.value.toUpperCase())}
              maxLength={2}
              placeholder="RJ"
              required
            />
          </Campo>

          <Campo label="Especialidade" htmlFor="especialidade" erro={erros.especialidade}>
            <Input
              id="especialidade"
              value={valores.especialidade}
              onChange={(e) => set('especialidade', e.target.value)}
              placeholder="Clínica geral, Cardiologia…"
            />
          </Campo>

          <Campo
            label="RQE"
            htmlFor="rqe"
            erro={erros.rqe}
            dica="Registro de Qualificação de Especialista."
          >
            <Input id="rqe" value={valores.rqe} onChange={(e) => set('rqe', e.target.value)} />
          </Campo>

          <Campo label="Validade do CRM" htmlFor="validadeCrm" erro={erros.validadeCrm}>
            <Input
              id="validadeCrm"
              type="date"
              value={valores.validadeCrm}
              onChange={(e) => set('validadeCrm', e.target.value)}
            />
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
            {pendente ? 'Promovendo…' : `Promover ${promocao.nome.split(' ')[0]} a médico`}
          </Button>
        </div>
      </form>
    );
  }

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
          dica={
            travarIdentidade ? (
              <span className="inline-flex items-center gap-1">
                <Lock className="h-3 w-3" /> Não pode ser editado.
              </span>
            ) : undefined
          }
        >
          <Input
            id="nomeCompleto"
            value={valores.nomeCompleto}
            onChange={(e) => set('nomeCompleto', e.target.value)}
            required
            disabled={travarIdentidade}
            readOnly={travarIdentidade}
          />
        </Campo>

        <Campo
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
          required={modo === 'criar'}
          dica={
            modo === 'editar' ? (
              'CPF não pode ser alterado.'
            ) : (
              <span className="inline-flex items-center gap-1">
                <Lock className="h-3 w-3" /> Imutável
              </span>
            )
          }
        >
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => set('cpf', e.target.value)}
            inputMode="numeric"
            required={modo === 'criar'}
            disabled={modo === 'editar' || travarIdentidade}
            readOnly={modo === 'editar' || travarIdentidade}
          />
        </Campo>

        {modo === 'criar' ? (
          <Campo label="E-mail" htmlFor="email" erro={erros.email}>
            <Input
              id="email"
              type="email"
              value={valores.email}
              onChange={(e) => set('email', e.target.value)}
              placeholder="medico@exemplo.com"
            />
          </Campo>
        ) : null}

        <Campo label="CRM" htmlFor="crm" erro={erros.crm} required>
          <Input
            id="crm"
            value={valores.crm}
            onChange={(e) => set('crm', e.target.value)}
            inputMode="numeric"
            required
          />
        </Campo>

        <Campo label="UF do CRM" htmlFor="ufCrm" erro={erros.ufCrm} required>
          <Input
            id="ufCrm"
            value={valores.ufCrm}
            onChange={(e) => set('ufCrm', e.target.value.toUpperCase())}
            maxLength={2}
            placeholder="RJ"
            required
          />
        </Campo>

        <Campo label="Especialidade" htmlFor="especialidade" erro={erros.especialidade}>
          <Input
            id="especialidade"
            value={valores.especialidade}
            onChange={(e) => set('especialidade', e.target.value)}
            placeholder="Clínica geral, Cardiologia…"
          />
        </Campo>

        <Campo label="RQE" htmlFor="rqe" erro={erros.rqe} dica="Registro de Qualificação de Especialista.">
          <Input id="rqe" value={valores.rqe} onChange={(e) => set('rqe', e.target.value)} />
        </Campo>

        <Campo label="Validade do CRM" htmlFor="validadeCrm" erro={erros.validadeCrm}>
          <Input
            id="validadeCrm"
            type="date"
            value={valores.validadeCrm}
            onChange={(e) => set('validadeCrm', e.target.value)}
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
