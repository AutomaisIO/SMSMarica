import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarAvaliacao,
  useAvaliacaoPorId,
  useRegistrarAvaliacao,
} from '@/features/avaliacoes/api/queries';

type Props = { modo: 'criar' | 'editar'; idAvaliacao?: string | null; aoConcluir: () => void };

type Valores = { sessaoId: string; nota: string; comentario: string };
const INICIAL: Valores = { sessaoId: '', nota: '5', comentario: '' };
type Erros = Partial<Record<keyof Valores, string>>;

export function FormularioAvaliacao({ modo, idAvaliacao, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const registrar = useRegistrarAvaliacao();
  const atualizar = useAtualizarAvaliacao();
  const detalhe = useAvaliacaoPorId(modo === 'editar' ? idAvaliacao ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        sessaoId: detalhe.data.sessaoId,
        nota: String(detalhe.data.nota),
        comentario: detalhe.data.comentario ?? '',
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

    const nota = Number(valores.nota);
    const ne: Erros = {};
    if (!Number.isInteger(nota) || nota < 1 || nota > 5) ne.nota = 'Nota precisa ser 1..5.';
    if (modo === 'criar' && !valores.sessaoId.trim()) ne.sessaoId = 'Sessão obrigatória.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }

    try {
      if (modo === 'criar') {
        await registrar.mutateAsync({
          sessaoId: valores.sessaoId.trim(),
          nota,
          comentario: valores.comentario.trim() || undefined,
        });
      } else {
        if (!idAvaliacao) throw new Error('ID ausente.');
        await atualizar.mutateAsync({
          id: idAvaliacao,
          payload: { nota, comentario: valores.comentario.trim() || undefined },
        });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = registrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-4">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <Campo
        label="Sessão"
        htmlFor="sessaoId"
        erro={erros.sessaoId}
        dica={
          modo === 'editar'
            ? 'Sessão não pode ser alterada.'
            : 'ID (GUID) da sessão de translado avaliada.'
        }
      >
        <Input
          id="sessaoId"
          value={valores.sessaoId}
          onChange={(e) => set('sessaoId', e.target.value)}
          placeholder="GUID"
          required={modo === 'criar'}
          disabled={modo === 'editar'}
        />
      </Campo>

      <Campo label="Nota" htmlFor="nota" erro={erros.nota}>
        <Select id="nota" value={valores.nota} onChange={(e) => set('nota', e.target.value)}>
          {[1, 2, 3, 4, 5].map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </Select>
      </Campo>

      <Campo label="Comentário (opcional)" htmlFor="comentario" erro={erros.comentario}>
        <textarea
          id="comentario"
          className="input min-h-[90px]"
          value={valores.comentario}
          onChange={(e) => set('comentario', e.target.value)}
          maxLength={1000}
        />
      </Campo>

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
          {pendente ? 'Salvando…' : modo === 'criar' ? 'Registrar' : 'Salvar alterações'}
        </Button>
      </div>
    </form>
  );
}
