import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { FilePlus2, FolderOpen, Loader2, Paperclip, Save } from 'lucide-react';
import { extrairMensagemDeErro, http } from '@/shared/api/httpClient';
import { SeletorAcervo } from '@/shared/acervo/SeletorAcervo';
import type { ItemAcervo } from '@/shared/acervo/tipos';
import { AcaoVisualizador, VisualizadorArquivo } from '@/shared/acervo/VisualizadorArquivo';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { AdicionarExameModal } from '@/features/anamnese/components/AdicionarExameModal';
import { AnexoExameItem } from '@/features/anamnese/components/AnexoExameItem';
import {
  anexosKeys,
  useAnexosExame,
  useCriarTokenAnexo,
  useExcluirAnexo,
  useSalvarAnexo,
} from '@/features/anamnese/api/queries';
import type { AnexoExameDto, AnexoUploadTokenDto } from '@/features/anamnese/types';

type Props = {
  solicitacaoExameId: string;
  podeEditar: boolean;
};

/**
 * Seção "Documentos / Exames anexados" da anamnese: lista os PDFs digitalizados
 * e orquestra a ponte QR. Botão "Adicionar Exame" cria um token e abre o modal
 * com o QR; enquanto o modal está aberto a lista faz polling (3s) para mostrar
 * os documentos que o PWA vai enviando, com Revisar / Salvar / Excluir.
 */
export function AnexosExameSecao({ solicitacaoExameId, podeEditar }: Props) {
  const [modalAberto, setModalAberto] = useState(false);
  const [token, setToken] = useState<AnexoUploadTokenDto | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [revisandoId, setRevisandoId] = useState<string | null>(null);
  const [salvandoId, setSalvandoId] = useState<string | null>(null);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const [vendo, setVendo] = useState<AnexoExameDto | null>(null);
  const [seletorAberto, setSeletorAberto] = useState(false);
  const qc = useQueryClient();

  const anexosQuery = useAnexosExame(solicitacaoExameId, { polling: modalAberto });
  const criarToken = useCriarTokenAnexo();
  const salvar = useSalvarAnexo(solicitacaoExameId);
  const excluir = useExcluirAnexo(solicitacaoExameId);

  const anexos = anexosQuery.data ?? [];

  function aoAdicionar() {
    setErro(null);
    criarToken.mutate(solicitacaoExameId, {
      onSuccess: (t) => {
        setToken(t);
        setModalAberto(true);
      },
      onError: (e) => setErro(extrairMensagemDeErro(e)),
    });
  }

  // Revisar = abrir no visualizador do sistema (PDF ou imagem), com "Salvar" ali mesmo.
  function aoRevisar(a: AnexoExameDto) {
    setErro(null);
    setRevisandoId(null);
    setVendo(a);
  }

  function aoSalvar(a: AnexoExameDto) {
    setErro(null);
    setSalvandoId(a.id);
    salvar.mutate(
      { id: a.id, payload: {} },
      {
        onError: (e) => setErro(extrairMensagemDeErro(e)),
        onSettled: () => setSalvandoId(null),
      },
    );
  }

  function aoExcluir(a: AnexoExameDto) {
    if (!window.confirm(`Excluir o documento "${a.nome}"?`)) return;
    setErro(null);
    setExcluindoId(a.id);
    excluir.mutate(a.id, {
      onError: (e) => setErro(extrairMensagemDeErro(e)),
      onSettled: () => setExcluindoId(null),
    });
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex items-center gap-2 rounded-full bg-emerald-600 px-4 py-1.5 text-sm font-bold text-white">
          <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/25 text-xs">
            6
          </span>
          <Paperclip className="h-4 w-4" />
          DOCUMENTOS / EXAMES ANEXADOS
        </div>
        {podeEditar ? (
          <div className="flex flex-wrap gap-2">
            <Button variante="outline" tamanho="sm" onClick={() => setSeletorAberto(true)}>
              <FolderOpen className="mr-2 h-4 w-4" />
              Anexar do cadastro
            </Button>
            <Button variante="outline" tamanho="sm" onClick={aoAdicionar} disabled={criarToken.isPending}>
              {criarToken.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <FilePlus2 className="mr-2 h-4 w-4" />
              )}
              Adicionar Exame
            </Button>
          </div>
        ) : null}
      </div>

      {erro ? (
        <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      <div className="mt-4">
        {anexosQuery.isPending ? (
          <p className="flex items-center gap-2 text-sm text-gray-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando documentos…
          </p>
        ) : anexos.length === 0 ? (
          <p className="text-sm text-gray-400">
            Nenhum documento anexado. Use “Adicionar Exame” para digitalizar pelo celular.
          </p>
        ) : (
          <ul className="space-y-2">
            {anexos.map((a) => (
              <AnexoExameItem
                key={a.id}
                anexo={a}
                podeEditar={podeEditar}
                aoRevisar={() => aoRevisar(a)}
                aoSalvar={() => aoSalvar(a)}
                aoExcluir={() => aoExcluir(a)}
                revisando={revisandoId === a.id}
                salvando={salvandoId === a.id}
                excluindo={excluindoId === a.id}
              />
            ))}
          </ul>
        )}
      </div>

      {vendo ? (
        <VisualizadorArquivo
          caminho={`/anexos/${vendo.id}/conteudo`}
          titulo={vendo.nome}
          descricao={vendo.descricao}
          aoFechar={() => setVendo(null)}
          acoes={
            podeEditar && vendo.status === 'Pendente' ? (
              <AcaoVisualizador
                destaque
                disabled={salvandoId === vendo.id}
                aoClicar={() => {
                  aoSalvar(vendo);
                  setVendo(null);
                }}
              >
                <Save className="size-4" /> Salvar na anamnese
              </AcaoVisualizador>
            ) : undefined
          }
        />
      ) : null}

      <SeletorAcervo
        aberto={seletorAberto}
        aoFechar={() => setSeletorAberto(false)}
        chaveConsulta={['anamnese', solicitacaoExameId, 'acervo']}
        carregar={async () =>
          (await http.get<ItemAcervo[]>(`/anamneses/${solicitacaoExameId}/acervo`)).data
        }
        caminhoConteudo={(item) =>
          `/anamneses/${solicitacaoExameId}/acervo/conteudo?chave=${encodeURIComponent(item.chave)}`
        }
        aoAnexar={async (item) => {
          await http.post(`/anamneses/${solicitacaoExameId}/anexos/do-acervo`, { chave: item.chave });
          notificar('Documento do cadastro anexado à anamnese.', 'sucesso');
          await qc.invalidateQueries({ queryKey: anexosKeys.lista(solicitacaoExameId) });
        }}
      />

      {token ? (
        <AdicionarExameModal
          aberto={modalAberto}
          aoFechar={() => setModalAberto(false)}
          token={token}
          anexos={anexos}
          carregando={anexosQuery.isFetching}
          podeEditar={podeEditar}
          aoRevisar={aoRevisar}
          aoSalvar={aoSalvar}
          aoExcluir={aoExcluir}
          revisandoId={revisandoId}
          salvandoId={salvandoId}
          excluindoId={excluindoId}
        />
      ) : null}
    </section>
  );
}
