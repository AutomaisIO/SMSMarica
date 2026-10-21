import { useEffect, useState } from 'react';
import { KeyRound, Loader2, PlugZap } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import {
  useAtualizarFonteDados,
  useCriarFonteDados,
  useGerarTokenAgente,
  useTestarFonteDados,
} from '@/features/fontes-dados/api/fontesDadosApi';
import { EnderecoSisregStatus } from '@/features/fontes-dados/components/EnderecoSisregStatus';
import { TokenAgenteGerado } from '@/features/fontes-dados/components/TokenAgenteGerado';
import { KLINIKOS_BUILDS, ROTULO_TIPO, TIPOS_CADASTRAVEIS, rotuloTipo } from '@/features/fontes-dados/lib/rotulos';
import type {
  AtualizarFonteDadosPayload,
  FonteDados,
  ResultadoTesteFonte,
  TokenAgente,
} from '@/features/fontes-dados/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  /** undefined => nova base; objeto => edição de qualquer fonte. */
  fonte?: FonteDados;
  podeTestar: boolean;
};

type Form = {
  nome: string;
  slug: string;
  tipo: string;
  dialeto: string;
  ambiente: string;
  host: string;
  porta: string;
  servico: string;
  usuario: string;
  senha: string;
  token: string;
  baseUrl: string;
  familia: string;
  ativo: boolean;
  parametros: Record<string, string>;
};

const DIALETO_POR_TIPO: Record<string, string> = {
  Postgres: 'Postgres',
  Regulacao: 'Postgres',
  Atendimento: 'Postgres',
  Fhir: '',
  KlinikosWeb: '',
};

function inicial(fonte?: FonteDados): Form {
  const parametros: Record<string, string> = {};
  for (const [k, v] of Object.entries(fonte?.parametros ?? {})) parametros[k] = v ?? '';
  return {
    nome: fonte?.nome ?? '',
    slug: fonte?.slug ?? '',
    tipo: fonte?.tipo ?? 'Salux',
    dialeto: fonte?.dialeto ?? 'Oracle',
    // A API devolve o NOME do enum ("Producao"): o select usa caixa alta.
    ambiente: (fonte?.ambiente ?? 'PRODUCAO').toUpperCase(),
    host: fonte?.host ?? '',
    porta: fonte?.porta != null ? String(fonte.porta) : '',
    servico: fonte?.servico ?? '',
    usuario: fonte?.usuario ?? '',
    senha: '',
    token: '',
    baseUrl: fonte?.baseUrl ?? '',
    familia: fonte?.familia ?? '',
    ativo: fonte?.ativo ?? true,
    parametros,
  };
}

/**
 * Cria ou edita uma fonte de dados (ADR-0076). Mostra só os campos de CONEXÃO de cada tipo; o uso
 * (agenda, cadência, conhecimento) fica na tela do recurso. Senha, token e login de operador nunca
 * voltam do servidor: vazio = mantém o que está gravado.
 */
export function FonteDadosModal({ aberto, aoFechar, fonte, podeTestar }: Props) {
  const ehEdicao = Boolean(fonte);
  const ehSistema = fonte?.origem === 'sistema';
  const [form, setForm] = useState<Form>(() => inicial(fonte));
  const [erro, setErro] = useState<string | null>(null);
  const [teste, setTeste] = useState<ResultadoTesteFonte | null>(null);
  const [tokenNovo, setTokenNovo] = useState<TokenAgente | null>(null);

  const criar = useCriarFonteDados();
  const atualizar = useAtualizarFonteDados();
  const testar = useTestarFonteDados();
  const gerarToken = useGerarTokenAgente();

  useEffect(() => {
    if (aberto) {
      setForm(inicial(fonte));
      setErro(null);
      setTeste(null);
      setTokenNovo(null);
    }
  }, [aberto, fonte]);

  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm((f) => ({ ...f, [k]: v }));
  const setParam = (k: string, v: string) => setForm((f) => ({ ...f, parametros: { ...f.parametros, [k]: v } }));
  const param = (k: string) => form.parametros[k] ?? '';

  const tipo = form.tipo;
  const ehAgente = fonte?.viaAgente ?? false;
  const ehWeb = tipo === 'Fhir' || tipo === 'KlinikosWeb';
  const salvando = criar.isPending || atualizar.isPending;

  function validar(): string | null {
    if (!ehSistema && !form.nome.trim()) return 'Informe o nome.';
    if (fonte?.loginJunto && Boolean(form.usuario.trim()) !== Boolean(form.senha)) {
      return 'Para trocar o login informe usuário e senha juntos — o sistema testa o login antes de gravar.';
    }
    if (fonte?.chave === 'esuspec') {
      const hora = /^\d{2}:\d{2}$/;
      if ((param('janelaInicio') && !hora.test(param('janelaInicio'))) || (param('janelaFim') && !hora.test(param('janelaFim')))) {
        return 'Informe a janela no formato HH:MM (ex.: 02:00 e 05:00).';
      }
    }
    return null;
  }

  function parametrosDoTipo(): Record<string, string | null> | undefined {
    if (tipo === 'KlinikosWeb') {
      return { appRoot: param('appRoot'), unidCodigo: param('unidCodigo'), metaSource: param('metaSource'), build: param('build') };
    }
    if (!ehSistema) return undefined;
    // Só as chaves de conexão que o servidor devolveu para este sistema.
    return Object.fromEntries(Object.keys(fonte?.parametros ?? {}).map((k) => [k, param(k)]));
  }

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    const invalido = validar();
    if (invalido) {
      setErro(invalido);
      return;
    }
    setErro(null);
    const parametros = parametrosDoTipo();

    if (!fonte) {
      criar.mutate(
        {
          nome: form.nome.trim(),
          slug: form.slug.trim() || undefined,
          tipo,
          dialeto: form.dialeto || undefined,
          ambiente: form.ambiente,
          host: form.host.trim() || undefined,
          porta: form.porta.trim() ? Number(form.porta) : undefined,
          servico: form.servico.trim() || undefined,
          usuario: form.usuario.trim() || undefined,
          senha: form.senha || undefined,
          baseUrl: form.baseUrl.trim() || undefined,
          familia: form.familia.trim() || undefined,
          parametros,
        },
        { onSuccess: aoFechar, onError: (err) => setErro(extrairMensagemDeErro(err)) },
      );
      return;
    }

    // Base: campo vazio vai como '' (limpa); a família nunca some por omissão.
    const payload: AtualizarFonteDadosPayload = ehSistema
      ? {
          usuario: form.usuario.trim() || undefined,
          senha: form.senha || undefined,
          token: form.token || undefined,
          baseUrl: form.baseUrl.trim(),
          ativo: form.ativo,
          parametros,
        }
      : {
          nome: form.nome.trim(),
          slug: form.slug.trim(),
          ambiente: form.ambiente,
          host: form.host.trim(),
          porta: form.porta.trim() ? Number(form.porta) : undefined,
          servico: form.servico.trim(),
          usuario: form.usuario.trim(),
          senha: form.senha || undefined,
          baseUrl: form.baseUrl.trim(),
          familia: form.familia.trim(),
          ativo: form.ativo,
          parametros,
        };
    if (fonte.chave === 'sisreg-api') payload.usuario = form.usuario.trim();

    atualizar.mutate(
      { chave: fonte.chave, payload },
      { onSuccess: aoFechar, onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  function aoTestar() {
    if (!fonte) return;
    setTeste(null);
    const payload = fonte.loginJunto
      ? { usuario: form.usuario.trim(), senha: form.senha, cliente: param('cliente') || undefined }
      : undefined;
    if (fonte.loginJunto && (!payload?.usuario || !payload.senha)) {
      setTeste({ sucesso: false, mensagem: 'Digite usuário e senha para testar — o login gravado não é exibido.' });
      return;
    }
    testar.mutate(
      { chave: fonte.chave, payload },
      {
        onSuccess: setTeste,
        onError: (err) => setTeste({ sucesso: false, mensagem: extrairMensagemDeErro(err) }),
      },
    );
  }

  function aoGerarToken() {
    if (!fonte) return;
    if (!window.confirm('Gerar um token novo desconecta o agente atual até o token novo ser colado no .env dele. Continuar?')) {
      return;
    }
    gerarToken.mutate(fonte.chave, {
      onSuccess: setTokenNovo,
      onError: (err) => setErro(extrairMensagemDeErro(err)),
    });
  }

  const titulo = !fonte ? 'Nova base de dados' : `Editar — ${fonte.nome}`;

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo={titulo} descricao={fonte ? rotuloTipo(fonte.tipo) : undefined} largura="lg">
      <form onSubmit={aoSalvar} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {!ehSistema ? camposDaBase() : null}
          {ehSistema ? camposDoSistema() : null}

          <label className="flex items-center gap-2 sm:col-span-2">
            <input
              type="checkbox"
              checked={form.ativo}
              onChange={(e) => set('ativo', e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-primary-600"
            />
            <span className="text-sm text-gray-700">Ativa — desmarcada, quem usa esta fonte para de ler dela</span>
          </label>
        </div>

        {fonte?.chave === 'sisreg' ? <EnderecoSisregStatus /> : null}

        {ehAgente && fonte ? (
          <div className="space-y-2 rounded-lg border border-gray-200 p-3">
            <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
              <span className={fonte.agenteConectado ? 'text-green-700' : 'text-amber-700'}>
                {fonte.agenteConectado ? 'Agente conectado agora.' : 'Agente desconectado.'}{' '}
                <span className="text-gray-500">
                  As credenciais do banco ficam no .env do agente, no servidor da unidade — não aqui.
                </span>
              </span>
              <Button type="button" variante="outline" tamanho="sm" onClick={aoGerarToken} disabled={gerarToken.isPending}>
                {gerarToken.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <KeyRound className="mr-2 h-4 w-4" />}
                Gerar novo token
              </Button>
            </div>
            {tokenNovo ? <TokenAgenteGerado token={tokenNovo} /> : null}
          </div>
        ) : null}

        {erro ? <Aviso tipo="erro">{erro}</Aviso> : null}
        {teste ? <Aviso tipo={teste.sucesso ? 'ok' : 'erro'}>{teste.mensagem ?? (teste.sucesso ? 'Conexão OK.' : 'Falhou.')}</Aviso> : null}

        <div className="flex items-center justify-between gap-3 border-t border-gray-100 pt-4">
          {fonte && podeTestar ? (
            fonte.testavel ? (
              <Button type="button" variante="outline" onClick={aoTestar} disabled={testar.isPending}>
                {testar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <PlugZap className="mr-2 h-4 w-4" />}
                {fonte.loginJunto ? 'Testar o login digitado' : 'Testar conexão'}
              </Button>
            ) : (
              <span className="max-w-sm text-xs text-gray-500">Sem teste: {fonte.motivoSemTeste}</span>
            )
          ) : (
            <span />
          )}
          <div className="flex items-center gap-2">
            <Button type="button" variante="ghost" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={salvando}>
              {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              {fonte?.loginJunto && form.usuario.trim() ? 'Testar e salvar' : 'Salvar'}
            </Button>
          </div>
        </div>
      </form>
    </Modal>
  );

  // ---- Campos de BASE (ia_fonte) ----
  // Funções que devolvem JSX, chamadas direto — não componentes: um componente declarado aqui dentro
  // seria um tipo novo a cada render, e cada tecla remontaria os campos (perdendo o foco).
  function camposDaBase() {
    return (
      <>
        <Campo label="Nome" htmlFor="fd-nome" required>
          <Input id="fd-nome" value={form.nome} onChange={(e) => set('nome', e.target.value)} placeholder="Ex.: Salux Produção" />
        </Campo>
        <Campo
          label="Slug (identificador)"
          htmlFor="fd-slug"
          dica="Curto e estável (ex.: salux-hcml). Marca a origem dos dados importados — não mude depois de importar."
        >
          <Input id="fd-slug" value={form.slug} onChange={(e) => set('slug', e.target.value)} placeholder="salux-hcml" />
        </Campo>

        <Campo label="Tipo" htmlFor="fd-tipo" required dica={ehEdicao ? 'Fixo depois de criada.' : undefined}>
          <Select
            id="fd-tipo"
            value={tipo}
            disabled={ehEdicao}
            onChange={(e) => {
              const t = e.target.value;
              setForm((f) => ({
                ...f,
                tipo: t,
                dialeto: DIALETO_POR_TIPO[t] ?? 'Oracle',
                familia: t === 'KlinikosWeb' && !f.familia ? 'klinikos' : f.familia,
              }));
            }}
          >
            {(ehEdicao ? [tipo] : TIPOS_CADASTRAVEIS).map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO[t] ?? t}
              </option>
            ))}
          </Select>
        </Campo>

        {!ehWeb ? (
          <Campo label="Dialeto SQL" htmlFor="fd-dialeto" dica={ehEdicao ? 'Fixo depois de criada.' : undefined}>
            <Select id="fd-dialeto" value={form.dialeto} disabled={ehEdicao} onChange={(e) => set('dialeto', e.target.value)}>
              <option value="Oracle">Oracle</option>
              <option value="Postgres">PostgreSQL</option>
              {form.dialeto === 'SqlServer' ? <option value="SqlServer">SQL Server (T-SQL)</option> : null}
            </Select>
          </Campo>
        ) : null}

        <Campo label="Ambiente" htmlFor="fd-ambiente" required>
          <Select id="fd-ambiente" value={form.ambiente} onChange={(e) => set('ambiente', e.target.value)}>
            <option value="PRODUCAO">Produção</option>
            <option value="TREINAMENTO">Treinamento</option>
          </Select>
        </Campo>

        <Campo label="Família" htmlFor="fd-familia" dica="Bases de mesma estrutura (ex.: klinikos). A importação reconhece a base por tipo + família.">
          <Input id="fd-familia" value={form.familia} onChange={(e) => set('familia', e.target.value)} placeholder="klinikos" />
        </Campo>

        {!ehWeb && !ehAgente ? (
          <>
            <Campo label="Host / IP" htmlFor="fd-host">
              <Input id="fd-host" value={form.host} onChange={(e) => set('host', e.target.value)} placeholder="10.0.0.10" />
            </Campo>
            <Campo label="Porta" htmlFor="fd-porta">
              <Input id="fd-porta" type="number" value={form.porta} onChange={(e) => set('porta', e.target.value)} placeholder={form.dialeto === 'Postgres' ? '5432' : '1521'} />
            </Campo>
            <Campo label={form.dialeto === 'Postgres' ? 'Database' : 'Serviço / SID'} htmlFor="fd-servico">
              <Input id="fd-servico" value={form.servico} onChange={(e) => set('servico', e.target.value)} />
            </Campo>
          </>
        ) : null}

        {ehWeb ? (
          <Campo label="URL" htmlFor="fd-url" className="sm:col-span-2">
            <Input
              id="fd-url"
              value={form.baseUrl}
              onChange={(e) => set('baseUrl', e.target.value)}
              placeholder={tipo === 'KlinikosWeb' ? 'https://klinikos.exemplo' : 'https://api.exemplo.com'}
            />
          </Campo>
        ) : null}

        {!ehAgente ? (
          <>
            <Campo label="Usuário" htmlFor="fd-usuario">
              <Input id="fd-usuario" value={form.usuario} onChange={(e) => set('usuario', e.target.value)} autoComplete="off" />
            </Campo>
            {campoSenha(fonte?.senhaDefinida ?? false)}
          </>
        ) : null}

        {tipo === 'KlinikosWeb' ? (
          <>
            <Campo
              label="Versão do Klinikos (build)"
              htmlFor="fd-build"
              dica="O conector confere a versão na tela e só lê se bater com esta."
            >
              <Select id="fd-build" value={param('build')} onChange={(e) => setParam('build', e.target.value)}>
                <option value="">— selecione a versão —</option>
                {KLINIKOS_BUILDS.map((b) => (
                  <option key={b.versao} value={b.versao}>
                    {b.rotulo}
                  </option>
                ))}
              </Select>
            </Campo>
            <Campo label="Caminho da aplicação" htmlFor="fd-approot" dica="/KlinikosNet (Conde) ou /UPA24H (UPAs).">
              <Input id="fd-approot" value={param('appRoot')} onChange={(e) => setParam('appRoot', e.target.value)} placeholder="/UPA24H" />
            </Campo>
            <Campo label="Código da unidade" htmlFor="fd-unid">
              <Input id="fd-unid" value={param('unidCodigo')} onChange={(e) => setParam('unidCodigo', e.target.value)} placeholder="0006" />
            </Campo>
            <Campo label="Origem no prontuário (meta.source)" htmlFor="fd-metasource" dica="Vazio = derivada do slug.">
              <Input id="fd-metasource" value={param('metaSource')} onChange={(e) => setParam('metaSource', e.target.value)} />
            </Campo>
          </>
        ) : null}
      </>
    );
  }

  // ---- Campos de SISTEMA com login (integracao_credencial / sisreg_configuracao) ----
  function camposDoSistema() {
    if (!fonte) return null;
    const chave = fonte.chave;
    const ehApi = chave === 'sisreg-api';
    const ehEsusSg = chave === 'esussg';
    const ehPec = chave === 'esuspec';

    return (
      <>
        {!ehEsusSg ? (
          <Campo
            label="URL"
            htmlFor="fd-url"
            className="sm:col-span-2"
            dica={fonte.urlPadrao ? `Vazio = ${fonte.urlPadrao}` : undefined}
          >
            <Input id="fd-url" value={form.baseUrl} onChange={(e) => set('baseUrl', e.target.value)} placeholder={fonte.urlPadrao ?? ''} />
          </Campo>
        ) : null}

        {ehApi ? (
          <Campo label="Autenticação" htmlFor="fd-auth">
            <Select id="fd-auth" value={param('tipoAutenticacao') || 'Basic'} onChange={(e) => setParam('tipoAutenticacao', e.target.value)}>
              <option value="Basic">Login e senha (Basic)</option>
              <option value="Bearer">Token (Bearer)</option>
              <option value="ApiKey">Chave de API</option>
            </Select>
          </Campo>
        ) : null}

        <Campo
          label={ehPec ? 'Usuário (CPF)' : 'Usuário'}
          htmlFor="fd-usuario"
          dica={
            ehApi
              ? undefined
              : fonte.loginDefinido
                ? fonte.loginJunto
                  ? 'Já definido — para trocar, informe usuário e senha.'
                  : 'Já definido — preencha apenas para trocar.'
                : 'Ainda não definido.'
          }
        >
          <Input
            id="fd-usuario"
            value={form.usuario}
            onChange={(e) => set('usuario', e.target.value)}
            placeholder={!ehApi && fonte.loginDefinido ? '••••••••' : ''}
            autoComplete="off"
          />
        </Campo>
        {campoSenha(fonte.senhaDefinida)}

        {ehApi ? (
          <Campo label="Token" htmlFor="fd-token" dica={fonte.tokenDefinido ? 'Já definido — preencha apenas para trocar.' : undefined}>
            <Input id="fd-token" type="password" value={form.token} onChange={(e) => set('token', e.target.value)} autoComplete="new-password" />
          </Campo>
        ) : null}

        {chave === 'sisreg' ? (
          <label className="flex items-center gap-2 sm:col-span-2">
            <input
              type="checkbox"
              checked={param('autoLogin') === 'true'}
              onChange={(e) => setParam('autoLogin', e.target.checked ? 'true' : 'false')}
              className="h-4 w-4 rounded border-gray-300 text-primary-600"
            />
            <span className="text-sm text-gray-700">
              Entrar de novo sozinho quando a sessão cair — desligado, o robô não briga com um operador usando o mesmo login
            </span>
          </label>
        ) : null}

        {ehEsusSg ? (
          <>
            <Campo label="Cliente" htmlFor="fd-cliente">
              <Input id="fd-cliente" value={param('cliente')} onChange={(e) => setParam('cliente', e.target.value)} placeholder="SGO" />
            </Campo>
            <Campo label="URL da API" htmlFor="fd-api" dica={fonte.urlPadrao ? `Vazio = ${fonte.urlPadrao}` : undefined}>
              <Input id="fd-api" value={param('apiUrl')} onChange={(e) => setParam('apiUrl', e.target.value)} />
            </Campo>
            <Campo label="URL do legado" htmlFor="fd-legado" className="sm:col-span-2">
              <Input id="fd-legado" value={param('legadoUrl')} onChange={(e) => setParam('legadoUrl', e.target.value)} />
            </Campo>
          </>
        ) : null}

        {ehPec ? (
          <>
            <Campo label="Acesso (acessoId)" htmlFor="fd-acesso">
              <Input id="fd-acesso" value={param('acessoId')} onChange={(e) => setParam('acessoId', e.target.value)} />
            </Campo>
            <Campo
              label="Janela da madrugada"
              htmlFor="fd-janela-ini"
              dica="A rotina só usa o login nesta janela: o login é cedido e a sessão é única."
            >
              <div className="flex items-center gap-2">
                <Input id="fd-janela-ini" value={param('janelaInicio')} onChange={(e) => setParam('janelaInicio', e.target.value)} placeholder="02:00" />
                <span className="text-sm text-gray-500">até</span>
                <Input id="fd-janela-fim" value={param('janelaFim')} onChange={(e) => setParam('janelaFim', e.target.value)} placeholder="05:00" />
              </div>
            </Campo>
          </>
        ) : null}
      </>
    );
  }

  function campoSenha(definida: boolean) {
    return (
      <Campo label="Senha" htmlFor="fd-senha" dica={definida ? 'Já definida — preencha apenas para trocar.' : 'Ainda não definida.'}>
        <Input
          id="fd-senha"
          type="password"
          value={form.senha}
          onChange={(e) => set('senha', e.target.value)}
          placeholder={definida ? '••••••••' : ''}
          autoComplete="new-password"
        />
      </Campo>
    );
  }
}

function Aviso({ tipo, children }: { tipo: 'ok' | 'erro'; children: React.ReactNode }) {
  return (
    <div
      className={
        tipo === 'ok'
          ? 'rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700'
          : 'rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700'
      }
    >
      {children}
    </div>
  );
}
