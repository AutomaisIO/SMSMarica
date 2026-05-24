import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQueries } from '@tanstack/react-query';
import { Copy, KeyRound, Lock, Search, ShieldAlert, Wand2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { consultarCpf } from '@/shared/api/integracoes';
import { consultarUsuarioPorCpf } from '@/features/usuarios/api/usuariosApi';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAlterarSenhaDoUsuario,
  useAtualizarOverridesDoUsuario,
  useAtualizarPerfisDoUsuario,
  useAtualizarUsuario,
  useCadastrarUsuario,
  useGerarNovaSenhaDoUsuario,
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
  dataNascimento: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
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
};

type Erros = Partial<Record<'nomeCompleto' | 'email' | 'senha' | 'cpf' | 'dataNascimento' | 'telefone', string>>;

function formatarCpfDigitos(cpf: string): string {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

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

  // Gate inicial em modo "criar": exigir CPF + data de nascimento + consulta no Hub
  // antes de abrir o resto do formulário (evita cadastros fake/duplicados).
  const [passoCpfConcluido, setPassoCpfConcluido] = useState(modo === 'editar');
  const [consultandoCpf, setConsultandoCpf] = useState(false);

  const cadastrar = useCadastrarUsuario();
  const atualizar = useAtualizarUsuario();
  const salvarPerfis = useAtualizarPerfisDoUsuario();
  const salvarOverrides = useAtualizarOverridesDoUsuario();
  const alterarSenha = useAlterarSenhaDoUsuario();
  const gerarSenha = useGerarNovaSenhaDoUsuario();
  const detalhe = useUsuarioPorId(modo === 'editar' ? idUsuario ?? null : null);
  const permissoesUsuario = useUsuarioPermissoes(modo === 'editar' ? idUsuario ?? null : null);
  const perfisDisponiveis = useListarPerfis();

  // Estado da seção Segurança (apenas em edição).
  const [senhaManual, setSenhaManual] = useState('');
  const [exigirTroca, setExigirTroca] = useState(true);
  const [erroSenha, setErroSenha] = useState<string | null>(null);
  const [senhaGerada, setSenhaGerada] = useState<string | null>(null);
  const [senhaCopiada, setSenhaCopiada] = useState(false);

  const deveTrocarAtual = detalhe.data?.deveTrocarSenha ?? false;

  // Hidrata o form em modo edição.
  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nomeCompleto: detalhe.data.nomeCompleto,
        email: detalhe.data.email,
        senha: '',
        cpf: detalhe.data.cpf ?? '',
        dataNascimento: detalhe.data.dataNascimento ?? '',
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

  async function consultarCpfHub() {
    setErros({});
    setErroGlobal(null);
    const cpfLimpo = valores.cpf.replace(/\D/g, '');
    const ne: Erros = {};
    if (cpfLimpo.length !== 11) ne.cpf = 'CPF deve ter 11 dígitos.';
    if (!valores.dataNascimento) ne.dataNascimento = 'Informe a data de nascimento.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }
    setConsultandoCpf(true);
    try {
      // Primeiro checa duplicidade no nosso DB; só vai ao Hub se for CPF inédito.
      const existente = await consultarUsuarioPorCpf(cpfLimpo);
      if (existente) {
        setErroGlobal(
          existente.ativo
            ? `Já existe usuário com este CPF: ${existente.nomeCompleto} (${existente.email}).`
            : `CPF pertence ao usuário inativo "${existente.nomeCompleto}". Peça para um administrador reativá-lo.`,
        );
        return;
      }

      const hub = await consultarCpf(cpfLimpo, valores.dataNascimento);
      // O Hub às vezes devolve dataNascimento em formato BR (dd/mm/aaaa) — mantém o ISO digitado.
      setValores((s) => ({
        ...s,
        nomeCompleto: hub.nome.trim() || s.nomeCompleto,
        cpf: hub.cpf || cpfLimpo,
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

    const ne: Erros = {};
    const nome = valores.nomeCompleto.trim();
    const email = valores.email.trim();
    const cpf = valores.cpf.trim();
    const senha = valores.senha;

    if (nome.length < 3) ne.nomeCompleto = 'Nome obrigatório (mínimo 3 caracteres).';
    if (modo === 'criar') {
      if (!/^\S+@\S+\.\S+$/.test(email)) ne.email = 'E-mail inválido.';
      if (senha && senha.length < 8) ne.senha = 'Mínimo 8 caracteres.';
      if (!valores.dataNascimento) ne.dataNascimento = 'Informe a data de nascimento.';
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
          dataNascimento: valores.dataNascimento || undefined,
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
        // Nome, CPF e data de nascimento são imutáveis — não vão no payload.
        await atualizar.mutateAsync({
          id: idUsuario,
          payload: {
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

  async function aplicarSenhaManual() {
    if (!idUsuario) return;
    setErroSenha(null);
    if (senhaManual.length < 8) {
      setErroSenha('Senha deve ter pelo menos 8 caracteres.');
      return;
    }
    try {
      await alterarSenha.mutateAsync({
        id: idUsuario,
        senhaNova: senhaManual,
        deveTrocarNoProximoLogin: exigirTroca,
      });
      setSenhaManual('');
    } catch (e) {
      setErroSenha(extrairMensagemDeErro(e));
    }
  }

  async function gerarNovaSenha() {
    if (!idUsuario) return;
    setErroSenha(null);
    setSenhaCopiada(false);
    try {
      const r = await gerarSenha.mutateAsync(idUsuario);
      setSenhaGerada(r.senhaGerada);
    } catch (e) {
      setErroSenha(extrairMensagemDeErro(e));
    }
  }

  async function copiarSenha() {
    if (!senhaGerada) return;
    try {
      await navigator.clipboard.writeText(senhaGerada);
      setSenhaCopiada(true);
      setTimeout(() => setSenhaCopiada(false), 2000);
    } catch {
      // ignore
    }
  }

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
          dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Não pode ser editado.</span>}
        >
          <Input
            id="nomeCompleto"
            value={valores.nomeCompleto}
            onChange={(e) => set('nomeCompleto', e.target.value)}
            required
            disabled
            readOnly
          />
        </Campo>

        <Campo
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
          dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}
        >
          <Input
            id="cpf"
            value={formatarCpfDigitos(valores.cpf)}
            onChange={(e) => set('cpf', e.target.value)}
            inputMode="numeric"
            placeholder="00000000000"
            disabled
            readOnly
          />
        </Campo>

        <Campo
          label="Data de nascimento"
          htmlFor="dataNascimento"
          erro={erros.dataNascimento}
          required={modo === 'criar'}
          dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}
        >
          <Input
            id="dataNascimento"
            type="date"
            value={valores.dataNascimento}
            onChange={(e) => set('dataNascimento', e.target.value)}
            disabled
            readOnly
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

      {modo === 'editar' ? (
        <section className="rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold text-gray-900">
            <KeyRound className="h-4 w-4" /> Segurança
          </h3>
          <p className="mb-3 text-xs text-gray-500">
            Defina uma nova senha manualmente ou gere uma aleatória para o usuário. Em ambos os casos
            ele pode ser forçado a trocar no próximo login.
          </p>

          {deveTrocarAtual ? (
            <div className="mb-3 flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              <ShieldAlert className="h-4 w-4 flex-shrink-0" />
              Este usuário já está marcado para trocar a senha no próximo login.
            </div>
          ) : null}

          {erroSenha ? (
            <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroSenha}
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <Campo
              label="Nova senha (manual)"
              htmlFor="senhaManual"
              className="md:col-span-2"
              dica="Mínimo 8 caracteres."
            >
              <Input
                id="senhaManual"
                type="password"
                value={senhaManual}
                onChange={(e) => setSenhaManual(e.target.value)}
                autoComplete="new-password"
                disabled={alterarSenha.isPending}
              />
            </Campo>
            <div className="flex items-end">
              <Button
                type="button"
                variante="outline"
                onClick={aplicarSenhaManual}
                disabled={alterarSenha.isPending || !senhaManual}
                className="w-full"
              >
                {alterarSenha.isPending ? 'Aplicando…' : 'Aplicar nova senha'}
              </Button>
            </div>
          </div>

          <label className="mt-3 flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={exigirTroca}
              onChange={(e) => setExigirTroca(e.target.checked)}
              disabled={alterarSenha.isPending}
            />
            <span>Exigir que o usuário troque a senha no próximo login</span>
          </label>

          <div className="mt-4 border-t border-gray-200 pt-3">
            <Button
              type="button"
              variante="secundaria"
              onClick={gerarNovaSenha}
              disabled={gerarSenha.isPending}
            >
              <Wand2 className="h-4 w-4" />
              {gerarSenha.isPending ? 'Gerando…' : 'Gerar nova senha aleatória'}
            </Button>
            <p className="mt-2 text-xs text-gray-500">
              A senha gerada será exibida uma única vez. O usuário sempre será obrigado a trocá-la no
              próximo login.
            </p>
          </div>
        </section>
      ) : null}
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
              onChange={(e) => set('cpf', e.target.value)}
              inputMode="numeric"
              placeholder="00000000000"
              autoFocus
              disabled={consultandoCpf}
            />
          </Campo>

          <Campo label="Data de nascimento" htmlFor="nascInicial" erro={erros.dataNascimento} required>
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
          <Button type="button" onClick={consultarCpfHub} disabled={consultandoCpf}>
            <Search className="h-4 w-4" />
            {consultandoCpf ? 'Consultando…' : 'Continuar'}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <>
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

      <Modal
        aberto={senhaGerada !== null}
        aoFechar={() => setSenhaGerada(null)}
        titulo="Senha gerada"
        descricao="Esta senha será exibida apenas uma vez. Anote ou copie agora e entregue ao usuário."
        largura="sm"
      >
        <div className="space-y-4">
          <div className="rounded-md border-2 border-dashed border-primary-200 bg-primary-50 px-4 py-3 text-center">
            <code className="select-all font-mono text-lg font-semibold tracking-wide text-primary-800">
              {senhaGerada}
            </code>
          </div>
          <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800">
            O usuário será obrigado a trocar esta senha no próximo login.
          </p>
          <div className="flex items-center justify-end gap-2">
            <Button type="button" variante="outline" onClick={copiarSenha}>
              <Copy className="h-4 w-4" />
              {senhaCopiada ? 'Copiada!' : 'Copiar'}
            </Button>
            <Button type="button" onClick={() => setSenhaGerada(null)}>
              Pronto
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}
