import { useState } from 'react';
import { CheckCircle2, Clock3, Loader2, MonitorCheck, MonitorX } from 'lucide-react';
import { useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { useAtivacao, useAutorizarAtivacao } from '@/features/extensao-navegador/api/queries';

/**
 * A página que o atualizador abre no navegador quando alguém usa o "Configurar" no computador:
 * quem está logado confere QUAL computador está pedindo e autoriza. O código vem no caminho da
 * URL (não em ?c=) porque a ida ao login guarda só o caminho.
 */
export function AutorizarComputadorPage() {
  const { codigo = '' } = useParams<{ codigo: string }>();
  const ativacao = useAtivacao(codigo);
  const autorizar = useAutorizarAtivacao(codigo);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    setErro(null);
    try {
      await autorizar.mutateAsync();
    } catch (e) {
      // O desfecho fica aqui, na frente de quem clicou — não num aviso de canto.
      setErro(extrairMensagemDeErro(e));
      void ativacao.refetch();
    }
  }

  const pedido = ativacao.data;
  const computador = pedido?.computador ?? 'um computador';

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Autorizar computador</h1>
        <p className="mt-1 text-sm text-gray-600">
          Um computador pediu para receber a extensão do Chrome desta plataforma.
        </p>
      </header>

      {ativacao.isLoading ? (
        <div className="flex items-center gap-2 text-sm text-gray-600">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando o pedido…
        </div>
      ) : null}

      {ativacao.isError ? (
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <div className="flex items-start gap-3">
            <MonitorX className="mt-0.5 h-6 w-6 text-gray-400" />
            <div>
              <p className="font-medium text-gray-900">Pedido não encontrado</p>
              <p className="mt-1 text-sm text-gray-600">
                O endereço não corresponde a nenhum pedido de autorização. No computador, clique no ícone "+" perto do
                relógio e escolha <em>Configurar</em> para gerar um pedido novo.
              </p>
            </div>
          </div>
        </div>
      ) : null}

      {pedido ? (
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt className="text-gray-500">Computador</dt>
            <dd className="font-medium text-gray-900">{pedido.computador ?? 'não informado'}</dd>
            <dt className="text-gray-500">Pedido em</dt>
            <dd className="text-gray-900">{formatarInstante(pedido.pedidoEm)}</dd>
            <dt className="text-gray-500">Vale até</dt>
            <dd className="text-gray-900">{formatarInstante(pedido.expiraEm)}</dd>
          </dl>

          <div className="mt-5 border-t border-gray-200 pt-4">
            {pedido.situacao === 'Pendente' ? (
              <>
                <p className="text-sm text-gray-700">
                  Autorize só se foi você (ou alguém da sua equipe) que pediu, neste computador. Ele passa a receber
                  a extensão e as atualizações; dá para revogar depois.
                </p>
                <div className="mt-4">
                  <Button onClick={confirmar} disabled={autorizar.isPending}>
                    {autorizar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <MonitorCheck className="h-4 w-4" />}
                    Autorizar {computador}
                  </Button>
                </div>
              </>
            ) : null}

            {pedido.situacao === 'Autorizada' ? (
              <div className="flex items-start gap-3">
                <Loader2 className="mt-0.5 h-5 w-5 animate-spin text-primary-600" />
                <div>
                  <p className="font-medium text-gray-900">Autorizado</p>
                  <p className="mt-1 text-sm text-gray-600">
                    Volte ao computador: ele conclui sozinho em alguns segundos.
                  </p>
                </div>
              </div>
            ) : null}

            {pedido.situacao === 'Usada' ? (
              <div className="flex items-start gap-3">
                <CheckCircle2 className="mt-0.5 h-5 w-5 text-primary-600" />
                <div>
                  <p className="font-medium text-gray-900">Computador ligado à plataforma</p>
                  <p className="mt-1 text-sm text-gray-600">
                    {computador} já está recebendo a extensão. Pode fechar esta página.
                  </p>
                </div>
              </div>
            ) : null}

            {pedido.situacao === 'Vencida' ? (
              <div className="flex items-start gap-3">
                <Clock3 className="mt-0.5 h-5 w-5 text-gray-400" />
                <div>
                  <p className="font-medium text-gray-900">Este pedido venceu</p>
                  <p className="mt-1 text-sm text-gray-600">
                    No computador, clique no ícone "+" perto do relógio e escolha <em>Configurar</em> de novo.
                  </p>
                </div>
              </div>
            ) : null}

            {erro ? (
              <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
