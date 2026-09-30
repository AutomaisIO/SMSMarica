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

/** Uma unidade requisitante que a conta do SISCAN enxerga. O CNES é a chave. */
export type UnidadeSiscan = { cnes: string; nome: string };

/** Uma opção de combo do SISCAN (Raça/Cor, Etnia). O código é o DELES. */
export type OpcaoSiscan = { codigo: string; rotulo: string };

/**
 * O que a pessoa escolheu quando o SISCAN pediu: a unidade (a do pedido fora da conta) e a
 * Raça/Cor — com a etnia, se Indígena — quando o CADSUS não tem.
 */
export type EscolhasSiscan = { cnesUnidade?: string; racaCor?: string; etnia?: string };

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
  /**
   * Algo na data não fecha e a pessoa precisa saber ANTES de confirmar — hoje, o estudo associado
   * ser posterior à anamnese, que é sequência impossível e cheira a conciliação errada. Avisa, não
   * bloqueia: quem olha o caso decide melhor que a regra.
   */
  avisoData: string | null;
  /**
   * A unidade do pedido NÃO está entre as unidades requisitantes que a conta do SISCAN do operador
   * enxerga: esta é a lista delas, para escolher por qual enviar. Null = a do pedido está lá.
   */
  unidadesDisponiveis: UnidadeSiscan[] | null;
  /** A escolhida no lugar da do pedido, quando o preparo foi pedido com uma. */
  unidadeEscolhida: UnidadeSiscan | null;
  /**
   * A paciente está SEM Raça/Cor no CADSUS e o SISCAN pede que seja informada — as opções são as
   * da tela dele. Null = o CADSUS já tem.
   */
  racaCorOpcoes: OpcaoSiscan[] | null;
  racaCorEscolhida: OpcaoSiscan | null;
  /** Raça/Cor Indígena: o SISCAN pede também a etnia. */
  etniaOpcoes: OpcaoSiscan[] | null;
  etniaEscolhida: OpcaoSiscan | null;
};

/**
 * O ano da última mamografia da anamnese era ANTERIOR ao que o SISCAN já tem da paciente — e ele
 * recusa isso no Salvar. Foi para lá o ano dele, e a anamnese foi corrigida junto.
 */
export type CorrecaoAnoUltimaMamografia = {
  anoDeclarado: number;
  anoNoSiscan: number;
};

/**
 * A requisição saiu por outra unidade, porque a do pedido não está na conta do SISCAN — e isso
 * ficou registrado na anamnese (`siscan.unidadeRequisitanteEscolhida`).
 */
export type UnidadeRequisitanteEscolhida = {
  cnes: string;
  nome: string;
  cnesDoPedido: string;
  nomeDoPedido: string;
  escolhidaPor: string | null;
  escolhidaEm: string;
};

/** A Raça/Cor que faltava no CADSUS e foi informada no SISCAN — registrada na anamnese. */
export type RacaCorInformada = {
  codigo: string;
  rotulo: string;
  etniaCodigo: string | null;
  etniaRotulo: string | null;
  informadaPor: string | null;
  informadaEm: string;
};

export type RequisicaoSiscan = {
  protocolo: string;
  numeroExame: string;
  geradaEm: string;
  responsavelNome: string;
  correcaoAnoUltimaMamografia: CorrecaoAnoUltimaMamografia | null;
  unidadeEscolhida: UnidadeRequisitanteEscolhida | null;
  racaCor: RacaCorInformada | null;
};

export const siscanKeys = {
  sessao: ['siscan', 'sessao'] as const,
  preparo: (exameImagemId: string, escolhas: EscolhasSiscan) =>
    [
      'siscan',
      'preparo',
      exameImagemId,
      escolhas.cnesUnidade ?? '',
      escolhas.racaCor ?? '',
      escolhas.etnia ?? '',
    ] as const,
};

export async function obterSessaoSiscan(): Promise<SessaoSiscan> {
  return (await http.get<SessaoSiscan>('/siscan/sessao')).data;
}

export async function entrarNoSiscan(usuario: string, senha: string): Promise<SessaoSiscan> {
  return (await http.post<SessaoSiscan>('/siscan/sessao', { usuario, senha })).data;
}

/** Só vai o que foi escolhido — parâmetro vazio seria lido como "escolheu vazio". */
function soPreenchidas(escolhas: EscolhasSiscan): EscolhasSiscan {
  return Object.fromEntries(
    Object.entries(escolhas).filter(([, v]) => typeof v === 'string' && v.length > 0),
  ) as EscolhasSiscan;
}

export async function prepararRequisicaoSiscan(
  exameImagemId: string,
  escolhas: EscolhasSiscan = {},
): Promise<PreparoSiscan> {
  return (
    await http.get<PreparoSiscan>(`/siscan/requisicao/${exameImagemId}`, {
      params: soPreenchidas(escolhas),
    })
  ).data;
}

export async function gerarRequisicaoSiscan(
  exameImagemId: string,
  cnsResponsavel: string,
  escolhas: EscolhasSiscan = {},
): Promise<RequisicaoSiscan> {
  const e = soPreenchidas(escolhas);
  return (await http.post<RequisicaoSiscan>(`/siscan/requisicao/${exameImagemId}`, {
    cnsResponsavel,
    cnesUnidade: e.cnesUnidade ?? null,
    racaCor: e.racaCor ?? null,
    etnia: e.etnia ?? null,
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
 * <p><b>Nada de cache aqui — `gcTime: 0`.</b> Esta consulta alimenta a tela de conferência, cuja
 * função é mostrar o que será afirmado sobre a paciente. Servir resposta guardada enquanto uma
 * nova carrega é o comportamento padrão do React Query e, neste lugar, é mentira: em 23/09/2026
 * alguém corrigiu o ano na anamnese, salvou, mandou gerar de novo e passou ~21 s olhando os
 * valores ANTIGOS — o tempo que o preparo leva para ir ao SISCAN e voltar.</p>
 *
 * <p>Descartar ao fechar o modal faz cada abertura começar do zero: aparece o "consultando o
 * SISCAN…" e o que se lê depois é o estado de agora. A lista de responsáveis também depende do
 * tipo de mamografia, então cache aqui traria a lista errada para a próxima paciente.</p>
 */
export function usePreparoSiscan(
  exameImagemId: string | undefined,
  habilitado: boolean,
  escolhas: EscolhasSiscan = {},
) {
  return useQuery({
    // As escolhas entram na chave: trocar a unidade ou a Raça/Cor é outro preparo (a lista de
    // responsáveis é por unidade), nunca o anterior servido de cache.
    queryKey: siscanKeys.preparo(exameImagemId ?? '', escolhas),
    queryFn: () => prepararRequisicaoSiscan(exameImagemId as string, escolhas),
    enabled: Boolean(exameImagemId) && habilitado,
    staleTime: 0,
    gcTime: 0,
    retry: false,
  });
}

export function useGerarRequisicaoSiscan(exameImagemId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ cnsResponsavel, ...escolhas }: { cnsResponsavel: string } & EscolhasSiscan) =>
      gerarRequisicaoSiscan(exameImagemId as string, cnsResponsavel, escolhas),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: ['anamnese'] });
      client.invalidateQueries({ queryKey: ['siscan'] });
    },
  });
}
