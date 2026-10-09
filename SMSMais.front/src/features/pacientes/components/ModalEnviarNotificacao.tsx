import { useState } from 'react';
import { CheckCircle2, Loader2, ShieldAlert, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { cn } from '@/shared/lib/cn';
import { useEnviarNotificacaoApp } from '@/features/pacientes/api/queries';
import type {
  EnviarNotificacaoAppPayload,
  ResultadoAparelhoNotificacao,
  ResultadoEnvioNotificacaoApp,
} from '@/features/pacientes/api/pacientesApi';
import {
  DESTINOS_APP_CIDADAO,
  ROTULO_PLATAFORMA,
  rotuloDestinoApp,
} from '@/features/pacientes/lib/destinosAppCidadao';

/** Limites do servidor. O título longo é cortado pelo próprio celular na tela bloqueada. */
const MAX_TITULO = 65;
const MAX_MENSAGEM = 240;

function Contador({ atual, maximo }: { atual: number; maximo: number }) {
  return (
    <span className={cn('text-xs tabular-nums', maximo - atual <= 10 ? 'text-amber-700' : 'text-gray-400')}>
      {atual}/{maximo}
    </span>
  );
}

/**
 * Envio de notificação ao app do paciente.
 *
 * <para>É um ato sem volta: a notificação sai na hora para todos os aparelhos e ninguém a recolhe
 * depois. Por isso o desfecho fica aqui dentro, aparelho por aparelho, e só some quando a pessoa
 * fechar — um toast no canto some antes de alguém ler que o iPhone recusou.</para>
 */
export function ModalEnviarNotificacao({
  pacienteId,
  nomePaciente,
  aoFechar,
}: {
  pacienteId: string;
  nomePaciente?: string;
  aoFechar: () => void;
}) {
  const enviar = useEnviarNotificacaoApp();
  const [titulo, setTitulo] = useState('');
  const [mensagem, setMensagem] = useState('');
  const [rota, setRota] = useState('');
  const [enviado, setEnviado] = useState<EnviarNotificacaoAppPayload | null>(null);
  const ocupado = enviar.isPending;

  // Fechar no meio não cancela o envio no servidor — só esconderia o desfecho de quem enviou.
  function fechar() {
    if (!ocupado) aoFechar();
  }

  if (enviar.data && enviado) {
    return (
      <Modal aberto aoFechar={fechar} titulo={tituloDoDesfecho(enviar.data)}>
        <DesfechoDoEnvio resultado={enviar.data} enviado={enviado} aoFechar={fechar} />
      </Modal>
    );
  }

  const tituloLimpo = titulo.trim();
  const mensagemLimpa = mensagem.trim();
  const valido =
    tituloLimpo.length > 0 &&
    tituloLimpo.length <= MAX_TITULO &&
    mensagemLimpa.length > 0 &&
    mensagemLimpa.length <= MAX_MENSAGEM;

  function aoEnviar(e: React.FormEvent) {
    e.preventDefault();
    if (!valido || ocupado) return;
    const corpo: EnviarNotificacaoAppPayload = {
      titulo: tituloLimpo,
      mensagem: mensagemLimpa,
      rota: rota || null,
    };
    setEnviado(corpo);
    enviar.mutate({ id: pacienteId, corpo });
  }

  return (
    <Modal
      aberto
      aoFechar={fechar}
      titulo="Enviar notificação ao app"
      descricao="Vai para todos os aparelhos do paciente com as notificações ativas."
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        {nomePaciente ? (
          <p className="text-sm text-gray-600">
            Para: <strong className="text-gray-900">{nomePaciente}</strong>
          </p>
        ) : null}

        {/* Fica sempre à vista, não num "?": é o único lugar em que dá para impedir o dado de
            saúde de sair, e quem escreve com pressa não abre ajuda. */}
        <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <ShieldAlert className="mt-0.5 size-4 shrink-0" />
          <p>
            Aparece na tela do celular, mesmo bloqueado, e passa pelos servidores do Google. Não escreva
            diagnóstico, resultado de exame nem outro dado de saúde — chame a pessoa para abrir o app.
          </p>
        </div>

        <label className="flex flex-col gap-1 text-sm">
          <span className="flex items-baseline justify-between gap-2">
            <span className="font-medium text-gray-700">Título</span>
            <Contador atual={titulo.length} maximo={MAX_TITULO} />
          </span>
          <Input
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            maxLength={MAX_TITULO}
            placeholder="ex.: Você tem uma atualização"
            autoFocus
            disabled={ocupado}
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          <span className="flex items-baseline justify-between gap-2">
            <span className="font-medium text-gray-700">Mensagem</span>
            <Contador atual={mensagem.length} maximo={MAX_MENSAGEM} />
          </span>
          <textarea
            className="input min-h-[90px]"
            value={mensagem}
            onChange={(e) => setMensagem(e.target.value)}
            maxLength={MAX_MENSAGEM}
            placeholder="ex.: Abra o app para ver os detalhes."
            disabled={ocupado}
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Abrir no app</span>
          <Select value={rota} onChange={(e) => setRota(e.target.value)} disabled={ocupado}>
            <option value="">Início</option>
            {DESTINOS_APP_CIDADAO.filter((d) => d.rota !== '/').map((d) => (
              <option key={d.rota} value={d.rota}>
                {d.rotulo}
              </option>
            ))}
          </Select>
          <span className="text-xs text-gray-500">A tela que abre quando a pessoa toca na notificação.</span>
        </label>

        {ocupado ? (
          <p className="flex items-center gap-2 rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
            <Loader2 className="size-4 shrink-0 animate-spin" />
            <span>Enviando… não feche esta janela.</span>
          </p>
        ) : null}

        {enviar.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(enviar.error)}
          </div>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={fechar} disabled={ocupado}>
            Cancelar
          </Button>
          <Button type="submit" disabled={!valido || ocupado}>
            {ocupado ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            {ocupado ? 'Enviando…' : 'Enviar notificação'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function tituloDoDesfecho(r: ResultadoEnvioNotificacaoApp): string {
  if (r.aparelhos > 0 && r.entregues === r.aparelhos) return 'Notificação entregue';
  if (r.entregues === 0) return 'Notificação não entregue';
  return 'Notificação entregue em parte';
}

/** "Android", ou "Android 1"/"Android 2" quando há mais de um da mesma plataforma. */
function nomesDosAparelhos(resultados: ResultadoAparelhoNotificacao[]): string[] {
  const total: Record<string, number> = {};
  for (const r of resultados) total[r.plataforma] = (total[r.plataforma] ?? 0) + 1;
  const visto: Record<string, number> = {};
  return resultados.map((r) => {
    const base = ROTULO_PLATAFORMA[r.plataforma] ?? r.plataforma;
    visto[r.plataforma] = (visto[r.plataforma] ?? 0) + 1;
    return total[r.plataforma] > 1 ? `${base} ${visto[r.plataforma]}` : base;
  });
}

function DesfechoDoEnvio({
  resultado,
  enviado,
  aoFechar,
}: {
  resultado: ResultadoEnvioNotificacaoApp;
  enviado: EnviarNotificacaoAppPayload;
  aoFechar: () => void;
}) {
  const nomes = nomesDosAparelhos(resultado.resultados);
  const algumRemovido = resultado.resultados.some((r) => r.aparelhoRemovido);
  return (
    <div className="space-y-3">
      <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
        <p className="font-semibold text-gray-900">{enviado.titulo}</p>
        <p className="whitespace-pre-line text-gray-700">{enviado.mensagem}</p>
        <p className="mt-1 text-xs text-gray-500">Abre em: {rotuloDestinoApp(enviado.rota)}</p>
      </div>

      <p className="text-sm text-gray-700">
        Entregue em <strong>{resultado.entregues}</strong> de <strong>{resultado.aparelhos}</strong>{' '}
        {resultado.aparelhos === 1 ? 'aparelho' : 'aparelhos'}.
      </p>

      <ul className="space-y-2">
        {resultado.resultados.map((r, i) => (
          <LinhaAparelho key={i} nome={nomes[i]} resultado={r} />
        ))}
      </ul>

      {algumRemovido ? (
        <p className="text-xs text-gray-500">
          Aparelho removido sai da lista e não recebe mais. Volta quando o paciente entrar de novo no app
          com as notificações permitidas.
        </p>
      ) : null}
      <p className="text-xs text-gray-500">O envio ficou registrado no histórico de notificações da ficha.</p>

      <div className="flex justify-end">
        <Button onClick={aoFechar} autoFocus>
          Fechar
        </Button>
      </div>
    </div>
  );
}

function LinhaAparelho({ nome, resultado }: { nome: string; resultado: ResultadoAparelhoNotificacao }) {
  const Icone = resultado.entregue ? CheckCircle2 : XCircle;
  return (
    <li className="flex items-start gap-2 rounded border border-gray-200 px-3 py-2">
      <Icone
        className={cn('mt-0.5 size-4 shrink-0', resultado.entregue ? 'text-emerald-600' : 'text-amber-600')}
      />
      <div className="text-sm">
        <p className="flex flex-wrap items-center gap-2 font-medium text-gray-900">
          {nome} — {resultado.entregue ? 'entregue' : 'não entregue'}
          {resultado.aparelhoRemovido ? (
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700">
              aparelho removido
            </span>
          ) : null}
        </p>
        <p className="text-gray-600">
          {resultado.entregue
            ? 'O Firebase aceitou e leva até o celular. Desligado ou sem internet, chega quando ele se conectar.'
            : (resultado.detalhe ?? 'Não entregue.')}
        </p>
      </div>
    </li>
  );
}
