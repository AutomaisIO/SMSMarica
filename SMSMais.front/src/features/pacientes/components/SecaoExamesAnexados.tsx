import { useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Eye, FolderOpen, Loader2, Paperclip, Pencil, Trash2, Upload } from 'lucide-react';

import {
  aceitarDocumentoPaciente,
  caminhoConteudoPaciente,
  editarDocumentoPaciente,
  enviarDocumentoPaciente,
  excluirDocumentoPaciente,
  listarAcervoPaciente,
} from '@/shared/acervo/api';
import { DialogoDocumento, tituloDoArquivo } from '@/shared/acervo/DialogoDocumento';
import { IconeItemAcervo, formatarDataAcervo, formatarTamanhoAcervo } from '@/shared/acervo/ListaAcervo';
import {
  LIMITE_MB_ACERVO,
  ROTULO_TIPO_ACERVO,
  TIPOS_ACEITOS_ACERVO,
  type ItemAcervo,
  type TipoItemAcervo,
} from '@/shared/acervo/tipos';
import { AcaoVisualizador, VisualizadorArquivo } from '@/shared/acervo/VisualizadorArquivo';
import { usePermissao } from '@/shared/auth/authStore';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { notificar } from '@/shared/ui/Notificacoes';

type Filtro = 'todos' | TipoItemAcervo;

const FILTROS: { id: Filtro; rotulo: string }[] = [
  { id: 'todos', rotulo: 'Todos' },
  { id: 'Documento', rotulo: 'Documentos' },
  { id: 'Laudo', rotulo: 'Laudos' },
  { id: 'ImagensExame', rotulo: 'Imagens de exame' },
  { id: 'AnexoExame', rotulo: 'Anamnese' },
];

/**
 * "Exames anexados" do cadastro: o acervo do paciente — o que fica PERENE, venha de onde vier
 * (anexo de solicitação, WhatsApp, app do paciente, anamnese), mais os laudos ASSINADOS e o PDF
 * das imagens dos exames. É daqui que a solicitação escolhe o que anexar.
 *
 * <p>O que o paciente mandou pelo app chega "Aguardando conferência": só depois de alguém da
 * equipe aceitar ele fica disponível para anexar numa solicitação.</p>
 */
export function SecaoExamesAnexados({ pacienteId }: { pacienteId: string }) {
  const qc = useQueryClient();
  const chave = ['pacientes', pacienteId, 'acervo'] as const;
  const q = useQuery({ queryKey: chave, queryFn: () => listarAcervoPaciente(pacienteId) });

  const podeEditar = usePermissao('Pacientes', 'Edicao');
  const podeExcluir = usePermissao('Pacientes', 'Exclusao');

  const [filtro, setFiltro] = useState<Filtro>('todos');
  const [vendo, setVendo] = useState<ItemAcervo | null>(null);
  const [arquivoNovo, setArquivoNovo] = useState<File | null>(null);
  const [editando, setEditando] = useState<{ item: ItemAcervo; aceitar: boolean } | null>(null);
  const [excluindo, setExcluindo] = useState<ItemAcervo | null>(null);
  const [excluindoCarregando, setExcluindoCarregando] = useState(false);
  const [erroExclusao, setErroExclusao] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const invalidar = () => qc.invalidateQueries({ queryKey: chave });

  const todos = q.data ?? [];
  const pendentes = todos.filter((i) => i.situacao === 'Pendente');
  const lista = useMemo(
    () => (q.data ?? []).filter((i) => i.situacao !== 'Pendente' && (filtro === 'todos' || i.tipo === filtro)),
    [q.data, filtro],
  );

  function escolherArquivo(files: FileList | null) {
    const f = files?.[0];
    if (inputRef.current) inputRef.current.value = '';
    if (!f) return;
    if (f.size > LIMITE_MB_ACERVO * 1024 * 1024) {
      notificar(`"${f.name}" passa do limite de ${LIMITE_MB_ACERVO} MB.`, 'erro');
      return;
    }
    if (f.type && !TIPOS_ACEITOS_ACERVO.includes(f.type)) {
      notificar('Só são aceitos PDF e imagens (JPG, PNG, WEBP, GIF).', 'erro');
      return;
    }
    setArquivoNovo(f);
  }

  async function confirmarExclusao() {
    if (!excluindo) return;
    setExcluindoCarregando(true);
    setErroExclusao(null);
    try {
      await excluirDocumentoPaciente(pacienteId, excluindo.id);
      notificar('Documento excluído do cadastro.', 'sucesso');
      setExcluindo(null);
      await invalidar();
    } catch (e) {
      setErroExclusao(extrairMensagemDeErro(e));
    } finally {
      setExcluindoCarregando(false);
    }
  }

  function linha(i: ItemAcervo) {
    return (
      <li key={i.chave} className="flex items-center gap-3 px-3 py-2.5">
        <IconeItemAcervo item={i} />
        <button type="button" onClick={() => setVendo(i)} className="min-w-0 flex-1 text-left" title="Visualizar">
          <p className="truncate text-sm font-medium text-gray-900 hover:text-primary-700">{i.titulo}</p>
          {i.descricao ? <p className="truncate text-xs text-gray-500">{i.descricao}</p> : null}
          <p className="mt-0.5 text-[11px] text-gray-400">
            {ROTULO_TIPO_ACERVO[i.tipo]} · {i.origem} · {formatarDataAcervo(i.data)}
            {i.tamanhoBytes ? ` · ${formatarTamanhoAcervo(i.tamanhoBytes)}` : ''}
            {i.paginas ? ` · ${i.paginas} pág.` : ''}
          </p>
        </button>
        {i.situacao === 'Pendente' && podeEditar ? (
          <button
            type="button"
            onClick={() => setEditando({ item: i, aceitar: true })}
            className="inline-flex shrink-0 items-center gap-1 rounded-md bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-emerald-500"
          >
            <Check className="size-3.5" /> Aceitar
          </button>
        ) : null}
        <button
          type="button"
          onClick={() => setVendo(i)}
          className="shrink-0 rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-700"
          aria-label={`Visualizar ${i.titulo}`}
        >
          <Eye className="size-4" />
        </button>
        {i.editavel && i.situacao !== 'Pendente' && podeEditar ? (
          <button
            type="button"
            onClick={() => setEditando({ item: i, aceitar: false })}
            className="shrink-0 rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-700"
            aria-label={`Editar ${i.titulo}`}
          >
            <Pencil className="size-4" />
          </button>
        ) : null}
        {i.editavel && podeExcluir ? (
          <button
            type="button"
            onClick={() => setExcluindo(i)}
            className="shrink-0 rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600"
            aria-label={`Excluir ${i.titulo}`}
          >
            <Trash2 className="size-4" />
          </button>
        ) : null}
      </li>
    );
  }

  return (
    <>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <Paperclip className="h-4 w-4" /> Documentos / Exames anexados
        </div>
        {podeEditar ? (
          <>
            <input
              ref={inputRef}
              type="file"
              className="hidden"
              accept={TIPOS_ACEITOS_ACERVO.join(',')}
              onChange={(e) => escolherArquivo(e.target.files)}
            />
            <button
              type="button"
              onClick={() => inputRef.current?.click()}
              className="inline-flex items-center gap-1.5 rounded-md border border-dashed border-gray-300 px-3 py-1.5 text-sm text-gray-600 hover:border-primary-300 hover:text-primary-700"
            >
              <Upload className="size-4" /> Anexar documento
            </button>
          </>
        ) : null}
      </div>

      {pendentes.length > 0 ? (
        <div className="mb-4 rounded-md border border-amber-200 bg-amber-50/60">
          <p className="border-b border-amber-200 px-3 py-2 text-xs font-medium text-amber-900">
            Aguardando conferência ({pendentes.length}) — enviados pelo paciente. Só depois de aceitos ficam disponíveis
            para anexar em solicitações.
          </p>
          <ul className="divide-y divide-amber-100">{pendentes.map(linha)}</ul>
        </div>
      ) : null}

      <div className="mb-3 flex flex-wrap gap-1.5">
        {FILTROS.map((f) => (
          <button
            key={f.id}
            type="button"
            onClick={() => setFiltro(f.id)}
            className={cn(
              'rounded-full border px-2.5 py-0.5 text-xs',
              filtro === f.id
                ? 'border-primary-300 bg-primary-50 text-primary-800'
                : 'border-gray-200 text-gray-600 hover:bg-gray-50',
            )}
          >
            {f.rotulo}
          </button>
        ))}
      </div>

      {q.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="size-4 animate-spin" /> Carregando…
        </p>
      ) : lista.length === 0 ? (
        <div className="rounded-md border border-dashed border-gray-200 p-6 text-center text-sm text-gray-500">
          <FolderOpen className="mx-auto mb-2 size-6 text-gray-300" />
          {filtro === 'todos' ? 'Nenhum documento no cadastro deste paciente.' : 'Nada deste tipo no cadastro.'}
        </div>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">{lista.map(linha)}</ul>
      )}

      {vendo ? (
        <VisualizadorArquivo
          caminho={caminhoConteudoPaciente(pacienteId, vendo)}
          titulo={vendo.titulo}
          descricao={vendo.descricao}
          aoFechar={() => setVendo(null)}
          acoes={
            vendo.situacao === 'Pendente' && podeEditar ? (
              <AcaoVisualizador destaque aoClicar={() => setEditando({ item: vendo, aceitar: true })}>
                <Check className="size-4" /> Aceitar no cadastro
              </AcaoVisualizador>
            ) : undefined
          }
        />
      ) : null}

      <DialogoDocumento
        aberto={arquivoNovo !== null}
        tituloModal="Anexar documento ao cadastro"
        descricaoModal={arquivoNovo?.name}
        tituloInicial={arquivoNovo ? tituloDoArquivo(arquivoNovo.name) : ''}
        rotuloConfirmar="Anexar"
        aoFechar={() => setArquivoNovo(null)}
        aoConfirmar={async (titulo, descricao) => {
          if (!arquivoNovo) return;
          await enviarDocumentoPaciente(pacienteId, arquivoNovo, titulo, descricao);
          notificar('Documento anexado ao cadastro.', 'sucesso');
          setArquivoNovo(null);
          await invalidar();
        }}
      />

      <DialogoDocumento
        aberto={editando !== null}
        tituloModal={editando?.aceitar ? 'Aceitar no cadastro do paciente' : 'Editar documento'}
        descricaoModal={
          editando?.aceitar ? 'Confira o nome e a descrição — é assim que o documento será achado depois.' : undefined
        }
        tituloInicial={editando?.item.titulo ?? ''}
        descricaoInicial={editando?.item.descricao}
        rotuloConfirmar={editando?.aceitar ? 'Aceitar' : 'Salvar'}
        aoFechar={() => setEditando(null)}
        aoConfirmar={async (titulo, descricao) => {
          if (!editando) return;
          if (editando.aceitar) {
            await aceitarDocumentoPaciente(pacienteId, editando.item.id, titulo, descricao);
            notificar('Documento aceito no cadastro.', 'sucesso');
            setVendo(null);
          } else {
            await editarDocumentoPaciente(pacienteId, editando.item.id, titulo, descricao);
          }
          setEditando(null);
          await invalidar();
        }}
      />

      <ConfirmDialog
        aberto={excluindo !== null}
        titulo="Excluir documento do cadastro?"
        mensagem={`"${excluindo?.titulo ?? ''}" sai do cadastro do paciente e o arquivo é apagado. As solicitações que já usaram este documento guardaram a própria cópia.`}
        rotuloConfirmar="Excluir"
        destrutivo
        carregando={excluindoCarregando}
        erro={erroExclusao}
        aoConfirmar={confirmarExclusao}
        aoCancelar={() => {
          setExcluindo(null);
          setErroExclusao(null);
        }}
      />
    </>
  );
}
