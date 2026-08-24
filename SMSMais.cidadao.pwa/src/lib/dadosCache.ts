import { useAuth } from '@/store/auth';

/**
 * Cache local dos DADOS do cidadão (listas/JSON) para acesso OFFLINE — sinal fraco e falta de
 * plano de dados são realidade do público. REGRA: a rede vem SEMPRE primeiro (nunca servimos
 * cache estando online); o cache só responde quando a rede falha, e o AppShell mostra o banner
 * "offline". Chaves incluem o id do paciente (aparelho compartilhado não vaza dados entre
 * contas) e tudo é limpo no logout (LGPD).
 */
const PREFIXO = 'sms.dados.';

function chaveCompleta(chave: string): string | null {
  const pacienteId = useAuth.getState().paciente?.id;
  return pacienteId ? `${PREFIXO}${pacienteId}.${chave}` : null;
}

export function salvarDados<T>(chave: string, dados: T): void {
  const k = chaveCompleta(chave);
  if (!k) return;
  try {
    localStorage.setItem(k, JSON.stringify({ em: Date.now(), dados }));
  } catch {
    /* quota cheia — cache é best-effort */
  }
}

export function lerDados<T>(chave: string): T | null {
  const k = chaveCompleta(chave);
  if (!k) return null;
  try {
    const bruto = localStorage.getItem(k);
    if (!bruto) return null;
    return (JSON.parse(bruto) as { dados: T }).dados;
  } catch {
    return null;
  }
}

/**
 * Rede-primeiro com fallback offline: tenta a rede (e atualiza o cache); se falhar E houver
 * cache local, devolve o cache em vez de quebrar a tela. Sem cache, o erro segue normal.
 */
export async function comCacheLocal<T>(chave: string, buscar: () => Promise<T>): Promise<T> {
  try {
    const dados = await buscar();
    salvarDados(chave, dados);
    return dados;
  } catch (e) {
    const cache = lerDados<T>(chave);
    if (cache !== null) return cache;
    throw e;
  }
}

/** Remove TODOS os dados locais (todas as contas) — chamado no logout (LGPD). */
export function limparDadosLocais(): void {
  try {
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const k = localStorage.key(i);
      if (k?.startsWith(PREFIXO)) localStorage.removeItem(k);
    }
  } catch {
    /* best-effort */
  }
}
