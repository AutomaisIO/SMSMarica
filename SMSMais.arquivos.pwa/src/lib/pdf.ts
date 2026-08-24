// Página A4 em pontos PDF (72 dpi): 210mm x 297mm.
const A4_LARGURA = 595.28;
const A4_ALTURA = 841.89;

/**
 * Monta um único PDF a partir das páginas digitalizadas (dataURLs JPEG/PNG).
 * Cada imagem entra centralizada em uma página A4 (retrato ou paisagem conforme
 * a proporção da foto), preservando o aspecto. Retorna o Blob pronto p/ upload.
 *
 * `pdf-lib` é importado sob demanda (só ao "Concluir", que já mostra progresso),
 * mantendo o bundle inicial leve no celular.
 */
export async function montarPdf(paginas: string[]): Promise<Blob> {
  const { PDFDocument } = await import('pdf-lib');
  const pdf = await PDFDocument.create();

  for (const dataUrl of paginas) {
    const bytes = dataUrlParaBytes(dataUrl);
    const ehPng = dataUrl.startsWith('data:image/png');
    const img = ehPng ? await pdf.embedPng(bytes) : await pdf.embedJpg(bytes);

    const retrato = img.height >= img.width;
    const larguraPg = retrato ? A4_LARGURA : A4_ALTURA;
    const alturaPg = retrato ? A4_ALTURA : A4_LARGURA;

    const pagina = pdf.addPage([larguraPg, alturaPg]);
    const escala = Math.min(larguraPg / img.width, alturaPg / img.height);
    const w = img.width * escala;
    const h = img.height * escala;
    pagina.drawImage(img, {
      x: (larguraPg - w) / 2,
      y: (alturaPg - h) / 2,
      width: w,
      height: h,
    });
  }

  const bytes = await pdf.save();
  // Cópia em um Uint8Array com ArrayBuffer concreto (BlobPart válido sob os
  // typed arrays genéricos do TS 5.7+; pdf.save() devolve Uint8Array<ArrayBufferLike>).
  return new Blob([new Uint8Array(bytes)], { type: 'application/pdf' });
}

function dataUrlParaBytes(dataUrl: string): Uint8Array {
  const base64 = dataUrl.split(',')[1] ?? '';
  const binario = atob(base64);
  const arr = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) {
    arr[i] = binario.charCodeAt(i);
  }
  return arr;
}
