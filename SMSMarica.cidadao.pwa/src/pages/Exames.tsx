import { useState } from 'react';
import {
  ChevronDown,
  FileText,
  FlaskConical,
  Image as ImageIcon,
  Loader2,
  ShieldCheck,
} from 'lucide-react';
import { api, pdfUrls, type AnexoResumo, type Exame } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { abrirPdf } from '@/lib/pdf';
import { cn } from '@/lib/cn';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function Exames() {
  return (
    <Lista
      eyebrow="Resultados"
      titulo="Exames"
      carregar={api.exames}
      emptyIcon={FlaskConical}
      emptyTitulo="Nenhum exame disponível"
      emptyDescricao="Resultados, documentos e imagens dos seus exames aparecem aqui assim que ficam prontos."
      renderItem={(e) => <ExameCard exame={e} />}
    />
  );
}

function ExameCard({ exame }: { exame: Exame }) {
  const [aberto, setAberto] = useState(false);
  const [gerando, setGerando] = useState(false);
  const [abrindoLaudo, setAbrindoLaudo] = useState(false);
  const [abrindoDoc, setAbrindoDoc] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const temDocs = exame.documentos.length > 0;
  const expansivel = temDocs || exame.temImagens || exame.laudoAssinado;

  async function verImagens() {
    setGerando(true);
    setErro(null);
    try {
      await abrirPdf(pdfUrls.exameImagens(exame.id), `exame-imagens-${exame.id}.pdf`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setGerando(false);
    }
  }

  // Laudo pertence AO EXAME: abre o PDF assinado aqui mesmo, dentro do card.
  async function verLaudo() {
    if (!exame.laudoId) return;
    setAbrindoLaudo(true);
    setErro(null);
    try {
      await abrirPdf(pdfUrls.laudo(exame.laudoId), `laudo-${exame.laudoId}.pdf`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setAbrindoLaudo(false);
    }
  }

  async function verDoc(doc: AnexoResumo) {
    setAbrindoDoc(doc.id);
    setErro(null);
    try {
      await abrirPdf(pdfUrls.anexo(doc.id), `${doc.nome}.pdf`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setAbrindoDoc(null);
    }
  }

  return (
    <Card className="overflow-hidden">
      <button
        type="button"
        onClick={() => expansivel && setAberto((v) => !v)}
        className={cn('flex w-full items-center justify-between gap-3 p-4 text-left', expansivel && 'active:bg-papel')}
      >
        <div className="min-w-0">
          <p className="truncate font-display font-semibold text-tinta">{exame.nome}</p>
          <p className="text-xs text-tinta-mute">{formatarData(exame.data)}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Etiqueta status={exame.status} />
          {expansivel && (
            <ChevronDown className={cn('h-5 w-5 text-tinta-mute transition', aberto && 'rotate-180')} />
          )}
        </div>
      </button>

      {aberto && (
        <div className="space-y-2 border-t border-areia bg-papel/40 p-3">
          {exame.temImagens && (
            <LinhaAcao
              icon={gerando ? Loader2 : ImageIcon}
              girando={gerando}
              titulo="Imagens do exame"
              descricao={gerando ? 'Preparando o documento…' : 'Gerar PDF com as imagens'}
              onClick={verImagens}
              destaque
            />
          )}

          {exame.laudoAssinado && exame.laudoId && (
            <LinhaAcao
              icon={abrindoLaudo ? Loader2 : ShieldCheck}
              girando={abrindoLaudo}
              titulo="Laudo assinado"
              descricao={abrindoLaudo ? 'Abrindo o laudo…' : 'Abrir o laudo (PDF)'}
              onClick={verLaudo}
              destaque
            />
          )}

          {exame.documentos.map((doc) => (
            <LinhaAcao
              key={doc.id}
              icon={abrindoDoc === doc.id ? Loader2 : FileText}
              girando={abrindoDoc === doc.id}
              titulo={doc.nome}
              descricao={descreverDoc(doc)}
              onClick={() => verDoc(doc)}
            />
          ))}

          {erro && <p className="px-1 text-xs text-marica">{erro}</p>}
        </div>
      )}
    </Card>
  );
}

function LinhaAcao({
  icon: Icon,
  girando,
  titulo,
  descricao,
  onClick,
  destaque,
}: {
  icon: typeof FileText;
  girando?: boolean;
  titulo: string;
  descricao: string;
  onClick: () => void;
  destaque?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={girando}
      className="flex w-full items-center gap-3 rounded-xl bg-white p-3 text-left shadow-carta transition active:scale-[.99] disabled:opacity-60"
    >
      <span
        className={cn(
          'grid h-9 w-9 shrink-0 place-items-center rounded-lg',
          destaque ? 'bg-marica/10 text-marica' : 'bg-lagoa-claro text-lagoa',
        )}
      >
        <Icon className={cn('h-5 w-5', girando && 'animate-spin')} />
      </span>
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-tinta">{titulo}</p>
        <p className="truncate text-xs text-tinta-mute">{descricao}</p>
      </div>
    </button>
  );
}

function descreverDoc(doc: AnexoResumo): string {
  const partes = [formatarTamanho(doc.tamanhoBytes)];
  if (doc.paginas) partes.push(`${doc.paginas} pág.`);
  return partes.join(' · ');
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
