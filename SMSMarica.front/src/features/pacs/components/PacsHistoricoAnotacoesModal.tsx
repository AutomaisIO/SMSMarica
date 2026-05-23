import { useEffect, useState } from 'react';
import { History, Loader2, RotateCcw } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { listarHistorico, obterVersao } from '@/features/pacs/api/anotacoesApi';
import type { EstudoAnotacaoVersao, EstudoAnotacaoVersaoResumo } from '@/features/pacs/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  studyInstanceUID: string;
  versaoAtual: number | null;
  /** Chamado quando o usuário escolhe restaurar uma versão antiga. */
  aoRestaurar: (versao: EstudoAnotacaoVersao) => void;
};

export function PacsHistoricoAnotacoesModal({
  aberto,
  aoFechar,
  studyInstanceUID,
  versaoAtual,
  aoRestaurar,
}: Props) {
  const [lista, setLista] = useState<EstudoAnotacaoVersaoResumo[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [restaurando, setRestaurando] = useState<number | null>(null);

  useEffect(() => {
    if (!aberto) return;
    let cancelado = false;
    setCarregando(true);
    setErro(null);
    listarHistorico(studyInstanceUID)
      .then((r) => {
        if (!cancelado) setLista(r);
      })
      .catch((e) => {
        if (!cancelado) setErro(extrairMensagemDeErro(e));
      })
      .finally(() => {
        if (!cancelado) setCarregando(false);
      });
    return () => {
      cancelado = true;
    };
  }, [aberto, studyInstanceUID]);

  async function restaurar(versao: number) {
    setRestaurando(versao);
    setErro(null);
    try {
      const dto = await obterVersao(studyInstanceUID, versao);
      aoRestaurar(dto);
      aoFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setRestaurando(null);
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Histórico de anotações" largura="md">
      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {carregando ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" />
          Carregando histórico…
        </div>
      ) : lista.length === 0 ? (
        <div className="flex flex-col items-center gap-2 py-8 text-sm text-gray-500">
          <History className="h-8 w-8 text-gray-300" />
          Nenhuma versão salva ainda.
        </div>
      ) : (
        <ul className="divide-y divide-gray-100">
          {lista.map((v) => {
            const eAtual = v.versao === versaoAtual;
            return (
              <li
                key={v.id}
                className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2 text-sm">
                    <span className="font-semibold text-gray-900">v{v.versao}</span>
                    {eAtual ? (
                      <span className="rounded bg-primary-100 px-1.5 py-0.5 text-[10px] font-medium uppercase text-primary-700">
                        atual
                      </span>
                    ) : null}
                    <span className="text-gray-700">{v.usuarioNome}</span>
                  </div>
                  <div className="mt-0.5 text-xs text-gray-500">
                    {formatarDataHora(v.criadoEm)}
                  </div>
                  {v.comentario ? (
                    <div className="mt-1 truncate text-sm text-gray-600" title={v.comentario}>
                      “{v.comentario}”
                    </div>
                  ) : null}
                </div>
                <Button
                  variante="outline"
                  tamanho="sm"
                  onClick={() => restaurar(v.versao)}
                  disabled={restaurando !== null || eAtual}
                  title={eAtual ? 'Versão já carregada' : 'Carregar esta versão no visualizador'}
                >
                  {restaurando === v.versao ? (
                    <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <RotateCcw className="mr-1 h-4 w-4" />
                  )}
                  Carregar
                </Button>
              </li>
            );
          })}
        </ul>
      )}
    </Modal>
  );
}

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
