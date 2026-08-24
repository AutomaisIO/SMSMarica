import { useEffect, useRef, useState } from 'react';
import { Ban, FolderUp, Loader2, PackageOpen, UploadCloud } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useCancelarLote,
  useImportarLote,
  useStatusLote,
} from '@/features/importacao-sisreg/api/queries';
import type { ImportacaoLoteAceito } from '@/features/importacao-sisreg/types';

// `webkitdirectory` habilita a escolha de PASTA — não está no tipo padrão do React.
declare module 'react' {
  interface InputHTMLAttributes<T> {
    webkitdirectory?: string;
    directory?: string;
  }
}

/**
 * Importação de VÁRIOS arquivos de uma vez (seleção múltipla, pasta ou .zip) — direto pro lote,
 * sem preview. O servidor processa em background; aqui a gente só acompanha o progresso e os
 * resultados aparecem nas abas Rastreio e Erros. Para conferir antes de importar, use a aba
 * "Um arquivo" (com preview).
 */
export function ImportacaoLote({ aoConcluir }: { aoConcluir: () => void }) {
  const arquivosRef = useRef<HTMLInputElement>(null);
  const pastaRef = useRef<HTMLInputElement>(null);
  const zipRef = useRef<HTMLInputElement>(null);

  const [ultimoEnvio, setUltimoEnvio] = useState<ImportacaoLoteAceito | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // Enviamos AGORA e ainda esperamos o runner começar? O POST responde 202 antes de o lote
  // iniciar; nessa janela o status volta como o RESUMO do último lote (emExecucao=false). Sem
  // este marcador o polling se desligaria e o lote rodaria invisível. Só liberamos quando
  // vemos emExecucao virar true (começou) e depois false (terminou).
  const [aguardandoInicio, setAguardandoInicio] = useState(false);

  const importar = useImportarLote();
  const cancelar = useCancelarLote();
  const status = useStatusLote(true, aguardandoInicio);

  const rodando = status.data?.emExecucao ?? false;

  // Assim que o lote realmente começou, saímos do "aguardando início".
  useEffect(() => {
    if (rodando) setAguardandoInicio(false);
  }, [rodando]);

  // Quando o lote vivo termina, sinaliza pra tela atualizar Rastreio/Erros.
  const rodandoAntes = useRef(false);
  useEffect(() => {
    if (rodandoAntes.current && !rodando) aoConcluir();
    rodandoAntes.current = rodando;
  }, [rodando, aoConcluir]);

  async function enviar(arquivos: FileList | null) {
    if (!arquivos || arquivos.length === 0) return;
    setErro(null);
    setUltimoEnvio(null);
    try {
      const res = await importar.mutateAsync(Array.from(arquivos));
      setUltimoEnvio(res);
      // Só há lote pra acompanhar se ao menos um arquivo foi aceito.
      if (res.arquivosAceitos > 0) setAguardandoInicio(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      // Permite reescolher os mesmos arquivos.
      for (const r of [arquivosRef, pastaRef, zipRef]) if (r.current) r.current.value = '';
    }
  }

  const prog = status.data;
  const pct = prog && prog.totalArquivos > 0 ? (prog.arquivosFeitos / prog.totalArquivos) * 100 : 0;

  return (
    <section className="space-y-4">
      <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <p className="mb-3 text-sm text-gray-600">
          Envie <strong>vários arquivos</strong>, uma <strong>pasta</strong> inteira ou um{' '}
          <strong>.zip</strong>. O sistema processa tudo de uma vez, no servidor — pode fechar esta
          tela que a importação continua. Arquivos que não forem <strong>.txt</strong> ou{' '}
          <strong>.csv</strong> são ignorados; os que forem, mas não forem do SISREG, aparecem em{' '}
          <strong>Erros</strong> como “arquivo incompatível”.
        </p>

        <input
          ref={arquivosRef}
          type="file"
          accept=".txt,.csv,text/plain,text/csv"
          multiple
          className="hidden"
          onChange={(e) => enviar(e.target.files)}
        />
        <input
          ref={pastaRef}
          type="file"
          webkitdirectory=""
          directory=""
          multiple
          className="hidden"
          onChange={(e) => enviar(e.target.files)}
        />
        <input
          ref={zipRef}
          type="file"
          accept=".zip,application/zip"
          className="hidden"
          onChange={(e) => enviar(e.target.files)}
        />

        <div className="flex flex-wrap gap-3">
          <Button variante="outline" onClick={() => arquivosRef.current?.click()} disabled={importar.isPending || rodando}>
            <UploadCloud className="h-4 w-4" /> Vários arquivos
          </Button>
          <Button variante="outline" onClick={() => pastaRef.current?.click()} disabled={importar.isPending || rodando}>
            <FolderUp className="h-4 w-4" /> Uma pasta
          </Button>
          <Button variante="outline" onClick={() => zipRef.current?.click()} disabled={importar.isPending || rodando}>
            <PackageOpen className="h-4 w-4" /> Um .zip
          </Button>
          {importar.isPending ? (
            <span className="inline-flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Enviando…
            </span>
          ) : null}
        </div>

        {erro ? (
          <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}

        {ultimoEnvio && ultimoEnvio.arquivosIgnorados.length > 0 ? (
          <div className="mt-3 rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-xs text-gray-600">
            <strong>{ultimoEnvio.arquivosIgnorados.length}</strong> ignorado(s) por não ser .txt/.csv:
            <ul className="mt-1 list-inside list-disc">
              {ultimoEnvio.arquivosIgnorados.slice(0, 8).map((n) => (
                <li key={n}>{n}</li>
              ))}
              {ultimoEnvio.arquivosIgnorados.length > 8 ? (
                <li>… e mais {ultimoEnvio.arquivosIgnorados.length - 8}</li>
              ) : null}
            </ul>
          </div>
        ) : null}
      </div>

      {aguardandoInicio && !rodando ? (
        <div className="rounded-lg border border-gray-200 bg-white p-4 text-sm text-gray-600 shadow-sm">
          <Loader2 className="mr-2 inline h-4 w-4 animate-spin" /> Iniciando importação…
        </div>
      ) : prog && (rodando || ultimoEnvio) ? (
        <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <div className="mb-1 flex items-center justify-between text-sm">
            <span className="font-medium text-gray-800">
              {rodando ? 'Importando…' : 'Última importação'}
            </span>
            {rodando ? (
              <Button variante="outline" onClick={() => cancelar.mutate()} disabled={cancelar.isPending}>
                <Ban className="h-4 w-4" /> Parar
              </Button>
            ) : null}
          </div>
          <div className="mb-2 text-xs text-gray-500">
            {prog.arquivosFeitos} de {prog.totalArquivos} arquivo(s)
            {prog.arquivoAtual ? ` · ${prog.arquivoAtual}` : ''}
            {' · '}
            <span className="text-emerald-700">{prog.validos} válidos</span>
            {prog.invalidos > 0 ? <span className="text-red-600"> · {prog.invalidos} inválidos</span> : null}
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-100">
            <div
              className={`h-full rounded-full transition-all ${rodando ? 'bg-blue-500' : 'bg-emerald-500'}`}
              style={{ width: `${pct}%` }}
            />
          </div>
        </div>
      ) : null}
    </section>
  );
}
