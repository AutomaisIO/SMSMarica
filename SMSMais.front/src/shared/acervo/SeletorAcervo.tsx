import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Eye, FolderOpen, Loader2, Paperclip, Search } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { IconeItemAcervo, formatarDataAcervo, formatarTamanhoAcervo } from './ListaAcervo';
import { ROTULO_TIPO_ACERVO, type ItemAcervo } from './tipos';
import { AcaoVisualizador, VisualizadorArquivo } from './VisualizadorArquivo';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  /** Chave de cache — o mesmo seletor serve a regulação, SER, SERNIT e anamnese. */
  chaveConsulta: readonly unknown[];
  carregar: () => Promise<ItemAcervo[]>;
  /** Caminho do conteúdo de um item, no endpoint do contexto (para o visualizador). */
  caminhoConteudo: (item: ItemAcervo) => string;
  aoAnexar: (item: ItemAcervo) => Promise<void>;
  /** Aviso quando a lista vem vazia por falta de paciente (ex.: CNS não cadastrado no SER). */
  avisoVazio?: string;
};

/**
 * "Anexar do cadastro": lista o que o paciente já tem guardado (documentos, laudos assinados,
 * imagens dos exames) para anexar na solicitação sem novo upload. Só aparece o que está aceito.
 */
export function SeletorAcervo({
  aberto,
  aoFechar,
  chaveConsulta,
  carregar,
  caminhoConteudo,
  aoAnexar,
  avisoVazio,
}: Props) {
  const [busca, setBusca] = useState('');
  const [anexando, setAnexando] = useState<string | null>(null);
  const [vendo, setVendo] = useState<ItemAcervo | null>(null);

  const consulta = useQuery({ queryKey: chaveConsulta, queryFn: carregar, enabled: aberto });

  const itens = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    const lista = consulta.data ?? [];
    return termo
      ? lista.filter((i) => `${i.titulo} ${i.descricao ?? ''} ${i.origem}`.toLowerCase().includes(termo))
      : lista;
  }, [consulta.data, busca]);

  async function anexar(item: ItemAcervo) {
    setAnexando(item.chave);
    try {
      await aoAnexar(item);
      setVendo(null);
      aoFechar();
    } catch (e) {
      // O interceptor só avisa 5xx: a recusa (tamanho, paciente sem CNS…) tem de aparecer aqui.
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setAnexando(null);
    }
  }

  return (
    <>
      <Modal
        aberto={aberto}
        aoFechar={aoFechar}
        titulo="Anexar do cadastro do paciente"
        descricao="Documentos que o paciente já tem guardados — sem precisar enviar de novo."
        largura="lg"
      >
        <div className="relative mb-3">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
          <input
            className="input pl-8"
            placeholder="Buscar pelo nome ou descrição…"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
          />
        </div>

        {consulta.isLoading ? (
          <p className="flex items-center gap-2 text-sm text-slate-500">
            <Loader2 className="size-4 animate-spin" /> Carregando…
          </p>
        ) : itens.length === 0 ? (
          <div className="rounded-md border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">
            <FolderOpen className="mx-auto mb-2 size-6 text-slate-300" />
            {busca ? 'Nada encontrado com essa busca.' : (avisoVazio ?? 'O paciente ainda não tem documentos no cadastro.')}
          </div>
        ) : (
          <ul className="divide-y divide-slate-100 rounded-md border border-slate-200">
            {itens.map((i) => (
              <li key={i.chave} className="flex items-center gap-3 px-3 py-2">
                <IconeItemAcervo item={i} />
                <button
                  type="button"
                  onClick={() => setVendo(i)}
                  className="min-w-0 flex-1 text-left"
                  title="Visualizar"
                >
                  <p className="truncate text-sm font-medium text-slate-800 hover:text-red-700">{i.titulo}</p>
                  <p className="truncate text-xs text-slate-500">
                    {ROTULO_TIPO_ACERVO[i.tipo]} · {i.origem} · {formatarDataAcervo(i.data)}
                    {i.tamanhoBytes ? ` · ${formatarTamanhoAcervo(i.tamanhoBytes)}` : ''}
                  </p>
                </button>
                <button
                  type="button"
                  onClick={() => setVendo(i)}
                  className="shrink-0 rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                  aria-label={`Visualizar ${i.titulo}`}
                >
                  <Eye className="size-4" />
                </button>
                <button
                  type="button"
                  onClick={() => anexar(i)}
                  disabled={anexando !== null}
                  className="inline-flex shrink-0 items-center gap-1 rounded-md border border-slate-300 px-2.5 py-1 text-xs font-medium text-slate-700 hover:border-red-300 hover:text-red-700 disabled:opacity-50"
                >
                  {anexando === i.chave ? <Loader2 className="size-3.5 animate-spin" /> : <Paperclip className="size-3.5" />}
                  Anexar
                </button>
              </li>
            ))}
          </ul>
        )}
      </Modal>

      {vendo ? (
        <VisualizadorArquivo
          caminho={caminhoConteudo(vendo)}
          titulo={vendo.titulo}
          descricao={vendo.descricao}
          aoFechar={() => setVendo(null)}
          acoes={
            <AcaoVisualizador destaque aoClicar={() => anexar(vendo)} disabled={anexando !== null}>
              {anexando === vendo.chave ? <Loader2 className="size-4 animate-spin" /> : <Paperclip className="size-4" />}
              Anexar na solicitação
            </AcaoVisualizador>
          }
        />
      ) : null}
    </>
  );
}
