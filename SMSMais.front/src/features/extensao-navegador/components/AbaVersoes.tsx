import { useRef, useState, type FormEvent } from 'react';
import { ArchiveX, Loader2, Rocket, Upload } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  usePacotes,
  usePromoverPacote,
  usePublicarPacote,
  useRetirarPacote,
} from '@/features/extensao-navegador/api/queries';
import { formatarTamanho, ROTULO_ARTEFATO } from '@/features/extensao-navegador/lib/formatar';
import type { Artefato, Pacote } from '@/features/extensao-navegador/types';

const ARTEFATOS: Artefato[] = ['Extensao', 'Atualizador'];

function Situacao({ pacote }: { pacote: Pacote }) {
  if (pacote.retiradoEm) return <span className="badge badge-error">Retirada</span>;
  if (pacote.atualEmProd) return <span className="badge badge-success">Em produção</span>;
  if (pacote.atualEmTeste) return <span className="badge badge-warning">Em teste</span>;
  return <span className="badge badge-gray">{pacote.promovidoEm ? 'Substituída' : 'Não promovida'}</span>;
}

/**
 * As versões publicadas da extensão e do atualizador. Publicar põe a versão no canal de teste;
 * "Pôr em produção" é o que a faz chegar a todos os computadores.
 */
export function AbaVersoes() {
  const pacotes = usePacotes();
  const promover = usePromoverPacote();
  const retirar = useRetirarPacote();
  const podeIncluir = usePermissao('ExtensaoNavegador', 'Inclusao');
  const podeEditar = usePermissao('ExtensaoNavegador', 'Edicao');
  const podeExcluir = usePermissao('ExtensaoNavegador', 'Exclusao');
  const [publicando, setPublicando] = useState(false);
  const [confirmacao, setConfirmacao] = useState<{ acao: 'promover' | 'retirar'; pacote: Pacote } | null>(null);
  const [erroConfirmacao, setErroConfirmacao] = useState<string | null>(null);

  const colunas: Coluna<Pacote>[] = [
    {
      chave: 'versao',
      cabecalho: 'Versão',
      render: (p) => (
        <div>
          <span className="font-medium tabular-nums text-gray-900">v{p.versao}</span>
          {p.notas ? <p className="max-w-md truncate text-xs text-gray-500" title={p.notas}>{p.notas}</p> : null}
        </div>
      ),
    },
    { chave: 'situacao', cabecalho: 'Situação', render: (p) => <Situacao pacote={p} /> },
    {
      chave: 'publicada',
      cabecalho: 'Publicada',
      render: (p) => (
        <div>
          <span className="tabular-nums">{formatarInstante(p.publicadoEm)}</span>
          <p className="text-xs text-gray-500">{p.publicadoPelaApi ? 'pela API' : (p.publicadoPorNome ?? '—')}</p>
        </div>
      ),
    },
    {
      chave: 'producao',
      cabecalho: 'Em produção desde',
      render: (p) =>
        p.promovidoEm ? (
          <div>
            <span className="tabular-nums">{formatarInstante(p.promovidoEm)}</span>
            {p.promovidoPelaApi ? <p className="text-xs text-gray-500">pela API</p> : null}
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        ),
    },
    { chave: 'tamanho', cabecalho: 'Tamanho', className: 'text-right', render: (p) => formatarTamanho(p.tamanho) },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (p) =>
        p.retiradoEm ? null : (
          <div className="flex items-center justify-end gap-1">
            {podeEditar && !p.promovidoEm ? (
              <BotaoLinhaAcao onClick={() => setConfirmacao({ acao: 'promover', pacote: p })}>
                <Rocket className="h-3.5 w-3.5" /> Pôr em produção
              </BotaoLinhaAcao>
            ) : null}
            {podeExcluir ? (
              <BotaoLinhaAcao tom="perigo" onClick={() => setConfirmacao({ acao: 'retirar', pacote: p })}>
                <ArchiveX className="h-3.5 w-3.5" /> Retirar
              </BotaoLinhaAcao>
            ) : null}
          </div>
        ),
    },
  ];

  async function confirmar() {
    if (!confirmacao) return;
    setErroConfirmacao(null);
    const { acao, pacote } = confirmacao;
    try {
      if (acao === 'promover') await promover.mutateAsync(pacote.id);
      else await retirar.mutateAsync(pacote.id);
      setConfirmacao(null);
      notificar(
        acao === 'promover'
          ? `${ROTULO_ARTEFATO[pacote.artefato]} v${pacote.versao} em produção.`
          : `${ROTULO_ARTEFATO[pacote.artefato]} v${pacote.versao} retirada.`,
      );
    } catch (e) {
      // O motivo aparece dentro do diálogo, onde a pessoa está olhando.
      setErroConfirmacao(extrairMensagemDeErro(e));
    }
  }

  const alvo = confirmacao ? `${ROTULO_ARTEFATO[confirmacao.pacote.artefato]} v${confirmacao.pacote.versao}` : '';

  return (
    <div className="space-y-6 pt-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-gray-600">
          Versão publicada chega só aos computadores de teste. <strong>Pôr em produção</strong> é o que a envia a todos.
        </p>
        {podeIncluir ? (
          <Button onClick={() => setPublicando(true)}>
            <Upload className="h-4 w-4" />
            Publicar versão
          </Button>
        ) : null}
      </div>

      {pacotes.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(pacotes.error)}
        </div>
      ) : null}

      {ARTEFATOS.map((artefato) => (
        <section key={artefato} className="space-y-2">
          <h2 className="text-base font-semibold text-gray-900">{ROTULO_ARTEFATO[artefato]}</h2>
          <Tabela
            colunas={colunas}
            dados={(pacotes.data ?? []).filter((p) => p.artefato === artefato)}
            chaveLinha={(p) => p.id}
            carregando={pacotes.isLoading}
            vazio={
              artefato === 'Extensao'
                ? 'Nenhuma versão da extensão publicada.'
                : 'Nenhuma versão do atualizador publicada — sem ela não há instalador para baixar.'
            }
          />
        </section>
      ))}

      <ModalPublicar aberto={publicando} aoFechar={() => setPublicando(false)} />

      <ConfirmDialog
        aberto={Boolean(confirmacao)}
        titulo={confirmacao?.acao === 'retirar' ? 'Retirar versão' : 'Pôr em produção'}
        mensagem={
          confirmacao?.acao === 'retirar'
            ? `Retirar ${alvo}? Ela deixa de ser entregue. Os computadores que já a receberam continuam com ela — computador não volta de versão; o conserto é publicar uma versão maior.`
            : `Pôr ${alvo} em produção? Todos os computadores autorizados passam a recebê-la em até 10 minutos. Não dá para desfazer nos computadores que já receberam.`
        }
        destrutivo={confirmacao?.acao === 'retirar'}
        rotuloConfirmar={confirmacao?.acao === 'retirar' ? 'Retirar' : 'Pôr em produção'}
        carregando={promover.isPending || retirar.isPending}
        erro={erroConfirmacao}
        aoConfirmar={confirmar}
        aoCancelar={() => {
          setConfirmacao(null);
          setErroConfirmacao(null);
        }}
      />
    </div>
  );
}

function ModalPublicar({ aberto, aoFechar }: { aberto: boolean; aoFechar: () => void }) {
  const publicar = usePublicarPacote();
  const arquivoRef = useRef<HTMLInputElement>(null);
  const [artefato, setArtefato] = useState<Artefato>('Extensao');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [versao, setVersao] = useState('');
  const [notas, setNotas] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function fechar() {
    setArquivo(null);
    setVersao('');
    setNotas('');
    setErro(null);
    if (arquivoRef.current) arquivoRef.current.value = '';
    aoFechar();
  }

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!arquivo) {
      setErro('Escolha o arquivo.');
      return;
    }
    setErro(null);
    try {
      const pacote = await publicar.mutateAsync({ artefato, arquivo, versao, notas });
      notificar(`${ROTULO_ARTEFATO[pacote.artefato]} v${pacote.versao} publicada no canal de teste.`);
      fechar();
    } catch (falha) {
      setErro(extrairMensagemDeErro(falha));
    }
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={fechar}
      titulo="Publicar versão"
      descricao="A versão entra no canal de teste. Depois de conferida num computador de teste, ponha em produção."
    >
      <form onSubmit={enviar} className="space-y-4">
        <Campo label="O que está sendo publicado" htmlFor="pub-artefato" required>
          <Select
            id="pub-artefato"
            value={artefato}
            onChange={(e) => {
              setArtefato(e.target.value as Artefato);
              setArquivo(null);
              if (arquivoRef.current) arquivoRef.current.value = '';
            }}
          >
            <option value="Extensao">Extensão (pacote .zip)</option>
            <option value="Atualizador">Atualizador (executável .exe)</option>
          </Select>
        </Campo>

        <Campo
          label="Arquivo"
          htmlFor="pub-arquivo"
          required
          dica={
            artefato === 'Extensao'
              ? 'O .zip gerado pelo empacotador. A versão é lida do manifest do pacote.'
              : 'O executável gerado pela compilação (não o instalador baixado daqui).'
          }
        >
          <input
            id="pub-arquivo"
            ref={arquivoRef}
            type="file"
            accept={artefato === 'Extensao' ? '.zip,application/zip' : '.exe'}
            className="input"
            onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
          />
        </Campo>

        {artefato === 'Atualizador' ? (
          <Campo label="Versão" htmlFor="pub-versao" required dica="A mesma do executável, no formato 1.2.3.">
            <Input id="pub-versao" value={versao} onChange={(e) => setVersao(e.target.value)} placeholder="0.1.0" />
          </Campo>
        ) : null}

        <Campo label="O que mudou" htmlFor="pub-notas" dica="Opcional. Aparece na lista de versões.">
          <textarea
            id="pub-notas"
            className="input min-h-[72px]"
            maxLength={1000}
            value={notas}
            onChange={(e) => setNotas(e.target.value)}
          />
        </Campo>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variante="secundaria" onClick={fechar} disabled={publicar.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={publicar.isPending}>
            {publicar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
            Publicar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
