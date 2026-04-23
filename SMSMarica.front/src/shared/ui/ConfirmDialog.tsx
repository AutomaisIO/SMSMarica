import { AlertTriangle } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';

type Props = {
  aberto: boolean;
  titulo: string;
  mensagem: string;
  rotuloConfirmar?: string;
  rotuloCancelar?: string;
  destrutivo?: boolean;
  carregando?: boolean;
  aoConfirmar: () => void;
  aoCancelar: () => void;
};

export function ConfirmDialog({
  aberto,
  titulo,
  mensagem,
  rotuloConfirmar = 'Confirmar',
  rotuloCancelar = 'Cancelar',
  destrutivo = false,
  carregando = false,
  aoConfirmar,
  aoCancelar,
}: Props) {
  return (
    <Modal aberto={aberto} aoFechar={aoCancelar} titulo={titulo} largura="sm">
      <div className="space-y-4">
        <div className="flex items-start gap-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-amber-100 text-amber-600">
            <AlertTriangle className="w-5 h-5" />
          </div>
          <p className="text-sm text-gray-700">{mensagem}</p>
        </div>
        <div className="flex items-center justify-end gap-3 pt-2">
          <Button type="button" variante="ghost" onClick={aoCancelar} disabled={carregando}>
            {rotuloCancelar}
          </Button>
          <Button
            type="button"
            variante={destrutivo ? 'danger' : 'primaria'}
            onClick={aoConfirmar}
            disabled={carregando}
          >
            {carregando ? 'Processando…' : rotuloConfirmar}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
