import { useState } from 'react';
import { KeyRound, Loader2, ShieldOff } from 'lucide-react';
import { apiBaseAbsoluto, extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import {
  useChavePublicacao,
  useGerarChavePublicacao,
  useRevogarChavePublicacao,
} from '@/features/extensao-navegador/api/queries';

/**
 * A API de publicação é uma OPÇÃO: enquanto não houver chave ativa, só se publica por esta tela.
 * A chave serve para listar, subir e pôr versão em produção — e para mais nada do sistema.
 */
export function AbaApiPublicacao() {
  const chave = useChavePublicacao();
  const gerar = useGerarChavePublicacao();
  const revogar = useRevogarChavePublicacao();
  const podeEditar = usePermissao('ExtensaoNavegador', 'Edicao');
  const [gerada, setGerada] = useState<string | null>(null);
  const [confirmacao, setConfirmacao] = useState<'gerar' | 'desligar' | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const ativa = chave.data?.ativa ?? false;

  async function gerarChave() {
    setErro(null);
    try {
      const resposta = await gerar.mutateAsync();
      setConfirmacao(null);
      setGerada(resposta.chave);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function desligar() {
    setErro(null);
    try {
      await revogar.mutateAsync();
      setConfirmacao(null);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="max-w-3xl space-y-4 pt-4">
      {chave.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(chave.error)}
        </div>
      ) : null}

      <section className="rounded-lg border border-gray-200 bg-white p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-semibold text-gray-900">Publicação por API</h2>
              {chave.isLoading ? null : ativa ? (
                <span className="badge badge-success">Ligada</span>
              ) : (
                <span className="badge badge-gray">Desligada</span>
              )}
            </div>
            {ativa && chave.data ? (
              <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
                <dt className="text-gray-500">Chave</dt>
                <dd className="font-mono text-gray-900">{chave.data.prefixo}…</dd>
                <dt className="text-gray-500">Gerada em</dt>
                <dd className="text-gray-900">
                  {formatarInstante(chave.data.criadaEm)}
                  {chave.data.criadaPorNome ? ` por ${chave.data.criadaPorNome}` : ''}
                </dd>
                <dt className="text-gray-500">Último uso</dt>
                <dd className="text-gray-900">{chave.data.ultimoUsoEm ? formatarInstante(chave.data.ultimoUsoEm) : 'nunca'}</dd>
              </dl>
            ) : (
              <p className="mt-1 text-sm text-gray-600">Só se publica por esta tela.</p>
            )}
          </div>
          {podeEditar ? (
            <div className="flex flex-wrap gap-2">
              <Button
                variante={ativa ? 'secundaria' : 'primaria'}
                onClick={() => (ativa ? setConfirmacao('gerar') : gerarChave())}
                disabled={gerar.isPending}
              >
                {gerar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <KeyRound className="h-4 w-4" />}
                {ativa ? 'Gerar outra chave' : 'Ligar e gerar chave'}
              </Button>
              {ativa ? (
                <Button variante="danger" onClick={() => setConfirmacao('desligar')}>
                  <ShieldOff className="h-4 w-4" />
                  Desligar
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>
        {erro && !confirmacao ? (
          <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}
      </section>

      <section className="rounded-lg border border-gray-200 bg-white p-5 text-sm text-gray-700">
        <h3 className="font-semibold text-gray-900">Como usar</h3>
        <p className="mt-1">
          A chave vai no cabeçalho <code className="rounded bg-gray-100 px-1">X-Chave-Publicacao</code>. Ela publica
          (a versão entra em teste) e põe em produção; não revoga computador nem retira versão.
        </p>
        <pre className="mt-3 overflow-x-auto rounded-md bg-gray-900 p-3 text-xs leading-relaxed text-gray-100">
{`# publicar a extensão (a versão vem do manifest)
curl -X POST ${apiBaseAbsoluto}/extensao/publicacao/pacotes \\
  -H "X-Chave-Publicacao: $CHAVE" -F artefato=Extensao -F arquivo=@extensao-1.2.3.zip

# publicar o atualizador (informar a versão)
curl -X POST ${apiBaseAbsoluto}/extensao/publicacao/pacotes \\
  -H "X-Chave-Publicacao: $CHAVE" -F artefato=Atualizador -F versao=1.2.3 -F arquivo=@smsmais-atualizador.exe

# pôr em produção (o id vem na resposta da publicação)
curl -X POST ${apiBaseAbsoluto}/extensao/publicacao/pacotes/<id>/promover -H "X-Chave-Publicacao: $CHAVE"`}
        </pre>
      </section>

      <Modal
        aberto={Boolean(gerada)}
        aoFechar={() => setGerada(null)}
        titulo="Chave de publicação"
        descricao="Copie agora: ela não será mostrada de novo. Quem tiver a chave publica e põe versão em produção."
      >
        <div className="space-y-4">
          <div className="break-all rounded-md border border-gray-200 bg-gray-50 p-3 font-mono text-sm">
            {gerada ? <CodigoCopiavel codigo={gerada} dica="Copiar a chave" /> : null}
          </div>
          <div className="flex justify-end">
            <Button onClick={() => setGerada(null)}>Já copiei</Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        aberto={Boolean(confirmacao)}
        titulo={confirmacao === 'desligar' ? 'Desligar a publicação por API' : 'Gerar outra chave'}
        mensagem={
          confirmacao === 'desligar'
            ? 'Desligar? A chave atual deixa de valer na hora, e só se publica por esta tela.'
            : 'Gerar outra chave? A chave atual deixa de valer na hora — quem a usa precisa trocar pela nova.'
        }
        destrutivo={confirmacao === 'desligar'}
        rotuloConfirmar={confirmacao === 'desligar' ? 'Desligar' : 'Gerar outra chave'}
        carregando={gerar.isPending || revogar.isPending}
        erro={erro}
        aoConfirmar={confirmacao === 'desligar' ? desligar : gerarChave}
        aoCancelar={() => {
          setConfirmacao(null);
          setErro(null);
        }}
      />
    </div>
  );
}
