import { useState } from 'react';

import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';

import type { FluxoRegulacao, SolicitacaoRegulacao } from '../tiposSolicitacao';
import type { SistemaRegulacao } from '../types';

/**
 * "Registrar envio": o agente incluiu o pedido pela tela do próprio sistema de regulação e traz
 * de volta o número gerado.
 *
 * <p><b>Nada aqui escreve em sistema externo</b> (D-11). É o caminho do incremento 3, e continua
 * valendo depois: mesmo com o envio automático ligado, há casos que o agente resolve à mão.</p>
 *
 * <p>O número é a chave: a partir dele a conciliação casa esta solicitação com o espelho que as
 * varreduras trazem. Digitar errado não corrompe nada — mas deixa o caso órfão até alguém
 * corrigir, e por isso o campo pede conferência.</p>
 */
export function ModalRegistrarEnvio({
  solicitacao,
  aberto,
  aoFechar,
  aoConfirmar,
  salvando,
}: {
  solicitacao: SolicitacaoRegulacao;
  aberto: boolean;
  aoFechar: () => void;
  aoConfirmar: (sistema: string, numeroExterno: string) => Promise<void>;
  salvando?: boolean;
}) {
  // No NAR o destino é SISREG por definição (D-9) — não se escolhe.
  const fluxo: FluxoRegulacao = solicitacao.fluxo;
  const destinoFixo: SistemaRegulacao | null =
    fluxo === 'Nar' || fluxo === 'Interno' ? 'Sisreg' : null;

  const [sistema, setSistema] = useState<SistemaRegulacao>(
    destinoFixo ?? solicitacao.sistemaDestino ?? 'Ser',
  );
  const [numero, setNumero] = useState('');

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Registrar envio">
      <div className="space-y-3">
        <p className="text-sm text-slate-600">
          Use isto depois de incluir o pedido na tela do próprio sistema. O número que ele gerou
          passa a identificar este caso.
        </p>

        <Campo label="Sistema" htmlFor="registrar-sistema">
          <Select
            id="registrar-sistema"
            value={sistema}
            onChange={(e) => setSistema(e.target.value as SistemaRegulacao)}
            disabled={!!destinoFixo}
          >
            <option value="Sisreg">SISREG</option>
            <option value="Ser">SER (SES-RJ)</option>
            <option value="Sernit">SERNIT (Niterói)</option>
          </Select>
        </Campo>

        <Campo label="Número gerado pelo sistema" htmlFor="registrar-numero" required>
          <Input
            id="registrar-numero"
            value={numero}
            onChange={(e) => setNumero(e.target.value)}
            placeholder="ex.: 123456789"
            autoFocus
          />
        </Campo>

        <div className="flex justify-end gap-2 pt-1">
          <Button variante="secundaria" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </Button>
          <Button
            onClick={() => aoConfirmar(sistema, numero.trim())}
            disabled={salvando || numero.trim().length === 0}
          >
            Registrar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
