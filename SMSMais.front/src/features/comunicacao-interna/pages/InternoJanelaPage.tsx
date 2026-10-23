import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTemConsulta } from '@/shared/auth/authStore';
import { obterPreferencias } from '@/shared/auth/preferenciasApi';
import { useResumoInterno } from '@/features/comunicacao-interna/api/queries';
import { useInternoHub } from '@/features/comunicacao-interna/hooks/useInternoHub';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import { PainelInterno } from '@/features/comunicacao-interna/components/PainelInterno';

/**
 * Comunicação interna em JANELA SEPARADA do navegador (aberta pelo botão do painel) — sem o menu
 * do sistema; minimizar/fechar são os controles nativos. Mesmo conteúdo do painel do canto. Com
 * ela aberta, é ESTA janela que toca o som e notifica (a principal só tenta trazê-la para frente).
 */
export function InternoJanelaPage() {
  const podeVer = useTemConsulta('ComunicacaoInterna');
  const pulo = useInterno((s) => s.pulo);
  const [params] = useSearchParams();
  const raizRef = useRef<HTMLDivElement>(null);

  useInternoHub(podeVer);
  useResumoInterno(podeVer);

  // A janela solta não tem o Layout (que hidrata as preferências): som e "abrir sozinho" daqui.
  useEffect(() => {
    let ativo = true;
    obterPreferencias()
      .then((p) => {
        if (!ativo) return;
        const s = useInterno.getState();
        s.setSom(!(p.somComunicacaoInternaSilenciado ?? false));
        s.setAbrirSozinho(!(p.comunicacaoInternaNaoAbrirSozinho ?? false));
      })
      .catch(() => {
        /* offline/erro — segue com o padrão */
      });
    return () => {
      ativo = false;
    };
  }, []);

  useEffect(() => {
    document.title = 'Comunicação interna';
    const inicial = params.get('conversa');
    if (inicial) useInterno.getState().setConversaAtiva(inicial);
    // Intencional: só na montagem — a troca posterior vem pelo BroadcastChannel (useInternoHub).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Mensagem chegou com a janela aberta: um pulinho no conteúdo chama o olho.
  useEffect(() => {
    if (pulo === 0 || !raizRef.current || typeof raizRef.current.animate !== 'function') return;
    raizRef.current.animate(
      [{ transform: 'translateY(0)' }, { transform: 'translateY(-8px)', offset: 0.35 }, { transform: 'translateY(0)' }],
      { duration: 450, easing: 'ease-out' },
    );
  }, [pulo]);

  if (!podeVer) {
    return (
      <div className="flex h-screen items-center justify-center text-sm text-gray-500">
        Sem acesso à Comunicação interna.
      </div>
    );
  }

  return (
    <div ref={raizRef} className="h-screen overflow-hidden">
      <PainelInterno modoJanela />
    </div>
  );
}
