import { useRef, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  FileText,
  FolderOpen,
  Image as ImagemIcone,
  Loader2,
  Paperclip,
  Trash2,
} from 'lucide-react';

import { formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
import { DialogoDocumento, tituloDoArquivo } from '@/shared/acervo/DialogoDocumento';
import {
  PreviaCompactacao,
  resumoCompactacao,
  type ArquivoPreparado,
  type CompactacaoNaEntrada,
} from '@/shared/acervo/PreviaCompactacao';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';

export type ArquivoResumo = {
  id: string;
  nome: string;
  contentType: string;
  tamanho: number;
  versao: number;
  /** Preenchido quando o arquivo já foi ao sistema de regulação — daí não se remove mais. */
  enviadoAoSistemaEm?: string | null;
  /** Nome do documento dado por quem anexou (o nome do arquivo fica como apoio). */
  titulo?: string | null;
  descricao?: string | null;
};

/** O que quem anexou disse do documento — vai junto com o arquivo. */
export type InfoDocumento = { titulo: string; descricao: string | null };

/** Uma prévia em andamento: o que o servidor compactou e o nome do arquivo escolhido. */
type Preparo = { preparado: ArquivoPreparado; nomeOriginal: string };

type Props = {
  titulo?: string;
  /** Texto da crítica, quando o sistema de regulação recusou o documento. */
  situacao?: string;
  obrigatoria?: boolean;
  multiple?: boolean;
  /** Content-types aceitos, vindos da configuração do módulo. */
  accept: string[];
  limiteMb: number;
  arquivos: ArquivoResumo[];
  /** Com `pedirTitulo`, recebe um arquivo por vez junto com o nome e a descrição. Sem uso com `compactar`. */
  onEnviar?: (files: File[], info?: InfoDocumento) => Promise<void>;
  /**
   * Compactação na entrada: o arquivo vai ao servidor, volta compactado numa prévia e só entra
   * quando a pessoa confere que ficou legível. Um arquivo por vez; com `pedirTitulo`, o nome é
   * pedido depois da prévia aceita.
   */
  compactar?: CompactacaoNaEntrada;
  onRemover?: (id: string) => Promise<void>;
  /** Abre o arquivo no visualizador (o item da lista vira clicável). */
  onAbrir?: (arquivo: ArquivoResumo) => void;
  /** Mostra "Anexar do cadastro" — escolher um documento que o paciente já tem guardado. */
  onAnexarDoCadastro?: () => void;
  /** Pede nome e descrição do documento antes de enviar (um arquivo por vez). */
  pedirTitulo?: boolean;
  disabled?: boolean;
};

/**
 * Caixinha de anexo: uma por documento exigido.
 *
 * <p>É uma caixinha por exigência, e não um monte único de arquivos, porque o sistema de
 * regulação critica <b>um</b> documento específico ("laudo ilegível") — sem separar, ninguém
 * sabe qual trocar.</p>
 *
 * <p>O tamanho e os tipos são validados aqui <b>e</b> no servidor. Aqui é só cortesia: evita
 * subir 20 MB para receber recusa. A validação que vale é a de lá.</p>
 */
export function UploadAnexo({
  titulo,
  situacao,
  obrigatoria,
  multiple = true,
  accept,
  limiteMb,
  arquivos,
  onEnviar,
  onRemover,
  onAbrir,
  onAnexarDoCadastro,
  pedirTitulo,
  compactar,
  disabled,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState(false);
  const [aguardandoTitulo, setAguardandoTitulo] = useState<File | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [removendo, setRemovendo] = useState<string | null>(null);
  /** Compactado pelo servidor, esperando a pessoa conferir. */
  const [previa, setPrevia] = useState<Preparo | null>(null);
  /** Prévia aceita, esperando o nome do documento (com `pedirTitulo`). */
  const [nomeando, setNomeando] = useState<Preparo | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const limiteBytes = limiteMb * 1024 * 1024;

  async function aoEscolher(lista: FileList | null) {
    if (!lista?.length) return;
    setErro(null);
    setAviso(null);

    const escolhidos = Array.from(lista);
    const grande = escolhidos.find((f) => f.size > limiteBytes);
    if (grande) {
      setErro(`"${grande.name}" tem ${formatarTamanhoBytes(grande.size)} — o limite é ${limiteMb} MB.`);
      return;
    }
    const tipoRuim = escolhidos.find((f) => f.type && !accept.includes(f.type));
    if (tipoRuim) {
      setErro(`"${tipoRuim.name}" não é um tipo aceito.`);
      return;
    }

    if (compactar) {
      const arquivo = escolhidos[0];
      setEnviando(true);
      try {
        setPrevia({ preparado: await compactar.preparar(arquivo), nomeOriginal: arquivo.name });
      } catch (e) {
        // Recusa de regra (tipo, tamanho) é 4xx e não passa pelo aviso geral: aparece aqui.
        setErro(extrairMensagemDeErro(e));
      } finally {
        setEnviando(false);
        if (inputRef.current) inputRef.current.value = '';
      }
      return;
    }

    if (pedirTitulo) {
      // O nome vem antes do envio: é ele que faz o documento ser achado depois no cadastro.
      setAguardandoTitulo(escolhidos[0]);
      if (inputRef.current) inputRef.current.value = '';
      return;
    }

    setEnviando(true);
    try {
      await onEnviar?.(escolhidos);
    } catch {
      // O interceptor do httpClient já mostra a mensagem do servidor.
    } finally {
      setEnviando(false);
      // Zera o input para permitir reenviar o MESMO arquivo depois de um erro.
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  async function aceitarPrevia() {
    if (!previa || !compactar) return;
    if (pedirTitulo) {
      setNomeando(previa);
      setPrevia(null);
      return;
    }
    await compactar.aceitar(previa.preparado, null, null);
    setPrevia(null);
  }

  /** Recusou (ou fechou) a prévia ou o nome: o servidor esquece o arquivo; nada fica guardado. */
  function descartar(alvo: Preparo) {
    void compactar?.descartar(alvo.preparado).catch(() => {
      // Expira sozinho em 30 min — não vale incomodar ninguém por isso.
    });
    setAviso('Arquivo descartado — nada foi anexado.');
  }

  async function aoRemover(id: string) {
    setRemovendo(id);
    try {
      await onRemover?.(id);
    } finally {
      setRemovendo(null);
    }
  }

  const criticada = !!situacao;

  return (
    <div
      className={cn(
        'rounded-md border p-3',
        criticada ? 'border-amber-300 bg-amber-50/60' : 'border-slate-200 bg-white',
      )}
    >
      {titulo ? (
        <div className="mb-2 flex items-start justify-between gap-2">
          <p className="text-sm font-medium text-slate-800">
            {titulo}
            {obrigatoria ? <span className="ml-1 text-red-600">*</span> : null}
          </p>
          {arquivos.length > 0 && !criticada ? (
            <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" />
          ) : null}
        </div>
      ) : null}

      {criticada ? (
        <p className="mb-2 flex items-start gap-1.5 text-xs text-amber-800">
          <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
          <span>{situacao}</span>
        </p>
      ) : null}

      {arquivos.length > 0 ? (
        <ul className="mb-2 space-y-1">
          {arquivos.map((a) => {
            const noSistema = !!a.enviadoAoSistemaEm;
            const Icone = a.contentType.startsWith('image/') ? ImagemIcone : FileText;
            return (
              <li
                key={a.id}
                className="flex items-center gap-2 rounded border border-slate-200 bg-slate-50 px-2 py-1.5 text-sm"
              >
                <Icone className="size-4 shrink-0 text-slate-400" />
                {onAbrir ? (
                  <button
                    type="button"
                    onClick={() => onAbrir(a)}
                    className="min-w-0 flex-1 text-left"
                    title="Visualizar arquivo"
                  >
                    <span className="block truncate text-slate-700 hover:text-red-700 hover:underline">
                      {a.titulo || a.nome}
                    </span>
                    {a.titulo || a.descricao ? (
                      <span className="block truncate text-[11px] text-slate-400">
                        {[a.descricao, a.titulo ? a.nome : null].filter(Boolean).join(' · ')}
                      </span>
                    ) : null}
                  </button>
                ) : (
                  <span className="min-w-0 flex-1 truncate text-slate-700">{a.titulo || a.nome}</span>
                )}
                <span className="shrink-0 text-xs text-slate-400">
                  {formatarTamanhoBytes(a.tamanho)}
                </span>
                {a.versao > 1 ? (
                  <span className="shrink-0 rounded bg-slate-200 px-1.5 text-[11px] text-slate-600">
                    v{a.versao}
                  </span>
                ) : null}
                {noSistema ? (
                  <span
                    className="shrink-0 rounded bg-emerald-100 px-1.5 text-[11px] text-emerald-800"
                    title="Já enviado ao sistema de regulação — não pode ser removido"
                  >
                    no sistema
                  </span>
                ) : onRemover ? (
                  <button
                    type="button"
                    onClick={() => aoRemover(a.id)}
                    disabled={disabled || removendo === a.id}
                    className="shrink-0 rounded p-0.5 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-50"
                    aria-label={`Remover ${a.nome}`}
                  >
                    {removendo === a.id ? (
                      <Loader2 className="size-3.5 animate-spin" />
                    ) : (
                      <Trash2 className="size-3.5" />
                    )}
                  </button>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}

      <input
        ref={inputRef}
        type="file"
        className="hidden"
        multiple={multiple && !pedirTitulo && !compactar}
        accept={accept.join(',')}
        onChange={(e) => aoEscolher(e.target.files)}
      />
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={disabled || enviando}
        className="inline-flex items-center gap-1.5 rounded-md border border-dashed border-slate-300 px-3 py-1.5 text-sm text-slate-600 hover:border-red-300 hover:text-red-700 disabled:opacity-50"
      >
        {enviando ? <Loader2 className="size-4 animate-spin" /> : <Paperclip className="size-4" />}
        {enviando
          ? compactar
            ? 'Compactando…'
            : 'Enviando…'
          : arquivos.length
            ? 'Anexar mais'
            : 'Anexar arquivo'}
      </button>
      {onAnexarDoCadastro ? (
        <button
          type="button"
          onClick={onAnexarDoCadastro}
          disabled={disabled || enviando}
          className="ml-2 inline-flex items-center gap-1.5 rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-600 hover:border-red-300 hover:text-red-700 disabled:opacity-50"
          title="Escolher um documento que o paciente já tem no cadastro"
        >
          <FolderOpen className="size-4" />
          Anexar do cadastro
        </button>
      ) : null}
      <p className="mt-1 text-[11px] text-slate-400">
        Até {limiteMb} MB por arquivo. Foto do celular serve.
        {compactar ? ' O arquivo é compactado e você confere se ficou legível antes de anexar.' : null}
      </p>

      {erro ? <p className="mt-1 text-xs font-medium text-red-600">{erro}</p> : null}
      {aviso ? <p className="mt-1 text-xs text-slate-500">{aviso}</p> : null}

      {previa && compactar ? (
        <PreviaCompactacao
          preparado={previa.preparado}
          caminho={compactar.caminhoConteudo(previa.preparado)}
          rotuloAceitar={pedirTitulo ? 'continuar' : 'anexar'}
          aoAceitar={aceitarPrevia}
          aoDescartar={() => {
            descartar(previa);
            setPrevia(null);
          }}
        />
      ) : null}

      <DialogoDocumento
        aberto={nomeando !== null}
        tituloModal={titulo ? `Anexar em "${titulo}"` : 'Anexar documento'}
        descricaoModal={nomeando ? resumoCompactacao(nomeando.preparado) : undefined}
        tituloInicial={nomeando ? tituloDoArquivo(nomeando.nomeOriginal) : ''}
        rotuloConfirmar="Anexar"
        aoFechar={() => {
          if (nomeando) descartar(nomeando);
          setNomeando(null);
        }}
        aoConfirmar={async (t, d) => {
          if (!nomeando || !compactar) return;
          await compactar.aceitar(nomeando.preparado, t, d);
          setNomeando(null);
        }}
      />

      <DialogoDocumento
        aberto={aguardandoTitulo !== null}
        tituloModal={titulo ? `Anexar em "${titulo}"` : 'Anexar documento'}
        descricaoModal={aguardandoTitulo?.name}
        tituloInicial={aguardandoTitulo ? tituloDoArquivo(aguardandoTitulo.name) : ''}
        rotuloConfirmar="Anexar"
        aoFechar={() => setAguardandoTitulo(null)}
        aoConfirmar={async (t, d) => {
          if (!aguardandoTitulo) return;
          setEnviando(true);
          try {
            await onEnviar?.([aguardandoTitulo], { titulo: t, descricao: d });
            setAguardandoTitulo(null);
          } finally {
            setEnviando(false);
          }
        }}
      />
    </div>
  );
}
