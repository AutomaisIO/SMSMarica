import { useState } from 'react';
import { BellRing, Check } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { useTemConsulta } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { formatarInstante } from '@/shared/lib/datas';

import { ROTULO_STATUS } from '../components/StatusRegulacaoBadge';
import {
  useMarcarNotificacaoVista,
  useNotificacoesRegulacao,
} from '../api/solicitacoesQueries';
import type { EscopoNotificacao, NotificacaoRegulacao } from '../tiposSolicitacao';

/** O texto que a pessoa lê. O nome do enum é do código. */
const ROTULO_TIPO: Record<string, string> = {
  NumeroExterno: 'Número do sistema registrado',
  SituacaoExterna: 'Situação mudou no sistema',
  Devolucao: 'Devolvida à unidade',
  Recusa: 'Recusada pela regulação',
  FalhaEnvio: 'Falha no envio',
  PendenciaAberta: 'Pendência aberta',
};

const CHAVE_ESCOPO = 'smsmarica.regulacao.escopoNotificacoes';

/**
 * O que mudou nas solicitações e ainda não foi visto (plano 05).
 *
 * <p>A lista mostra só os eventos que <b>pedem atenção</b> — o resto da trilha conta a história do
 * caso, mas não é aviso. Misturar tudo faria a tela nascer inútil de tão cheia.</p>
 */
export function NotificacoesRegulacaoPage() {
  const navegar = useNavigate();
  const ehAgente = useTemConsulta('RegulacaoTriagem');

  const [escopo, setEscopo] = useState<EscopoNotificacao>(() => {
    // Preferência por navegador: o agente costuma trabalhar em "todas", a ponta em "minha".
    try {
      return (localStorage.getItem(CHAVE_ESCOPO) as EscopoNotificacao) || 'minha';
    } catch {
      return 'minha';
    }
  });
  const [soNaoVistas, setSoNaoVistas] = useState(true);

  const pagina = useNotificacoesRegulacao(escopo, soNaoVistas);
  const marcarVista = useMarcarNotificacaoVista();

  function trocarEscopo(novo: EscopoNotificacao) {
    setEscopo(novo);
    try {
      localStorage.setItem(CHAVE_ESCOPO, novo);
    } catch {
      // Navegador com armazenamento bloqueado: a escolha vale só nesta sessão.
    }
  }

  const colunas: Coluna<NotificacaoRegulacao>[] = [
    {
      chave: 'quando',
      cabecalho: 'Quando',
      className: 'whitespace-nowrap text-xs text-slate-500',
      ordenar: (n) => n.criadoEm,
      render: (n) => formatarInstante(n.criadoEm),
    },
    {
      chave: 'tipo',
      cabecalho: 'O que aconteceu',
      render: (n) => (
        <div>
          <p className={n.vista ? 'text-slate-600' : 'font-medium text-slate-900'}>
            {ROTULO_TIPO[n.tipo] ?? n.tipo}
          </p>
          {n.de && n.para && (
            <p className="text-xs text-slate-500">
              {ROTULO_STATUS[n.de]} → {ROTULO_STATUS[n.para]}
            </p>
          )}
        </div>
      ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (n) => n.pacienteNome,
      render: (n) => (
        <div className="min-w-0">
          <p className="truncate text-slate-900">{n.pacienteNome}</p>
          <p className="truncate text-xs text-slate-500">{n.procedimento}</p>
        </div>
      ),
    },
    {
      chave: 'unidade',
      cabecalho: 'Unidade',
      ordenar: (n) => n.unidadeSolicitante,
      render: (n) => <span className="text-slate-700">{n.unidadeSolicitante}</span>,
    },
    {
      chave: 'numero',
      cabecalho: 'Número',
      className: 'whitespace-nowrap font-mono text-xs',
      render: (n) => n.numeroExterno ?? `PR-${n.numeroLocal}`,
    },
    {
      chave: 'acao',
      cabecalho: '',
      className: 'whitespace-nowrap',
      render: (n) =>
        n.vista ? (
          <span className="text-xs text-slate-400">vista</span>
        ) : (
          <button
            type="button"
            className="flex items-center gap-1 text-xs text-slate-500 hover:text-slate-900"
            onClick={() => marcarVista.mutate(n.eventoId)}
          >
            <Check className="size-3.5" /> marcar como vista
          </button>
        ),
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-3">
        <BellRing className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Notificações da regulação</h1>
          <p className="text-sm text-slate-600">
            Movimentações das solicitações que ainda não foram vistas.
          </p>
        </div>
      </header>

      <div className="flex flex-wrap items-center gap-2">
        <div className="inline-flex overflow-hidden rounded border border-slate-300 text-sm">
          <button
            type="button"
            onClick={() => trocarEscopo('minha')}
            className={escopo === 'minha' ? 'bg-red-600 px-3 py-1.5 text-white' : 'px-3 py-1.5'}
          >
            Minhas unidades
          </button>
          {/* "Todas" é do agente; a ponta só alcança se a configuração do município abrir. */}
          {ehAgente && (
            <button
              type="button"
              onClick={() => trocarEscopo('todas')}
              className={escopo === 'todas' ? 'bg-red-600 px-3 py-1.5 text-white' : 'px-3 py-1.5'}
            >
              Todas as unidades
            </button>
          )}
        </div>

        <Button variante="secundaria" onClick={() => setSoNaoVistas((v) => !v)}>
          {soNaoVistas ? 'Mostrar também as vistas' : 'Só as não vistas'}
        </Button>
      </div>

      <Tabela
        colunas={colunas}
        dados={pagina.data?.itens ?? []}
        chaveLinha={(n) => n.eventoId}
        carregando={pagina.isLoading}
        aoClicarLinha={(n) => navegar(`/app/regulacao/solicitacoes/${n.solicitacaoId}`)}
        dicaLinha="Clique para abrir a solicitação"
        vazio={soNaoVistas ? 'Nada novo por aqui.' : 'Nenhuma movimentação registrada.'}
        idTabela="regulacao-notificacoes"
      />
    </div>
  );
}
