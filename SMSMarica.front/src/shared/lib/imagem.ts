/**
 * Recorta uma imagem com base na área retornada pelo react-easy-crop e
 * redimensiona para um quadrado de `tamanhoFinal` px. Saída em base64
 * JPEG com a qualidade informada (default 0.85 — equivalente a foto de
 * perfil "estilo WhatsApp": legível em qualquer tamanho de avatar e
 * compacta o bastante para caber tranquilamente numa coluna text).
 */
export async function recortarParaBase64(
  imagemSrc: string,
  area: { x: number; y: number; width: number; height: number },
  tamanhoFinal = 512,
  qualidade = 0.85,
): Promise<string> {
  const img = await carregarImagem(imagemSrc);
  const canvas = document.createElement('canvas');
  canvas.width = tamanhoFinal;
  canvas.height = tamanhoFinal;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas indisponível.');

  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(
    img,
    area.x, area.y, area.width, area.height,
    0, 0, tamanhoFinal, tamanhoFinal,
  );

  return canvas.toDataURL('image/jpeg', qualidade);
}

function carregarImagem(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = (e) => reject(new Error('Falha ao carregar imagem.' + (typeof e === 'string' ? ` ${e}` : '')));
    img.src = src;
  });
}

/** Lê um File como dataURL (base64) para alimentar o react-easy-crop. */
export function arquivoParaDataUrl(arquivo: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error('Falha ao ler arquivo.'));
    reader.readAsDataURL(arquivo);
  });
}
