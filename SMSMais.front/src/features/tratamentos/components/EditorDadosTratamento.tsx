import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarDuracao } from '@/shared/lib/tempoMedio';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  useAtualizarTratamento,
  useOpcoesUnidadesAtendimento,
  useTiposTratamento,
} from '@/features/tratamentos/api/queries';
import { CampoLimiteAcompanhantes } from '@/features/tratamentos/components/CampoLimiteAcompanhantes';
import { CamposNecessidades } from '@/features/tratamentos/components/CamposNecessidades';
import { necessidadesValidas, paraNecessidadesPayload } from '@/features/tratamentos/lib/necessidades';
import type { Necessidades, RegraAcompanhantesPayload, Tratamento } from '@/features/tratamentos/types';

type Props = {
  tratamento: Tratamento;
  aoConcluir: () => void;
};

/**
 * Edita os dados do atendimento, a condição do paciente e o limite de acompanhantes (a agenda se
 * troca à parte). Trocar o destino vale para as próximas rotas geradas; rota já montada não se
 * refaz sozinha.
 */
export function EditorDadosTratamento({ tratamento: t, aoConcluir }: Props) {
  const atualizar = useAtualizarTratamento();
  const destinos = useOpcoesUnidadesAtendimento();
  const tipos = useTiposTratamento();
  const [dados, setDados] = useState({
    descricao: t.descricao,
    unidadeAtendimentoId: t.unidadeAtendimentoId,
    tipoTratamentoId: t.tipoTratamentoId ?? '',
    observacoes: t.observacoes ?? '',
  });
  const [necessidades, setNecessidades] = useState<Necessidades>(t.necessidades);
  const [regra, setRegra] = useState<RegraAcompanhantesPayload>({
    quantidade: t.acompanhantes.quantidade === 2 ? 2 : 1,
    justificativaSegundo: t.acompanhantes.justificativaSegundo,
  });
  const [erro, setErro] = useState<string | null>(null);

  const opcoes = destinos.data ?? [];
  // O destino atual pode ter sido desativado depois — continua aparecendo para não sumir do select.
  const destinoAtualForaDaLista = !opcoes.some((u) => u.id === t.unidadeAtendimentoId);
  const tipoEscolhido = (tipos.data ?? []).find((tp) => tp.id === dados.tipoTratamentoId);
  const podeSalvar =
    dados.descricao.trim().length > 0 &&
    Boolean(dados.unidadeAtendimentoId) &&
    Boolean(dados.tipoTratamentoId) &&
    necessidadesValidas(necessidades) &&
    (regra.quantidade === 1 || Boolean(regra.justificativaSegundo?.trim()));

  async function salvar() {
    setErro(null);
    try {
      await atualizar.mutateAsync({
        id: t.id,
        payload: {
          descricao: dados.descricao.trim(),
          unidadeAtendimentoId: dados.unidadeAtendimentoId,
          tipoTratamentoId: dados.tipoTratamentoId,
          observacoes: dados.observacoes.trim() || null,
          necessidades: paraNecessidadesPayload(necessidades),
          acompanhantes: regra,
        },
      });
      aoConcluir();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Unidade de atendimento (destino)" htmlFor="ed-destino" required className="md:col-span-2">
          <Select
            id="ed-destino"
            value={dados.unidadeAtendimentoId}
            onChange={(e) => setDados((d) => ({ ...d, unidadeAtendimentoId: e.target.value }))}
          >
            {destinoAtualForaDaLista ? (
              <option value={t.unidadeAtendimentoId}>{t.unidadeAtendimentoNome} (desativada)</option>
            ) : null}
            {opcoes.map((u) => (
              <option key={u.id} value={u.id}>
                {u.nome}
                {u.cidade ? ` — ${u.cidade}${u.uf ? `/${u.uf}` : ''}` : ''}
              </option>
            ))}
          </Select>
        </Campo>
        <Campo
          label="Tipo de tratamento"
          htmlFor="ed-tipo"
          required
          dica={
            tipoEscolhido
              ? `Tempo médio do tipo: ${formatarDuracao(tipoEscolhido.tempoMedioMinutos)}.`
              : 'O tempo médio vem do tipo.'
          }
        >
          <Select
            id="ed-tipo"
            value={dados.tipoTratamentoId}
            onChange={(e) => setDados((d) => ({ ...d, tipoTratamentoId: e.target.value }))}
          >
            <option value="">— Selecione —</option>
            {(tipos.data ?? []).map((tp) => (
              <option key={tp.id} value={tp.id}>{tp.nome}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Descrição" htmlFor="ed-descricao" required>
          <Input
            id="ed-descricao"
            value={dados.descricao}
            onChange={(e) => setDados((d) => ({ ...d, descricao: e.target.value }))}
            maxLength={500}
          />
        </Campo>
        <Campo label="Observações" htmlFor="ed-obs" className="md:col-span-2">
          <textarea
            id="ed-obs"
            className="input min-h-[70px]"
            value={dados.observacoes}
            onChange={(e) => setDados((d) => ({ ...d, observacoes: e.target.value }))}
          />
        </Campo>
      </div>

      <div className="border-t border-gray-100 pt-4">
        <CamposNecessidades valor={necessidades} aoMudar={setNecessidades} />
      </div>
      <div className="border-t border-gray-100 pt-4">
        <CampoLimiteAcompanhantes valor={regra} aoMudar={setRegra} liberadoPorNome={t.acompanhantes.liberadoPorNome} />
      </div>

      {erro ? (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      <div className="flex justify-end gap-2">
        <Button type="button" variante="ghost" onClick={aoConcluir}>Cancelar</Button>
        <Button type="button" onClick={salvar} disabled={!podeSalvar || atualizar.isPending}>
          {atualizar.isPending ? 'Salvando…' : 'Salvar'}
        </Button>
      </div>
    </div>
  );
}
