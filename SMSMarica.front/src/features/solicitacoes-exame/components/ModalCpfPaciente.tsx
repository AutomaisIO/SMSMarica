import { useEffect, useState } from 'react';
import { AlertTriangle, IdCard, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { apenasDigitosCpf, cpfValido } from '@/shared/lib/cpf';
import { useDefinirCpfDoPaciente } from '@/features/solicitacoes-exame/api/queries';

export type SolicitacaoSemCpf = {
  id: string;
  pacienteNome: string;
  procedimento?: string | null;
};

type Props = {
  /** Solicitação-alvo, ou null com o modal fechado. */
  alvo: SolicitacaoSemCpf | null;
  aoFechar: () => void;
  /** Chamado quando o CPF entrou e a solicitação pode ser aberta. */
  aoLiberar: (solicitacaoId: string) => void;
};

/**
 * Portão do CPF na recepção.
 *
 * O paciente entrou pela importação do SISREG ancorado só no CNS — o CADSUS nem sempre devolve
 * CPF. O agendamento é real e precisava existir no sistema, então ele entra; o débito é cobrado
 * aqui, no balcão, quando a pessoa aparece. Sem CPF a solicitação nem abre, e por consequência
 * não há como autorizar nem mandar para a worklist — o que é correto também tecnicamente: o
 * PatientID do DICOM **é** o CPF.
 *
 * O DV é validado antes de ir ao servidor: um CPF mal digitado que por acaso exista vincularia o
 * exame ao PACIENTE ERRADO — o pior modo de falha desta tela.
 */
export function ModalCpfPaciente({ alvo, aoFechar, aoLiberar }: Props) {
  const [cpf, setCpf] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const definir = useDefinirCpfDoPaciente();

  useEffect(() => {
    if (alvo) {
      setCpf('');
      setErro(null);
    }
  }, [alvo?.id]);

  if (!alvo) return null;

  const digitos = apenasDigitosCpf(cpf);
  const valido = cpfValido(digitos);

  async function confirmar() {
    if (!valido || !alvo) return;
    setErro(null);
    try {
      const r = await definir.mutateAsync({ id: alvo.id, cpf: digitos });

      // Repontada: o cadastro sem CPF era uma sombra de alguém que já existia. O nome na tela
      // vai mudar — avisar é obrigatório, senão o operador acha que abriu a solicitação errada.
      notificar(
        r.repontado
          ? `CPF já cadastrado para ${r.nomePaciente} — a solicitação foi vinculada a esse cadastro.`
          : `CPF cadastrado para ${r.nomePaciente}.`,
        r.repontado ? 'info' : 'sucesso',
      );

      aoLiberar(alvo.id);
      aoFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal aberto={alvo !== null} aoFechar={aoFechar} titulo="Informe o CPF do paciente">
      <div className="space-y-4">
        <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Este agendamento veio do SISREG sem o CPF do paciente. Cadastre o CPF para abrir a
            solicitação e liberar o exame — sem ele o pedido não pode ser enviado ao equipamento.
          </span>
        </div>

        <p className="text-sm text-gray-600">
          <strong>{alvo.pacienteNome}</strong>
          {alvo.procedimento ? ` — ${alvo.procedimento}` : ''}
        </p>

        <Campo label="CPF do paciente" htmlFor="solicitacao-cpf">
          <Input
            id="solicitacao-cpf"
            value={cpf}
            onChange={(e) => setCpf(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && valido) void confirmar();
            }}
            placeholder="000.000.000-00"
            inputMode="numeric"
            autoFocus
          />
        </Campo>

        {digitos.length === 11 && !valido && (
          <p className="text-sm text-red-600">
            CPF inválido — confira os dígitos.
          </p>
        )}

        {erro && (
          <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{erro}</div>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={definir.isPending}>
            Cancelar
          </Button>
          <Button onClick={confirmar} disabled={!valido || definir.isPending}>
            {definir.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" /> Cadastrando…
              </>
            ) : (
              <>
                <IdCard className="h-4 w-4" /> Cadastrar CPF e abrir
              </>
            )}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
