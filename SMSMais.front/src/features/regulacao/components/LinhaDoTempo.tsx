import {
  ArrowRightLeft,
  CheckCircle2,
  CircleDot,
  FileEdit,
  Hash,
  Paperclip,
  Send,
  Undo2,
  XCircle,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import { formatarInstante } from '@/shared/lib/datas';

import { ROTULO_STATUS } from './StatusRegulacaoBadge';
import type { EventoRegulacao, TipoEventoRegulacao } from '../tiposSolicitacao';

/**
 * A história do caso, em ordem.
 *
 * <p>Cada linha responde "quem fez o quê, e o que mudou". O <b>diff</b> é o que dá valor a isto:
 * "o agente ajustou" sem dizer o campo obriga a pessoa a comparar versões de cabeça — e é
 * exatamente a pergunta que aparece meses depois, quando alguém quer entender por que o pedido
 * demorou.</p>
 */
const ICONES: Partial<Record<TipoEventoRegulacao, LucideIcon>> = {
  Criacao: CircleDot,
  Edicao: FileEdit,
  Ajuste: FileEdit,
  Anexo: Paperclip,
  EnvioFila: Send,
  Assumida: CheckCircle2,
  Devolucao: Undo2,
  EnvioSistema: Send,
  NumeroExterno: Hash,
  SituacaoExterna: ArrowRightLeft,
  TrocaProcedimento: ArrowRightLeft,
  Cancelamento: XCircle,
  Recusa: XCircle,
  FalhaEnvio: XCircle,
};

const ROTULOS: Record<TipoEventoRegulacao, string> = {
  Criacao: 'Solicitação aberta',
  Edicao: 'Editada pela unidade',
  Anexo: 'Anexo',
  RespostaRegra: 'Regra respondida',
  EnvioFila: 'Enviada à pré-regulação',
  Assumida: 'Assumida pela regulação',
  Ajuste: 'Ajustada pelo agente',
  Devolucao: 'Devolvida à unidade',
  EnvioSistema: 'Envio ao sistema disparado',
  FalhaEnvio: 'Falha no envio',
  NumeroExterno: 'Número do sistema registrado',
  RessalvaDestino: 'Ressalva de destino',
  PendenciaAberta: 'Pendência aberta',
  PendenciaRespondida: 'Pendência respondida',
  PendenciaSubmetida: 'Pendência submetida',
  PendenciaBaixada: 'Pendência baixada',
  SituacaoExterna: 'Situação mudou no sistema',
  Cancelamento: 'Cancelada',
  Recusa: 'Recusada',
  OkInterno: 'Conferida pela regulação',
  TrocaProcedimento: 'Procedimento trocado',
};

export function LinhaDoTempo({
  eventos,
  carregando,
}: {
  eventos: EventoRegulacao[] | undefined;
  carregando?: boolean;
}) {
  if (carregando) return <p className="text-sm text-slate-500">Carregando a história do caso…</p>;
  if (!eventos?.length) {
    return <p className="text-sm text-slate-500">Nada registrado ainda.</p>;
  }

  return (
    <ol className="space-y-3">
      {eventos.map((e) => {
        const Icone = ICONES[e.tipo] ?? CircleDot;
        const autor =
          e.papel === 'Sistema' ? 'pelo sistema' : e.usuarioNome ? `por ${e.usuarioNome}` : '';
        const motivo = typeof e.detalhe?.motivo === 'string' ? e.detalhe.motivo : null;

        return (
          <li key={e.id} className="flex gap-3">
            <div className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full bg-slate-100">
              <Icone className="size-4 text-slate-600" />
            </div>
            <div className="min-w-0 flex-1 border-b border-slate-100 pb-3">
              <p className="text-sm text-slate-900">
                <strong>{ROTULOS[e.tipo] ?? e.tipo}</strong>{' '}
                <span className="text-slate-500">{autor}</span>
              </p>

              {e.de && e.para && (
                <p className="text-xs text-slate-500">
                  {ROTULO_STATUS[e.de]} → {ROTULO_STATUS[e.para]}
                </p>
              )}

              {motivo && <p className="mt-1 text-sm text-slate-700">“{motivo}”</p>}

              {e.diff && Object.keys(e.diff).length > 0 && (
                <ul className="mt-1 space-y-0.5 text-xs text-slate-600">
                  {Object.entries(e.diff).map(([campo, v]) => (
                    <li key={campo}>
                      <span className="font-medium">{campo}:</span>{' '}
                      <span className="text-slate-400 line-through">{v.de || '(vazio)'}</span>{' '}
                      → <span>{v.para || '(vazio)'}</span>
                    </li>
                  ))}
                </ul>
              )}

              <p className="mt-1 text-xs text-slate-400">{formatarInstante(e.criadoEm)}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
