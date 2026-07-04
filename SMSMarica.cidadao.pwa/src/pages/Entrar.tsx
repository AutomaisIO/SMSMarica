import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { http } from '@/lib/httpClient';
import { useAuth } from '@/store/auth';

type RespostaMagic = {
  token: string;
  paciente: { id: string; nome: string; cpf: string };
  destino: string;
  confirmacaoAgendamento: {
    solicitacaoExameId: string;
    titulo: string;
    inicioEm: string | null;
    unidade: string | null;
    confirmadaAgora: boolean;
  } | null;
};

/** Chave usada por Agendados.tsx para exibir o modal "Agenda confirmada" após o magic link. */
export const CHAVE_CONFIRMACAO_AGENDAMENTO = 'smsmarica-confirmacao-agendamento';

/**
 * Magic-link: troca o token do link do WhatsApp por uma sessão (login em 1 clique).
 * Uso único — se já usado/expirado, cai na home (se já logado) ou no login.
 * O token é removido da URL assim que autentica (não fica no histórico/instalação).
 */
export function Entrar() {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const entrar = useAuth((s) => s.entrar);
  const [erro, setErro] = useState(false);
  const jaRodou = useRef(false);

  useEffect(() => {
    if (jaRodou.current) return;
    jaRodou.current = true;

    (async () => {
      try {
        const { data } = await http.post<RespostaMagic>('/auth/paciente/magic', { token });
        entrar(data.token, data.paciente);
        // O uso do link já confirmou a presença no exame — a tela de destino mostra o modal.
        if (data.confirmacaoAgendamento) {
          sessionStorage.setItem(
            CHAVE_CONFIRMACAO_AGENDAMENTO,
            JSON.stringify(data.confirmacaoAgendamento),
          );
        }
        // Remove o token da URL antes de navegar (não fica no histórico nem numa instalação).
        window.history.replaceState(null, '', '/');
        navigate(data.destino || '/', { replace: true });
      } catch {
        // Token inválido/usado/expirado: se já há sessão, vai pra home; senão, login.
        if (useAuth.getState().token) {
          navigate('/', { replace: true });
        } else {
          setErro(true);
          setTimeout(() => navigate('/login', { replace: true }), 2200);
        }
      }
    })();
  }, [token, entrar, navigate]);

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-areia px-6 text-center">
      <Loader2 className="h-8 w-8 animate-spin text-marica" />
      <p className="text-sm text-tinta-mute">
        {erro ? 'Este link expirou. Redirecionando para o login…' : 'Entrando…'}
      </p>
    </div>
  );
}
