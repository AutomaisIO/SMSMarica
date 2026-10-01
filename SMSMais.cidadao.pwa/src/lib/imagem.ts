/**
 * Recorta a área selecionada no react-easy-crop e redimensiona para um quadrado
 * de `tamanhoFinal` px. Saída base64 JPEG (qualidade 0.85) — foto de perfil
 * "estilo WhatsApp": nítida em qualquer avatar e leve o suficiente para trafegar.
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
  ctx.drawImage(img, area.x, area.y, area.width, area.height, 0, 0, tamanhoFinal, tamanhoFinal);

  return canvas.toDataURL('image/jpeg', qualidade);
}

function carregarImagem(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Falha ao carregar imagem.'));
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

/** Tipos de imagem que o servidor aceita como documento. */
const TIPOS_IMAGEM_ACEITOS = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];

/**
 * Prepara a FOTO de um documento para envio: a câmera do celular gera 4–12 MB por foto, e o
 * público usa sinal fraco e plano de dados curto. Reduz para no máx. `ladoMax` px no lado maior,
 * em JPEG `qualidade` — ainda legível para receita/exame em papel. Regras:
 * - GIF vai como está (pode ser animado e costuma ser pequeno);
 * - imagem já pequena e em tipo aceito vai como está (não piora o que já está bom);
 * - formato que o servidor não aceita (ex.: HEIC) é convertido para JPEG — se o navegador não
 *   souber decodificar, devolve o original e o servidor responde com a mensagem dele;
 * - se a versão reduzida sair maior que a original, fica a original.
 * Nunca é chamada para PDF.
 */
export async function prepararFotoParaEnvio(
  arquivo: File,
  ladoMax = 2000,
  qualidade = 0.85,
): Promise<File> {
  if (arquivo.type === 'image/gif') return arquivo;
  const aceito = TIPOS_IMAGEM_ACEITOS.includes(arquivo.type);

  const url = URL.createObjectURL(arquivo);
  try {
    const img = await carregarImagem(url);
    const maior = Math.max(img.naturalWidth, img.naturalHeight);
    if (aceito && maior <= ladoMax && arquivo.size <= 1024 * 1024) return arquivo;

    const fator = maior > ladoMax ? ladoMax / maior : 1;
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(img.naturalWidth * fator));
    canvas.height = Math.max(1, Math.round(img.naturalHeight * fator));
    const ctx = canvas.getContext('2d');
    if (!ctx) return arquivo;
    // Fundo branco: PNG com transparência viraria preto no JPEG.
    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', qualidade));
    if (!blob || (aceito && blob.size >= arquivo.size)) return arquivo;
    const base = arquivo.name.replace(/\.[^.]+$/, '') || 'foto';
    return new File([blob], `${base}.jpg`, { type: 'image/jpeg', lastModified: Date.now() });
  } catch {
    return arquivo;
  } finally {
    URL.revokeObjectURL(url);
  }
}
