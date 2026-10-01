import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

type Props = {
  aberto: boolean;
  /** Título do modal (ex.: "Anexar documento", "Adicionar ao cadastro do paciente"). */
  tituloModal: string;
  descricaoModal?: string;
  tituloInicial?: string;
  descricaoInicial?: string | null;
  rotuloConfirmar?: string;
  aoConfirmar: (titulo: string, descricao: string | null) => Promise<void>;
  aoFechar: () => void;
  /** Conteúdo extra acima dos campos (ex.: escolher o paciente). */
  children?: React.ReactNode;
};

/**
 * Pede o nome e a descrição de um documento antes de anexá-lo. É o que faz o documento ser
 * achável depois no cadastro do paciente — "IMG_20260930.jpg" não diz a ninguém o que é.
 */
export function DialogoDocumento({
  aberto,
  tituloModal,
  descricaoModal,
  tituloInicial = '',
  descricaoInicial,
  rotuloConfirmar = 'Salvar',
  aoConfirmar,
  aoFechar,
  children,
}: Props) {
  const [titulo, setTitulo] = useState(tituloInicial);
  const [descricao, setDescricao] = useState(descricaoInicial ?? '');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (aberto) {
      setTitulo(tituloInicial);
      setDescricao(descricaoInicial ?? '');
      setErro(null);
    }
  }, [aberto, tituloInicial, descricaoInicial]);

  async function confirmar(e: React.FormEvent) {
    e.preventDefault();
    if (!titulo.trim()) return;
    setSalvando(true);
    setErro(null);
    try {
      await aoConfirmar(titulo.trim(), descricao.trim() || null);
    } catch (e) {
      // O interceptor só avisa 5xx; recusa de regra (limite, tipo, tamanho) aparece aqui, no
      // próprio modal, onde a pessoa está olhando.
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo={tituloModal} descricao={descricaoModal} largura="sm" porCima>
      <form onSubmit={confirmar} className="space-y-4">
        {children}
        <div>
          <label htmlFor="doc-titulo" className="mb-1 block text-sm font-medium text-slate-700">
            Nome do documento <span className="text-red-600">*</span>
          </label>
          <Input
            id="doc-titulo"
            autoFocus
            maxLength={200}
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            placeholder="Ex.: Laudo da ressonância de joelho"
          />
        </div>
        <div>
          <label htmlFor="doc-descricao" className="mb-1 block text-sm font-medium text-slate-700">
            Descrição
          </label>
          <textarea
            id="doc-descricao"
            className="input min-h-[80px]"
            maxLength={2000}
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            placeholder="Opcional — de quando é, quem pediu, o que mostra…"
          />
        </div>
        {erro ? (
          <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button variante="ghost" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </Button>
          <Button type="submit" disabled={salvando || !titulo.trim()}>
            {salvando ? <Loader2 className="mr-1.5 size-4 animate-spin" /> : null}
            {rotuloConfirmar}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

/** Sugestão de título a partir do nome do arquivo: tira a extensão e troca _ e - por espaço. */
export function tituloDoArquivo(nome: string): string {
  return nome
    .replace(/\.[^.]+$/, '')
    .replace(/[_-]+/g, ' ')
    .trim()
    .slice(0, 200);
}
