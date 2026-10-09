import { useEffect, useState } from 'react';
import { LogIn, ShieldAlert, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import {
  sandboxApi,
  type PersonificacaoStatus,
  type SandboxPaciente,
} from '@/features/sandbox/sandboxApi';

/**
 * "Entrar como paciente": enquanto valer, o CPF do operador no app do cidadão (PWA ou Flutter)
 * abre como o paciente escolhido. O código continua indo para o WhatsApp do próprio operador —
 * o paciente não recebe nada e não perde a sessão dele.
 */
export function EntrarComoPaciente({ selecionado }: { selecionado: SandboxPaciente | null }) {
  const [status, setStatus] = useState<PersonificacaoStatus | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    sandboxApi
      .personificacao()
      .then(setStatus)
      .catch((e) => setErro(extrairMensagemDeErro(e)));
  }, []);

  async function ativar() {
    if (!selecionado) return;
    setOcupado(true);
    setErro(null);
    try {
      setStatus(await sandboxApi.ativarPersonificacao(selecionado.id));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function encerrar() {
    setOcupado(true);
    setErro(null);
    try {
      await sandboxApi.encerrarPersonificacao();
      setStatus(await sandboxApi.personificacao());
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  const ativa = status?.ativa ?? null;
  const semCpf = selecionado !== null && !selecionado.cpf;
  const jaEhEste = ativa !== null && selecionado !== null && ativa.pacienteId === selecionado.id;

  return (
    <section className="rounded-lg border border-gray-200 p-4">
      <p className="mb-1 text-sm font-semibold text-gray-800">2. Entrar no app como o paciente</p>
      <p className="mb-3 text-sm text-gray-600">
        Abra o app do cidadão (PWA ou Flutter) e entre com o <strong>seu</strong> CPF. O código chega no{' '}
        <strong>seu</strong> WhatsApp e o app abre como o paciente escolhido. O paciente não recebe nada. Vale 12
        horas ou até você encerrar.
      </p>

      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {status && !status.apta ? (
        <div className="mb-3 flex gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{status.motivoInapta}</span>
        </div>
      ) : null}

      {status?.apta ? (
        <p className="mb-3 flex items-center gap-1.5 text-xs text-gray-500">
          <Smartphone className="h-3.5 w-3.5" />
          Seu CPF: <span className="font-mono">{status.cpfMascarado}</span> · código no WhatsApp{' '}
          <span className="font-mono">{status.telefoneMascarado}</span>
        </p>
      ) : null}

      {ativa ? (
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
          <span>
            Agora o seu CPF abre o app como <strong>{ativa.pacienteNome}</strong>, até{' '}
            {formatarInstante(ativa.expiraEm)}.{' '}
            {ativa.sessoesAbertas > 0
              ? `${ativa.sessoesAbertas} aparelho(s) logado(s) como ele.`
              : 'Nenhum aparelho logado ainda.'}
          </span>
          <Button tamanho="sm" variante="outline" onClick={encerrar} disabled={ocupado}>
            Encerrar
          </Button>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={ativar} disabled={ocupado || !status?.apta || !selecionado || semCpf || jaEhEste}>
          <LogIn className="mr-1.5 h-4 w-4" />
          {selecionado ? `Entrar como ${selecionado.nome}` : 'Escolha um paciente acima'}
        </Button>
        {semCpf ? <span className="text-xs text-amber-700">Este paciente não tem CPF — escolha outro.</span> : null}
        {ativa && selecionado && !jaEhEste && !semCpf ? (
          <span className="text-xs text-gray-500">Substitui {ativa.pacienteNome} e desconecta quem estiver logado.</span>
        ) : null}
      </div>

      <p className="mt-3 text-xs text-gray-500">
        Pelo app não dá para trocar o contato, a foto ou os acompanhantes do paciente, nem aceitar o termo em nome
        dele. <strong>Confirmar presença ou avisar falta grava de verdade</strong> (com o canal “sandbox”, para a
        equipe saber que foi teste) — desfaça na seção 4. Os acessos ficam no Histórico de Acesso da ficha, com o
        seu nome.
      </p>
    </section>
  );
}
