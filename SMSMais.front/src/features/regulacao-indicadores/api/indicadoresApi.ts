import { AxiosError } from 'axios';
import { extrairMensagemDeErro, http, type ProblemaApi } from '@/shared/api/httpClient';
import type { FonteIndicadores, IndicadoresRegulacao, PeriodoMeses } from '../types';

/** Só vai o que foi escolhido: sem período, o backend decide (últimos 12 meses fechados). */
function parametros(periodo: PeriodoMeses) {
  const p: Record<string, string> = {};
  if (periodo.inicio) p.inicio = periodo.inicio;
  if (periodo.fim) p.fim = periodo.fim;
  return p;
}

export async function obterIndicadores(
  fonte: FonteIndicadores,
  periodo: PeriodoMeses,
): Promise<IndicadoresRegulacao> {
  const { data } = await http.get<IndicadoresRegulacao>(`/regulacao/indicadores/${fonte}`, {
    params: parametros(periodo),
  });
  return data;
}

/**
 * Baixa o PDF do relatório (mesmo período da tela) como arquivo. O endpoint exige o bearer no
 * header, então o download passa pelo axios e vira um blob — mesmo padrão do PDF do laudo.
 * O nome vem do Content-Disposition; sem ele, um nome local.
 */
export async function baixarPdfIndicadores(fonte: FonteIndicadores, periodo: PeriodoMeses): Promise<void> {
  const resp = await http.get<Blob>(`/regulacao/indicadores/${fonte}/pdf`, {
    params: parametros(periodo),
    responseType: 'blob',
  });

  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  let nome = `indicadores-regulacao-${fonte}.pdf`;
  if (match?.[1]) {
    try {
      nome = decodeURIComponent(match[1]);
    } catch {
      nome = match[1];
    }
  }

  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/**
 * Mensagem de erro do download. Com `responseType: 'blob'` o ProblemDetails do backend chega como
 * Blob, e o `extrairMensagemDeErro` padrão não enxerga o `detail` — aqui o blob é lido antes.
 */
export async function mensagemErroPdf(erro: unknown): Promise<string> {
  if (erro instanceof AxiosError && erro.response?.data instanceof Blob) {
    try {
      const texto = await erro.response.data.text();
      const dados = JSON.parse(texto) as ProblemaApi;
      let msg = dados.detail || dados.title;
      if (msg) {
        if (dados.codigoReferencia && !msg.includes(dados.codigoReferencia)) {
          msg = `${msg} (código ${dados.codigoReferencia})`;
        }
        return msg;
      }
    } catch {
      // Corpo não é JSON: cai na mensagem genérica abaixo.
    }
  }
  return extrairMensagemDeErro(erro);
}
