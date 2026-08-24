import { http, apiBaseAbsoluto } from '@/shared/api/httpClient';
import { derivarEscala, paraHex, lerHex } from './paleta';

/**
 * Identidade da instituição desta instância (ADR-0043), servida sem autenticação em
 * `GET /publico/instituicao`. É o que substitui os textos, cores e logo de Maricá que
 * viviam fixos no código.
 */
export type Instituicao = {
  nome: string;
  nomeSecretaria: string;
  nomeCurto: string;
  sigla: string | null;
  cnpj: string | null;
  codigoIbge: string | null;
  uf: string;
  dddPadrao: number | null;
  telefone: string | null;
  emailContato: string | null;
  emailDpo: string | null;
  whatsAppNumeroPublico: string | null;
  logoMidiaId: string | null;
  faviconMidiaId: string | null;
  corPrimaria: string | null;
  corSecundaria: string | null;
  corGradienteInicio: string | null;
  corGradienteFim: string | null;
  urlPainel: string | null;
  urlApp: string | null;
  urlArquivos: string | null;
  assinaturaProdutoHtml: string | null;
  atualizadoEm: string | null;
};

/**
 * Fallback usado quando a API não responde. Deliberadamente **sem** nome de município:
 * um painel que não conseguiu falar com o backend não pode se apresentar como a
 * prefeitura errada. As cores ficam com o default do CSS.
 */
const NEUTRA: Instituicao = {
  nome: 'Prefeitura Municipal',
  nomeSecretaria: 'Secretaria Municipal de Saúde',
  nomeCurto: 'Saúde',
  sigla: null,
  cnpj: null,
  codigoIbge: null,
  uf: '',
  dddPadrao: null,
  telefone: null,
  emailContato: null,
  emailDpo: null,
  whatsAppNumeroPublico: null,
  logoMidiaId: null,
  faviconMidiaId: null,
  corPrimaria: null,
  corSecundaria: null,
  corGradienteInicio: null,
  corGradienteFim: null,
  urlPainel: null,
  urlApp: null,
  urlArquivos: null,
  assinaturaProdutoHtml: null,
  atualizadoEm: null,
};

let atual: Instituicao = NEUTRA;

/** Identidade já carregada. Síncrono de propósito: componentes leem sem suspense. */
export const instituicao = (): Instituicao => atual;

/** URL da mídia (logo/favicon) servida pelo backend. */
export const urlMidia = (id: string): string => `${apiBaseAbsoluto}/midias/${id}`;

/** URL do logo, ou null se a instituição ainda não subiu um. */
export function urlLogo(): string | null {
  return atual.logoMidiaId ? urlMidia(atual.logoMidiaId) : null;
}

/**
 * Busca a identidade e a aplica: cores, título da aba e favicon.
 *
 * Chamada uma vez no boot, **antes** de renderizar — é o que impede o painel de piscar com a
 * marca errada. Nunca rejeita: se a API estiver fora, o painel sobe com a identidade neutra
 * e o CSS default em vez de não subir.
 */
export async function carregarInstituicao(): Promise<Instituicao> {
  try {
    const { data } = await http.get<Instituicao>('/publico/instituicao');
    atual = { ...NEUTRA, ...data };
  } catch {
    atual = NEUTRA;
  }
  aplicar(atual);
  return atual;
}

function aplicar(i: Instituicao) {
  if (typeof document === 'undefined') return;
  const raiz = document.documentElement;

  // Cores: só sobrescreve quando a instituição definiu. Sem isso, o CSS mantém a paleta
  // padrão (hoje a de Maricá) — trocar por uma escala derivada seria regressão visual.
  aplicarEscala(raiz, 'primary', i.corPrimaria);
  aplicarEscala(raiz, 'secondary', i.corSecundaria ?? i.corPrimaria);

  if (i.corPrimaria) {
    raiz.style.setProperty('--theme-primary', i.corPrimaria);
    raiz.style.setProperty('--theme-button-primary-solid', i.corPrimaria);
    raiz.style.setProperty('--theme-avatar-border-color', i.corPrimaria);
  }
  if (i.corSecundaria) {
    raiz.style.setProperty('--theme-secondary', i.corSecundaria);
    raiz.style.setProperty('--theme-button-secondary-solid', i.corSecundaria);
  }

  // Gradiente do menu: usa o par informado; com só a cor primária, deriva início/fim da
  // própria escala, para o menu não descolar da marca.
  const escala = i.corPrimaria ? derivarEscala(i.corPrimaria) : null;
  const inicio = i.corGradienteInicio ?? (escala ? paraHex(escala[700]) : null);
  const fim = i.corGradienteFim ?? (escala ? paraHex(escala[950]) : null);
  if (inicio) raiz.style.setProperty('--theme-gradient-start', inicio);
  if (inicio && fim) raiz.style.setProperty('--theme-gradient-mid', i.corPrimaria ?? inicio);
  if (fim) raiz.style.setProperty('--theme-gradient-end', fim);

  document.title = `${i.nomeCurto} — Painel`;
  aplicarFavicon(i);
}

function aplicarEscala(raiz: HTMLElement, nome: string, hex: string | null) {
  if (!hex || !lerHex(hex)) return;
  const escala = derivarEscala(hex);
  if (!escala) return;
  for (const [passo, triplet] of Object.entries(escala)) {
    raiz.style.setProperty(`--c-${nome}-${passo}`, triplet);
  }
}

function aplicarFavicon(i: Instituicao) {
  if (!i.faviconMidiaId) return;
  let link = document.querySelector<HTMLLinkElement>("link[rel~='icon']");
  if (!link) {
    link = document.createElement('link');
    link.rel = 'icon';
    document.head.appendChild(link);
  }
  link.href = urlMidia(i.faviconMidiaId);
}
