import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { formatarWallClock } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { Modal } from '@/shared/ui/Modal';
import type { ManifestacaoCriadaDto } from '@/features/ouvidoria/types';

type Props = {
  criada: ManifestacaoCriadaDto | null;
  aoFechar: () => void;
  aoAbrirDetalhe: (id: string) => void;
  aoRegistrarOutra: () => void;
};

/**
 * Mostra o protocolo e o código de acesso UMA única vez — o backend guarda só o hash, então fechar
 * este modal sem anotar significa que o código não volta. Anônima: só o protocolo.
 */
export function ModalProtocoloCriado({ criada, aoFechar, aoAbrirDetalhe, aoRegistrarOutra }: Props) {
  if (!criada) return null;
  const anonima = !criada.codigoAcesso;

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Manifestação registrada" largura="sm">
      <div className="space-y-4">
        <div className="flex items-start gap-2 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            Registrada com sucesso. Prazo de resposta ao cidadão: <strong>{formatarWallClock(criada.prazoRespostaEm)}</strong>.
          </span>
        </div>

        <div className="rounded-lg border border-slate-200 p-3">
          <p className="text-xs text-slate-500">Protocolo</p>
          <div className="text-lg font-semibold text-slate-900">
            <CodigoCopiavel codigo={criada.protocolo} dica="Copiar protocolo" />
          </div>
        </div>

        {anonima ? (
          <p className="text-sm text-slate-600">
            Manifestação anônima: não há código de acesso e o manifestante <strong>não terá acompanhamento</strong>. Informe
            só o protocolo, se ele quiser guardar.
          </p>
        ) : (
          <>
            <div className="rounded-lg border border-slate-200 p-3">
              <p className="text-xs text-slate-500">Código de acesso (para o cidadão acompanhar)</p>
              <div className="text-lg font-semibold tracking-widest text-slate-900">
                <CodigoCopiavel codigo={criada.codigoAcesso ?? ''} dica="Copiar código de acesso" />
              </div>
            </div>
            <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>
                <strong>Guarde e repasse este código agora</strong> — ele não será mostrado de novo. O sistema guarda
                apenas uma versão cifrada.
              </span>
            </div>
          </>
        )}

        <div className="flex flex-wrap justify-end gap-2 pt-1">
          <Button type="button" variante="ghost" onClick={aoRegistrarOutra}>
            Registrar outra
          </Button>
          <Button type="button" onClick={() => aoAbrirDetalhe(criada.id)}>
            Abrir manifestação
          </Button>
        </div>
      </div>
    </Modal>
  );
}
