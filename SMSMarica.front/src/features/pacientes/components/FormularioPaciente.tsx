import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  atualizarPacienteSchema,
  cadastrarPacienteSchema,
} from '@/features/pacientes/schemas/pacienteSchema';
import {
  useAtualizarPaciente,
  useCadastrarPaciente,
  usePacientePorId,
} from '@/features/pacientes/api/queries';

type Props = {
  modo: 'criar' | 'editar';
  idPaciente?: string | null;
  aoConcluir: () => void;
};

type Valores = {
  nomeCompleto: string;
  cpf: string;
  cns: string;
  latitude: string;
  longitude: string;
};

const INICIAL: Valores = { nomeCompleto: '', cpf: '', cns: '', latitude: '', longitude: '' };
type Erros = Partial<Record<keyof Valores, string>>;

export function FormularioPaciente({ modo, idPaciente, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarPaciente();
  const atualizar = useAtualizarPaciente();
  const detalhe = usePacientePorId(modo === 'editar' ? idPaciente ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        cpf: detalhe.data.cpf,
        cns: detalhe.data.cns ?? '',
        latitude: String(detalhe.data.latitude),
        longitude: String(detalhe.data.longitude),
      });
    }
  }, [modo, detalhe.data]);

  function atualizarCampo<K extends keyof Valores>(campo: K, valor: string) {
    setValores((v) => ({ ...v, [campo]: valor }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const payloadBase = {
      nomeCompleto: valores.nomeCompleto.trim(),
      cns: valores.cns,
      latitude: Number(valores.latitude),
      longitude: Number(valores.longitude),
    };

    try {
      if (modo === 'criar') {
        const parsed = cadastrarPacienteSchema.safeParse({ ...payloadBase, cpf: valores.cpf });
        if (!parsed.success) {
          const ne: Erros = {};
          for (const i of parsed.error.issues) {
            const k = i.path[0] as keyof Valores | undefined;
            if (k && !ne[k]) ne[k] = i.message;
          }
          setErros(ne);
          return;
        }
        await cadastrar.mutateAsync(parsed.data);
      } else {
        if (!idPaciente) throw new Error('ID ausente para edição.');
        const parsed = atualizarPacienteSchema.safeParse(payloadBase);
        if (!parsed.success) {
          const ne: Erros = {};
          for (const i of parsed.error.issues) {
            const k = i.path[0] as keyof Valores | undefined;
            if (k && !ne[k]) ne[k] = i.message;
          }
          setErros(ne);
          return;
        }
        await atualizar.mutateAsync({ id: idPaciente, payload: parsed.data });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;
  const carregandoDetalhe = modo === 'editar' && detalhe.isFetching;

  return (
    <form onSubmit={aoEnviar} className="space-y-4">
      {carregandoDetalhe ? (
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
            onChange={(e) => atualizarCampo('nomeCompleto', e.target.value)}
            required
          />
        </Campo>

        <Campo
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
          dica={modo === 'editar' ? 'CPF não pode ser alterado.' : 'Somente números.'}
        >
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => atualizarCampo('cpf', e.target.value)}
            placeholder="00000000000"
            inputMode="numeric"
            required={modo === 'criar'}
            disabled={modo === 'editar'}
          />
        </Campo>

        <Campo label="CNS (opcional)" htmlFor="cns" erro={erros.cns}>
          <Input
            id="cns"
            value={valores.cns}
            onChange={(e) => atualizarCampo('cns', e.target.value)}
            placeholder="000000000000000"
            inputMode="numeric"
          />
        </Campo>

        <Campo label="Latitude da residência" htmlFor="latitude" erro={erros.latitude}>
          <Input
            id="latitude"
            value={valores.latitude}
            onChange={(e) => atualizarCampo('latitude', e.target.value)}
            placeholder="-22.9188"
            inputMode="decimal"
            required
          />
        </Campo>

        <Campo label="Longitude da residência" htmlFor="longitude" erro={erros.longitude}>
          <Input
            id="longitude"
            value={valores.longitude}
            onChange={(e) => atualizarCampo('longitude', e.target.value)}
            placeholder="-42.8186"
            inputMode="decimal"
            required
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
          {pendente ? 'Salvando…' : modo === 'criar' ? 'Cadastrar' : 'Salvar alterações'}
        </Button>
      </div>
    </form>
  );
}
