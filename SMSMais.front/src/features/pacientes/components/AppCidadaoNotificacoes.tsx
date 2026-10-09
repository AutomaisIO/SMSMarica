import { useState } from 'react';
import { AlertTriangle, BellRing, Loader2, Send, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { cn } from '@/shared/lib/cn';
import { formatarInstante } from '@/shared/lib/datas';
import { useAppCidadao } from '@/features/pacientes/api/queries';
import type { AparelhoAppCidadao, NotificacaoAppEnviada } from '@/features/pacientes/api/pacientesApi';
import { ModalEnviarNotificacao } from '@/features/pacientes/components/ModalEnviarNotificacao';
import { ROTULO_PLATAFORMA, rotuloDestinoApp } from '@/features/pacientes/lib/destinosAppCidadao';

/**
 * Notificações no app do cidadão, na aba Histórico de Acesso da ficha: os aparelhos que recebem,
 * o botão de enviar e o que já foi enviado. Módulo próprio (NotificacaoAppCidadao) — quem só
 * consulta o paciente não vê nada disto.
 */
export function AppCidadaoNotificacoes({
  pacienteId,
  nomePaciente,
}: {
  pacienteId: string;
  nomePaciente?: string;
}) {
  const podeVer = usePermissao('NotificacaoAppCidadao', 'Consulta');
  if (!podeVer) return null;
  return <BlocoNotificacoes pacienteId={pacienteId} nomePaciente={nomePaciente} />;
}

function BlocoNotificacoes({ pacienteId, nomePaciente }: { pacienteId: string; nomePaciente?: string }) {
  const podeEnviar = usePermissao('NotificacaoAppCidadao', 'Edicao');
  const q = useAppCidadao(pacienteId);
  const [enviando, setEnviando] = useState(false);

  const dados = q.data;
  const aparelhos = dados?.aparelhos ?? [];
  // O motivo fica escrito ao lado do botão: botão cinza sem explicação parece defeito.
  const motivoBloqueio = !dados
    ? null
    : !dados.configurado
      ? 'O envio ainda não foi configurado.'
      : aparelhos.length === 0
        ? 'O paciente não tem aparelho com as notificações ativas.'
        : null;

  return (
    <section className="mb-6 rounded-lg border border-gray-200 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <BellRing className="h-4 w-4" /> Notificações no app
          <AjudaManual artigo="pacientes" secao="app-notificacoes" />
        </div>
        {podeEnviar ? (
          <div className="flex flex-wrap items-center justify-end gap-2">
            {motivoBloqueio ? <span className="text-xs text-gray-500">{motivoBloqueio}</span> : null}
            <Button
              tamanho="sm"
              onClick={() => setEnviando(true)}
              disabled={!dados || Boolean(motivoBloqueio)}
              title={motivoBloqueio ?? 'Enviar uma notificação aos aparelhos do paciente'}
            >
              <Send className="mr-1.5 h-3.5 w-3.5" />
              Enviar notificação
            </Button>
          </div>
        ) : null}
      </div>

      {q.isLoading ? (
        <p className="mt-3 flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </p>
      ) : null}
      {q.isError ? (
        <p className="mt-3 text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p>
      ) : null}

      {dados ? (
        <div className="mt-3 space-y-4">
          {!dados.configurado ? (
            <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <p>
                O envio de notificações ao app ainda não foi configurado. Quem cuida das integrações liga em{' '}
                <strong>Integrações → Credenciais → Firebase — notificações do app do cidadão</strong>.
              </p>
            </div>
          ) : null}

          <div>
            <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-gray-500">
              Aparelhos com notificações ativas
            </p>
            {aparelhos.length === 0 ? (
              <p className="text-sm text-gray-500">
                Nenhum. Para receber, o paciente precisa entrar no aplicativo instalado no celular e permitir
                as notificações — pelo navegador, mesmo com atalho na tela inicial, não recebe.
              </p>
            ) : (
              <ul className="space-y-1.5">
                {aparelhos.map((a) => (
                  <LinhaAparelho key={a.sessaoId} aparelho={a} />
                ))}
              </ul>
            )}
          </div>

          <div>
            <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-gray-500">
              Últimas notificações enviadas
            </p>
            {dados.notificacoes.length === 0 ? (
              <p className="text-sm text-gray-500">Nenhuma notificação enviada a este paciente.</p>
            ) : (
              <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">
                {dados.notificacoes.map((n) => (
                  <LinhaNotificacao key={n.id} notificacao={n} />
                ))}
              </ul>
            )}
          </div>
        </div>
      ) : null}

      {enviando ? (
        <ModalEnviarNotificacao
          pacienteId={pacienteId}
          nomePaciente={nomePaciente}
          aoFechar={() => setEnviando(false)}
        />
      ) : null}
    </section>
  );
}

function LinhaAparelho({ aparelho: a }: { aparelho: AparelhoAppCidadao }) {
  return (
    <li className="flex items-start gap-2 text-sm">
      <Smartphone className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
      <div className="min-w-0">
        <p className="text-gray-900">
          <span className="font-medium">{ROTULO_PLATAFORMA[a.plataforma] ?? a.plataforma}</span>
          <span className="text-gray-500">
            {' '}
            · notificações ativas desde {formatarInstante(a.registradoEm)} · entrou no app em{' '}
            {formatarInstante(a.entrouEm)}
          </span>
        </p>
        {a.dispositivo ? (
          <p className="max-w-xl truncate text-xs text-gray-500" title={a.dispositivo}>
            {a.dispositivo}
          </p>
        ) : null}
      </div>
    </li>
  );
}

function SeloEntrega({ n }: { n: NotificacaoAppEnviada }) {
  const classe =
    n.aparelhos > 0 && n.entregues === n.aparelhos
      ? 'bg-emerald-100 text-emerald-700'
      : n.entregues === 0
        ? 'bg-red-100 text-red-700'
        : 'bg-amber-100 text-amber-800';
  return (
    <span className={cn('rounded-full px-2 py-0.5 text-xs font-medium', classe)}>
      {n.entregues} de {n.aparelhos} {n.aparelhos === 1 ? 'aparelho' : 'aparelhos'}
    </span>
  );
}

function LinhaNotificacao({ notificacao: n }: { notificacao: NotificacaoAppEnviada }) {
  return (
    <li className="space-y-0.5 px-3 py-2 text-sm">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-gray-500">
        <span>{formatarInstante(n.criadaEm)}</span>
        {n.enviadaPor ? <span>· por {n.enviadaPor}</span> : null}
        <span>· abre em {rotuloDestinoApp(n.rota)}</span>
        <SeloEntrega n={n} />
      </div>
      <p className="font-medium text-gray-900">{n.titulo}</p>
      <p className="whitespace-pre-line text-gray-700">{n.mensagem}</p>
      {n.falha ? (
        <p className={cn('text-xs', n.entregues === 0 ? 'text-red-700' : 'text-amber-700')}>{n.falha}</p>
      ) : null}
    </li>
  );
}
