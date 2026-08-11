import { useState } from 'react';
import { Siren } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { abrirIncidente } from '@/features/pacs/api/incidenteIdentidadeApi';

/**
 * "Este exame não é deste paciente" — congela o exame na hora.
 *
 * Aberto a qualquer um que enxergue o exame de propósito: quem percebe a troca é a médica ao
 * laudar ou a recepção ao conferir, não o administrador. Congelar é reversível (dá para
 * descartar como falso alarme); deixar um exame trocado circular, não.
 */
export function ModalReportarIdentidade({
  studyInstanceUID,
  nomeExibido,
  aoFechar,
  aoReportar,
}: {
  studyInstanceUID: string;
  nomeExibido: string;
  aoFechar: () => void;
  aoReportar: () => void;
}) {
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);

  async function confirmar() {
    setEnviando(true);
    try {
      await abrirIncidente(studyInstanceUID, motivo.trim());
      notificar('Exame colocado em conferência. Ninguém consegue laudar nem enviar até a correção.');
      aoReportar();
      aoFechar();
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Este exame não é deste paciente?"
      descricao={`Exame que aparece hoje como de ${nomeExibido}.`}
    >
      <div className="space-y-4">
        <div className="flex gap-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <Siren className="mt-0.5 h-5 w-5 shrink-0" />
          <div>
            Ao confirmar, o exame entra em <b>conferência</b>: ninguém consegue laudar, o resultado
            não é enviado ao paciente e o sistema para de mexer nele. Um responsável faz a correção
            depois.
          </div>
        </div>

        <Campo
          label="O que fez você desconfiar?"
          htmlFor="motivo-incidente"
          required
          dica="Ex.: as imagens não batem com o paciente; exame de mama em paciente do sexo masculino."
        >
          <Input
            id="motivo-incidente"
            autoFocus
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Descreva em poucas palavras"
          />
        </Campo>

        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar} disabled={enviando}>
            Cancelar
          </Button>
          <Button variante="danger" onClick={confirmar} disabled={motivo.trim().length < 5 || enviando}>
            {enviando ? 'Enviando…' : 'Colocar em conferência'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
