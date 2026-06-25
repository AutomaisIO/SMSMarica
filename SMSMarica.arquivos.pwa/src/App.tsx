import { useCallback, useEffect, useState } from 'react';
import { api, type SessaoValida } from '@/lib/api';
import { useSessao } from '@/store/sessao';
import { AppShell } from '@/components/AppShell';
import { Spinner } from '@/components/ui';
import { Home } from '@/pages/Home';
import { NovoDocumento } from '@/pages/NovoDocumento';
import { SemToken } from '@/pages/SemToken';

type Estado =
  | { fase: 'validando' }
  | { fase: 'invalido' }
  | { fase: 'valido'; token: string; sessao: SessaoValida };

export function App() {
  const definirToken = useSessao((s) => s.definirToken);
  const [estado, setEstado] = useState<Estado>({ fase: 'validando' });
  const [vista, setVista] = useState<'home' | 'novo'>('home');

  // Valida um token na API (reaproveitado pela carga inicial via ?t= e pelo leitor
  // de QR dentro do app). Retorna um "cancelador" para descartar respostas tardias.
  const validarToken = useCallback((token: string) => {
    let cancelado = false;
    setEstado({ fase: 'validando' });
    api
      .validarSessao(token)
      .then((sessao) => {
        if (!cancelado) setEstado({ fase: 'valido', token, sessao });
      })
      .catch(() => {
        if (cancelado) return;
        // Token inválido/expirado/revogado — limpa para não insistir num código morto.
        // (A mensagem técnica não é exposta ao cidadão; a tela orienta a reler o QR.)
        useSessao.getState().limpar();
        setEstado({ fase: 'invalido' });
      });
    return () => {
      cancelado = true;
    };
  }, []);

  // Resultado do leitor de QR dentro do app: grava o token e revalida a sessão.
  const aoLerToken = useCallback(
    (token: string) => {
      definirToken(token);
      validarToken(token);
    },
    [definirToken, validarToken],
  );

  // Carga inicial: token do QR vem na URL (?t=TOKEN). Damos prioridade ao da URL;
  // se não houver, reaproveitamos o persistido (multi-uso dentro do TTL).
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const tokenUrl = params.get('t')?.trim() || null;

    if (tokenUrl) {
      definirToken(tokenUrl);
      // Remove o token da barra de endereço (não fica exposto no histórico/compartilhamento).
      const url = new URL(window.location.href);
      url.searchParams.delete('t');
      window.history.replaceState({}, '', url.pathname + url.search + url.hash);
    }

    // Sem token na URL: só reaproveita o persistido se for recente (~20 min, acima do TTL
    // do servidor). Evita "fixar" o código de uma sessão antiga ao reabrir o app instalado;
    // o servidor revalida o TTL de qualquer forma.
    const persistido = useSessao.getState();
    const recente =
      persistido.definidoEm != null && Date.now() - persistido.definidoEm < 20 * 60 * 1000;
    if (!tokenUrl && persistido.token && !recente) {
      persistido.limpar();
    }
    const efetivo = tokenUrl || (recente ? persistido.token : null);

    if (!efetivo) {
      setEstado({ fase: 'invalido' });
      return;
    }

    return validarToken(efetivo);
  }, [definirToken, validarToken]);

  if (estado.fase === 'validando') {
    return (
      <AppShell subtitulo="Validando acesso">
        <div className="flex flex-col items-center gap-3 py-24 text-center">
          <Spinner className="h-8 w-8" />
          <p className="text-sm text-tinta-mute">Validando o código do exame…</p>
        </div>
      </AppShell>
    );
  }

  if (estado.fase === 'invalido') {
    return <SemToken aoLerToken={aoLerToken} />;
  }

  if (vista === 'novo') {
    return <NovoDocumento token={estado.token} aoSair={() => setVista('home')} />;
  }

  return <Home sessao={estado.sessao} aoNovo={() => setVista('novo')} />;
}
