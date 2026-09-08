import { useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { useListarEquipamentos } from '@/features/equipamentos/api/queries';
import { rotuloModalidade } from '@/features/equipamentos/types';
import {
  useAtualizarEscopo,
  useEscopoDaUnidade,
  useRemoverEscopo,
} from '@/features/escopo-exames/api/queries';
import type { EscopoExameItem } from '@/features/escopo-exames/types';
import { ModalAdicionarExameAoEscopo } from '@/features/escopo-exames/components/ModalAdicionarExameAoEscopo';

type Props = { unidadeId: string; podeEditar?: boolean };

/**
 * O que esta unidade executa de imagem — worklist e aparelho de destino por exame.
 *
 * É a tela onde a configuração de fato acontece, e substitui o toggle "Integração PACS" que ficava
 * no cadastro GLOBAL do tipo de exame: uma decisão de unidade morando na tela do município, que não
 * conseguia valer `false` para quem não tem aparelho e `true` para quem tem.
 *
 * **Não há aviso de pendência aqui.** Exame desligado é decisão da unidade, não cadastro pela
 * metade: no CDT, ecocardiograma e ecodoppler não devem ir à worklist. A primeira versão marcava
 * 40 linhas de lá como "a configurar" — quarenta alarmes falsos ensinam a ignorar a tela.
 */
export function EscopoExamesDaUnidadeSecao({ unidadeId, podeEditar = true }: Props) {
  const [incluirInativos, setIncluirInativos] = useState(false);
  const [busca, setBusca] = useState('');
  const [modalAberto, setModalAberto] = useState(false);
  const [paraRemover, setParaRemover] = useState<EscopoExameItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const lista = useEscopoDaUnidade(unidadeId, incluirInativos);
  const equipamentos = useListarEquipamentos(unidadeId);
  const atualizar = useAtualizarEscopo();
  const remover = useRemoverEscopo();

  const itens = lista.data ?? [];

  const visiveis = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return itens.filter((i) => {
      if (!termo) return true;
      return (
        i.tipoExameNome.toLowerCase().includes(termo) ||
        (i.codigoSisreg ?? '').toLowerCase().includes(termo)
      );
    });
  }, [itens, busca]);

  function salvar(item: EscopoExameItem, mudanca: Partial<EscopoExameItem>) {
    setErroAcao(null);
    atualizar.mutate(
      {
        id: item.id,
        payload: {
          enviarParaWorklist: mudanca.enviarParaWorklist ?? item.enviarParaWorklist,
          equipamentoId:
            mudanca.equipamentoId !== undefined ? mudanca.equipamentoId : item.equipamentoId,
          ativo: item.ativo,
        },
      },
      { onError: (err) => setErroAcao(extrairMensagemDeErro(err)) },
    );
  }

  async function confirmarRemocao() {
    if (!paraRemover) return;
    setErroAcao(null);
    remover.mutate(paraRemover.id, {
      onSuccess: () => setParaRemover(null),
      onError: (err) => {
        setErroAcao(extrairMensagemDeErro(err));
        setParaRemover(null);
      },
    });
  }

  const colunas: Coluna<EscopoExameItem>[] = [
    {
      chave: 'exame',
      cabecalho: 'Exame',
      render: (i) => (
        <div className={i.ativo ? '' : 'opacity-60'}>
          <span className="font-medium text-gray-900">{i.tipoExameNome}</span>
          <span className="block text-xs text-gray-500">
            {rotuloModalidade(i.modalidadeDicom)}
            {i.codigoSisreg ? ` · SISREG ${i.codigoSisreg}` : ''}
            {i.ativo ? '' : ' · fora do escopo'}
          </span>
        </div>
      ),
    },
    {
      chave: 'equipamento',
      cabecalho: 'Aparelho de destino',
      render: (i) => {
        // Só os aparelhos DESTA unidade E da modalidade DESTE exame. A unidade é o que impede
        // mandar exame daqui para a máquina de outra (o backend valida de novo); a modalidade é o
        // que impede oferecer o mamógrafo para uma radiografia de tórax. Lista vazia aqui costuma
        // ser sintoma de modalidade errada no cadastro do tipo — foi assim que cinco radiografias
        // ficaram marcadas como MG.
        const opcoes = (equipamentos.data ?? []).filter(
          (e) => e.ativo && e.identificadorDicom && e.modalidadeDicom === i.modalidadeDicom,
        );
        return (
          <Select
            aria-label={`Aparelho de destino de ${i.tipoExameNome}`}
            value={i.equipamentoId ?? ''}
            disabled={!podeEditar || atualizar.isPending}
            onChange={(e) => salvar(i, { equipamentoId: e.target.value || null })}
          >
            <option value="">
              {i.equipamentosCompativeis > 1
                ? 'A recepção escolhe'
                : i.equipamentosCompativeis === 1
                  ? 'Único da modalidade'
                  : 'Nenhum aparelho compatível'}
            </option>
            {opcoes.map((e) => (
              <option key={e.id} value={e.id}>
                {e.nome} ({e.identificadorDicom})
              </option>
            ))}
          </Select>
        );
      },
    },
    {
      chave: 'worklist',
      cabecalho: 'Worklist',
      render: (i) => (
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={i.enviarParaWorklist}
            disabled={!podeEditar || atualizar.isPending}
            onChange={(e) => salvar(i, { enviarParaWorklist: e.target.checked })}
          />
          {i.enviarParaWorklist ? 'Envia' : 'Não envia'}
        </label>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (i) =>
        podeEditar ? (
          <button
            type="button"
            onClick={() => setParaRemover(i)}
            className="text-gray-500 hover:text-red-700"
            title="Tirar do escopo desta unidade"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        ) : null,
    },
  ];

  return (
    <div className="space-y-4">
      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Input
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
          placeholder="Buscar exame nesta unidade…"
          className="max-w-xs"
        />
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input
            type="checkbox"
            checked={incluirInativos}
            onChange={(e) => setIncluirInativos(e.target.checked)}
          />
          Mostrar os que saíram do escopo
        </label>
        {podeEditar ? (
          <Button onClick={() => setModalAberto(true)} className="ml-auto">
            <Plus className="mr-2 h-4 w-4" />
            Adicionar exame ao escopo
          </Button>
        ) : null}
      </div>

      <Tabela
        colunas={colunas}
        dados={visiveis}
        chaveLinha={(i) => i.id}
        carregando={lista.isLoading}
        vazio={
          itens.length === 0
            ? 'Esta unidade ainda não tem exames de imagem no escopo. Eles entram sozinhos na importação, ou você pode adicionar aqui.'
            : 'Nenhum exame para este filtro.'
        }
      />

      <ModalAdicionarExameAoEscopo
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        unidadeId={unidadeId}
        jaNoEscopo={itens.map((i) => i.tipoExameId)}
      />

      <ConfirmDialog
        aberto={paraRemover !== null}
        titulo="Tirar do escopo"
        mensagem={
          paraRemover
            ? `Tirar "${paraRemover.tipoExameNome}" do escopo desta unidade? Os exames já feitos não são afetados; os novos deixam de ir à worklist daqui.`
            : ''
        }
        aoConfirmar={confirmarRemocao}
        aoCancelar={() => setParaRemover(null)}
      />
    </div>
  );
}
