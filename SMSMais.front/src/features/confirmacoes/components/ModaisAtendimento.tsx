import { useState } from 'react';
import { CheckCircle2, Loader2, MessageSquareOff, MessageSquareText, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';
import { useAtendentes } from '@/features/confirmacoes/api';
import type { AcaoResultado, SolicitacaoAtendimento } from '@/features/confirmacoes/types';
import { dataHora } from '@/features/mensageria/lib/rotulos';

type Base = {
  item: SolicitacaoAtendimento;
  ocupado: boolean;
  erro: unknown;
  aoFechar: () => void;
};

function Cabecalho({ item }: { item: SolicitacaoAtendimento }) {
  return (
    <p className="text-sm text-gray-600">
      <strong>{item.pacienteNome ?? '(sem nome)'}</strong> · {item.procedimento ?? item.categoria} ·{' '}
      {dataHora(item.dataAgendada)}{item.codigoSolicitacao ? ` · SISREG ${item.codigoSolicitacao}` : ''}
    </p>
  );
}

function Erro({ erro }: { erro: unknown }) {
  return erro ? <p className="text-sm text-red-700">{extrairMensagemDeErro(erro)}</p> : null;
}

const MEIOS = [
  { valor: 'Ligacao', rotulo: 'Ligação' },
  { valor: 'WhatsApp', rotulo: 'WhatsApp' },
  { valor: 'Presencial', rotulo: 'Presencial' },
  { valor: 'Outro', rotulo: 'Outro' },
];

export function ModalConfirmar({ item, ocupado, erro, aoFechar, aoConfirmar }: Base & { aoConfirmar: (meio: string, observacao: string) => void }) {
  const [meio, setMeio] = useState('Ligacao');
  const [obs, setObs] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Confirmar presença">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Como falou com o paciente</span>
          <Select value={meio} onChange={(e) => setMeio(e.target.value)}>
            {MEIOS.map((m) => <option key={m.valor} value={m.valor}>{m.rotulo}</option>)}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Observação (opcional)</span>
          <Input value={obs} onChange={(e) => setObs(e.target.value)} placeholder="ex.: confirmou com a filha" />
        </label>
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>Voltar</Button>
          <Button disabled={ocupado} onClick={() => aoConfirmar(meio, obs)}>Confirmar presença</Button>
        </div>
      </div>
    </Modal>
  );
}

/**
 * Cancelamento. Pede o motivo e o meio, e nada mais.
 *
 * <para>Já teve um aviso aqui explicando que o SISREG precisava ser cancelado à parte. Saiu: quem
 * clica é sempre uma pessoa, que sabe o que está fazendo, e texto a mais em modal de ação vira
 * ruído — lido nas duas primeiras vezes e ignorado nas mil seguintes.</para>
 */
export function ModalCancelar({ item, ocupado, erro, resultado, aoFechar, aoCancelar }: Base & {
  /** Preenchido quando o cancelamento terminou — o modal vira a prestação de contas. */
  resultado: AcaoResultado | null;
  aoCancelar: (motivo: string, meio: string) => void;
}) {
  // Quem veio da aba Cancelamento já disse por que — repetir isso à mão é trabalho à toa, e
  // digitado de novo o motivo perde as palavras da pessoa, que é o que vale numa auditoria.
  const pediuPeloZap = item.statusConfirmacao === 'Cancelada';
  const [motivo, setMotivo] = useState(
    pediuPeloZap && item.motivoCancelamentoPaciente ? item.motivoCancelamentoPaciente : '');
  const [meio, setMeio] = useState(pediuPeloZap ? 'WhatsApp' : 'Ligacao');

  if (resultado) {
    return (
      <Modal aberto aoFechar={aoFechar} titulo="Cancelamento concluído">
        <ResultadoDoCancelamento item={item} resultado={resultado} aoFechar={aoFechar} />
      </Modal>
    );
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Cancelar agendamento">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Motivo</span>
          <Input value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="ex.: paciente pediu, vai fazer particular" autoFocus disabled={ocupado} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Como falou com o paciente</span>
          <Select value={meio} onChange={(e) => setMeio(e.target.value)} disabled={ocupado}>
            {MEIOS.map((m) => <option key={m.valor} value={m.valor}>{m.rotulo}</option>)}
          </Select>
        </label>
        {/* O cancelamento vai ao SISREG e volta — são alguns segundos de nada na tela. Sem
            esta linha, "botão cinza" e "sistema travado" são a mesma coisa para quem olha. */}
        {ocupado ? (
          <p className="flex items-center gap-2 rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
            <Loader2 className="size-4 shrink-0 animate-spin" />
            <span>Cancelando no SISREG com o seu login e conferindo a ficha. Não feche esta janela.</span>
          </p>
        ) : null}
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar} disabled={ocupado}>Voltar</Button>
          <Button variante="danger" disabled={ocupado || motivo.trim().length === 0} onClick={() => aoCancelar(motivo.trim(), meio)}>
            {ocupado ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            {ocupado ? 'Cancelando…' : 'Confirmo o cancelamento'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}


/**
 * O que aconteceu, item por item.
 *
 * <para>São três coisas diferentes — cancelar aqui, cancelar no SISREG e avisar o paciente — e a
 * atendente não tem como saber quais saíram. Isto já foi um toast de quatro segundos no canto da
 * tela, aparecendo no instante em que dois modais sumiam do meio dela: quem olhava para o meio
 * não via nada e concluía, com razão, que o clique não tinha feito efeito. O desfecho de um ato
 * irreversível fica onde o olho já está, e só sai quando a pessoa mandar sair.</para>
 */
function ResultadoDoCancelamento({ item, resultado, aoFechar }: {
  item: SolicitacaoAtendimento;
  resultado: AcaoResultado;
  aoFechar: () => void;
}) {
  return (
    <div className="space-y-3">
      <Cabecalho item={item} />
      <ul className="space-y-2">
        <LinhaResultado ok titulo="Aqui no sistema" detalhe="Agendamento cancelado e atendimento encerrado." />
        <LinhaResultado
          ok={Boolean(resultado.sisregSituacao)}
          neutro={!resultado.sisregSituacao}
          titulo="No SISREG"
          detalhe={resultado.sisregSituacao
            ? `A vaga foi liberada — a ficha agora diz “${resultado.sisregSituacao}”.`
            : 'Nada a fazer: este agendamento não existia no SISREG.'}
        />
        <LinhaResultado
          ok={resultado.pacienteAvisado}
          titulo="Aviso ao paciente"
          detalhe={resultado.pacienteAvisado
            ? 'Mensagem de cancelamento enviada por WhatsApp.'
            : 'NÃO foi avisado por WhatsApp — se ainda estiver na linha, avise agora.'}
          icone={resultado.pacienteAvisado ? MessageSquareText : MessageSquareOff}
        />
      </ul>
      <div className="flex justify-end">
        <Button onClick={aoFechar} autoFocus>Fechar</Button>
      </div>
    </div>
  );
}

/** Uma das três linhas do desfecho: o que é, e como ficou. */
function LinhaResultado({ ok, neutro, titulo, detalhe, icone }: {
  ok: boolean;
  neutro?: boolean;
  titulo: string;
  detalhe: string;
  icone?: typeof CheckCircle2;
}) {
  const Icone = icone ?? (ok ? CheckCircle2 : XCircle);
  const cor = neutro ? 'text-gray-400' : ok ? 'text-emerald-600' : 'text-amber-600';
  return (
    <li className="flex items-start gap-2 rounded border border-gray-200 px-3 py-2">
      <Icone className={`mt-0.5 size-4 shrink-0 ${cor}`} />
      <div className="text-sm">
        <p className="font-medium text-gray-900">{titulo}</p>
        <p className="text-gray-600">{detalhe}</p>
      </div>
    </li>
  );
}

export function ModalPendente({ item, ocupado, erro, aoFechar, aoEnviar }: Base & { aoEnviar: (motivo: string) => void }) {
  const [motivo, setMotivo] = useState('');
  const sugestoes = ['Não atendeu', 'Caixa postal', 'Pediu para ligar depois', 'Vai confirmar com a família'];
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Enviar para pendente">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <div className="flex flex-wrap gap-1.5">
          {sugestoes.map((s) => (
            <button key={s} type="button" className="rounded-full border border-gray-200 px-2 py-0.5 text-xs text-gray-700 hover:bg-gray-50" onClick={() => setMotivo(s)}>
              {s}
            </button>
          ))}
        </div>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Motivo</span>
          <Input value={motivo} onChange={(e) => setMotivo(e.target.value)} autoFocus />
        </label>
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>Voltar</Button>
          <Button disabled={ocupado || motivo.trim().length === 0} onClick={() => aoEnviar(motivo.trim())}>Enviar para pendente</Button>
        </div>
      </div>
    </Modal>
  );
}

export function ModalContatoErrado({ item, ocupado, erro, aoFechar, aoRegistrar }: Base & { aoRegistrar: (observacao: string) => void }) {
  const [obs, setObs] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Contato errado">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <p className="text-sm text-gray-600">
          O número <strong>{item.telefone ?? '—'}</strong> fica marcado como <strong>negado</strong> para este paciente:
          nada automático sai mais para ele. A solicitação vai para a aba <strong>Contato errado</strong> até o telefone ser corrigido.
        </p>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">O que aconteceu (opcional)</span>
          <Input value={obs} onChange={(e) => setObs(e.target.value)} placeholder="ex.: atendeu outra pessoa, não conhece" />
        </label>
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>Voltar</Button>
          <Button variante="danger" disabled={ocupado} onClick={() => aoRegistrar(obs.trim())}>Registrar contato errado</Button>
        </div>
      </div>
    </Modal>
  );
}

export function ModalTransferir({ item, ocupado, erro, aoFechar, aoTransferir }: Base & { aoTransferir: (paraUsuarioId: string, observacao: string) => void }) {
  const atendentes = useAtendentes();
  const [para, setPara] = useState('');
  const [obs, setObs] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Transferir atendimento">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Para</span>
          <Select value={para} onChange={(e) => setPara(e.target.value)}>
            <option value="">Escolha a colega…</option>
            {(atendentes.data ?? []).filter((a) => a.id !== item.atendimento?.atendenteId).map((a) => (
              <option key={a.id} value={a.id}>{a.nome}</option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Recado (opcional)</span>
          <Input value={obs} onChange={(e) => setObs(e.target.value)} />
        </label>
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>Voltar</Button>
          <Button disabled={ocupado || !para} onClick={() => aoTransferir(para, obs.trim())}>Transferir</Button>
        </div>
      </div>
    </Modal>
  );
}

/**
 * Corrigir contato: a recepção digita/ajusta o número e confirma o código (OTP) — o número novo
 * fica verificado no cadastro. Só então a pendência é fechada e a solicitação volta à fila automática.
 */
export function ModalContatoCorrigido({ item, aoFechar, aoCorrigido }: { item: SolicitacaoAtendimento; aoFechar: () => void; aoCorrigido: (telefone: string) => void }) {
  const [numero] = useState(item.telefone ?? '');
  if (!item.pacienteCpf) return null;
  return (
    <ModalOtpTelefone
      aberto
      cpf={item.pacienteCpf}
      numeroInicial={numero}
      numeroEditavel
      aoFechar={aoFechar}
      aoValidado={() => aoCorrigido(numero)}
    />
  );
}
