import { useState } from 'react';
import { Trash2, UserPlus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarCpf } from '@/shared/lib/cpf';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { useAcompanhantes, useRemoverAcompanhante } from '@/features/acompanhantes/api/queries';
import { ModalCadastrarAcompanhante } from '@/features/acompanhantes/components/ModalCadastrarAcompanhante';
import { ROTULO_PARENTESCO, type Acompanhante } from '@/features/acompanhantes/types';

type Props = {
  pacienteId: string;
  podeEditar: boolean;
  /** Texto curto sob o título (ex.: o limite do atendimento). */
  dica?: string;
};

/**
 * Acompanhantes do paciente no transporte — a lista vale para todos os atendimentos dele. Em cada
 * viagem se escolhe quem vai, dentro do limite do atendimento.
 */
export function ListaAcompanhantes({ pacienteId, podeEditar, dica }: Props) {
  const lista = useAcompanhantes(pacienteId);
  const remover = useRemoverAcompanhante(pacienteId);
  const [cadastrando, setCadastrando] = useState(false);
  const [paraRemover, setParaRemover] = useState<Acompanhante | null>(null);
  const [erroRemover, setErroRemover] = useState<string | null>(null);

  async function confirmarRemocao() {
    if (!paraRemover) return;
    setErroRemover(null);
    try {
      await remover.mutateAsync(paraRemover.id);
      setParaRemover(null);
    } catch (e) {
      setErroRemover(extrairMensagemDeErro(e));
    }
  }

  const acompanhantes = lista.data ?? [];

  return (
    <div className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-gray-900">Acompanhantes do paciente</h3>
          <p className="text-xs text-gray-500">
            {dica ?? 'Valem para todos os atendimentos do paciente. Em cada viagem se escolhe quem vai.'}
          </p>
        </div>
        {podeEditar ? (
          <Button variante="outline" tamanho="sm" onClick={() => setCadastrando(true)}>
            <UserPlus className="mr-1.5 h-4 w-4" /> Cadastrar acompanhante
          </Button>
        ) : null}
      </div>

      {lista.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : lista.isError ? (
        <p className="text-sm text-red-700">{extrairMensagemDeErro(lista.error)}</p>
      ) : acompanhantes.length === 0 ? (
        <p className="rounded-md border border-dashed border-gray-300 px-3 py-4 text-center text-sm text-gray-500">
          Nenhum acompanhante cadastrado.
        </p>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">
          {acompanhantes.map((a) => (
            <li key={a.id} className="flex items-center justify-between gap-3 px-3 py-2">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-gray-900">{a.nome}</p>
                <p className="text-xs text-gray-500">
                  {a.parentesco ? `${ROTULO_PARENTESCO[a.parentesco]} · ` : ''}CPF {formatarCpf(a.cpf)}
                  {a.telefone ? ` · ${a.telefone}` : ''}
                </p>
                <div className="mt-1 flex flex-wrap gap-1">
                  {a.tambemEPaciente ? (
                    <span className="rounded-full bg-sky-50 px-2 py-0.5 text-[11px] font-medium text-sky-800">
                      também é paciente
                    </span>
                  ) : null}
                  {a.origem === 'App' ? (
                    <span className="rounded-full bg-violet-50 px-2 py-0.5 text-[11px] font-medium text-violet-800">
                      cadastrado pelo app
                    </span>
                  ) : null}
                </div>
              </div>
              {podeEditar ? (
                <button
                  type="button"
                  onClick={() => setParaRemover(a)}
                  className="rounded-md p-1.5 text-red-700 hover:bg-red-50"
                  aria-label={`Tirar ${a.nome} da lista`}
                  title="Tirar da lista"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      <ModalCadastrarAcompanhante
        pacienteId={pacienteId}
        aberto={cadastrando}
        aoFechar={() => setCadastrando(false)}
      />

      <ConfirmDialog
        aberto={Boolean(paraRemover)}
        titulo="Tirar acompanhante da lista"
        mensagem={
          paraRemover
            ? `Tirar ${paraRemover.nome} da lista de acompanhantes? As viagens em que já acompanhou continuam registradas.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Tirar da lista"
        carregando={remover.isPending}
        erro={erroRemover}
        aoConfirmar={confirmarRemocao}
        aoCancelar={() => { setParaRemover(null); setErroRemover(null); }}
      />
    </div>
  );
}
