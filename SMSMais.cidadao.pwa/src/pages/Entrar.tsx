import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { http } from '@/lib/httpClient';
import { useAuth } from '@/store/auth';
import { ConfirmarCpf } from './ConfirmarCpf';

type RespostaMagic = {
  // null quando o token já foi usado/expirado (não autentica — ver Entrar()).
  token: string | null;
  paciente: { id: string; nome: string; cpf: string } | null;
  destino: string;
  confirmacaoAgendamento: {
    solicitacaoExameId: string;
    titulo: string;
    inicioEm: string | null;
    unidade: string | null;
    confirmadaAgora: boolean;
  } | null;
  // Link que carrega RESULTADO: só vira sessão depois de confirmar o CPF do titular.
  requerConfirmacaoCpf?: boolean;
  tentativasRestantes?: number | null;
};

/** Chave usada por Agendados.tsx para exibir o modal "Agenda confirmada" após o magic link. */
export const CHAVE_CONFIRMACAO_AGENDAMENTO = 'smsmarica-confirmacao-agendamento';

type Fase = 'trocando' | 'cpf' | 'expirado';

/**
 * Magic-link: troca o token do link do WhatsApp por uma sessão.
 *
 * Links de agendamento seguem em 1 clique. Links de RESULTADO (exame liberado, laudo pronto)
 * passam pelo desafio de CPF: o backend não devolve JWT nem destino enquanto o titular não se
 * identificar, e não consome o token. Uso único — se já usado/expirado, cai na home (se já
 * logado) ou no login.
 */
export function Entrar() {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const entrar = useAuth((s) => s.entrar);
  const [fase, setFase] = useState<Fase>('trocando');
  const [enviando, setEnviando] = useState(false);
  const [erroCpf, setErroCpf] = useState<string | null>(null);
  const [tentativas, setTentativas] = useState<number | null>(null);
  const jaRodou = useRef(false);

  /** Conclui a troca bem-sucedida: autentica, limpa a URL e vai ao destino. */
  function concluir(data: RespostaMagic) {
    // Só aqui o token sai da URL — antes disso, o desafio de CPF precisa sobreviver a um
    // recarregamento da página. Manter o token na URL durante o desafio é seguro justamente
    // porque ele deixou de ser suficiente: sem o CPF do titular, não abre nada.
    window.history.replaceState(null, '', '/');
    entrar(data.token!, data.paciente!);
    if (data.confirmacaoAgendamento) {
      sessionStorage.setItem(
        CHAVE_CONFIRMACAO_AGENDAMENTO,
        JSON.stringify(data.confirmacaoAgendamento),
      );
    }
    navigate(data.destino || '/', { replace: true });
  }

  /** Token gasto/inexistente: nunca autentica. Aparelho com sessão abre o app; senão, login. */
  function semSessao(destino?: string) {
    window.history.replaceState(null, '', '/');
    if (useAuth.getState().token) {
      navigate(destino || '/', { replace: true });
      return;
    }
    setFase('expirado');
    setTimeout(() => navigate('/login', { replace: true }), 3200);
  }

  useEffect(() => {
    if (jaRodou.current) return;
    jaRodou.current = true;

    (async () => {
      try {
        const { data } = await http.post<RespostaMagic>('/auth/paciente/magic', { token });

        if (data.requerConfirmacaoCpf) {
          setTentativas(data.tentativasRestantes ?? null);
          setFase('cpf');
          return;
        }
        if (data.token && data.paciente) {
          concluir(data);
          return;
        }
        semSessao(data.destino);
      } catch {
        // Token inexistente/queimado (410) ou erro de rede.
        semSessao();
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  async function confirmarCpf(cpf: string) {
    setEnviando(true);
    setErroCpf(null);
    try {
      const { data } = await http.post<RespostaMagic>('/auth/paciente/magic/confirmar', {
        token,
        cpf,
      });
      if (data.token && data.paciente) {
        concluir(data);
        return;
      }
      // Ainda no desafio = CPF não conferiu. A mensagem nunca revela de quem é o exame.
      setTentativas(data.tentativasRestantes ?? null);
      setErroCpf('Esse CPF não confere.');
    } catch {
      // 410 — o link foi queimado na última tentativa.
      setFase('expirado');
      setTimeout(() => navigate('/login', { replace: true }), 4000);
    } finally {
      setEnviando(false);
    }
  }

  if (fase === 'cpf') {
    return (
      <ConfirmarCpf
        onConfirmar={confirmarCpf}
        enviando={enviando}
        erro={erroCpf}
        tentativasRestantes={tentativas}
      />
    );
  }

  if (fase === 'expirado') {
    return (
      <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col items-center justify-center gap-3 bg-papel px-8 text-center">
        <p className="font-display text-[22px] font-semibold leading-tight text-tinta">
          Este link não vale mais
        </p>
        <p className="max-w-[18rem] text-[16px] leading-relaxed text-tinta-mute">
          Procure a unidade de saúde onde você fez o exame para receber um link novo.
        </p>
      </div>
    );
  }

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-areia px-6 text-center">
      <Loader2 className="h-8 w-8 animate-spin text-marica" />
      <p className="text-sm text-tinta-mute">Entrando…</p>
    </div>
  );
}
