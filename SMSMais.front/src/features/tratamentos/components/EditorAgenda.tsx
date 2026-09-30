import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { useAlterarAgenda } from '@/features/tratamentos/api/queries';
import { CamposAgenda } from '@/features/tratamentos/components/CamposAgenda';
import { paraAgendaPayload, type EstadoAgenda } from '@/features/tratamentos/lib/agenda';
import type { Tratamento } from '@/features/tratamentos/types';

type Props = {
  tratamento: Tratamento;
  aoConcluir: () => void;
};

/**
 * Troca a agenda a partir de uma data (hoje ou depois). Sessões pendentes e ainda sem rota daquele
 * dia em diante são refeitas; realizadas, confirmadas e já alocadas ficam.
 */
export function EditorAgenda({ tratamento: t, aoConcluir }: Props) {
  const alterar = useAlterarAgenda();
  const hoje = hojeSP();
  const [agenda, setAgenda] = useState<EstadoAgenda>({
    dataInicio: hoje,
    diasSemanaMascara: t.agenda.diasSemanaMascara,
    continuo: t.agenda.continuo,
    quantidade: t.agenda.quantidadeSessoes ? String(t.agenda.quantidadeSessoes) : '12',
  });
  const [erro, setErro] = useState<string | null>(null);
  const payload = paraAgendaPayload(agenda);

  async function salvar() {
    if (!payload) return;
    setErro(null);
    try {
      await alterar.mutateAsync({ id: t.id, payload });
      aoConcluir();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-4">
      <CamposAgenda valor={agenda} aoMudar={setAgenda} rotuloInicio="Vale a partir de" inicioMinimo={hoje} />
      {!agenda.continuo ? (
        <p className="text-xs text-gray-500">
          O número de sessões é o total do atendimento: as que já aconteceram ou estão confirmadas contam.
        </p>
      ) : null}
      {erro ? (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      <div className="flex justify-end gap-2">
        <Button variante="ghost" onClick={aoConcluir}>Cancelar</Button>
        <Button onClick={salvar} disabled={!payload || payload.dataInicio < hoje || alterar.isPending}>
          {alterar.isPending ? 'Salvando…' : 'Trocar agenda'}
        </Button>
      </div>
    </div>
  );
}
