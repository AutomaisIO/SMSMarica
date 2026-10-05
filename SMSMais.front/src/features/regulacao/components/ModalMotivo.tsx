import { useState } from 'react';
import { CheckCircle2, Loader2 } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';

/**
 * O motivo de devolver, recusar ou cancelar — e o desfecho, no mesmo lugar.
 *
 * <p>Substitui o `window.prompt`. O motivo é obrigatório no backend porque é o que a outra ponta
 * lê: sem ele a unidade recebe o caso de volta sem saber o que corrigir.</p>
 *
 * <p><b>O resultado fica aqui, e o modal só fecha quando a pessoa mandar.</b> Recusar e cancelar
 * não têm volta; um aviso no canto da tela, no instante em que o modal some, é o tipo de
 * confirmação que ninguém vê.</p>
 *
 * <p>Monte só quando for usar (`{acao && <ModalMotivo … />}`): o estado nasce limpo a cada
 * abertura, sem precisar zerar campo na mão.</p>
 */
export function ModalMotivo({
  titulo,
  pergunta,
  rotuloConfirmar,
  desfecho,
  perigo,
  aoConfirmar,
  aoFechar,
}: {
  titulo: string;
  /** O rótulo do campo — o que a pessoa precisa escrever, e para quem. */
  pergunta: string;
  rotuloConfirmar: string;
  /** O que aconteceu, dito depois que deu certo. */
  desfecho: string;
  /** Ação sem volta: o botão de confirmar sai em vermelho. */
  perigo?: boolean;
  /** Lançar = o erro aparece aqui e o texto digitado fica. */
  aoConfirmar: (motivo: string) => Promise<unknown>;
  aoFechar: () => void;
}) {
  const [motivo, setMotivo] = useState('');
  const [fase, setFase] = useState<'escrevendo' | 'gravando' | 'feito'>('escrevendo');
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    setErro(null);
    setFase('gravando');
    try {
      await aoConfirmar(motivo.trim());
      setFase('feito');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
      setFase('escrevendo');
    }
  }

  return (
    // Fechar no meio da gravação deixaria a pessoa sem saber se gravou.
    <Modal aberto aoFechar={fase === 'gravando' ? () => {} : aoFechar} titulo={titulo}>
      {fase === 'feito' ? (
        <div className="space-y-4">
          <p className="flex items-start gap-2 rounded bg-emerald-50 p-3 text-sm text-emerald-900">
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
            <span>{desfecho}</span>
          </p>
          <div className="flex justify-end">
            <Button onClick={aoFechar}>Fechar</Button>
          </div>
        </div>
      ) : (
        <div className="space-y-3">
          <Campo label={pergunta} htmlFor="motivo-regulacao" required>
            <textarea
              id="motivo-regulacao"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              rows={4}
              autoFocus
              disabled={fase === 'gravando'}
              className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
            />
          </Campo>

          {erro && <p className="rounded bg-red-50 p-2 text-sm text-red-800">{erro}</p>}

          <div className="flex justify-end gap-2 pt-1">
            <Button variante="secundaria" onClick={aoFechar} disabled={fase === 'gravando'}>
              Voltar
            </Button>
            <Button
              variante={perigo ? 'danger' : 'primaria'}
              onClick={confirmar}
              disabled={fase === 'gravando' || motivo.trim().length === 0}
            >
              {fase === 'gravando' && <Loader2 className="size-4 animate-spin" />}
              {fase === 'gravando' ? 'Gravando…' : rotuloConfirmar}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}
