import { BellPlus, ExternalLink, HelpCircle, MessagesSquare, Minus, Volume2, VolumeX, Zap, ZapOff } from 'lucide-react';
import { pedirNavegacaoJanelaPrincipal } from '@/shared/lib/janela';
import { cn } from '@/shared/lib/cn';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import {
  abrirJanelaInterna,
  definirAbrirSozinho,
  definirSomInterno,
} from '@/features/comunicacao-interna/lib/janelaInterna';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import { ListaConversasInternas } from '@/features/comunicacao-interna/components/ListaConversasInternas';
import { NovaConversaInterna } from '@/features/comunicacao-interna/components/NovaConversaInterna';
import { ThreadInterna } from '@/features/comunicacao-interna/components/ThreadInterna';

/** Slug do artigo do manual (o "?" do cabeçalho). */
export const ARTIGO_MANUAL_INTERNO = 'comunicacao-interna';

type Props = {
  /** Dentro da janela solta: sem fechar/abrir em janela, e o "?" abre o manual na principal. */
  modoJanela?: boolean;
};

/**
 * O conteúdo da Comunicação interna — o MESMO no painel encaixado no canto e na janela solta:
 * cabeçalho laranja (som, abrir sozinho, notificação do Windows, janela separada, manual) e as
 * duas colunas (conversas | conversa aberta), ou a escolha de unidade/pessoa da Nova conversa.
 */
export function PainelInterno({ modoJanela = false }: Props) {
  const conversaAtivaId = useInterno((s) => s.conversaAtivaId);
  const modo = useInterno((s) => s.modo);
  const somAtivo = useInterno((s) => s.somAtivo);
  const abrirSozinho = useInterno((s) => s.abrirSozinho);
  const { permissao, solicitar } = useNotificacoesNavegador();
  const st = useInterno.getState;

  function abrirEmJanela() {
    abrirJanelaInterna(conversaAtivaId);
    st().fecharPainel();
  }

  return (
    <div className="flex h-full min-h-0 flex-col overflow-hidden bg-white">
      <div className="flex items-center gap-1 bg-orange-500 px-3 py-2 text-white">
        <MessagesSquare className="h-4 w-4 shrink-0" />
        <span className="min-w-0 flex-1 truncate text-sm font-semibold">Comunicação interna</span>

        {permissao === 'default' && (
          <BotaoCabecalho
            onClick={() => void solicitar()}
            titulo="Ativar as notificações do Windows (avisam mesmo com o painel minimizado)"
          >
            <BellPlus className="h-4 w-4" />
          </BotaoCabecalho>
        )}
        <BotaoCabecalho
          onClick={() => definirSomInterno(!somAtivo)}
          titulo={somAtivo ? 'Som ligado — clique para silenciar' : 'Som desligado — clique para ligar'}
          apagado={!somAtivo}
        >
          {somAtivo ? <Volume2 className="h-4 w-4" /> : <VolumeX className="h-4 w-4" />}
        </BotaoCabecalho>
        <BotaoCabecalho
          onClick={() => definirAbrirSozinho(!abrirSozinho)}
          titulo={
            abrirSozinho
              ? 'Abre sozinho quando chega mensagem — clique para não abrir'
              : 'Não abre sozinho (só o balão avisa) — clique para abrir quando chegar mensagem'
          }
          apagado={!abrirSozinho}
        >
          {abrirSozinho ? <Zap className="h-4 w-4" /> : <ZapOff className="h-4 w-4" />}
        </BotaoCabecalho>

        {modoJanela ? (
          <BotaoCabecalho
            onClick={() => pedirNavegacaoJanelaPrincipal(`/app/manual/${ARTIGO_MANUAL_INTERNO}`)}
            titulo="Como funciona (abre o manual na janela principal)"
          >
            <HelpCircle className="h-4 w-4" />
          </BotaoCabecalho>
        ) : (
          <>
            <AjudaManual
              artigo={ARTIGO_MANUAL_INTERNO}
              className="text-white/80 hover:bg-white/15 hover:text-white focus:ring-white"
            />
            <BotaoCabecalho onClick={abrirEmJanela} titulo="Abrir em janela separada">
              <ExternalLink className="h-4 w-4" />
            </BotaoCabecalho>
            <BotaoCabecalho onClick={() => st().fecharPainel()} titulo="Minimizar (o balão laranja continua avisando)">
              <Minus className="h-4 w-4" />
            </BotaoCabecalho>
          </>
        )}
      </div>

      {permissao === 'denied' && (
        <p className="bg-amber-50 px-3 py-1 text-[11px] text-amber-800">
          O navegador bloqueou as notificações deste site — o aviso fica só no painel e no som.
        </p>
      )}

      {modo === 'nova' ? (
        <NovaConversaInterna aoAbrir={(id) => st().setConversaAtiva(id)} aoVoltar={() => st().setModo('lista')} />
      ) : (
        <div className="flex min-h-0 flex-1">
          <div
            className={cn(
              'min-h-0 border-r border-gray-200 sm:w-72 sm:shrink-0',
              conversaAtivaId ? 'hidden sm:block' : 'flex-1 sm:flex-none',
            )}
          >
            <ListaConversasInternas
              conversaAtivaId={conversaAtivaId}
              aoSelecionar={(id, alvo) => st().setConversaAtiva(id, alvo ?? null)}
              aoNova={() => st().setModo('nova')}
            />
          </div>
          <div className={cn('min-h-0 min-w-0 flex-1', conversaAtivaId ? 'block' : 'hidden sm:block')}>
            {conversaAtivaId ? (
              <ThreadInterna
                key={conversaAtivaId}
                conversaId={conversaAtivaId}
                aoVoltar={() => st().setConversaAtiva(null)}
              />
            ) : (
              <div className="flex h-full flex-col items-center justify-center gap-2 bg-[#faf7f4] p-6 text-center text-sm text-gray-400">
                <MessagesSquare className="h-10 w-10 text-orange-200" />
                Escolha uma conversa ou clique em <b className="text-gray-500">Nova</b> para falar com outra unidade.
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function BotaoCabecalho({
  onClick,
  titulo,
  apagado,
  children,
}: {
  onClick: () => void;
  titulo: string;
  apagado?: boolean;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={titulo}
      aria-label={titulo}
      className={cn('rounded-md p-1.5 hover:bg-white/15', apagado && 'text-white/60')}
    >
      {children}
    </button>
  );
}
