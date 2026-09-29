import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  useAtualizarTratamento,
  useOpcoesUnidadesAtendimento,
  useTiposTratamento,
} from '@/features/tratamentos/api/queries';
import { CampoTempoMedio } from '@/features/tratamentos/components/CampoTempoMedio';
import { deMinutos, paraMinutos } from '@/features/tratamentos/lib/tempoMedio';
import type { Tratamento } from '@/features/tratamentos/types';

type Props = {
  tratamento: Tratamento;
  aoConcluir: () => void;
};

/**
 * Edita os dados do tratamento (não a periodicidade — essa se ajusta sessão a sessão). Trocar o
 * destino vale para as próximas rotas geradas; rota já montada não se refaz sozinha.
 */
export function EditorDadosTratamento({ tratamento: t, aoConcluir }: Props) {
  const atualizar = useAtualizarTratamento();
  const destinos = useOpcoesUnidadesAtendimento();
  const tipos = useTiposTratamento();
  const tempoInicial = deMinutos(t.tempoMedioMinutos);
  const [dados, setDados] = useState({
    descricao: t.descricao,
    unidadeAtendimentoId: t.unidadeAtendimentoId,
    tipoTratamentoId: t.tipoTratamentoId ?? '',
    codigoSusLiberacao: t.codigoSusLiberacao ?? '',
    horaPrevistaBusca: t.horaPrevistaBusca?.slice(0, 5) ?? '',
    tempoMedioHoras: tempoInicial.horas,
    tempoMedioMinutos: tempoInicial.minutos,
    observacoes: t.observacoes ?? '',
  });
  const [erro, setErro] = useState<string | null>(null);

  const tempoMedio = paraMinutos(dados.tempoMedioHoras, dados.tempoMedioMinutos);
  const opcoes = destinos.data ?? [];
  // O destino atual pode ter sido desativado depois — continua aparecendo para não sumir do select.
  const destinoAtualForaDaLista = !opcoes.some((u) => u.id === t.unidadeAtendimentoId);
  const podeSalvar = dados.descricao.trim().length > 0 && Boolean(dados.unidadeAtendimentoId) && tempoMedio != null;

  async function salvar() {
    if (tempoMedio == null) return;
    setErro(null);
    try {
      await atualizar.mutateAsync({
        id: t.id,
        payload: {
          descricao: dados.descricao.trim(),
          unidadeAtendimentoId: dados.unidadeAtendimentoId,
          tipoTratamentoId: dados.tipoTratamentoId || null,
          codigoSusLiberacao: dados.codigoSusLiberacao.trim() || null,
          observacoes: dados.observacoes.trim() || null,
          horaPrevistaBusca: dados.horaPrevistaBusca || null,
          tempoMedioMinutos: tempoMedio,
        },
      });
      aoConcluir();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-4">
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
        <Campo label="Tipo de tratamento" htmlFor="ed-tipo">
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
        <CampoTempoMedio
          horas={dados.tempoMedioHoras}
          minutos={dados.tempoMedioMinutos}
          aoMudar={(v) => setDados((d) => ({ ...d, tempoMedioHoras: v.horas, tempoMedioMinutos: v.minutos }))}
          erro={tempoMedio == null ? 'Entre 1 minuto e 24 horas (minutos de 0 a 59).' : undefined}
        />
        <Campo label="Descrição" htmlFor="ed-descricao" required className="md:col-span-2">
          <Input
            id="ed-descricao"
            value={dados.descricao}
            onChange={(e) => setDados((d) => ({ ...d, descricao: e.target.value }))}
            maxLength={500}
          />
        </Campo>
        <Campo label="Código SUS de liberação" htmlFor="ed-sus">
          <Input
            id="ed-sus"
            value={dados.codigoSusLiberacao}
            onChange={(e) => setDados((d) => ({ ...d, codigoSusLiberacao: e.target.value }))}
            maxLength={60}
          />
        </Campo>
        <Campo label="Horário previsto da busca" htmlFor="ed-hora"
          dica="Vale para sessões novas; as já criadas mantêm o horário delas.">
          <Input
            id="ed-hora"
            type="time"
            value={dados.horaPrevistaBusca}
            onChange={(e) => setDados((d) => ({ ...d, horaPrevistaBusca: e.target.value }))}
          />
        </Campo>
        <Campo label="Observações" htmlFor="ed-obs" className="md:col-span-2">
          <textarea
            id="ed-obs"
            className="input min-h-[80px]"
            value={dados.observacoes}
            onChange={(e) => setDados((d) => ({ ...d, observacoes: e.target.value }))}
          />
        </Campo>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
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
