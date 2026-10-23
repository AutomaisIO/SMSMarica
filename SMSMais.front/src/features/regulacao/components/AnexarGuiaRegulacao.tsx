import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Paperclip, Upload } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { PreviaCompactacao, type ArquivoPreparado } from '@/shared/acervo/PreviaCompactacao';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';

import {
  aceitarAnexoPreparado,
  caminhoAnexoPreparado,
  descartarAnexoPreparado,
  prepararAnexo,
} from '../api/acervoRegulacao';
import { garantirAnexosGerais } from '../api/solicitacoesApi';

/**
 * A regulação anexa um documento (a guia de agendamento / autorização) na solicitação já enviada,
 * sem precisar de e-mail: a unidade pega pela própria plataforma e a trilha registra quem baixou.
 *
 * <p>Só aparece para quem regula (a rota já é gateada por `RegulacaoTriagem`) e tem edição em
 * `Regulacao` — a mesma permissão que o endpoint exige. O arquivo entra na caixinha "Anexos
 * gerais", que a unidade enxerga em "Anexos".</p>
 *
 * <p>Como todo anexo da solicitação, é compactado na entrada e só entra depois de quem anexou
 * conferir a prévia.</p>
 */
export function AnexarGuiaRegulacao({ solicitacaoId }: { solicitacaoId: string }) {
  const podeAnexar = usePermissao('Regulacao', 'Edicao');
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [titulo, setTitulo] = useState('Guia de agendamento');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [preparando, setPreparando] = useState(false);
  const [previa, setPrevia] = useState<ArquivoPreparado | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [desfecho, setDesfecho] = useState<'anexado' | 'descartado' | null>(null);

  if (!podeAnexar) return null;

  async function preparar() {
    if (!arquivo) return;
    setErro(null);
    setDesfecho(null);
    setPreparando(true);
    try {
      const caixinha = await garantirAnexosGerais(solicitacaoId);
      setPrevia(await prepararAnexo(solicitacaoId, caixinha.id, arquivo));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setPreparando(false);
    }
  }

  async function aceitar() {
    if (!previa) return;
    await aceitarAnexoPreparado(solicitacaoId, previa.token, titulo.trim() || null, null);
    await queryClient.invalidateQueries({ queryKey: ['regulacao', 'solicitacoes'] });
    setPrevia(null);
    setArquivo(null);
    if (inputRef.current) inputRef.current.value = '';
    setDesfecho('anexado');
  }

  function descartar() {
    if (previa) void descartarAnexoPreparado(solicitacaoId, previa.token).catch(() => {});
    setPrevia(null);
    setDesfecho('descartado');
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h2 className="mb-1 flex items-center gap-1.5 text-sm font-semibold text-slate-900">
        <Paperclip className="size-4 text-slate-500" />
        Anexar documento para a unidade
      </h2>
      <p className="mb-3 text-xs text-slate-500">
        A unidade baixa o documento pela plataforma — sem e-mail. A linha do tempo registra quem o
        abriu.
      </p>

      <div className="space-y-2">
        <label className="block text-xs font-medium text-slate-700">
          Nome do documento
          <input
            type="text"
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
            placeholder="Guia de agendamento"
          />
        </label>

        <input
          ref={inputRef}
          type="file"
          onChange={(e) => {
            setArquivo(e.target.files?.[0] ?? null);
            setDesfecho(null);
          }}
          className="block w-full text-sm text-slate-600 file:mr-3 file:rounded file:border-0 file:bg-slate-100 file:px-3 file:py-1.5 file:text-sm file:text-slate-700 hover:file:bg-slate-200"
        />

        <div className="flex items-center gap-3">
          <Button onClick={preparar} disabled={!arquivo || preparando}>
            <Upload className="size-4" />
            {preparando ? 'Compactando…' : 'Anexar'}
          </Button>
          {desfecho === 'anexado' && <span className="text-xs text-emerald-700">Documento anexado.</span>}
          {desfecho === 'descartado' && (
            <span className="text-xs text-slate-500">Arquivo descartado — nada foi anexado.</span>
          )}
        </div>

        {erro && <p className="text-sm text-red-700">{erro}</p>}
      </div>

      {previa ? (
        <PreviaCompactacao
          preparado={previa}
          caminho={caminhoAnexoPreparado(solicitacaoId, previa.token)}
          rotuloAceitar="anexar"
          aoAceitar={aceitar}
          aoDescartar={descartar}
        />
      ) : null}
    </section>
  );
}
