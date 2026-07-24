/**
 * Renderiza um gráfico de pizza num canvas e devolve o PNG em base64 (sem o prefixo
 * `data:image/png;base64,`), pronto para `workbook.addImage`.
 *
 * O `exceljs` não cria gráficos nativos do Excel — então a pizza vai como imagem embutida:
 * é uma "foto" do momento da geração. As tabelas e fórmulas do arquivo continuam vivas e
 * recalculam; a pizza, não. (Ver conversa do ticket #52.)
 */

export type FatiaPizza = { rotulo: string; valor: number; cor: string };

export type ImagemPizza = { base64: string; largura: number; altura: number };

const LARGURA = 360;
const ALTURA = 210;

export function renderizarPizza(titulo: string, fatias: FatiaPizza[]): ImagemPizza | null {
  const canvas = document.createElement('canvas');
  const escala = 2; // nitidez em telas retina; o Excel exibe no tamanho lógico
  canvas.width = LARGURA * escala;
  canvas.height = ALTURA * escala;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  ctx.scale(escala, escala);

  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, LARGURA, ALTURA);

  ctx.fillStyle = '#3f3f46';
  ctx.font = '600 13px system-ui, Segoe UI, Arial, sans-serif';
  ctx.textBaseline = 'top';
  ctx.fillText(titulo, 14, 12);

  const total = fatias.reduce((s, f) => s + Math.max(0, f.valor), 0);
  const cx = 95;
  const cy = 118;
  const raio = 68;

  if (total <= 0) {
    ctx.strokeStyle = '#e4e4e7';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(cx, cy, raio, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = '#a1a1aa';
    ctx.font = '12px system-ui, Segoe UI, Arial, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('sem dados no período', cx, cy);
    ctx.textAlign = 'left';
  } else {
    let ang = -Math.PI / 2;
    for (const f of fatias) {
      const fatia = (Math.max(0, f.valor) / total) * Math.PI * 2;
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.arc(cx, cy, raio, ang, ang + fatia);
      ctx.closePath();
      ctx.fillStyle = f.cor;
      ctx.fill();
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 2;
      ctx.stroke();
      ang += fatia;
    }
  }

  // Legenda à direita
  let ly = 44;
  const lx = 196;
  ctx.textBaseline = 'middle';
  ctx.font = '12px system-ui, Segoe UI, Arial, sans-serif';
  for (const f of fatias) {
    const pct = total > 0 ? (Math.max(0, f.valor) / total) * 100 : 0;
    ctx.fillStyle = f.cor;
    ctx.fillRect(lx, ly - 6, 12, 12);
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 1;
    ctx.strokeRect(lx, ly - 6, 12, 12);
    ctx.fillStyle = '#3f3f46';
    const rotulo =
      f.rotulo.length > 22 ? `${f.rotulo.slice(0, 21)}…` : f.rotulo;
    ctx.fillText(
      `${rotulo} — ${pct.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`,
      lx + 18,
      ly,
    );
    ly += 24;
  }

  const dataUrl = canvas.toDataURL('image/png');
  return { base64: dataUrl.split(',')[1] ?? '', largura: LARGURA, altura: ALTURA };
}
