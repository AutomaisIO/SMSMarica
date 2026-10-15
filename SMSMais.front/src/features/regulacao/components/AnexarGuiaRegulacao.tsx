import { useRef, useState } from 'react';
import { Paperclip, Upload } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';

import { useAnexarGuiaRegulacao } from '../api/solicitacoesQueries';

/**
 * A regulação anexa um documento (a guia de agendamento / autorização) na solicitação já enviada,
 * sem precisar de e-mail: a unidade pega pela própria plataforma e a trilha registra quem baixou.
 *
 * <p>Só aparece para quem regula (a rota já é gateada por `RegulacaoTriagem`) e tem edição em
 * `Regulacao` — a mesma permissão que o endpoint exige. O arquivo entra na caixinha "Anexos
 * gerais", que a unidade enxerga em "Anexos".</p>
 */
export function AnexarGuiaRegulacao({ solicitacaoId }: { solicitacaoId: string }) {
  const podeAnexar = usePermissao('Regulacao', 'Edicao');
  const anexar = useAnexarGuiaRegulacao();
  const inputRef = useRef<HTMLInputElement>(null);
  const [titulo, setTitulo] = useState('Guia de agendamento');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState(false);

  if (!podeAnexar) return null;

  async function enviar() {
    if (!arquivo) return;
    setErro(null);
    setOk(false);
    try {
      await anexar.mutateAsync({ solicitacaoId, arquivo, titulo });
      setArquivo(null);
      if (inputRef.current) inputRef.current.value = '';
      setOk(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
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
            setOk(false);
          }}
          className="block w-full text-sm text-slate-600 file:mr-3 file:rounded file:border-0 file:bg-slate-100 file:px-3 file:py-1.5 file:text-sm file:text-slate-700 hover:file:bg-slate-200"
        />

        <div className="flex items-center gap-3">
          <Button onClick={enviar} disabled={!arquivo || anexar.isPending}>
            <Upload className="size-4" />
            {anexar.isPending ? 'Anexando…' : 'Anexar'}
          </Button>
          {ok && <span className="text-xs text-emerald-700">Documento anexado.</span>}
        </div>

        {erro && <p className="text-sm text-red-700">{erro}</p>}
      </div>
    </section>
  );
}
