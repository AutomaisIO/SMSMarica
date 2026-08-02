import { useEffect, useState } from 'react';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { apenasDigitosCpf, cpfValido } from '@/shared/lib/cpf';
import { useResolverPendenciaComPaciente } from '@/features/painel-inicio/api/queries';

type PendenciaAlvo = {
  id: string;
  pacienteNome: string | null;
  procedimento: string | null;
  codigoSolicitacao: string | null;
};

type Props = {
  pendencia: PendenciaAlvo | null;
  aoFechar: () => void;
};

/**
 * "Informar CPF e importar" (ADR-0035 §4). O CPF **vincula um paciente já cadastrado** — não
 * cadastra: o export do SISREG não traz data de nascimento, e criar o Patient por aqui gravaria
 * data default no hub. Quando o CPF não existe, o backend responde dizendo isso com todas as
 * letras, e é essa mensagem que aparece ao operador.
 */
export function ModalInformarCpf({ pendencia, aoFechar }: Props) {
  const [cpf, setCpf] = useState('');
  const resolver = useResolverPendenciaComPaciente();

  useEffect(() => {
    if (pendencia) setCpf('');
  }, [pendencia]);

  if (!pendencia) return null;

  const digitos = apenasDigitosCpf(cpf);
  // Valida o dígito verificador aqui, antes da ida ao servidor: um CPF mal digitado que por acaso
  // exista importaria a marcação para o PACIENTE ERRADO — o pior modo de falha desta tela.
  const valido = cpfValido(digitos);

  async function confirmar() {
    if (!valido || !pendencia) return;
    try {
      const r = await resolver.mutateAsync({ falhaId: pendencia.id, cpf: digitos });
      notificar(r.mensagem, r.resolvida ? 'sucesso' : 'erro');
      if (r.resolvida) aoFechar();
    } catch {
      // O interceptor do httpClient já notifica o ProblemDetails (inclusive o
      // "cadastre o paciente em Pacientes e volte") — não duplicar a mensagem aqui.
    }
  }

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Informar CPF e importar"
      descricao="Esta marcação do SISREG não entrou no sistema porque o CADSUS não devolveu o CPF do paciente."
      largura="sm"
    >
      <div className="space-y-4">
        <div className="rounded-lg bg-gray-50 p-3 text-sm">
          <p className="font-medium text-gray-900">{pendencia.pacienteNome ?? 'Paciente não identificado'}</p>
          {pendencia.procedimento && <p className="text-gray-600">{pendencia.procedimento}</p>}
          {pendencia.codigoSolicitacao && (
            <p className="mt-1 text-xs text-gray-500">SISREG nº {pendencia.codigoSolicitacao}</p>
          )}
        </div>

        <Campo
          label="CPF do paciente"
          htmlFor="cpf-pendencia"
          required
          dica="O paciente precisa já estar cadastrado. Se ainda não estiver, cadastre-o em Pacientes e volte aqui — o SISREG não informa a data de nascimento, então o cadastro não pode ser feito por esta tela."
        >
          <Input
            id="cpf-pendencia"
            inputMode="numeric"
            autoFocus
            value={cpf}
            onChange={(e) => setCpf(e.target.value)}
            placeholder="000.000.000-00"
          />
        </Campo>

        <div className="flex justify-end gap-2">
          <Button variante="ghost" onClick={aoFechar} disabled={resolver.isPending}>
            Cancelar
          </Button>
          <Button onClick={confirmar} disabled={!valido || resolver.isPending}>
            {resolver.isPending ? 'Importando…' : 'Importar'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
