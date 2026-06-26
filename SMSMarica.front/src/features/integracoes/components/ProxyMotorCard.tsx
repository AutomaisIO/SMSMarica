import { useEffect, useState } from 'react';
import { CheckCircle2, ChevronDown, Loader2, Server, Wifi, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useSalvarProxyMotor,
  useTestarProxyCep,
  useTestarProxyCpf,
} from '@/features/integracoes/api';
import type { ProxyMotor, ServicoProxy } from '@/features/integracoes/types';

/**
 * Um motor de proxy (ex.: Hub do Desenvolvedor) de um serviço (CPF/CEP). Token write-only;
 * `ordem` define a posição na cadeia de fallback; `timeout`/`tentativas` valem por motor
 * antes de cair para o próximo.
 */
export function ProxyMotorCard({ servico, motor }: { servico: ServicoProxy; motor: ProxyMotor }) {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const salvar = useSalvarProxyMotor(servico);

  const [aberto, setAberto] = useState(false);
  const [token, setToken] = useState('');
  const [ativo, setAtivo] = useState(motor.ativo);
  const [ordem, setOrdem] = useState(motor.ordem);
  const [timeout, setTimeoutSeg] = useState(motor.timeoutSegundos);
  const [tentativas, setTentativas] = useState(motor.tentativas);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  // Ressincroniza com a lista recarregada só com o card fechado (não atropela edição).
  useEffect(() => {
    if (aberto) return;
    setAtivo(motor.ativo);
    setOrdem(motor.ordem);
    setTimeoutSeg(motor.timeoutSegundos);
    setTentativas(motor.tentativas);
  }, [aberto, motor.ativo, motor.ordem, motor.timeoutSegundos, motor.tentativas]);

  const tokenOk = !motor.exigeToken || motor.tokenDefinido;
  const pronto = tokenOk && motor.ativo;

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      {
        motor: motor.motor,
        payload: {
          token: token || undefined,
          ativo,
          ordem,
          timeoutSegundos: timeout,
          tentativas,
          parametrosJson: motor.parametrosJson, // preserva overrides (ex.: baseUrl)
        },
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setToken('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <Server className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">{motor.rotulo}</span>
          <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-500">ordem {motor.ordem}</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              pronto
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {!tokenOk ? 'Falta token' : motor.ativo ? 'Ativo' : 'Inativo'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-4 border-t border-gray-100 pt-4">
          {motor.exigeToken ? (
            <Campo
              label="Token"
              htmlFor={`proxy-${servico}-${motor.motor}-token`}
              dica={motor.tokenDefinido ? 'Já definido — preencha apenas para substituir.' : 'Ainda não definido.'}
            >
              <Input
                id={`proxy-${servico}-${motor.motor}-token`}
                type="password"
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder={motor.tokenDefinido ? '••••••••••••' : 'Cole o token do motor aqui'}
                autoComplete="new-password"
                disabled={!podeEditar}
              />
            </Campo>
          ) : null}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Campo label="Ordem (fallback)" htmlFor={`proxy-${servico}-${motor.motor}-ordem`} dica="Menor primeiro.">
              <Input
                id={`proxy-${servico}-${motor.motor}-ordem`}
                type="number"
                min={0}
                value={ordem}
                onChange={(e) => setOrdem(Number(e.target.value))}
                disabled={!podeEditar}
              />
            </Campo>
            <Campo label="Timeout (s)" htmlFor={`proxy-${servico}-${motor.motor}-timeout`} dica="1–60 por tentativa.">
              <Input
                id={`proxy-${servico}-${motor.motor}-timeout`}
                type="number"
                min={1}
                max={60}
                value={timeout}
                onChange={(e) => setTimeoutSeg(Number(e.target.value))}
                disabled={!podeEditar}
              />
            </Campo>
            <Campo label="Tentativas" htmlFor={`proxy-${servico}-${motor.motor}-tentativas`} dica="1–5 antes do próximo.">
              <Input
                id={`proxy-${servico}-${motor.motor}-tentativas`}
                type="number"
                min={1}
                max={5}
                value={tentativas}
                onChange={(e) => setTentativas(Number(e.target.value))}
                disabled={!podeEditar}
              />
            </Campo>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Motor ativo na cadeia de fallback
          </label>

          {podeEditar ? (
            servico === 'cpf' ? (
              <TesteCpf motor={motor.motor} />
            ) : (
              <TesteCep motor={motor.motor} />
            )
          ) : null}

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              <Button type="submit" disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
            </div>
          ) : (
            <p className="text-xs text-gray-500">Você não tem permissão para editar integrações.</p>
          )}
        </form>
      ) : null}
    </div>
  );
}

/** Bloco de teste de um motor de CPF: usa a config salva do motor (token/timeout). */
function TesteCpf({ motor }: { motor: string }) {
  const testar = useTestarProxyCpf(motor);
  const [cpf, setCpf] = useState('');
  const [dataNascimento, setDataNascimento] = useState('');
  const cpfLimpo = cpf.replace(/\D/g, '');
  const valido = cpfLimpo.length === 11 && !!dataNascimento;

  return (
    <div className="space-y-3 rounded-lg border border-dashed border-gray-200 bg-gray-50/60 p-3">
      <p className="text-xs font-medium text-gray-600">Testar este motor (usa o token/timeout salvos)</p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Campo label="CPF" htmlFor={`teste-cpf-${motor}`}>
          <Input
            id={`teste-cpf-${motor}`}
            value={cpf}
            onChange={(e) => setCpf(e.target.value)}
            placeholder="Somente números"
            inputMode="numeric"
          />
        </Campo>
        <Campo label="Data de nascimento" htmlFor={`teste-data-${motor}`}>
          <Input
            id={`teste-data-${motor}`}
            type="date"
            value={dataNascimento}
            onChange={(e) => setDataNascimento(e.target.value)}
          />
        </Campo>
      </div>
      <Button
        type="button"
        variante="outline"
        disabled={!valido || testar.isPending}
        onClick={() => testar.mutate({ cpf: cpfLimpo, dataNascimento })}
      >
        {testar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Wifi className="mr-2 h-4 w-4" />}
        Testar CPF
      </Button>
      <ResultadoTeste
        erroRede={testar.isError ? extrairMensagemDeErro(testar.error) : null}
        ok={testar.data?.ok ?? null}
        mensagem={testar.data?.mensagem ?? null}
        duracaoMs={testar.data?.duracaoMs}
        linhas={
          testar.data?.resultado
            ? [
                ['Nome', testar.data.resultado.nome],
                ['CPF', testar.data.resultado.cpf],
                ['Nascimento', testar.data.resultado.dataNascimento],
                ['Situação', testar.data.resultado.situacaoCadastral ?? '—'],
              ]
            : null
        }
      />
    </div>
  );
}

/** Bloco de teste de um motor de CEP. */
function TesteCep({ motor }: { motor: string }) {
  const testar = useTestarProxyCep(motor);
  const [cep, setCep] = useState('');
  const cepLimpo = cep.replace(/\D/g, '');
  const valido = cepLimpo.length === 8;

  return (
    <div className="space-y-3 rounded-lg border border-dashed border-gray-200 bg-gray-50/60 p-3">
      <p className="text-xs font-medium text-gray-600">Testar este motor (usa o token/timeout salvos)</p>
      <Campo label="CEP" htmlFor={`teste-cep-${motor}`}>
        <Input
          id={`teste-cep-${motor}`}
          value={cep}
          onChange={(e) => setCep(e.target.value)}
          placeholder="Somente números"
          inputMode="numeric"
        />
      </Campo>
      <Button
        type="button"
        variante="outline"
        disabled={!valido || testar.isPending}
        onClick={() => testar.mutate({ cep: cepLimpo })}
      >
        {testar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Wifi className="mr-2 h-4 w-4" />}
        Testar CEP
      </Button>
      <ResultadoTeste
        erroRede={testar.isError ? extrairMensagemDeErro(testar.error) : null}
        ok={testar.data?.ok ?? null}
        mensagem={testar.data?.mensagem ?? null}
        duracaoMs={testar.data?.duracaoMs}
        linhas={
          testar.data?.resultado
            ? [
                ['Logradouro', testar.data.resultado.logradouro || '—'],
                ['Bairro', testar.data.resultado.bairro || '—'],
                ['Cidade/UF', `${testar.data.resultado.localidade}/${testar.data.resultado.uf}`],
                ['CEP', testar.data.resultado.cep],
              ]
            : null
        }
      />
    </div>
  );
}

/** Caixa de resultado compartilhada pelos testes (sucesso verde / falha vermelha). */
function ResultadoTeste({
  erroRede,
  ok,
  mensagem,
  duracaoMs,
  linhas,
}: {
  erroRede: string | null;
  ok: boolean | null;
  mensagem: string | null;
  duracaoMs?: number;
  linhas: [string, string][] | null;
}) {
  if (erroRede) {
    return (
      <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        <XCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
        <span>{erroRede}</span>
      </div>
    );
  }
  if (ok === null) return null;
  if (!ok) {
    return (
      <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        <XCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
        <span>{mensagem ?? 'Falha no teste.'}</span>
      </div>
    );
  }
  return (
    <div className="space-y-1 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
      <div className="flex items-center gap-2 font-medium">
        <CheckCircle2 className="h-4 w-4 flex-shrink-0" />
        Motor respondeu{typeof duracaoMs === 'number' ? ` em ${duracaoMs} ms` : ''}.
      </div>
      {linhas ? (
        <dl className="grid grid-cols-[auto,1fr] gap-x-3 gap-y-0.5 text-xs text-green-900/90">
          {linhas.map(([rotulo, valor]) => (
            <div key={rotulo} className="contents">
              <dt className="font-medium">{rotulo}</dt>
              <dd>{valor}</dd>
            </div>
          ))}
        </dl>
      ) : null}
    </div>
  );
}
