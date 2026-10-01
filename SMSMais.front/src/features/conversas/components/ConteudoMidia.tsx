import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Ban, Check, Eye, FilePlus2, FileText, Image as ImageIcon, Loader2, RotateCw, Trash2 } from 'lucide-react';

import { caminhoMidiaMensagem } from '@/features/conversas/api/conversasApi';
import {
  useAceitarMidia,
  useBaixarMidiaDeNovo,
  useDescartarMidia,
  usePacientesDoTelefone,
} from '@/features/conversas/api/queries';
import type { Mensagem } from '@/features/conversas/types';
import { baixarArquivo } from '@/shared/acervo/api';
import { DialogoDocumento, tituloDoArquivo } from '@/shared/acervo/DialogoDocumento';
import { AcaoVisualizador, VisualizadorArquivo } from '@/shared/acervo/VisualizadorArquivo';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTemConsulta } from '@/shared/auth/authStore';
import { formatarNomeProprio } from '@/shared/lib/nomes';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';

/**
 * Miniatura só para foto pequena: o conteúdo exige o token (vem como blob), então cada miniatura
 * é um download. Até 2 MB a thread continua leve; acima disso fica só o ícone + "Visualizar".
 */
const LIMITE_MINIATURA_BYTES = 2 * 1024 * 1024;

type PacientePadrao = { id: string; nome: string | null };

type Props = {
  m: Mensagem;
  /** Aceitar/descartar/tentar de novo — quem tem Conversas · Edição, e só na Central (não na ficha). */
  podeDecidir?: boolean;
  /** Paciente da conversa: o destino sugerido ao adicionar o arquivo ao cadastro. */
  pacientePadrao?: PacientePadrao | null;
};

function tamanhoFmt(bytes: number | null | undefined): string | null {
  if (bytes == null) return null;
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`;
}

function ehImagem(m: Mensagem): boolean {
  return m.tipoMensagem === 'Imagem' || Boolean(m.midiaMimeType?.startsWith('image/'));
}

/**
 * O que a bolha mostra quando a mensagem é foto/PDF (ou áudio/vídeo, que só ficam registrados).
 * Devolve null para mensagem sem mídia — a bolha segue só com o texto.
 *
 * <p>A situação da mídia fica SEMPRE visível na bolha: antes, foto e PDF chegavam como bolha vazia
 * (só a hora) e ninguém sabia que o paciente tinha mandado um exame.</p>
 */
export function ConteudoMidia({ m, podeDecidir = false, pacientePadrao = null }: Props) {
  const podeVer = useTemConsulta('Conversas');
  const [visualizando, setVisualizando] = useState(false);
  const [erroRetentar, setErroRetentar] = useState<string | null>(null);
  const retentar = useBaixarMidiaDeNovo();

  const situacao = m.midiaSituacao ?? null;
  const imagem = ehImagem(m);

  if (!situacao) {
    // Áudio/vídeo não são guardados (só foto e PDF entram no fluxo); foto/PDF sem situação são
    // mensagens de antes do armazenamento existir. Em todos os casos a bolha não pode ficar vazia.
    const rotulo =
      m.tipoMensagem === 'Audio'
        ? '🎤 Áudio (não suportado aqui)'
        : m.tipoMensagem === 'Video'
          ? '🎬 Vídeo (não suportado aqui)'
          : m.tipoMensagem === 'Imagem'
            ? '📷 Imagem (arquivo não guardado)'
            : m.tipoMensagem === 'Documento'
              ? `📎 ${m.midiaNomeArquivo ?? 'Documento'} (arquivo não guardado)`
              : null;
    return rotulo ? <p className="italic text-gray-500">{rotulo}</p> : null;
  }

  const nome = m.midiaNomeArquivo ?? (imagem ? 'Foto' : 'Documento');
  const tamanho = tamanhoFmt(m.midiaTamanho);

  async function tentarDeNovo() {
    setErroRetentar(null);
    try {
      await retentar.mutateAsync({ mensagemId: m.id, conversaId: m.conversaId });
    } catch (e) {
      // 409: ainda há 10 pendentes no número, ou a Meta já apagou (mais de 30 dias).
      setErroRetentar(extrairMensagemDeErro(e));
    }
  }

  if (situacao === 'Recebendo') {
    return (
      <p className="flex items-center gap-1.5 text-gray-500">
        <Loader2 className="h-3.5 w-3.5 animate-spin" /> Recebendo arquivo…
      </p>
    );
  }

  if (situacao === 'Descartada') {
    return (
      <p className="flex items-center gap-1.5 italic text-gray-400">
        <Ban className="h-3.5 w-3.5" /> Arquivo descartado
      </p>
    );
  }

  if (situacao === 'Bloqueada' || situacao === 'Falhou') {
    return (
      <div className="space-y-1">
        <p className={situacao === 'Bloqueada' ? 'text-amber-700' : 'text-gray-600'}>
          {situacao === 'Bloqueada'
            ? 'Não guardado: este número já tem 10 arquivos aguardando decisão'
            : 'Não foi possível baixar o arquivo'}
        </p>
        {podeDecidir && (
          <button
            type="button"
            onClick={() => void tentarDeNovo()}
            disabled={retentar.isPending}
            className="flex items-center gap-1 text-xs font-medium text-primary-600 hover:text-primary-700 disabled:opacity-50"
            title={
              situacao === 'Bloqueada'
                ? 'Decida os arquivos pendentes deste número (adicionar ou descartar) e tente de novo'
                : 'Baixa o arquivo de novo do WhatsApp (ele fica disponível por cerca de 30 dias)'
            }
          >
            <RotateCw className={`h-3.5 w-3.5 ${retentar.isPending ? 'animate-spin' : ''}`} /> Tentar de novo
          </button>
        )}
        {erroRetentar && <p className="text-xs text-error-600">{erroRetentar}</p>}
      </div>
    );
  }

  // Pendente ou Aceita: o arquivo está guardado e pode ser aberto.
  const Icone = imagem ? ImageIcon : FileText;
  const mostrarMiniatura =
    podeVer && imagem && m.midiaTamanho != null && m.midiaTamanho <= LIMITE_MINIATURA_BYTES;

  return (
    <div className="space-y-1.5">
      {mostrarMiniatura && <Miniatura mensagemId={m.id} aoClicar={() => setVisualizando(true)} />}
      <div className="flex items-center gap-2">
        <Icone className="h-4 w-4 shrink-0 text-gray-400" />
        <span className="min-w-0 truncate text-xs text-gray-600" title={nome}>
          {nome}
          {tamanho ? <span className="text-gray-400"> · {tamanho}</span> : null}
        </span>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {podeVer && (
          <button
            type="button"
            onClick={() => setVisualizando(true)}
            className="flex items-center gap-1 rounded-md bg-gray-100 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-gray-200"
          >
            <Eye className="h-3.5 w-3.5" /> Visualizar arquivo
          </button>
        )}
        {situacao === 'Pendente' ? (
          <span className="rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-medium text-amber-700 ring-1 ring-amber-200">
            Aguardando decisão
          </span>
        ) : (
          <span className="inline-flex items-center gap-0.5 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700 ring-1 ring-emerald-200">
            <Check className="h-3 w-3" /> No cadastro do paciente
          </span>
        )}
      </div>

      {visualizando && (
        <VisualizadorMidia
          m={m}
          nome={nome}
          podeDecidir={podeDecidir && situacao === 'Pendente'}
          pacientePadrao={pacientePadrao}
          aoFechar={() => setVisualizando(false)}
        />
      )}
    </div>
  );
}

/** Miniatura da foto (blob com o token). Falhou? Some — o "Visualizar arquivo" continua ali. */
function Miniatura({ mensagemId, aoClicar }: { mensagemId: string; aoClicar: () => void }) {
  const q = useQuery({
    queryKey: ['conversas', 'midia-miniatura', mensagemId],
    queryFn: () => baixarArquivo(caminhoMidiaMensagem(mensagemId)),
    // O arquivo de uma mensagem não muda; o refetch da thread (SignalR/poll) não pode rebaixar.
    staleTime: Infinity,
    gcTime: 10 * 60_000,
    retry: false,
  });
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!q.data) return;
    const u = URL.createObjectURL(q.data.blob);
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [q.data]);

  if (q.isLoading) return <div className="h-28 w-40 animate-pulse rounded-lg bg-gray-100" />;
  if (!url) return null;
  return (
    <button type="button" onClick={aoClicar} className="block overflow-hidden rounded-lg ring-1 ring-gray-200">
      <img src={url} alt="Foto enviada pelo paciente" className="max-h-48 max-w-[16rem] object-cover" />
    </button>
  );
}

function VisualizadorMidia({
  m,
  nome,
  podeDecidir,
  pacientePadrao,
  aoFechar,
}: {
  m: Mensagem;
  nome: string;
  podeDecidir: boolean;
  pacientePadrao: PacientePadrao | null;
  aoFechar: () => void;
}) {
  const [aceitando, setAceitando] = useState(false);
  const [descartando, setDescartando] = useState(false);
  const [erroDescarte, setErroDescarte] = useState<string | null>(null);
  const descartar = useDescartarMidia();

  async function confirmarDescarte() {
    setErroDescarte(null);
    try {
      await descartar.mutateAsync({ mensagemId: m.id, conversaId: m.conversaId });
      setDescartando(false);
      aoFechar();
    } catch (e) {
      // Fica no próprio modal: é onde a pessoa está olhando (o 4xx não vira toast).
      setErroDescarte(extrairMensagemDeErro(e));
    }
  }

  return (
    <>
      <VisualizadorArquivo
        caminho={caminhoMidiaMensagem(m.id)}
        titulo={m.midiaNomeArquivo ?? (ehImagem(m) ? 'Foto enviada pelo WhatsApp' : nome)}
        descricao={m.midiaLegenda}
        nomeArquivo={m.midiaNomeArquivo}
        aoFechar={aoFechar}
        acoes={
          podeDecidir ? (
            <>
              <AcaoVisualizador perigo aoClicar={() => setDescartando(true)}>
                <Trash2 className="h-4 w-4" /> Descartar
              </AcaoVisualizador>
              <AcaoVisualizador destaque aoClicar={() => setAceitando(true)}>
                <FilePlus2 className="h-4 w-4" /> Adicionar ao cadastro do paciente
              </AcaoVisualizador>
            </>
          ) : undefined
        }
      />

      {aceitando && (
        <DialogoAceitar
          m={m}
          pacientePadrao={pacientePadrao}
          aoFechar={() => setAceitando(false)}
          aoAceitar={() => {
            setAceitando(false);
            aoFechar();
          }}
        />
      )}

      {/* O ConfirmDialog compartilhado abre em z-50, ATRÁS do visualizador (z-60): aqui o Modal
          vai com `porCima`. */}
      <Modal
        aberto={descartando}
        aoFechar={() => !descartar.isPending && setDescartando(false)}
        titulo="Descartar arquivo?"
        largura="sm"
        porCima
      >
        <div className="space-y-4">
          <p className="text-sm text-gray-700">
            O arquivo será apagado e não poderá ser recuperado. A mensagem continua na conversa,
            marcada como &quot;Arquivo descartado&quot;.
          </p>
          {erroDescarte ? (
            <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroDescarte}
            </div>
          ) : null}
          <div className="flex justify-end gap-3">
            <Button variante="ghost" onClick={() => setDescartando(false)} disabled={descartar.isPending}>
              Cancelar
            </Button>
            <Button variante="danger" onClick={() => void confirmarDescarte()} disabled={descartar.isPending}>
              {descartar.isPending ? 'Descartando…' : 'Descartar'}
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}

/**
 * Nome + descrição + (celular de família) de QUAL paciente é o arquivo. O servidor recusa
 * paciente sem ligação com a conversa; aqui só oferecemos os que têm.
 */
function DialogoAceitar({
  m,
  pacientePadrao,
  aoFechar,
  aoAceitar,
}: {
  m: Mensagem;
  pacientePadrao: PacientePadrao | null;
  aoFechar: () => void;
  aoAceitar: () => void;
}) {
  const doTelefone = usePacientesDoTelefone(m.conversaId);
  const aceitar = useAceitarMidia();
  const [erro, setErro] = useState<string | null>(null);

  // Candidatos: os cadastros com o telefone da conversa + o paciente vinculado à conversa
  // (que pode ter entrado por outro caminho e não ter este número no cadastro).
  const opcoes = useMemo(() => {
    const lista = (doTelefone.data ?? []).map((p) => ({ id: p.pacienteId, nome: p.nome, titular: p.titular }));
    if (pacientePadrao && !lista.some((p) => p.id === pacientePadrao.id)) {
      lista.unshift({ id: pacientePadrao.id, nome: pacientePadrao.nome ?? 'Paciente da conversa', titular: true });
    }
    return lista;
  }, [doTelefone.data, pacientePadrao]);

  const [escolhido, setEscolhido] = useState<string>('');
  const padrao = pacientePadrao?.id ?? opcoes.find((p) => p.titular)?.id ?? opcoes[0]?.id ?? '';
  const pacienteId = escolhido || padrao;

  const tituloInicial = useMemo(
    () => (m.midiaNomeArquivo ? tituloDoArquivo(m.midiaNomeArquivo) : ''),
    [m.midiaNomeArquivo],
  );

  async function confirmar(titulo: string, descricao: string | null) {
    setErro(null);
    if (!pacienteId) {
      setErro('Esta conversa não está ligada a nenhum paciente. Vincule o paciente antes de guardar o arquivo.');
      throw new Error('sem paciente');
    }
    try {
      await aceitar.mutateAsync({ mensagemId: m.id, conversaId: m.conversaId, pacienteId, titulo, descricao });
    } catch (e) {
      // O DialogoDocumento engole o erro contando com o toast — mas 4xx não vira toast; mostra aqui.
      setErro(extrairMensagemDeErro(e));
      throw e;
    }
    const nomePaciente = opcoes.find((p) => p.id === pacienteId)?.nome;
    notificar(
      nomePaciente
        ? `Arquivo adicionado ao cadastro de ${formatarNomeProprio(nomePaciente)}.`
        : 'Arquivo adicionado ao cadastro do paciente.',
    );
    aoAceitar();
  }

  return (
    <DialogoDocumento
      aberto
      tituloModal="Adicionar ao cadastro do paciente"
      descricaoModal="Dê um nome que diga o que é o arquivo — é assim que ele será achado depois na ficha."
      tituloInicial={tituloInicial}
      descricaoInicial={m.midiaLegenda ?? null}
      rotuloConfirmar="Adicionar"
      aoConfirmar={confirmar}
      aoFechar={aoFechar}
    >
      {opcoes.length > 1 ? (
        <div>
          <label htmlFor="midia-paciente" className="mb-1 block text-sm font-medium text-slate-700">
            Paciente <span className="text-red-600">*</span>
          </label>
          <Select id="midia-paciente" value={pacienteId} onChange={(e) => setEscolhido(e.target.value)}>
            {opcoes.map((p) => (
              <option key={p.id} value={p.id}>
                {formatarNomeProprio(p.nome)}
                {p.id === pacientePadrao?.id ? ' (paciente da conversa)' : ''}
              </option>
            ))}
          </Select>
          <p className="mt-1 text-xs text-gray-500">
            Este telefone está em mais de um cadastro. Confira de quem é o arquivo.
          </p>
        </div>
      ) : opcoes.length === 1 ? (
        <p className="text-sm text-gray-600">
          Paciente: <span className="font-medium text-gray-900">{formatarNomeProprio(opcoes[0].nome)}</span>
        </p>
      ) : doTelefone.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Buscando o paciente…
        </p>
      ) : (
        <p className="text-sm text-amber-700">
          Esta conversa não está ligada a nenhum paciente. Vincule o paciente antes de guardar o arquivo.
        </p>
      )}
      {erro ? (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}
    </DialogoDocumento>
  );
}
