import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Camera,
  CheckCircle2,
  Clock,
  FileText,
  FolderOpen,
  Image as ImageIcon,
  Loader2,
  Paperclip,
  Plus,
  ScanLine,
  ShieldCheck,
  Trash2,
  X,
} from 'lucide-react';
import { AxiosError } from 'axios';
import { api, ORIGEM_ENVIADO_PELO_PACIENTE, urlConteudoAcervo, type ItemAcervo } from '@/lib/api';
import { classificarErro, extrairMensagemDeErro } from '@/lib/httpClient';
import { abrirDocumento } from '@/lib/pdf';
import { prepararFotoParaEnvio } from '@/lib/imagem';
import { cn } from '@/lib/cn';
import {
  Card,
  EmptyState,
  ErroCard,
  Field,
  GhostButton,
  PrimaryButton,
  SectionHeader,
  Skeleton,
} from '@/components/ui';

/** Mesmo teto do servidor (25 MB): conferir antes poupa o cidadão de subir o arquivo à toa. */
const TAMANHO_MAXIMO = 25 * 1024 * 1024;
const TITULO_MAX = 200;
const DESCRICAO_MAX = 2000;

/**
 * Documentos do paciente: TUDO o que está no cadastro dele (documentos, anexos da anamnese,
 * laudos assinados, imagens dos exames — sim, repete o que está em Exames, de propósito: aqui
 * é a pasta completa) e o que ele mesmo enviou. O envio entra "Em conferência" até a equipe
 * aceitar; enquanto isso, só ele vê e pode retirar.
 */
export function Documentos() {
  const [itens, setItens] = useState<ItemAcervo[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [anexando, setAnexando] = useState(false);
  const [enviadoAgora, setEnviadoAgora] = useState(false);

  // `silencioso`: recarrega sem trocar a lista por skeleton (após enviar/retirar).
  const carregar = useCallback((silencioso = false) => {
    setErro(null);
    if (!silencioso) setItens(null);
    api
      .documentos()
      .then(setItens)
      .catch((e) => setErro(extrairMensagemDeErro(e)));
  }, []);

  useEffect(() => carregar(), [carregar]);

  const emConferencia = itens?.filter((i) => i.situacao === 'Pendente').length ?? 0;

  return (
    <div className="animate-rise">
      <SectionHeader eyebrow="Cadastro" title="Documentos" />
      <p className="-mt-1 mb-4 text-sm text-tinta-mute">
        Tudo o que está no seu cadastro: exames, laudos, receitas e documentos. Você também pode
        enviar uma foto ou PDF para a equipe da Saúde.
      </p>

      <PrimaryButton
        onClick={() => {
          setEnviadoAgora(false);
          setAnexando(true);
        }}
      >
        <Plus className="h-5 w-5" /> Anexar documento
      </PrimaryButton>

      {enviadoAgora && (
        <div
          role="status"
          className="mt-3 flex items-start gap-3 rounded-2xl bg-lagoa-claro p-3 text-sm text-lagoa-escuro"
        >
          <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
          <p className="flex-1">
            Documento enviado! Ele fica <strong>em conferência</strong> até a equipe da Saúde
            confirmar e, depois disso, passa a valer no seu cadastro.
          </p>
          <button
            type="button"
            onClick={() => setEnviadoAgora(false)}
            aria-label="Fechar aviso"
            className="-m-1 rounded-lg p-1 active:bg-white/60"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      )}

      {emConferencia > 0 && !enviadoAgora && (
        <p className="mt-3 flex items-center gap-2 text-xs text-tinta-mute">
          <Clock className="h-4 w-4 text-amber-600" />
          {emConferencia === 1
            ? '1 documento seu está em conferência pela equipe.'
            : `${emConferencia} documentos seus estão em conferência pela equipe.`}
        </p>
      )}

      <div className="mt-5">
        {erro ? (
          <ErroCard mensagem={erro} aoTentar={() => carregar()} />
        ) : itens === null ? (
          <div className="space-y-3">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-20 w-full" />
            ))}
          </div>
        ) : itens.length === 0 ? (
          <EmptyState
            icon={FolderOpen}
            titulo="Nenhum documento ainda"
            descricao="Exames, laudos e documentos do seu cadastro aparecem aqui. Use “Anexar documento” para enviar uma foto ou PDF."
          />
        ) : (
          <ul className="space-y-3">
            {itens.map((item) => (
              <li key={item.chave}>
                <DocumentoCard item={item} aoRetirar={() => carregar(true)} />
              </li>
            ))}
          </ul>
        )}
      </div>

      {anexando && (
        <AnexarDocumentoSheet
          aoFechar={() => setAnexando(false)}
          aoEnviar={() => {
            setAnexando(false);
            setEnviadoAgora(true);
            carregar(true);
          }}
        />
      )}
    </div>
  );
}

function DocumentoCard({ item, aoRetirar }: { item: ItemAcervo; aoRetirar: () => void }) {
  const [abrindo, setAbrindo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [confirmando, setConfirmando] = useState(false);
  const [retirando, setRetirando] = useState(false);

  const pendente = item.situacao === 'Pendente';
  // Só o envio do próprio paciente, ainda não conferido, pode ser retirado por ele.
  const podeRetirar = item.tipo === 'Documento' && pendente && item.origem === ORIGEM_ENVIADO_PELO_PACIENTE;
  const { icon: Icon, destaque } = iconeDoItem(item);

  async function abrir() {
    setAbrindo(true);
    setErro(null);
    try {
      await abrirDocumento(urlConteudoAcervo(item), nomeArquivo(item), item.mimeType);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setAbrindo(false);
    }
  }

  async function retirar() {
    setRetirando(true);
    setErro(null);
    try {
      await api.retirarDocumento(item.id);
      setConfirmando(false);
      aoRetirar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setRetirando(false);
    }
  }

  return (
    <Card className={cn('overflow-hidden', pendente && 'border-amber-200')}>
      <div className="flex items-start gap-1">
        <button
          type="button"
          onClick={abrir}
          disabled={abrindo}
          className="flex min-w-0 flex-1 items-start gap-3 p-4 text-left transition active:bg-papel disabled:opacity-70"
        >
          <span
            className={cn(
              'grid h-11 w-11 shrink-0 place-items-center rounded-xl',
              destaque ? 'bg-marica/10 text-marica' : 'bg-lagoa-claro text-lagoa',
            )}
          >
            {abrindo ? <Loader2 className="h-5 w-5 animate-spin" /> : <Icon className="h-5 w-5" />}
          </span>
          <div className="min-w-0 flex-1">
            <p className="font-display font-semibold leading-snug text-tinta">{item.titulo}</p>
            {item.descricao && (
              <p className="mt-0.5 line-clamp-2 text-sm text-tinta-mute">{item.descricao}</p>
            )}
            <p className="mt-1 text-xs text-tinta-mute">{descreverMeta(item)}</p>
            {pendente && (
              <span className="mt-2 inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-700">
                <Clock className="h-3.5 w-3.5" /> Em conferência
              </span>
            )}
            {abrindo && (
              <p className="mt-1 text-xs font-medium text-lagoa">
                {item.tipo === 'ImagensExame' ? 'Preparando o documento… pode levar alguns segundos.' : 'Abrindo…'}
              </p>
            )}
          </div>
        </button>
        {podeRetirar && !confirmando && (
          <button
            type="button"
            onClick={() => {
              setConfirmando(true);
              setErro(null);
            }}
            className="m-2 rounded-xl p-2 text-red-700 active:bg-red-50"
            aria-label={`Retirar ${item.titulo}`}
          >
            <Trash2 className="h-5 w-5" />
          </button>
        )}
      </div>

      {confirmando && (
        <div className="mx-3 mb-3 space-y-2 rounded-xl bg-papel p-3">
          <p className="text-sm text-tinta">
            Retirar <strong>{item.titulo}</strong>? A equipe ainda não conferiu este envio; ele deixa de
            ir para o seu cadastro.
          </p>
          <div className="flex gap-2">
            <GhostButton className="flex-1" onClick={() => setConfirmando(false)} disabled={retirando}>
              Não
            </GhostButton>
            <PrimaryButton className="flex-1" carregando={retirando} onClick={retirar}>
              Retirar
            </PrimaryButton>
          </div>
        </div>
      )}

      {erro && (
        <p role="alert" className="px-4 pb-3 text-xs text-marica">
          {erro}
        </p>
      )}
    </Card>
  );
}

/**
 * Folha (bottom sheet) para anexar: foto pela câmera, foto da galeria ou PDF, com nome
 * obrigatório e descrição opcional. Foto grande é reduzida no aparelho antes de subir.
 */
function AnexarDocumentoSheet({ aoFechar, aoEnviar }: { aoFechar: () => void; aoEnviar: () => void }) {
  const cameraRef = useRef<HTMLInputElement | null>(null);
  const arquivoRef = useRef<HTMLInputElement | null>(null);
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [previa, setPrevia] = useState<string | null>(null);
  const [preparando, setPreparando] = useState(false);
  const [titulo, setTitulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [progresso, setProgresso] = useState<number | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const enviando = progresso !== null;

  // Prévia da foto: o object URL é liberado ao trocar de arquivo ou fechar a folha.
  useEffect(() => {
    if (!arquivo || !arquivo.type.startsWith('image/')) {
      setPrevia(null);
      return;
    }
    const url = URL.createObjectURL(arquivo);
    setPrevia(url);
    return () => URL.revokeObjectURL(url);
  }, [arquivo]);

  async function escolher(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = ''; // permite escolher o mesmo arquivo de novo depois
    if (!f) return;
    setErro(null);

    const ehPdf = f.type === 'application/pdf' || /\.pdf$/i.test(f.name);
    const ehImagem = f.type.startsWith('image/') || /\.(heic|heif)$/i.test(f.name);
    if (!ehPdf && !ehImagem) {
      setErro('Escolha uma foto ou um arquivo PDF.');
      return;
    }

    let pronto = f;
    if (ehImagem) {
      setPreparando(true);
      try {
        pronto = await prepararFotoParaEnvio(f);
      } finally {
        setPreparando(false);
      }
    } else if (f.type !== 'application/pdf') {
      // Alguns celulares não informam o tipo do PDF — o servidor só aceita o tipo declarado.
      pronto = new File([f], f.name, { type: 'application/pdf', lastModified: f.lastModified });
    }

    if (pronto.size > TAMANHO_MAXIMO) {
      setArquivo(null);
      setErro(`O arquivo tem ${formatarTamanho(pronto.size)}. O limite é 25 MB.`);
      return;
    }
    setArquivo(pronto);
  }

  async function enviar() {
    if (!arquivo) {
      setErro('Escolha a foto ou o PDF do documento.');
      return;
    }
    const t = titulo.trim();
    if (!t) {
      setErro('Informe o nome do documento.');
      return;
    }
    setErro(null);
    setProgresso(0);
    try {
      await api.enviarDocumento(arquivo, t, descricao.trim() || null, setProgresso);
      aoEnviar();
    } catch (e) {
      setErro(mensagemErroEnvio(e));
    } finally {
      setProgresso(null);
    }
  }

  return (
    <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] items-end" role="dialog" aria-modal="true">
      <button
        type="button"
        aria-label="Fechar"
        onClick={() => !enviando && aoFechar()}
        className="absolute inset-0 animate-fade-in bg-tinta/40"
      />
      <div className="relative max-h-[92dvh] w-full animate-rise overflow-y-auto rounded-t-3xl bg-papel p-6 pb-[calc(env(safe-area-inset-bottom)+1.5rem)] shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <SectionHeader eyebrow="Documentos" title="Anexar documento" />
          <button
            type="button"
            onClick={aoFechar}
            disabled={enviando}
            aria-label="Fechar"
            className="-mr-2 -mt-1 grid h-10 w-10 place-items-center rounded-xl text-tinta-mute active:bg-areia/60 disabled:opacity-40"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="text-sm text-tinta-mute">
          Envie uma foto ou PDF (receita, exame em papel, encaminhamento…). A equipe da Saúde confere
          antes de o documento valer no seu cadastro.
        </p>

        {/* Câmera direto (capture) e galeria/arquivos — o 2º aceita foto OU PDF. */}
        <input ref={cameraRef} type="file" accept="image/*" capture="environment" className="hidden" onChange={escolher} />
        <input ref={arquivoRef} type="file" accept="application/pdf,image/*" className="hidden" onChange={escolher} />

        <div className="mt-4">
          {preparando ? (
            <div className="flex min-h-[96px] items-center justify-center gap-2 rounded-2xl border border-dashed border-areia bg-white text-sm text-tinta-mute">
              <Loader2 className="h-5 w-5 animate-spin text-marica" /> Preparando a foto…
            </div>
          ) : arquivo ? (
            <div className="flex items-center gap-3 rounded-2xl border border-areia bg-white p-3">
              {previa ? (
                <img src={previa} alt="Prévia do documento" className="h-16 w-16 shrink-0 rounded-xl object-cover" />
              ) : (
                <span className="grid h-16 w-16 shrink-0 place-items-center rounded-xl bg-lagoa-claro text-lagoa">
                  <FileText className="h-7 w-7" />
                </span>
              )}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold text-tinta">{arquivo.name}</p>
                <p className="text-xs text-tinta-mute">{formatarTamanho(arquivo.size)}</p>
              </div>
              <button
                type="button"
                onClick={() => setArquivo(null)}
                disabled={enviando}
                className="shrink-0 rounded-xl px-3 py-2 text-sm font-semibold text-marica active:bg-marica/10 disabled:opacity-40"
              >
                Trocar
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() => cameraRef.current?.click()}
                className="flex flex-col items-center gap-2 rounded-2xl border border-areia bg-white p-4 text-sm font-semibold text-tinta shadow-carta transition active:scale-[.98]"
              >
                <span className="grid h-12 w-12 place-items-center rounded-2xl bg-marica/10 text-marica">
                  <Camera className="h-6 w-6" />
                </span>
                Tirar foto
              </button>
              <button
                type="button"
                onClick={() => arquivoRef.current?.click()}
                className="flex flex-col items-center gap-2 rounded-2xl border border-areia bg-white p-4 text-sm font-semibold text-tinta shadow-carta transition active:scale-[.98]"
              >
                <span className="grid h-12 w-12 place-items-center rounded-2xl bg-lagoa-claro text-lagoa">
                  <Paperclip className="h-6 w-6" />
                </span>
                Foto ou PDF
              </button>
            </div>
          )}
        </div>

        <div className="mt-4 space-y-4">
          <Field
            label="Nome do documento"
            placeholder="Ex.: Receita do cardiologista"
            value={titulo}
            maxLength={TITULO_MAX}
            onChange={(e) => setTitulo(e.target.value)}
            disabled={enviando}
          />
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-tinta">
              Descrição <span className="font-normal text-tinta-mute">(opcional)</span>
            </span>
            <textarea
              rows={3}
              maxLength={DESCRICAO_MAX}
              value={descricao}
              onChange={(e) => setDescricao(e.target.value)}
              disabled={enviando}
              placeholder="Algo que a equipe precise saber sobre este documento"
              className={cn(
                'w-full rounded-2xl border border-areia bg-white px-4 py-3 text-base text-tinta',
                'placeholder:text-tinta-mute/60 focus:border-lagoa focus:outline-none',
                'disabled:bg-papel disabled:text-tinta-mute',
              )}
            />
          </label>

          {erro && (
            <p role="alert" className="text-sm text-marica">
              {erro}
            </p>
          )}

          {enviando && (
            <div>
              <div className="h-2 overflow-hidden rounded-full bg-areia">
                <div
                  className="h-full rounded-full bg-marica transition-[width] duration-200"
                  style={{ width: `${Math.round((progresso ?? 0) * 100)}%` }}
                />
              </div>
              <p className="mt-1 text-center text-xs text-tinta-mute">
                {(progresso ?? 0) < 1 ? `Enviando… ${Math.round((progresso ?? 0) * 100)}%` : 'Finalizando…'}
              </p>
            </div>
          )}

          <PrimaryButton onClick={enviar} carregando={enviando} disabled={preparando || !arquivo || !titulo.trim()}>
            Enviar documento
          </PrimaryButton>
        </div>
      </div>
    </div>
  );
}

/** Texto do erro do envio: o servidor já fala a língua do cidadão (ex.: limite de pendentes). */
function mensagemErroEnvio(e: unknown): string {
  const { status } = classificarErro(e);
  // 413 vem do proxy/servidor web, antes da API — sem ProblemDetails.
  if (status === 413) return 'O arquivo passou do limite de 25 MB.';
  if (e instanceof AxiosError && !e.response) {
    return 'Não foi possível enviar agora. Confira a sua internet e tente de novo.';
  }
  return extrairMensagemDeErro(e);
}

function iconeDoItem(item: ItemAcervo): { icon: typeof FileText; destaque: boolean } {
  switch (item.tipo) {
    case 'Laudo':
      return { icon: ShieldCheck, destaque: true };
    case 'ImagensExame':
      return { icon: ScanLine, destaque: true };
    case 'AnexoExame':
      return { icon: Paperclip, destaque: false };
    default:
      return { icon: item.mimeType.startsWith('image/') ? ImageIcon : FileText, destaque: false };
  }
}

function descreverMeta(item: ItemAcervo): string {
  const partes = [item.origem, formatarData(item.data)];
  if (item.paginas) partes.push(`${item.paginas} pág.`);
  if (item.tamanhoBytes) partes.push(formatarTamanho(item.tamanhoBytes));
  return partes.filter(Boolean).join(' · ');
}

/** Nome para Baixar/Compartilhar quando o servidor não mandar um (ex.: abrindo do cache). */
function nomeArquivo(item: ItemAcervo): string {
  const ext =
    item.mimeType === 'application/pdf' ? 'pdf' : item.mimeType.split('/')[1]?.replace('jpeg', 'jpg') || 'pdf';
  const base = item.titulo.replace(/[\\/:*?"<>|]+/g, ' ').trim() || 'documento';
  return `${base}.${ext}`;
}

function formatarTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatarData(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
