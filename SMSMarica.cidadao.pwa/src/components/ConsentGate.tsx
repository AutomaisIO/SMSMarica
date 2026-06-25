import { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { api, type ConsentimentoStatus } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { useAuth } from '@/store/auth';
import { PrimaryButton } from '@/components/ui';

type Estado = 'carregando' | 'pendente' | 'ok';

/**
 * Gate de consentimento LGPD: antes de liberar o app, confere se o cidadão já aceitou
 * o termo vigente. Se não, mostra o termo em tela cheia (bloqueante) — só libera após
 * o aceite. O backend também barra cada requisição sem consentimento (defesa em profundidade).
 */
export function ConsentGate({ children }: { children: React.ReactNode }) {
  const sair = useAuth((s) => s.sair);
  const [estado, setEstado] = useState<Estado>('carregando');
  const [termo, setTermo] = useState<ConsentimentoStatus | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    setErro(null);
    setEstado('carregando');
    try {
      const s = await api.consentimento();
      setTermo(s);
      setEstado(s.aceito ? 'ok' : 'pendente');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
      setEstado('pendente'); // na dúvida, NÃO libera o app
    }
  }

  useEffect(() => {
    void carregar();
  }, []);

  async function aceitar() {
    setErro(null);
    setEnviando(true);
    try {
      await api.aceitarConsentimento();
      setEstado('ok');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  function recusar() {
    void api.logout().catch(() => {});
    sair();
  }

  if (estado === 'ok') return <>{children}</>;

  if (estado === 'carregando') {
    return <div className="min-h-dvh bg-areia" />;
  }

  // Título = 1ª linha; resto = parágrafos separados por linha em branco.
  const linhas = (termo?.texto ?? '').split('\n\n');
  const titulo = linhas[0] ?? 'Termo de Consentimento';
  const paragrafos = linhas.slice(1);

  return (
    <div className="flex min-h-dvh flex-col bg-areia">
      <header className="flex items-center gap-3 bg-marica px-5 py-4 text-white">
        <ShieldCheck className="h-6 w-6 shrink-0" />
        <div className="leading-tight">
          <p className="font-display text-[15px] font-semibold">Privacidade e seus dados</p>
          <p className="text-[11px] text-white/80">Leia e confirme para continuar</p>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto px-5 py-5">
        <h1 className="mb-3 font-display text-lg font-semibold text-tinta">{titulo}</h1>
        <div className="space-y-3 text-sm leading-relaxed text-tinta">
          {paragrafos.map((p, i) => (
            <p key={i}>{p}</p>
          ))}
        </div>
      </main>

      <footer className="space-y-3 border-t border-areia bg-white px-5 py-4">
        {erro ? <p className="text-center text-sm text-marica">{erro}</p> : null}
        <PrimaryButton type="button" onClick={aceitar} carregando={enviando}>
          Li e concordo
        </PrimaryButton>
        <button
          type="button"
          onClick={recusar}
          className="block w-full text-center text-sm font-medium text-tinta-mute underline"
        >
          Não concordo e quero sair
        </button>
      </footer>
    </div>
  );
}
