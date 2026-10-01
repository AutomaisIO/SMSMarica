import { useRef, useState } from 'react';
import { FolderOpen, Loader2, Paperclip, Trash2 } from 'lucide-react';

import { DialogoDocumento, tituloDoArquivo } from './DialogoDocumento';
import { SeletorAcervo } from './SeletorAcervo';
import type { ItemAcervo } from './tipos';
import { VisualizadorArquivo } from './VisualizadorArquivo';

export type AnexoRascunho = {
  id: string;
  nomeArquivo: string;
  titulo?: string | null;
  descricao?: string | null;
  tamanho: number;
  enviadoEm: string | null;
};

type Props = {
  anexos: AnexoRascunho[];
  somenteLeitura: boolean;
  /** "SER" / "SERNIT" — o selo do anexo que já subiu. */
  sistema: string;
  rascunhoId: string | null;
  /**
   * Salva o rascunho como está na tela e devolve o id — o anexo precisa de dono, e o "anexar do
   * cadastro" precisa do CNS gravado para achar o paciente.
   */
  salvarRascunho: () => Promise<string>;
  enviar: (rascunhoId: string, arquivo: File, titulo: string, descricao: string | null) => Promise<void>;
  remover: (rascunhoId: string, anexoId: string) => void;
  caminhoConteudo: (rascunhoId: string, anexoId: string) => string;
  listarAcervo: (rascunhoId: string) => Promise<ItemAcervo[]>;
  caminhoConteudoAcervo: (rascunhoId: string, item: ItemAcervo) => string;
  anexarDoAcervo: (rascunhoId: string, chave: string) => Promise<void>;
  /** Avisos e erros vão para a faixa da própria tela. */
  aoAvisar: (mensagem: string) => void;
  aoErro: (erro: unknown) => void;
};

/**
 * Anexos de um rascunho do SER/SERNIT: pede nome e descrição no upload (o arquivo também fica no
 * cadastro do paciente), abre o anexo no visualizador e anexa direto do cadastro.
 */
export function AnexosRascunho({
  anexos,
  somenteLeitura,
  sistema,
  rascunhoId,
  salvarRascunho,
  enviar,
  remover,
  caminhoConteudo,
  listarAcervo,
  caminhoConteudoAcervo,
  anexarDoAcervo,
  aoAvisar,
  aoErro,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [arquivoNovo, setArquivoNovo] = useState<File | null>(null);
  const [aberto, setAberto] = useState<{ rascunhoId: string; anexo: AnexoRascunho } | null>(null);
  const [seletorRascunho, setSeletorRascunho] = useState<string | null>(null);
  const [preparando, setPreparando] = useState(false);

  function abrir(anexo: AnexoRascunho) {
    if (rascunhoId) setAberto({ rascunhoId, anexo });
  }

  async function abrirSeletor() {
    setPreparando(true);
    try {
      setSeletorRascunho(await salvarRascunho());
    } catch (e) {
      aoErro(e);
    } finally {
      setPreparando(false);
    }
  }

  return (
    <>
      <ul className="mb-2 space-y-1">
        {anexos.map((a) => (
          <li key={a.id} className="flex items-center gap-2 text-sm">
            <button type="button" onClick={() => abrir(a)} className="min-w-0 flex-1 text-left" title="Visualizar arquivo">
              <span className="block truncate hover:text-red-700 hover:underline">{a.titulo || a.nomeArquivo}</span>
              {a.titulo || a.descricao ? (
                <span className="block truncate text-[11px] text-slate-400">
                  {[a.descricao, a.titulo ? a.nomeArquivo : null].filter(Boolean).join(' · ')}
                </span>
              ) : null}
            </button>
            <span className="shrink-0 text-xs text-slate-500">{(a.tamanho / 1024).toFixed(0)} KB</span>
            {a.enviadoEm ? (
              <span className="shrink-0 text-xs text-emerald-700">no {sistema}</span>
            ) : (
              !somenteLeitura && (
                <button
                  type="button"
                  title="Remover anexo"
                  onClick={() => rascunhoId && remover(rascunhoId, a.id)}
                  className="shrink-0 text-slate-400 hover:text-red-700"
                >
                  <Trash2 className="size-4" />
                </button>
              )
            )}
          </li>
        ))}
      </ul>

      {!somenteLeitura && (
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50"
          >
            <Paperclip className="size-4" />
            Anexar arquivo
          </button>
          <input
            ref={inputRef}
            type="file"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) setArquivoNovo(f);
              e.target.value = '';
            }}
          />
          <button
            type="button"
            onClick={abrirSeletor}
            disabled={preparando}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50 disabled:opacity-50"
            title="Escolher um documento que o paciente (pelo CNS) já tem no cadastro"
          >
            {preparando ? <Loader2 className="size-4 animate-spin" /> : <FolderOpen className="size-4" />}
            Anexar do cadastro
          </button>
        </div>
      )}

      <DialogoDocumento
        aberto={arquivoNovo !== null}
        tituloModal="Anexar documento"
        descricaoModal={arquivoNovo?.name}
        tituloInicial={arquivoNovo ? tituloDoArquivo(arquivoNovo.name) : ''}
        rotuloConfirmar="Anexar"
        aoFechar={() => setArquivoNovo(null)}
        aoConfirmar={async (titulo, descricao) => {
          if (!arquivoNovo) return;
          // Erro sobe para o próprio modal, que mostra a recusa sem perder o que foi digitado.
          const id = await salvarRascunho();
          await enviar(id, arquivoNovo, titulo, descricao);
          aoAvisar(`"${titulo}" anexado. Fica guardado aqui e sobe junto no envio.`);
          setArquivoNovo(null);
        }}
      />

      {aberto ? (
        <VisualizadorArquivo
          caminho={caminhoConteudo(aberto.rascunhoId, aberto.anexo.id)}
          titulo={aberto.anexo.titulo || aberto.anexo.nomeArquivo}
          descricao={aberto.anexo.descricao}
          nomeArquivo={aberto.anexo.nomeArquivo}
          aoFechar={() => setAberto(null)}
        />
      ) : null}

      {seletorRascunho ? (
        <SeletorAcervo
          aberto
          aoFechar={() => setSeletorRascunho(null)}
          chaveConsulta={['acervo-rascunho', sistema, seletorRascunho]}
          carregar={() => listarAcervo(seletorRascunho)}
          caminhoConteudo={(item) => caminhoConteudoAcervo(seletorRascunho, item)}
          aoAnexar={async (item) => {
            await anexarDoAcervo(seletorRascunho, item.chave);
            aoAvisar(`"${item.titulo}" anexado do cadastro.`);
          }}
          avisoVazio="Nada no cadastro — confira se o CNS informado é de um paciente cadastrado."
        />
      ) : null}
    </>
  );
}
