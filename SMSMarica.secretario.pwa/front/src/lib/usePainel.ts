import { useCallback, useEffect, useRef, useState } from 'react';
import type { Painel } from '@/types/painel';
import mockJson from '@/mock/painel.json';

export interface EstadoPainel {
  dados: Painel | null;
  /** Dados de demonstração (contrato) — sinalizado na UI, nunca escondido. */
  usandoMock: boolean;
  /** Última tentativa de rede falhou (os dados exibidos podem estar velhos). */
  erroRede: boolean;
  /** Primeira carga ainda em andamento (skeleton). */
  carregandoInicial: boolean;
  recarregar: () => void;
}

const INTERVALO_POLL_MS = 60_000;

function mockForcado(): boolean {
  return new URLSearchParams(window.location.search).get('mock') === '1';
}

/**
 * Mock com os carimbos de atualização rebatidos para agora, para o layout de
 * demonstração ficar coerente. A UI marca "dados de exemplo" sempre que o mock
 * está ativo — frescor de verdade só com o back respondendo.
 */
function mockRebaseado(): Painel {
  const base = mockJson as unknown as Painel;
  const agora = new Date().toISOString();
  return {
    ...base,
    geradoEm: agora,
    oracle: { ...base.oracle, ultimaAtualizacaoOk: agora },
    agora: base.agora ? { ...base.agora, atualizadoEm: agora } : base.agora,
    atendimentos: base.atendimentos ? { ...base.atendimentos, atualizadoEm: agora } : base.atendimentos,
    internacoes: base.internacoes ? { ...base.internacoes, atualizadoEm: agora } : base.internacoes,
    esperaPorCor: base.esperaPorCor ? { ...base.esperaPorCor, atualizadoEm: agora } : base.esperaPorCor,
  };
}

/**
 * GET /api/painel: poll a cada 60 s + refetch ao voltar para a aba.
 * Fallback para o mock do contrato quando o fetch falha em DEV ou com ?mock=1.
 */
export function usePainel(): EstadoPainel {
  const [dados, setDados] = useState<Painel | null>(null);
  const [usandoMock, setUsandoMock] = useState(false);
  const [erroRede, setErroRede] = useState(false);
  const [carregandoInicial, setCarregandoInicial] = useState(true);
  const temDadosReais = useRef(false);

  const buscar = useCallback(async () => {
    if (mockForcado()) {
      setDados(mockRebaseado());
      setUsandoMock(true);
      setErroRede(false);
      setCarregandoInicial(false);
      return;
    }
    try {
      const resposta = await fetch('/api/painel', {
        cache: 'no-store',
        headers: { Accept: 'application/json' },
      });
      if (!resposta.ok) throw new Error(`HTTP ${resposta.status}`);
      const json = (await resposta.json()) as Painel;
      // Cold start: o back pode responder só com a seção "agora" — é payload válido.
      if (!json || !json.agora) throw new Error('payload inesperado');
      temDadosReais.current = true;
      setDados(json);
      setUsandoMock(false);
      setErroRede(false);
    } catch {
      setErroRede(true);
      // Em DEV, sem back no ar, o contrato entra como demonstração — mas nunca
      // por cima de dados reais já recebidos.
      if (import.meta.env.DEV && !temDadosReais.current) {
        setDados(mockRebaseado());
        setUsandoMock(true);
        setErroRede(false);
      }
    } finally {
      setCarregandoInicial(false);
    }
  }, []);

  useEffect(() => {
    void buscar();
    const intervalo = window.setInterval(() => void buscar(), INTERVALO_POLL_MS);
    const aoMudarVisibilidade = () => {
      if (document.visibilityState === 'visible') void buscar();
    };
    document.addEventListener('visibilitychange', aoMudarVisibilidade);
    return () => {
      window.clearInterval(intervalo);
      document.removeEventListener('visibilitychange', aoMudarVisibilidade);
    };
  }, [buscar]);

  return { dados, usandoMock, erroRede, carregandoInicial, recarregar: () => void buscar() };
}
