import { useEffect, useState, type FormEvent } from 'react';
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
import {
  useAtualizarMedico,
  useCadastrarMedico,
  useMedicoPorId,
} from '@/features/medicos/api/queries';
import {
  atualizarMedicoSchema,
  cadastrarMedicoSchema,
} from '@/features/medicos/schemas/medicoSchema';

type Props = { modo: 'criar' | 'editar'; idMedico?: string | null; aoConcluir: () => void };

type Valores = {
  nomeCompleto: string;
  cpf: string;
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

export function FormularioMedico({ modo, idMedico, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarMedico();
  const atualizar = useAtualizarMedico();
  const detalhe = useMedicoPorId(modo === 'editar' ? idMedico ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        cpf: detalhe.data.cpf,
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
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
          required={modo === 'criar'}
          dica={modo === 'editar' ? 'CPF não pode ser alterado.' : undefined}
        >
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => set('cpf', e.target.value)}
            inputMode="numeric"
            required={modo === 'criar'}
            disabled={modo === 'editar'}
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
