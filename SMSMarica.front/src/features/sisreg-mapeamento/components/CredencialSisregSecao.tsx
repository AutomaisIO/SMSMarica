import { useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  KeyRound,
  Loader2,
  Trash2,
  UserCheck,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { ModalCredencial } from '@/features/sisreg-mapeamento/components/ModalCredencial';
import {
  useCredencialUnidade,
  useRemoverCredencial,
  useTestarCredencial,
} from '@/features/sisreg-mapeamento/api/queries';

type Props = {
  /** Unidade-alvo da credencial. Enviada no header X-Unidade-Id de cada chamada. */
  unidadeId: string;
  /** Só usado no texto; a unidade real vem da própria credencial. */
  nomeUnidade?: string;
};

/**
 * Cartão da credencial do SISREG de UMA unidade: mostra o estado (própria/global,
 * validada em), cadastra/troca via {@link ModalCredencial} (que autentica de verdade e
 * confere a unidade antes de gravar), testa e remove.
 *
 * Componente compartilhado entre a página de Mapeamento SISREG, a Configuração SISREG
 * (lista de unidades) e o detalhe da unidade (aba SISREG) — a mesma credencial por
 * unidade exibida em qualquer um dos três pontos.
 */
export function CredencialSisregSecao({ unidadeId, nomeUnidade }: Props) {
  const credencial = useCredencialUnidade(unidadeId);
  const testar = useTestarCredencial(unidadeId);
  const remover = useRemoverCredencial(unidadeId);

  const [modalAberto, setModalAberto] = useState(false);
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);

  async function executar(acao: () => Promise<unknown>, sucesso: (r: unknown) => string) {
    setAviso(null);
    try {
      const resultado = await acao();
      setAviso({ tipo: 'ok', texto: sucesso(resultado) });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  const semCredencialPropria = credencial.data?.usandoFallbackGlobal ?? false;

  return (
    <>
      <section className="rounded-lg border border-gray-200 bg-white p-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex gap-3">
            <KeyRound className="mt-0.5 h-5 w-5 shrink-0 text-gray-500" />
            <div>
              <h2 className="font-medium text-gray-900">
                Credencial do SISREG{nomeUnidade ? ` — ${nomeUnidade}` : ' desta unidade'}
              </h2>
              {credencial.isLoading ? (
                <p className="mt-1 text-sm text-gray-500">Carregando…</p>
              ) : credencial.data?.usuario ? (
                <div className="mt-1 space-y-1 text-sm text-gray-600">
                  <p>
                    Usuário <strong className="font-mono">{credencial.data.usuario}</strong>
                    {credencial.data.unidadeSisregNome && (
                      <>
                        {' '}— confirmado em{' '}
                        <strong>{credencial.data.unidadeSisregNome}</strong>
                        {credencial.data.cnesConfirmado ? ` (CNES ${credencial.data.cnesConfirmado})` : ''}
                      </>
                    )}
                  </p>
                  {credencial.data.validadoEm && (
                    <p className="text-xs text-gray-500">
                      Última validação em{' '}
                      {new Date(credencial.data.validadoEm).toLocaleString('pt-BR')}
                    </p>
                  )}
                </div>
              ) : (
                <p className="mt-1 text-sm text-gray-600">
                  Nenhuma credencial própria — esta unidade está usando a credencial global das
                  Integrações, que enxerga apenas a unidade do operador dela.
                </p>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button variante="outline" onClick={() => setModalAberto(true)}>
              <KeyRound className="h-4 w-4" />
              {credencial.data?.usuario ? 'Trocar usuário/senha' : 'Cadastrar credencial'}
            </Button>
            <Button
              variante="secundaria"
              disabled={testar.isPending || !credencial.data?.usuario}
              onClick={() =>
                executar(
                  () => testar.mutateAsync(),
                  (r) => (r as { mensagem: string }).mensagem,
                )
              }
            >
              {testar.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <UserCheck className="h-4 w-4" />
              )}
              Testar
            </Button>
            {credencial.data?.usuario && (
              <Button
                variante="ghost"
                disabled={remover.isPending}
                onClick={() =>
                  executar(
                    () => remover.mutateAsync(),
                    () => 'Credencial da unidade removida — voltou a usar a credencial global.',
                  )
                }
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            )}
          </div>
        </div>

        {semCredencialPropria && !credencial.isLoading && (
          <p className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
            Sem credencial própria, atualizar o mapeamento vai falhar se a credencial global for de
            outra unidade — a conferência de unidade barra antes de gravar qualquer coisa errada.
          </p>
        )}

        {aviso && (
          <div
            className={`mt-3 flex items-start gap-2 rounded-md border p-3 text-sm ${
              aviso.tipo === 'ok'
                ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                : 'border-red-200 bg-red-50 text-red-700'
            }`}
          >
            {aviso.tipo === 'ok' ? (
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            ) : (
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            )}
            <span>{aviso.texto}</span>
          </div>
        )}
      </section>

      <ModalCredencial
        aberto={modalAberto}
        unidadeId={unidadeId}
        credencial={credencial.data}
        aoFechar={() => setModalAberto(false)}
      />
    </>
  );
}
