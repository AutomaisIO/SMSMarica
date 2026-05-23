import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueries } from '@tanstack/react-query';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarOverridesDoUsuario,
  useAtualizarPerfisDoUsuario,
  useAtualizarUsuario,
  useCadastrarUsuario,
  useUsuarioPermissoes,
  useUsuarioPorId,
} from '@/features/usuarios/api/queries';
import { useListarPerfis } from '@/features/perfis/api/queries';
import { obterPerfilPorId } from '@/features/perfis/api/perfisApi';
import {
  deMatriz,
  paraMatriz,
} from '@/features/perfis/lib/acoes';
import type { MatrizEdicao, PermissaoModuloApi } from '@/features/perfis/types';
import { MatrizPermissoes } from '@/features/perfis/components/MatrizPermissoes';
import { cn } from '@/shared/lib/cn';
import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';

type Props = { modo: 'criar' | 'editar'; idUsuario?: string | null; aoConcluir: () => void };

type Valores = {
  nomeCompleto: string;
  email: string;
  senha: string;
  cpf: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
};

const INICIAL: Valores = {
  nomeCompleto: '',
  email: '',
  senha: '',
  cpf: '',
  telefone: '',
  endereco: enderecoVazio,
  fotoBase64: null,
};

type Erros = Partial<Record<'nomeCompleto' | 'email' | 'senha' | 'cpf' | 'telefone', string>>;

/** Une duas matrizes (OR por módulo). */
function unirMatrizes(a: MatrizEdicao, b: MatrizEdicao): MatrizEdicao {
  const out: MatrizEdicao = {};
  const modulos = new Set<ModuloPermissao>([
    ...(Object.keys(a) as ModuloPermissao[]),
    ...(Object.keys(b) as ModuloPermissao[]),
  ]);
  for (const m of modulos) {
    const set = new Set<AcaoPermissao>([...(a[m] ?? []), ...(b[m] ?? [])]);
    out[m] = Array.from(set);
  }
  return out;
}

export function FormularioUsuario({ modo, idUsuario, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [perfilIdsSelecionados, setPerfilIdsSelecionados] = useState<string[]>([]);
  const [overrides, setOverrides] = useState<MatrizEdicao>({});
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);

  const cadastrar = useCadastrarUsuario();
  const atualizar = useAtualizarUsuario();
  const salvarPerfis = useAtualizarPerfisDoUsuario();
  const salvarOverrides = useAtualizarOverridesDoUsuario();
  const detalhe = useUsuarioPorId(modo === 'editar' ? idUsuario ?? null : null);
  const permissoesUsuario = useUsuarioPermissoes(modo === 'editar' ? idUsuario ?? null : null);
  const perfisDisponiveis = useListarPerfis();

  // Hidrata o form em modo edição.
  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        email: detalhe.data.email,
        senha: '',
        cpf: detalhe.data.cpf ?? '',
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
      setPerfilIdsSelecionados(detalhe.data.perfilIds ?? []);
    }
  }, [modo, detalhe.data]);

  // Inicializa overrides quando as permissões do usuário chegam.
  useEffect(() => {
    if (modo === 'editar' && permissoesUsuario.data) {
      setOverrides(paraMatriz(permissoesUsuario.data.overrides));
    }
  }, [modo, permissoesUsuario.data]);

  // Carrega permissões de cada perfil selecionado para compor a matriz herdada
  // localmente (sem depender do estado salvo no backend).
  const perfisCompletos = useQueries({
    queries: perfilIdsSelecionados.map((id) => ({
      queryKey: ['perfis', 'detalhe', id],
      queryFn: () => obterPerfilPorId(id),
      enabled: Boolean(id),
    })),
  });

  const herdadas = useMemo<MatrizEdicao>(() => {
    let resultado: MatrizEdicao = {};
    for (const q of perfisCompletos) {
      if (q.data) {
        resultado = unirMatrizes(resultado, paraMatriz(q.data.permissoes));
      }
    }
    return resultado;
  }, [perfisCompletos]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  function alternarPerfil(id: string) {
    setPerfilIdsSelecionados((atual) =>
      atual.includes(id) ? atual.filter((x) => x !== id) : [...atual, id],
    );
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
    if (modo === 'criar') {
      if (!/^\S+@\S+\.\S+$/.test(email)) ne.email = 'E-mail inválido.';
      if (senha && senha.length < 8) ne.senha = 'Mínimo 8 caracteres.';
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

    const overridesParaApi: PermissaoModuloApi[] = deMatriz(overrides);

    try {
      if (modo === 'criar') {
        const novoId = await cadastrar.mutateAsync({
          nomeCompleto: nome,
          email,
          cpf: cpf || undefined,
          telefone: valores.telefone || undefined,
          endereco: enderecoPayload,
          fotoBase64: valores.fotoBase64,
          senha: senha || undefined,
          perfilIds: perfilIdsSelecionados,
        });
        if (overridesParaApi.length > 0) {
          await salvarOverrides.mutateAsync({ id: novoId, overrides: overridesParaApi });
        }
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

  const abaDados = (
    <div className="space-y-5">
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

        {modo === 'criar' ? (
          <Campo label="Senha inicial" htmlFor="senha" erro={erros.senha} dica="Opcional. Mínimo 8 caracteres.">
            <Input
              id="senha"
              type="password"
              value={valores.senha}
              onChange={(e) => set('senha', e.target.value)}
              autoComplete="new-password"
            />
          </Campo>
        ) : null}

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
      </div>

      <section>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Endereço</h3>
        <FormularioEndereco
          valor={valores.endereco}
          aoMudar={(e) => set('endereco', e)}
          desabilitado={pendente}
          mostrarPontoReferencia={false}
        />
      </section>
    </div>
  );

  const perfisAtivos = (perfisDisponiveis.data ?? []).filter((p) => p.ativo);
  const carregandoHerdadas = perfisCompletos.some((q) => q.isLoading);

  const abaPermissoes = (
    <div className="space-y-6">
      <section>
        <h3 className="mb-2 text-sm font-semibold text-gray-900">Perfis</h3>
        <p className="mb-3 text-xs text-gray-500">
          Selecione um ou mais perfis. As permissões herdadas aparecem marcadas e bloqueadas na matriz abaixo.
        </p>
        {perfisDisponiveis.isLoading ? (
          <p className="text-sm text-gray-500">Carregando perfis...</p>
        ) : perfisAtivos.length === 0 ? (
          <p className="text-sm text-gray-500">Nenhum perfil cadastrado. Crie em "Cadastros → Perfis".</p>
        ) : (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {perfisAtivos.map((p) => {
              const selecionado = perfilIdsSelecionados.includes(p.id);
              return (
                <label
                  key={p.id}
                  className={cn(
                    'flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2 transition-colors',
                    selecionado ? 'border-primary-500 bg-primary-50' : 'border-gray-200 hover:bg-gray-50',
                  )}
                >
                  <input
                    type="checkbox"
                    checked={selecionado}
                    onChange={() => alternarPerfil(p.id)}
                    disabled={pendente}
                    className="mt-1"
                  />
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-gray-900">{p.nome}</div>
                    {p.descricao ? (
                      <div className="text-xs text-gray-600">{p.descricao}</div>
                    ) : null}
                    <div className="text-xs text-gray-400">{p.modulos} módulo(s)</div>
                  </div>
                </label>
              );
            })}
          </div>
        )}
      </section>

      <section>
        <div className="mb-2 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-900">Permissões resolvidas</h3>
          {carregandoHerdadas ? (
            <span className="text-xs text-gray-500">Recalculando herdadas…</span>
          ) : null}
        </div>
        <p className="mb-3 text-xs text-gray-500">
          Ações com fundo cinza são <strong>herdadas dos perfis</strong> selecionados (não podem ser
          desmarcadas). Marque ações adicionais como override individual.
        </p>
        <MatrizPermissoes
          herdadas={herdadas}
          editaveis={overrides}
          aoMudarEditaveis={setOverrides}
          desabilitado={pendente}
        />
      </section>
    </div>
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
