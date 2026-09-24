import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BellRing, Loader2, PlugZap, Settings2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { testarConexaoSisreg } from '@/features/sisreg/api/sisregApi';
import {
  useAtualizarConfiguracaoSisreg,
  useConfiguracaoSisreg,
} from '@/features/sisreg/api/queries';
import { FilaEsperaSecao } from '@/features/sisreg/components/FilaEsperaSecao';
import { SincronismoAutomaticoSecao } from '@/features/sisreg/components/SincronismoAutomaticoSecao';
import { SincronismoEscalasSecao } from '@/features/sisreg/components/SincronismoEscalasSecao';
import { SincronizarTudoSecao } from '@/features/sisreg/components/SincronizarTudoSecao';
import type {
  AtualizarSisregConfiguracaoPayload,
  EscopoSisreg,
  FonteCadastroPaciente,
  TipoAutenticacaoSisreg,
} from '@/features/sisreg/types';

type Form = {
  baseUrl: string;
  escopo: EscopoSisreg;
  uf: string;
  municipio: string;
  centraisReguladoras: string;
  tipoAutenticacao: TipoAutenticacaoSisreg;
  login: string;
  senha: string;
  token: string;
  ativo: boolean;
  fonteCadastroPaciente: FonteCadastroPaciente;
  consultasSimultaneasSer: number;
};

/** Campo vazio vira 1 em vez de NaN; a faixa aceita pelo backend é 1..8. */
function normalizarSimultaneas(valor: string | number): number {
  const n = Math.trunc(Number(valor));
  if (!Number.isFinite(n) || n < 1) return 1;
  return Math.min(n, 8);
}

const FORM_VAZIO: Form = {
  baseUrl: 'https://sisreg-es.saude.gov.br/',
  escopo: 'Municipal',
  uf: '',
  municipio: '',
  centraisReguladoras: '',
  tipoAutenticacao: 'Basic',
  login: '',
  senha: '',
  token: '',
  ativo: true,
  fonteCadastroPaciente: 'Sisreg',
  consultasSimultaneasSer: 1,
};

export function SisregConfiguracaoPage() {
  const config = useConfiguracaoSisreg();
  const salvar = useAtualizarConfiguracaoSisreg();

  const [form, setForm] = useState<Form>(FORM_VAZIO);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);
  const [teste, setTeste] = useState<{ ok: boolean; msg: string } | null>(null);
  const [testando, setTestando] = useState(false);

  useEffect(() => {
    if (config.data) {
      setForm((f) => ({
        ...f,
        baseUrl: config.data.baseUrl,
        escopo: config.data.escopo,
        uf: config.data.uf,
        municipio: config.data.municipio,
        centraisReguladoras: config.data.centraisReguladoras,
        tipoAutenticacao: config.data.tipoAutenticacao,
        login: config.data.login ?? '',
        ativo: config.data.ativo,
        fonteCadastroPaciente: config.data.fonteCadastroPaciente,
        consultasSimultaneasSer: normalizarSimultaneas(config.data.consultasSimultaneasSer),
      }));
    }
  }, [config.data]);

  function set<K extends keyof Form>(chave: K, valor: Form[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
    setSalvo(false);
  }

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    const payload: AtualizarSisregConfiguracaoPayload = {
      baseUrl: form.baseUrl.trim(),
      escopo: form.escopo,
      uf: form.uf.trim(),
      municipio: form.municipio.trim(),
      centraisReguladoras: form.centraisReguladoras.trim(),
      tipoAutenticacao: form.tipoAutenticacao,
      login: form.login.trim() || null,
      senha: form.senha ? form.senha : undefined,
      token: form.token ? form.token : undefined,
      ativo: form.ativo,
      fonteCadastroPaciente: form.fonteCadastroPaciente,
      consultasSimultaneasSer: normalizarSimultaneas(form.consultasSimultaneasSer),
    };
    salvar.mutate(payload, {
      onSuccess: () => {
        setSalvo(true);
        setForm((f) => ({ ...f, senha: '', token: '' }));
      },
      onError: (err) => setErro(extrairMensagemDeErro(err)),
    });
  }

  async function aoTestar() {
    setTestando(true);
    setTeste(null);
    try {
      const r = await testarConexaoSisreg();
      setTeste({ ok: r.sucesso, msg: r.mensagem });
    } catch (err) {
      setTeste({ ok: false, msg: extrairMensagemDeErro(err) });
    } finally {
      setTestando(false);
    }
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Settings2 className="h-6 w-6 text-primary-600" />
          Configuração SISREG
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Credenciais e escopo da integração de leitura com o SISREG (DATASUS). Senha e token são
          gravados de forma segura e nunca exibidos.
        </p>
      </header>

      {config.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(config.error)}
        </div>
      ) : null}

      {/* Antes do formulário: é o que se procura com pressa quando o sincronismo precisa parar. */}
      <SincronismoAutomaticoSecao />

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <form onSubmit={aoSalvar} className="space-y-5">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="URL base" htmlFor="sr-url" className="sm:col-span-2">
              <Input id="sr-url" value={form.baseUrl} onChange={(e) => set('baseUrl', e.target.value)} />
            </Campo>

            <Campo label="Escopo" htmlFor="sr-escopo">
              <Select
                id="sr-escopo"
                value={form.escopo}
                onChange={(e) => set('escopo', e.target.value as EscopoSisreg)}
              >
                <option value="Municipal">Municipal</option>
                <option value="Nacional">Nacional</option>
              </Select>
            </Campo>

            <Campo label="Tipo de autenticação" htmlFor="sr-auth">
              <Select
                id="sr-auth"
                value={form.tipoAutenticacao}
                onChange={(e) => set('tipoAutenticacao', e.target.value as TipoAutenticacaoSisreg)}
              >
                <option value="Basic">Basic (login/senha)</option>
                <option value="Bearer">Bearer (token)</option>
                <option value="ApiKey">ApiKey (token)</option>
              </Select>
            </Campo>

            <Campo label="UF" htmlFor="sr-uf" dica="Ex.: RJ — usado no escopo municipal.">
              <Input id="sr-uf" value={form.uf} onChange={(e) => set('uf', e.target.value)} placeholder="RJ" />
            </Campo>

            <Campo label="Município (IBGE)" htmlFor="sr-mun" dica="Ex.: 3302700 (Maricá).">
              <Input
                id="sr-mun"
                value={form.municipio}
                onChange={(e) => set('municipio', e.target.value)}
                placeholder="3302700"
              />
            </Campo>

            <Campo
              label="Centrais reguladoras"
              htmlFor="sr-centrais"
              className="sm:col-span-2"
              dica="Códigos separados por vírgula (campo codigo_central_reguladora)."
            >
              <Input
                id="sr-centrais"
                value={form.centraisReguladoras}
                onChange={(e) => set('centraisReguladoras', e.target.value)}
                placeholder="32C164, 32C206, 32C211"
              />
            </Campo>

            <Campo
              label="Consulta de cadastro do paciente"
              htmlFor="sr-fonte-cadastro"
              className="sm:col-span-2"
              dica="Onde a importação de agendamentos busca os dados de cadastro (CADSUS) do paciente. O SISREG aceita cerca de 500 consultas por hora e depois passa a exigir CAPTCHA, travando o operador da unidade — para importações grandes, prefira o SER, que consulta o mesmo cadastro sem esse limite."
            >
              <Select
                id="sr-fonte-cadastro"
                value={form.fonteCadastroPaciente}
                onChange={(e) => set('fonteCadastroPaciente', e.target.value as FonteCadastroPaciente)}
              >
                <option value="Sisreg">SISREG (CADSUS)</option>
                <option value="Ser">SER (SES-RJ)</option>
                <option value="SerComFallbackSisreg">SER, com retorno ao SISREG se o SER falhar</option>
              </Select>
            </Campo>

            <Campo
              label="Consultas simultâneas no SER"
              htmlFor="sr-consultas-ser"
              className="sm:col-span-2"
              dica="Quantas consultas de cadastro correm ao mesmo tempo, cada uma em sua própria sessão do SER. 1 = uma de cada vez."
            >
              <Input
                id="sr-consultas-ser"
                type="number"
                min={1}
                max={8}
                step={1}
                className="sm:max-w-xs"
                value={form.consultasSimultaneasSer}
                disabled={form.fonteCadastroPaciente === 'Sisreg'}
                onChange={(e) => set('consultasSimultaneasSer', normalizarSimultaneas(e.target.value))}
              />
            </Campo>

            <Campo label="Login" htmlFor="sr-login" dica="Usado no esquema Basic.">
              <Input id="sr-login" value={form.login} onChange={(e) => set('login', e.target.value)} autoComplete="off" />
            </Campo>

            <Campo
              label="Senha"
              htmlFor="sr-senha"
              dica={config.data?.senhaDefinida ? 'Já definida — preencha apenas para substituir.' : 'Ainda não definida.'}
            >
              <Input
                id="sr-senha"
                type="password"
                value={form.senha}
                onChange={(e) => set('senha', e.target.value)}
                placeholder={config.data?.senhaDefinida ? '••••••••' : 'Senha (esquema Basic)'}
                autoComplete="new-password"
              />
            </Campo>

            <Campo
              label="Token"
              htmlFor="sr-token"
              className="sm:col-span-2"
              dica={config.data?.tokenDefinido ? 'Já definido — preencha apenas para substituir.' : 'Usado nos esquemas Bearer/ApiKey.'}
            >
              <Input
                id="sr-token"
                type="password"
                value={form.token}
                onChange={(e) => set('token', e.target.value)}
                placeholder={config.data?.tokenDefinido ? '••••••••••••' : 'Cole o token aqui'}
                autoComplete="new-password"
              />
            </Campo>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={form.ativo} onChange={(e) => set('ativo', e.target.checked)} />
            Integração ativa
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {teste ? (
            <div
              className={
                teste.ok
                  ? 'rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700'
                  : 'rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-700'
              }
            >
              {teste.msg}
            </div>
          ) : null}

          <div className="flex flex-wrap items-center justify-end gap-3">
            {salvo ? <span className="text-sm text-green-600">Configuração salva.</span> : null}
            <Button type="button" variante="outline" onClick={aoTestar} disabled={testando}>
              {testando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <PlugZap className="mr-2 h-4 w-4" />}
              Testar conexão
            </Button>
            <Button type="submit" disabled={salvar.isPending || config.isPending}>
              {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar configuração
            </Button>
          </div>
        </form>
      </section>

      <SincronizarTudoSecao />

      <SincronismoEscalasSecao />

      <FilaEsperaSecao />

      {/* Não há telefone de aviso por integração: a lista é uma só, em Avisos no celular. */}
      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <BellRing className="h-5 w-5 text-primary-600" />
          Avisos de falha do sincronismo
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          CAPTCHA, credencial derrubada e unidade com erro chegam no WhatsApp de quem está em{' '}
          <Link to="/app/avisos-celular" className="font-medium text-primary-700 underline">
            Sistema → Avisos no celular
          </Link>
          {' '}— a única lista de avisos de erro da plataforma. Cadastre ou tire telefones lá.
        </p>
      </section>
    </div>
  );
}
