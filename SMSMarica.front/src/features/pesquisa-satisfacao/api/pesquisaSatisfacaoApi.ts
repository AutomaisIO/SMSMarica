import { http } from '@/shared/api/httpClient';

export type PesquisaConfig = {
  unidadeId: string;
  unidadeNome: string;
  cnes: string | null;
  envioWhatsAppAtivo: boolean;
  linkResponder: string | null;
  linkPainel: string | null;
  horasAposAtendimento: number;
  atualizadoEm: string | null;
};

export type PesquisaPerfil = { sexo: string; faixaEtaria: string; cliques: number };

export type PesquisaPainel = {
  enviadas: number;
  entregues: number;
  vistas: number;
  /** Cliques no link. NÃO é "respondidas" — quem sabe isso é a AvanteSocial. */
  clicadas: number;
  horasMedianasAteClique: number | null;
  perfil: PesquisaPerfil[];
};

export async function obterPesquisaConfig(unidadeId: string): Promise<PesquisaConfig> {
  const { data } = await http.get(`/pesquisas-satisfacao/unidades/${unidadeId}`);
  return data;
}

export async function salvarPesquisaConfig(
  unidadeId: string,
  body: {
    envioWhatsAppAtivo: boolean;
    linkResponder: string | null;
    linkPainel: string | null;
    horasAposAtendimento: number;
  },
): Promise<PesquisaConfig> {
  const { data } = await http.put(`/pesquisas-satisfacao/unidades/${unidadeId}`, body);
  return data;
}

export async function obterPesquisaPainel(unidadeId: string, dias = 30): Promise<PesquisaPainel> {
  const { data } = await http.get(`/pesquisas-satisfacao/unidades/${unidadeId}/painel`, {
    params: { dias },
  });
  return data;
}
