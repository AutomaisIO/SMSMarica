import { http } from '@/shared/api/httpClient';

export type ConsultaCpfResposta = {
  cpf: string;
  nome: string;
  dataNascimento: string;
  situacaoCadastral: string | null;
  /** "Masculino" | "Feminino" quando a Receita/Hub retorna; null caso contrário. */
  sexo: string | null;
};

export type ConsultaCnsResposta = {
  cns: string;
  cpf: string;
  nome: string;
  /** "Masculino" | "Feminino" quando o CADSUS informa; null caso contrário. */
  sexo: string | null;
  /** ISO (yyyy-MM-dd) — o CADSUS devolve mesmo sem exigi-la na busca; null se ausente. */
  dataNascimento: string | null;
  nomeMae: string | null;
};

export type ConsultaCepResposta = {
  cep: string;
  logradouro: string;
  complemento: string | null;
  bairro: string;
  localidade: string;
  uf: string;
  ibge: string | null;
};

export async function consultarCpf(cpf: string, dataNascimentoIso: string): Promise<ConsultaCpfResposta> {
  const cpfLimpo = cpf.replace(/\D/g, '');
  const { data } = await http.get<ConsultaCpfResposta>('/integracoes/cpf', {
    params: { cpf: cpfLimpo, dataNascimento: dataNascimentoIso },
  });
  return data;
}

/** Consulta paciente por CNS no SISREG (CADSUS). Não exige data de nascimento. */
export async function consultarCns(cns: string): Promise<ConsultaCnsResposta> {
  const cnsLimpo = cns.replace(/\D/g, '');
  const { data } = await http.get<ConsultaCnsResposta>('/integracoes/cns', {
    params: { cns: cnsLimpo },
  });
  return data;
}

export async function consultarCep(cep: string): Promise<ConsultaCepResposta> {
  const cepLimpo = cep.replace(/\D/g, '');
  const { data } = await http.get<ConsultaCepResposta>(`/integracoes/cep/${cepLimpo}`);
  return data;
}
