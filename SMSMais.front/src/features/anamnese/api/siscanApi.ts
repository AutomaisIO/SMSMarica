import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';

/**
 * Requisição do SISCAN a partir da anamnese.
 *
 * A sessão do SISCAN é do PRÓPRIO operador e vive só na memória do servidor, amarrada à sessão
 * dele no painel — não gravamos essa senha. Sair do sistema derruba a sessão de lá junto
 * (ver `authStore.sair()`).
 */

export type SessaoSiscan = {
  autenticado: boolean;
  usuarioSiscan: string | null;
  autenticadaEm: string | null;
  expiraEm: string | null;
};

export type ResponsavelSiscan = {
  /** Posicional no combo deles — NÃO guardar. A chave é o CNS. */
  indice: string;
  nome: string;
  cns: string;
};

export type CampoEnvioSiscan = { pergunta: string; resposta: string };

export type LacunaAnamnese = { campo: string; pergunta: string };

/** Uma requisição que já existe no SISCAN e apareceu na crítica de duplicidade. */
export type RequisicaoEncontrada = {
  protocolo: string;
  numeroExame: string;
  datas: string;
  unidade: string;
  status: string;
};

export type PreparoSiscan = {
  jaGerada: boolean;
  protocolo: string | null;
  numeroExame: string | null;
  pacienteNome: string;
  cnesUnidade: string;
  unidadeNome: string;
  tipoMamografia: string;
  tipoMamografiaRotulo: string;
  responsaveis: ResponsavelSiscan[];
  cnsResponsavelSugerido: string | null;
  nomeSolicitanteDaFicha: string | null;
  envio: CampoEnvioSiscan[];
  lacunas: LacunaAnamnese[];
  /**
   * A requisição DESTE pedido já está no SISCAN, mas ainda não estava carimbada aqui — acontece
   * quando ela nasceu fora do painel. Não se cria outra: vincula-se esta.
   */
  encontradaPeloProntuario: RequisicaoEncontrada | null;
  /**
   * A paciente já tem requisição no período e ela NÃO é deste pedido. Aqui o sistema para: qual
   * das duas vale é decisão de gente, no SISCAN.
   */
  duplicidades: RequisicaoEncontrada[] | null;
};

export type RequisicaoSiscan = {
  protocolo: string;
  numeroExame: string;
  geradaEm: string;
  responsavelNome: string;
};

export const siscanKeys = {
  sessao: ['siscan', 'sessao'] as const,
  preparo: (exameImagemId: string) => ['siscan', 'preparo', exameImagemId] as const,
};

export async function obterSessaoSiscan(): Promise<SessaoSiscan> {
  return (await http.get<SessaoSiscan>('/siscan/sessao')).data;
}

export async function entrarNoSiscan(usuario: string, senha: string): Promise<SessaoSiscan> {
  return (await http.post<SessaoSiscan>('/siscan/sessao', { usuario, senha })).data;
}

export async function prepararRequisicaoSiscan(exameImagemId: string): Promise<PreparoSiscan> {
  return (await http.get<PreparoSiscan>(`/siscan/requisicao/${exameImagemId}`)).data;
}

export async function gerarRequisicaoSiscan(
  exameImagemId: string,
  cnsResponsavel: string,
): Promise<RequisicaoSiscan> {
  return (await http.post<RequisicaoSiscan>(`/siscan/requisicao/${exameImagemId}`, {
    cnsResponsavel,
  })).data;
}

export function useSessaoSiscan(habilitado = true) {
  return useQuery({
    queryKey: siscanKeys.sessao,
    queryFn: obterSessaoSiscan,
    enabled: habilitado,
    staleTime: 60_000,
  });
}

export function useEntrarNoSiscan() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      entrarNoSiscan(usuario, senha),
    onSuccess: (dados) => client.setQueryData(siscanKeys.sessao, dados),
  });
}

/**
 * Percorre o assistente do SISCAN e devolve o que será enviado. NÃO grava.
 *
 * `staleTime: 0` de propósito: cada preparo é uma ida real ao SISCAN e a lista de responsáveis
 * depende do tipo de mamografia — cachear traria a lista errada para a próxima paciente.
 */
export function usePreparoSiscan(exameImagemId: string | undefined, habilitado: boolean) {
  return useQuery({
    queryKey: siscanKeys.preparo(exameImagemId ?? ''),
    queryFn: () => prepararRequisicaoSiscan(exameImagemId as string),
    enabled: Boolean(exameImagemId) && habilitado,
    staleTime: 0,
    retry: false,
  });
}

export function useGerarRequisicaoSiscan(exameImagemId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (cnsResponsavel: string) =>
      gerarRequisicaoSiscan(exameImagemId as string, cnsResponsavel),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: ['anamnese'] });
      client.invalidateQueries({ queryKey: ['siscan'] });
    },
  });
}
