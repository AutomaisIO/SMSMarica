import { api, pdfUrls } from './api';
import { http } from './httpClient';
import { obterPdfCache, salvarPdfCache } from './pdfCache';
import { useAuth } from '@/store/auth';

/**
 * Sincronização OFFLINE-FIRST: ao entrar no app, baixa em segundo plano tudo do paciente —
 * listas (via api, que já grava o cache local) e os DOCUMENTOS (laudos assinados, anexos e
 * PDFs de imagens) para o cache de PDFs (IndexedDB). Com sinal fraco ou sem plano de dados,
 * o cidadão continua vendo tudo o que já foi sincronizado.
 *
 * Regras: roda 1x por sessão/entrada (por paciente), sequencial (não disputa banda com a
 * tela), pula o que já está cacheado, para se ficar offline e NUNCA quebra nada (best-effort).
 */
let sincronizadoPara: string | null = null;

export function sincronizarTudo(): void {
  const pacienteId = useAuth.getState().paciente?.id;
  if (!pacienteId || sincronizadoPara === pacienteId) return;
  sincronizadoPara = pacienteId;
  void executar(pacienteId);
}

/** Permite re-sincronizar após novo login (logout limpa a marca). */
export function resetarSincronizacao(): void {
  sincronizadoPara = null;
}

async function executar(pacienteId: string): Promise<void> {
  try {
    // 1. Listas (o próprio api.* grava o cache local de dados).
    const [exames, laudos] = await Promise.all([
      api.exames().catch(() => []),
      api.laudos().catch(() => []),
    ]);
    await Promise.all([
      api.agendamentos('exame').catch(() => null),
      api.agendamentos('consulta').catch(() => null),
      api.atendimentos().catch(() => null),
      api.perfil().catch(() => null),
    ]);

    // 2. Documentos, do mais leve/valioso ao mais pesado: laudos → anexos → imagens.
    const fila: { url: string }[] = [
      ...laudos.map((l) => ({ url: pdfUrls.laudo(l.id) })),
      ...exames.flatMap((e) => e.documentos.map((d) => ({ url: pdfUrls.anexo(d.id) }))),
      ...exames.filter((e) => e.temImagens).map((e) => ({ url: pdfUrls.exameImagens(e.id) })),
    ];

    for (const item of fila) {
      // Sessão trocou ou usuário saiu no meio → aborta.
      if (useAuth.getState().paciente?.id !== pacienteId) return;
      if (typeof navigator !== 'undefined' && !navigator.onLine) return; // sem rede → tenta na próxima entrada
      try {
        if (await obterPdfCache(item.url)) continue; // já está no aparelho
        const resp = await http.get(item.url, { responseType: 'arraybuffer' });
        await salvarPdfCache(item.url, resp.data as ArrayBuffer);
      } catch {
        /* um documento falhou (ex.: PACS fora) — segue para o próximo */
      }
    }
  } catch {
    /* sincronização é best-effort — nunca afeta a navegação */
  }
}
