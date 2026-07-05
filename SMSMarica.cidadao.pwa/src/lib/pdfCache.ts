/**
 * Cache local de PDFs já baixados (IndexedDB), chaveado pela URL do documento.
 * Documentos do cidadão são imutáveis (laudo assinado é byte-estável; o PDF de imagens é
 * gerado e cacheado no servidor pelo StudyUID) — então, uma vez baixado, reabrir usa o
 * arquivo salvo no aparelho, sem rede. Mantém no máx. MAX itens (remove os mais antigos).
 */
const DB_NOME = 'smsmarica-pdf';
const STORE = 'pdf';
const MAX = 40;

function abrir(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NOME, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, { keyPath: 'url' });
        store.createIndex('criadoEm', 'criadoEm');
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

export async function obterPdfCache(url: string): Promise<ArrayBuffer | null> {
  try {
    const db = await abrir();
    return await new Promise((resolve) => {
      const req = db.transaction(STORE, 'readonly').objectStore(STORE).get(url);
      req.onsuccess = () => resolve((req.result?.data as ArrayBuffer) ?? null);
      req.onerror = () => resolve(null);
    });
  } catch {
    return null;
  }
}

export async function salvarPdfCache(url: string, data: ArrayBuffer): Promise<void> {
  try {
    const db = await abrir();
    await new Promise<void>((resolve) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put({ url, data, criadoEm: Date.now() });
      tx.oncomplete = () => resolve();
      tx.onerror = () => resolve();
    });
    await podar(db);
  } catch {
    /* cache é best-effort — falha não impede exibir */
  }
}

/** Apaga TODOS os PDFs locais — chamado no logout (LGPD, aparelho compartilhado). */
export async function limparPdfCache(): Promise<void> {
  try {
    const db = await abrir();
    await new Promise<void>((resolve) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).clear();
      tx.oncomplete = () => resolve();
      tx.onerror = () => resolve();
    });
  } catch {
    /* best-effort */
  }
}

/** Remove os itens mais antigos quando passa de MAX. */
async function podar(db: IDBDatabase): Promise<void> {
  await new Promise<void>((resolve) => {
    const tx = db.transaction(STORE, 'readwrite');
    const store = tx.objectStore(STORE);
    const cont = store.count();
    cont.onsuccess = () => {
      const excesso = cont.result - MAX;
      if (excesso <= 0) {
        resolve();
        return;
      }
      let removidos = 0;
      const cursor = store.index('criadoEm').openCursor();
      cursor.onsuccess = () => {
        const c = cursor.result;
        if (c && removidos < excesso) {
          c.delete();
          removidos++;
          c.continue();
        } else {
          resolve();
        }
      };
      cursor.onerror = () => resolve();
    };
    cont.onerror = () => resolve();
  });
}
