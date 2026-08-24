import { http } from '@/shared/api/httpClient';
import type { Instituicao } from '@/shared/tema/instituicao';

export type { Instituicao };

/**
 * Escrita da identidade da instituição desta instância (ADR-0043).
 * Espelha `SalvarInstituicaoRequest` no backend.
 */
export type SalvarInstituicaoPayload = {
  nome: string;
  nomeSecretaria: string;
  nomeCurto: string;
  sigla: string | null;
  cnpj: string | null;
  codigoIbge: string | null;
  uf: string;
  dddPadrao: number | null;
  endereco: null;
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
};

/** Leitura administrativa (exige permissão). A leitura pública é `/publico/instituicao`. */
export async function obterInstituicao(): Promise<Instituicao> {
  const { data } = await http.get<Instituicao>('/instituicao');
  return data;
}

export async function salvarInstituicao(payload: SalvarInstituicaoPayload): Promise<Instituicao> {
  const { data } = await http.put<Instituicao>('/instituicao', payload);
  return data;
}
