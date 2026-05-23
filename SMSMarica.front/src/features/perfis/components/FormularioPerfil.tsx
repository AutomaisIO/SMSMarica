import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarPerfil,
  useCadastrarPerfil,
  usePerfilPorId,
} from '@/features/perfis/api/queries';
import { deMatriz, paraMatriz } from '@/features/perfis/lib/acoes';
import type { MatrizEdicao } from '@/features/perfis/types';
import { MatrizPermissoes } from '@/features/perfis/components/MatrizPermissoes';

type Props = { modo: 'criar' | 'editar'; id?: string | null; aoConcluir: () => void };

type Valores = {
  nome: string;
  descricao: string;
  ativo: boolean;
  matriz: MatrizEdicao;
};

const INICIAL: Valores = { nome: '', descricao: '', ativo: true, matriz: {} };
type Erros = Partial<Record<'nome' | 'descricao', string>>;

export function FormularioPerfil({ modo, id, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarPerfil();
  const atualizar = useAtualizarPerfil();
  const detalhe = usePerfilPorId(modo === 'editar' ? id ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        nome: detalhe.data.nome,
        descricao: detalhe.data.descricao ?? '',
        ativo: detalhe.data.ativo,
        matriz: paraMatriz(detalhe.data.permissoes),
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
    if (valores.nome.trim().length < 2) ne.nome = 'Nome obrigatório.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }

    const permissoes = deMatriz(valores.matriz);

    try {
      if (modo === 'criar') {
        await cadastrar.mutateAsync({
          nome: valores.nome.trim(),
          descricao: valores.descricao.trim() || null,
          permissoes,
        });
      } else {
        if (!id) throw new Error('ID ausente.');
        await atualizar.mutateAsync({
          id,
          payload: {
            nome: valores.nome.trim(),
            descricao: valores.descricao.trim() || null,
            ativo: valores.ativo,
            permissoes,
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

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Nome" htmlFor="nome" erro={erros.nome} required>
          <Input id="nome" value={valores.nome} onChange={(e) => set('nome', e.target.value)} required />
        </Campo>

        <Campo label="Descrição" htmlFor="descricao" erro={erros.descricao}>
          <Input
            id="descricao"
            value={valores.descricao}
            onChange={(e) => set('descricao', e.target.value)}
            placeholder="Opcional"
          />
        </Campo>

        {modo === 'editar' ? (
          <Campo label="Situação" htmlFor="ativo">
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                id="ativo"
                type="checkbox"
                checked={valores.ativo}
                onChange={(e) => set('ativo', e.target.checked)}
              />
              <span>Ativo</span>
            </label>
          </Campo>
        ) : null}
      </div>

      <section>
        <h3 className="mb-2 text-sm font-semibold text-gray-900">Permissões</h3>
        <p className="mb-3 text-xs text-gray-500">
          Marque o que este perfil pode fazer em cada módulo. Quem tiver este perfil herda essas permissões.
        </p>
        <MatrizPermissoes
          matriz={valores.matriz}
          aoMudar={(m) => set('matriz', m)}
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
