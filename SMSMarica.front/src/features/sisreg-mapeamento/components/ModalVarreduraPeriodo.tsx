import { useState } from 'react';
import { Loader2, Play } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useExecutarVarreduraPeriodo } from '@/features/sisreg-mapeamento/api/queries';

const MAX_DIAS = 31;

type Props = {
  unidadeId: string;
  /** "HH:mm:ss" — extremos do bloqueio do SISREG, para o aviso. */
  corteEntradaLocal?: string;
  bloqueioFimLocal?: string;
  aoFechar: () => void;
  /** Chamado quando a varredura foi aceita — o pai passa a acompanhar e fecha o modal. */
  aoIniciado: (mensagem: string) => void;
};

function hojeIso(): string {
  const agora = new Date();
  const local = new Date(agora.getTime() - agora.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function diasEntre(inicio: string, fim: string): number {
  return Math.round((new Date(fim).getTime() - new Date(inicio).getTime()) / 86_400_000);
}

/**
 * Dispara uma varredura MANUAL por período específico — inclusive datas passadas (backfill).
 *
 * Diferente de "Sincronizar agora", que varre de hoje até hoje + dias à frente. Duas coisas o
 * operador precisa saber ANTES de rodar, e por isso ficam à vista no modal: (1) backfill NÃO avisa
 * o paciente por WhatsApp — a mensagem seria sobre um exame que já passou; (2) o SISREG bloqueia a
 * exportação num intervalo do dia, então o disparo pode ser recusado por horário.
 */
export function ModalVarreduraPeriodo({
  unidadeId,
  corteEntradaLocal,
  bloqueioFimLocal,
  aoFechar,
  aoIniciado,
}: Props) {
  const [dataInicio, setDataInicio] = useState(hojeIso);
  const [dataFim, setDataFim] = useState(hojeIso);
  const [erro, setErro] = useState<string | null>(null);

  const executar = useExecutarVarreduraPeriodo(unidadeId);

  const dias = diasEntre(dataInicio, dataFim);
  const invalido =
    !dataInicio || !dataFim
      ? 'Informe as duas datas.'
      : dias < 0
        ? 'A data inicial não pode ser depois da data final.'
        : dias > MAX_DIAS
          ? `O período não pode passar de ${MAX_DIAS} dias — o SISREG recusa intervalo maior. Rode em partes.`
          : null;

  async function iniciar() {
    setErro(null);
    try {
      const r = await executar.mutateAsync({ dataInicio, dataFim });
      aoIniciado(r.mensagem);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Sincronizar um período"
      descricao="Lê no SISREG a agenda desta unidade no intervalo escolhido e cria as solicitações."
      largura="md"
    >
      <div className="flex flex-wrap items-end gap-4">
        <Campo label="De" htmlFor="periodo-inicio">
          <Input
            id="periodo-inicio"
            type="date"
            className="w-44"
            value={dataInicio}
            onChange={(e) => setDataInicio(e.target.value)}
          />
        </Campo>
        <Campo label="Até" htmlFor="periodo-fim">
          <Input
            id="periodo-fim"
            type="date"
            className="w-44"
            value={dataFim}
            onChange={(e) => setDataFim(e.target.value)}
          />
        </Campo>
        {!invalido && (
          <p className="pb-2 text-sm text-gray-500">
            {dias === 0 ? '1 dia' : `${dias + 1} dias`}
          </p>
        )}
      </div>

      <ul className="mt-4 space-y-1 text-sm text-gray-600">
        <li>
          • Varre os mesmos profissionais e procedimentos marcados na configuração desta unidade.
        </li>
        <li>
          • <strong>Não avisa o paciente por WhatsApp</strong> — é uma recuperação de agenda, não um
          novo agendamento.
        </li>
        {corteEntradaLocal && bloqueioFimLocal && (
          <li>
            • Não é possível iniciar entre {corteEntradaLocal.slice(0, 5)} e{' '}
            {bloqueioFimLocal.slice(0, 5)} (Brasília): o SISREG bloqueia a exportação da agenda nesse
            intervalo.
          </li>
        )}
      </ul>

      {(invalido || erro) && (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {erro ?? invalido}
        </p>
      )}

      <div className="mt-5 flex justify-end gap-2">
        <Button variante="ghost" tamanho="sm" onClick={aoFechar} disabled={executar.isPending}>
          Cancelar
        </Button>
        <Button
          tamanho="sm"
          disabled={Boolean(invalido) || executar.isPending}
          onClick={iniciar}
        >
          {executar.isPending ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Play className="h-4 w-4" />
          )}
          Iniciar
        </Button>
      </div>
    </Modal>
  );
}
