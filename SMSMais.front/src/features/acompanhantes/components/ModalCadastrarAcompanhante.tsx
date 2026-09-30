import { useState } from 'react';
import { CheckCircle2, Loader2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { apenasDigitosCpf, cpfValido, formatarCpf } from '@/shared/lib/cpf';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import {
  useAdicionarAcompanhante,
  useConsultarAcompanhante,
} from '@/features/acompanhantes/api/queries';
import {
  PARENTESCOS,
  ROTULO_PARENTESCO,
  type Acompanhante,
  type ConsultaAcompanhante,
  type Parentesco,
} from '@/features/acompanhantes/types';

type Props = {
  pacienteId: string;
  aberto: boolean;
  aoFechar: () => void;
  aoCadastrar?: (acompanhante: Acompanhante) => void;
};

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

/**
 * Cadastra um acompanhante na lista do paciente em duas etapas: CPF + nascimento → o sistema
 * encontra o nome (base de pacientes ou consulta de CPF) → confirmar. O nome nunca é digitado.
 */
export function ModalCadastrarAcompanhante({ pacienteId, aberto, aoFechar, aoCadastrar }: Props) {
  const consultar = useConsultarAcompanhante(pacienteId);
  const adicionar = useAdicionarAcompanhante(pacienteId);
  const [cpf, setCpf] = useState('');
  const [nascimento, setNascimento] = useState('');
  const [parentesco, setParentesco] = useState<Parentesco | ''>('');
  const [telefone, setTelefone] = useState('');
  const [encontrado, setEncontrado] = useState<ConsultaAcompanhante | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const cpfDigitos = apenasDigitosCpf(cpf);
  const cpfErro = cpfDigitos.length === 11 && !cpfValido(cpfDigitos) ? 'CPF inválido. Confira os números.' : undefined;
  const podeConferir = cpfValido(cpfDigitos) && Boolean(nascimento) && !consultar.isPending;

  function fechar() {
    setCpf('');
    setNascimento('');
    setParentesco('');
    setTelefone('');
    setEncontrado(null);
    setErro(null);
    aoFechar();
  }

  async function conferir() {
    setErro(null);
    try {
      setEncontrado(await consultar.mutateAsync({ cpf: cpfDigitos, dataNascimento: nascimento }));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function cadastrar() {
    setErro(null);
    try {
      const salvo = await adicionar.mutateAsync({
        cpf: cpfDigitos,
        dataNascimento: nascimento,
        parentesco: parentesco || null,
        telefone: telefone.trim() || null,
      });
      aoCadastrar?.(salvo);
      fechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={fechar}
      titulo="Cadastrar acompanhante"
      descricao="Informe o CPF e a data de nascimento: o sistema confere e traz o nome."
      largura="md"
    >
      <div className="space-y-4">
        {encontrado ? (
          <div className="rounded-md border border-green-200 bg-green-50 p-4">
            <p className="flex items-center gap-2 text-sm font-medium text-green-900">
              <CheckCircle2 className="h-4 w-4" /> Pessoa encontrada
            </p>
            <p className="mt-2 text-base font-semibold text-gray-900">{encontrado.nome}</p>
            <p className="text-sm text-gray-700">
              CPF {formatarCpf(encontrado.cpf)} · nascimento {formatarData(encontrado.dataNascimento)}
            </p>
            {encontrado.jaCadastrado ? (
              <p className="mt-2 text-sm text-amber-800">Essa pessoa já está na lista de acompanhantes do paciente.</p>
            ) : null}
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="CPF" htmlFor="acomp-cpf" required erro={cpfErro}>
              <Input
                id="acomp-cpf"
                inputMode="numeric"
                placeholder="000.000.000-00"
                value={cpf}
                onChange={(e) => setCpf(e.target.value)}
                maxLength={14}
              />
            </Campo>
            <Campo label="Data de nascimento" htmlFor="acomp-nasc" required>
              <Input id="acomp-nasc" type="date" value={nascimento} onChange={(e) => setNascimento(e.target.value)} />
            </Campo>
            <Campo label="Parentesco" htmlFor="acomp-parentesco">
              <Select
                id="acomp-parentesco"
                value={parentesco}
                onChange={(e) => setParentesco(e.target.value as Parentesco | '')}
              >
                <option value="">— Não informado —</option>
                {PARENTESCOS.map((p) => (
                  <option key={p} value={p}>{ROTULO_PARENTESCO[p]}</option>
                ))}
              </Select>
            </Campo>
            <Campo label="Telefone" htmlFor="acomp-tel">
              <Input
                id="acomp-tel"
                inputMode="tel"
                placeholder="(21) 99999-0000"
                value={telefone}
                onChange={(e) => setTelefone(e.target.value)}
                maxLength={20}
              />
            </Campo>
          </div>
        )}

        {erro ? (
          <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        <div className="flex justify-end gap-2">
          {encontrado ? (
            <>
              <Button variante="ghost" onClick={() => { setEncontrado(null); setErro(null); }}>
                Corrigir dados
              </Button>
              {encontrado.jaCadastrado ? (
                <Button onClick={fechar}>Fechar</Button>
              ) : (
                <Button onClick={cadastrar} disabled={adicionar.isPending}>
                  {adicionar.isPending ? (
                    <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Cadastrando…</>
                  ) : (
                    'Cadastrar acompanhante'
                  )}
                </Button>
              )}
            </>
          ) : (
            <>
              <Button variante="ghost" onClick={fechar}>Cancelar</Button>
              <Button onClick={conferir} disabled={!podeConferir}>
                {consultar.isPending ? (
                  <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Conferindo…</>
                ) : (
                  <><Search className="mr-2 h-4 w-4" /> Conferir</>
                )}
              </Button>
            </>
          )}
        </div>
      </div>
    </Modal>
  );
}
