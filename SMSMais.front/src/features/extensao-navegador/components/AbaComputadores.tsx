import { useMemo, useState } from 'react';
import { ArrowRightLeft, ShieldOff } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDefinirCanal,
  useDispositivos,
  usePacotes,
  useRevogarDispositivo,
} from '@/features/extensao-navegador/api/queries';
import {
  compararVersoes,
  emContato,
  ROTULO_CANAL,
  situacaoDoChrome,
  versaoEmVigor,
} from '@/features/extensao-navegador/lib/formatar';
import type { Artefato, Canal, Dispositivo } from '@/features/extensao-navegador/types';

/** Os computadores autorizados, com o que cada um informa de si a cada consulta. */
export function AbaComputadores() {
  const dispositivos = useDispositivos();
  const pacotes = usePacotes();
  const definirCanal = useDefinirCanal();
  const revogar = useRevogarDispositivo();
  const podeEditar = usePermissao('ExtensaoNavegador', 'Edicao');
  const podeExcluir = usePermissao('ExtensaoNavegador', 'Exclusao');
  const [mostrarRevogados, setMostrarRevogados] = useState(false);
  const [paraRevogar, setParaRevogar] = useState<Dispositivo | null>(null);
  const [erroRevogar, setErroRevogar] = useState<string | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const visiveis = useMemo(
    () => (dispositivos.data ?? []).filter((d) => mostrarRevogados || !d.revogadoEm),
    [dispositivos.data, mostrarRevogados],
  );

  /** A versão do computador está atrás da que o canal dele recebe? */
  function atrasada(versao: string | null, artefato: Artefato, canal: Canal): boolean {
    const vigente = versaoEmVigor(pacotes.data ?? [], artefato, canal);
    return Boolean(versao && vigente && compararVersoes(versao, vigente) < 0);
  }

  function versao(d: Dispositivo, valor: string | null, artefato: Artefato) {
    if (!valor) return <span className="text-xs text-gray-400">—</span>;
    return (
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="tabular-nums">v{valor}</span>
        {!d.revogadoEm && atrasada(valor, artefato, d.canal) ? (
          <span className="badge badge-warning">Desatualizada</span>
        ) : null}
      </div>
    );
  }

  const colunas: Coluna<Dispositivo>[] = [
    {
      chave: 'computador',
      cabecalho: 'Computador',
      ordenar: (d) => d.computador,
      render: (d) => (
        <div>
          <span className="font-medium text-gray-900">{d.computador}</span>
          <p className="whitespace-nowrap text-xs text-gray-500">
            Autorizado em {formatarInstante(d.autorizadoEm)}
            {d.autorizadoPorNome ? ` por ${d.autorizadoPorNome}` : ''}
          </p>
        </div>
      ),
    },
    {
      chave: 'canal',
      cabecalho: 'Canal',
      ordenar: (d) => d.canal,
      render: (d) =>
        d.revogadoEm ? (
          <span className="badge badge-error">Revogado</span>
        ) : (
          <span className={d.canal === 'Teste' ? 'badge badge-warning' : 'badge badge-gray'}>{ROTULO_CANAL[d.canal]}</span>
        ),
    },
    { chave: 'extensao', cabecalho: 'Extensão', render: (d) => versao(d, d.versaoExtensao, 'Extensao') },
    { chave: 'atualizador', cabecalho: 'Atualizador', render: (d) => versao(d, d.versaoAtualizador, 'Atualizador') },
    {
      chave: 'chrome',
      cabecalho: 'No Chrome',
      render: (d) => {
        const s = situacaoDoChrome(d.situacaoChrome);
        return s.atencao ? (
          <span className="badge badge-error whitespace-nowrap">{s.rotulo}</span>
        ) : (
          <span className="whitespace-nowrap text-sm">{s.rotulo}</span>
        );
      },
    },
    {
      chave: 'contato',
      cabecalho: 'Último contato',
      ordenar: (d) => d.ultimoContatoEm,
      render: (d) => (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="whitespace-nowrap tabular-nums">{formatarInstante(d.ultimoContatoEm)}</span>
          {!d.revogadoEm && !emContato(d) ? <span className="badge badge-gray">Sem contato</span> : null}
        </div>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (d) =>
        d.revogadoEm ? null : (
          <div className="flex items-center justify-end gap-1">
            {podeEditar ? (
              <BotaoLinhaAcao onClick={() => trocarCanal(d)} disabled={definirCanal.isPending} className="whitespace-nowrap">
                <ArrowRightLeft className="h-3.5 w-3.5" />
                {d.canal === 'Teste' ? 'Passar para produção' : 'Passar para teste'}
              </BotaoLinhaAcao>
            ) : null}
            {podeExcluir ? (
              <BotaoLinhaAcao tom="perigo" onClick={() => setParaRevogar(d)} className="whitespace-nowrap">
                <ShieldOff className="h-3.5 w-3.5" /> Revogar
              </BotaoLinhaAcao>
            ) : null}
          </div>
        ),
    },
  ];

  async function trocarCanal(d: Dispositivo) {
    setErroAcao(null);
    try {
      await definirCanal.mutateAsync({ id: d.id, canal: d.canal === 'Teste' ? 'Prod' : 'Teste' });
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  async function confirmarRevogar() {
    if (!paraRevogar) return;
    setErroRevogar(null);
    try {
      await revogar.mutateAsync(paraRevogar.id);
      setParaRevogar(null);
    } catch (e) {
      setErroRevogar(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-4 pt-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" checked={mostrarRevogados} onChange={(e) => setMostrarRevogados(e.target.checked)} />
        Mostrar revogados
      </label>

      {dispositivos.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(dispositivos.error)}
        </div>
      ) : null}
      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erroAcao}</div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={visiveis}
        chaveLinha={(d) => d.id}
        carregando={dispositivos.isLoading}
        vazio="Nenhum computador autorizado ainda. Quem baixa e executa o instalador aparece aqui."
      />

      <ConfirmDialog
        aberto={Boolean(paraRevogar)}
        titulo="Revogar computador"
        mensagem={
          paraRevogar
            ? `Revogar "${paraRevogar.computador}"? Ele deixa de receber a extensão e as atualizações. A extensão que já está nele continua lá; para voltar a receber, é preciso autorizar de novo.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Revogar"
        carregando={revogar.isPending}
        erro={erroRevogar}
        aoConfirmar={confirmarRevogar}
        aoCancelar={() => {
          setParaRevogar(null);
          setErroRevogar(null);
        }}
      />
    </div>
  );
}
