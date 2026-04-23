import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarMotorista,
  useCadastrarMotorista,
  useMotoristaPorId,
} from '@/features/motoristas/api/queries';
import {
  atualizarMotoristaSchema,
  cadastrarMotoristaSchema,
} from '@/features/motoristas/schemas/motoristaSchema';

type Props = { modo: 'criar' | 'editar'; idMotorista?: string | null; aoConcluir: () => void };

type Valores = { nomeCompleto: string; cpf: string; cnh: string; telefone: string };
const INICIAL: Valores = { nomeCompleto: '', cpf: '', cnh: '', telefone: '' };
type Erros = Partial<Record<keyof Valores, string>>;

export function FormularioMotorista({ modo, idMotorista, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarMotorista();
  const atualizar = useAtualizarMotorista();
  const detalhe = useMotoristaPorId(modo === 'editar' ? idMotorista ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        cpf: detalhe.data.cpf,
        cnh: detalhe.data.cnh,
        telefone: detalhe.data.telefone ?? '',
      });
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: string) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const base = {
      nomeCompleto: valores.nomeCompleto.trim(),
      cnh: valores.cnh.trim(),
      telefone: valores.telefone,
    };

    try {
      if (modo === 'criar') {
        const parsed = cadastrarMotoristaSchema.safeParse({ ...base, cpf: valores.cpf });
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
        if (!idMotorista) throw new Error('ID ausente.');
        const parsed = atualizarMotoristaSchema.safeParse(base);
        if (!parsed.success) {
          const ne: Erros = {};
          for (const i of parsed.error.issues) {
            const k = i.path[0] as keyof Valores | undefined;
            if (k && !ne[k]) ne[k] = i.message;
          }
          setErros(ne);
          return;
        }
        await atualizar.mutateAsync({ id: idMotorista, payload: parsed.data });
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
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
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

        <Campo label="CNH" htmlFor="cnh" erro={erros.cnh}>
          <Input id="cnh" value={valores.cnh} onChange={(e) => set('cnh', e.target.value)} required />
        </Campo>

        <Campo label="Telefone (opcional)" htmlFor="telefone" erro={erros.telefone}>
          <Input
            id="telefone"
            value={valores.telefone}
            onChange={(e) => set('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
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
