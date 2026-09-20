import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';
import { useAtendentes } from '@/features/confirmacoes/api';
import type { SolicitacaoAtendimento } from '@/features/confirmacoes/types';
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
 * Cancelamento — FASE 1: cancela no SMSMais (a vaga volta a contar) e orienta a cancelar no SISREG
 * pelo navegador; a extensão observa e concilia. A escrita direta no SISREG vem na fase 2.
 */
export function ModalCancelar({ item, ocupado, erro, aoFechar, aoCancelar }: Base & { aoCancelar: (motivo: string, meio: string) => void }) {
  // Quem veio da aba Cancelamento já disse por que — repetir isso à mão é trabalho à toa, e
  // digitado de novo o motivo perde as palavras da pessoa, que é o que vale numa auditoria.
  const pediuPeloZap = item.statusConfirmacao === 'Cancelada';
  const [motivo, setMotivo] = useState(
    pediuPeloZap && item.motivoCancelamentoPaciente ? item.motivoCancelamentoPaciente : '');
  const [meio, setMeio] = useState(pediuPeloZap ? 'WhatsApp' : 'Ligacao');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Cancelar agendamento">
      <div className="space-y-3">
        <Cabecalho item={item} />
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          O cancelamento vale no SMSMais na hora (a vaga volta a contar e o paciente sai das filas). No{' '}
          <strong>SISREG</strong> ele ainda precisa ser feito pelo navegador — com a extensão instalada, o sistema
          reconhece o cancelamento sozinho.
        </p>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Motivo</span>
          <Input value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="ex.: paciente pediu, vai fazer particular" autoFocus />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Como falou com o paciente</span>
          <Select value={meio} onChange={(e) => setMeio(e.target.value)}>
            {MEIOS.map((m) => <option key={m.valor} value={m.valor}>{m.rotulo}</option>)}
          </Select>
        </label>
        <Erro erro={erro} />
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>Voltar</Button>
          <Button variante="danger" disabled={ocupado || motivo.trim().length === 0} onClick={() => aoCancelar(motivo.trim(), meio)}>
            Confirmo o cancelamento
          </Button>
        </div>
      </div>
    </Modal>
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
