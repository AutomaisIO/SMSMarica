import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { EyeOff, KeyRound, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import {
  revelarChaveSisreg,
  type ChaveConfirmacaoSisreg,
} from '@/features/solicitacoes-exame/api/solicitacoesExameApi';

/**
 * Seção "SISREG" no pé do detalhe de exame e de consulta. Hoje: "Mostrar chave" (permissão
 * própria, RevelarChaveSisreg). A baixa no SISREG — confirmar execução / registrar falta, com o
 * login SISREG de quem faz — entra aqui depois.
 *
 * A chave NÃO fica em cache: some ao ocultar ou sair da tela, e cada "Mostrar" consulta o SISREG
 * de novo (e registra na auditoria).
 */
export function SecaoSisreg({
  solicitacaoId,
  codigoSolicitacao,
}: {
  solicitacaoId: string;
  codigoSolicitacao: string | null | undefined;
}) {
  const podeRevelar = usePermissao('RevelarChaveSisreg', 'Consulta');
  const [chave, setChave] = useState<ChaveConfirmacaoSisreg | null>(null);
  const revelar = useMutation({
    mutationFn: () => revelarChaveSisreg(solicitacaoId),
    onSuccess: setChave,
  });

  const temCodigo = !!codigoSolicitacao && codigoSolicitacao !== '0000';
  if (!podeRevelar || !temCodigo) return null;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm lg:col-span-2">
      <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">SISREG</h2>

      <div className="flex flex-wrap items-center gap-3">
        {chave ? (
          <>
            <div>
              <div className="text-xs text-gray-500">Chave de confirmação</div>
              <div className="font-mono text-2xl font-semibold tracking-widest text-gray-900">{chave.chave}</div>
              <div className="text-xs text-gray-400">
                Lida no SISREG em {formatarInstante(chave.lidaEm)} · solicitação {chave.codigoSolicitacao}
              </div>
            </div>
            <button
              type="button"
              onClick={() => setChave(null)}
              className="inline-flex items-center gap-1 rounded border border-gray-200 px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50"
            >
              <EyeOff className="h-3.5 w-3.5" /> Ocultar
            </button>
          </>
        ) : (
          <button
            type="button"
            onClick={() => revelar.mutate()}
            disabled={revelar.isPending}
            className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-60"
            title="Consulta o SISREG agora. A consulta fica registrada na auditoria."
          >
            {revelar.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <KeyRound className="h-4 w-4" />
            )}
            Mostrar chave
          </button>
        )}
      </div>

      {revelar.isError && !chave ? (
        <p className="mt-2 text-sm text-red-700">{extrairMensagemDeErro(revelar.error)}</p>
      ) : null}
    </section>
  );
}
