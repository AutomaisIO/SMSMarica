import { QRCodeSVG } from 'qrcode.react';
import { Loader2, ScanLine, Smartphone } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { AnexoExameItem } from '@/features/anamnese/components/AnexoExameItem';
import type { AnexoExameDto, AnexoUploadTokenDto } from '@/features/anamnese/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  token: AnexoUploadTokenDto;
  anexos: AnexoExameDto[];
  carregando: boolean;
  podeEditar: boolean;
  aoRevisar: (a: AnexoExameDto) => void;
  aoSalvar: (a: AnexoExameDto) => void;
  aoExcluir: (a: AnexoExameDto) => void;
  revisandoId: string | null;
  salvandoId: string | null;
  excluindoId: string | null;
};

function dicaExpiracao(expiraEm: string): string {
  const fim = new Date(expiraEm).getTime();
  if (Number.isNaN(fim)) return '';
  const restanteMin = Math.round((fim - Date.now()) / 60_000);
  const hora = new Date(expiraEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  if (restanteMin <= 0) return `O código expirou (${hora}). Feche e gere um novo.`;
  return `O código expira às ${hora} (em ~${restanteMin} min).`;
}

/**
 * Modal da ponte QR → PWA. Exibe o QR que codifica a `url` do token; enquanto
 * aberto, o componente-pai faz polling da lista de anexos e passa os documentos
 * que vão chegando — aqui eles são revisados, salvos (Pendente → Salvo) ou
 * excluídos. Fechar o modal apenas encerra o polling.
 */
export function AdicionarExameModal({
  aberto,
  aoFechar,
  token,
  anexos,
  carregando,
  podeEditar,
  aoRevisar,
  aoSalvar,
  aoExcluir,
  revisandoId,
  salvandoId,
  excluindoId,
}: Props) {
  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo="Adicionar exame (digitalizar documento)"
      descricao={`Paciente: ${token.paciente.nome}`}
      largura="lg"
    >
      <div className="grid grid-cols-1 gap-6 md:grid-cols-[auto_1fr]">
        {/* QR + instruções */}
        <div className="flex flex-col items-center gap-3 md:w-64">
          <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            <QRCodeSVG value={token.url} size={200} level="M" includeMargin />
          </div>
          <p className="flex items-center gap-1.5 text-center text-sm text-gray-600">
            <Smartphone className="h-4 w-4 shrink-0 text-primary-600" />
            Aponte a câmera do celular para o QR e digitalize o documento no app.
          </p>
          <p className="text-center text-xs text-amber-700">{dicaExpiracao(token.expiraEm)}</p>
          <p className="text-center text-[11px] text-gray-400">
            O código pode ser usado várias vezes enquanto válido — cada documento gera um novo arquivo.
          </p>
        </div>

        {/* Lista que chega em tempo real */}
        <div>
          <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-900">
            <ScanLine className="h-4 w-4 text-primary-600" />
            Documentos recebidos
            {carregando && anexos.length === 0 ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin text-gray-400" />
            ) : null}
          </div>
          {anexos.length === 0 ? (
            <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-8 text-center text-sm text-gray-500">
              Aguardando o envio dos documentos pelo celular…
            </div>
          ) : (
            <ul className="space-y-2">
              {anexos.map((a) => (
                <AnexoExameItem
                  key={a.id}
                  anexo={a}
                  podeEditar={podeEditar}
                  aoRevisar={() => aoRevisar(a)}
                  aoSalvar={() => aoSalvar(a)}
                  aoExcluir={() => aoExcluir(a)}
                  revisando={revisandoId === a.id}
                  salvando={salvandoId === a.id}
                  excluindo={excluindoId === a.id}
                />
              ))}
            </ul>
          )}
        </div>
      </div>
    </Modal>
  );
}
