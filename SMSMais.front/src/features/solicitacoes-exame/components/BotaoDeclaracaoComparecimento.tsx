import { useState } from 'react';
import { FileCheck, Loader2 } from 'lucide-react';
import {
  abrirDeclaracaoComparecimento,
  type ParametrosDeclaracaoComparecimento,
} from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Button } from '@/shared/ui/Button';
import { agoraInputLocalSP } from '@/shared/lib/datas';

// A declaração guarda a hora de comparecimento como wall-clock local (Brasília) ponta a ponta —
// o "agora" padrão vem em Brasília (não no fuso do browser); o valor enviado fica wall-clock.
const agoraLocal = agoraInputLocalSP;

/**
 * Ícone de "recibo" ao lado do badge Realizada/Laudada. Abre um modal com a
 * hora de entrada, hora de saída e o motivo (pré-preenchidos, mas editáveis pela
 * atendente) e só então gera a declaração de comparecimento (PDF) em nova aba.
 */
export function BotaoDeclaracaoComparecimento({ solicitacaoId }: { solicitacaoId: string }) {
  const [aberto, setAberto] = useState(false);
  const [carregando, setCarregando] = useState(false);
  const [horaEntrada, setHoraEntrada] = useState('');
  const [horaSaida, setHoraSaida] = useState('');
  const [motivo, setMotivo] = useState('');

  function abrirModal() {
    // Pré-preenche entrada e saída com a datahora atual; motivo em branco.
    const agora = agoraLocal();
    setHoraEntrada(agora);
    setHoraSaida(agora);
    setMotivo('');
    setAberto(true);
  }

  async function confirmar() {
    if (carregando) return;
    setCarregando(true);
    try {
      const parametros: ParametrosDeclaracaoComparecimento = { horaEntrada, horaSaida, motivo };
      await abrirDeclaracaoComparecimento(solicitacaoId, parametros);
      setAberto(false);
    } catch {
      alert('Não foi possível gerar a declaração de comparecimento.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <>
      <button
        type="button"
        onClick={abrirModal}
        title="Declaração de comparecimento (recibo)"
        aria-label="Declaração de comparecimento"
        className="inline-flex items-center rounded p-0.5 text-emerald-600 transition-colors hover:text-emerald-800"
      >
        <FileCheck className="h-3.5 w-3.5" />
      </button>

      <Modal
        aberto={aberto}
        aoFechar={() => (carregando ? undefined : setAberto(false))}
        titulo="Declaração de comparecimento"
        descricao="Confira os horários e o motivo antes de gerar o documento."
        largura="sm"
      >
        <div className="space-y-4">
          <Campo label="Hora de entrada" htmlFor="dc-entrada">
            <Input
              id="dc-entrada"
              type="datetime-local"
              value={horaEntrada}
              onChange={(e) => setHoraEntrada(e.target.value)}
            />
          </Campo>
          <Campo label="Hora de saída" htmlFor="dc-saida">
            <Input
              id="dc-saida"
              type="datetime-local"
              value={horaSaida}
              onChange={(e) => setHoraSaida(e.target.value)}
            />
          </Campo>
          <Campo label="Motivo" htmlFor="dc-motivo">
            <Input
              id="dc-motivo"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              placeholder="Ex.: realização de exame de imagem"
            />
          </Campo>

          <div className="flex items-center justify-end gap-2 pt-1">
            <Button variante="outline" onClick={() => setAberto(false)} disabled={carregando}>
              Cancelar
            </Button>
            <Button onClick={confirmar} disabled={carregando || !horaEntrada || !horaSaida}>
              {carregando ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <FileCheck className="mr-2 h-4 w-4" />
              )}
              Gerar documento
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}
