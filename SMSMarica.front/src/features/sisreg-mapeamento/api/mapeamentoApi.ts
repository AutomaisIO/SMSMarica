import { http } from '@/shared/api/httpClient';
import type {
  ProcedimentoSigtapDePara,
  SalvarCredencialPayload,
  SisregAutenticacaoResultado,
  SisregCredencialUnidade,
  SisregMapeamento,
  SisregMapeamentoAtualizacao,
  SisregSincronizacaoFhir,
} from '@/features/sisreg-mapeamento/types';

/**
 * Todos os endpoints trabalham no contexto de UMA unidade. Quando um `unidadeId` é informado,
 * ele vai explicitamente no header da requisição (o interceptor não o sobrescreve com a unidade
 * ativa do topo) — é o que permite operar a unidade da ROTA em `/app/unidades/{id}`, sem depender
 * do seletor global. Omitido, cai na unidade ativa; sem nenhuma das duas a API recusa com 400.
 */
function cabecalhoUnidade(unidadeId?: string | null) {
  return unidadeId ? { headers: { 'X-Unidade-Id': unidadeId } } : undefined;
}

export async function obterMapeamento(unidadeId?: string | null): Promise<SisregMapeamento> {
  const { data } = await http.get<SisregMapeamento>('/sisreg/mapeamento', cabecalhoUnidade(unidadeId));
  return data;
}

export async function atualizarMapeamento(unidadeId?: string | null): Promise<SisregMapeamentoAtualizacao> {
  const { data } = await http.post<SisregMapeamentoAtualizacao>(
    '/sisreg/mapeamento/atualizar',
    undefined,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function alternarProfissional(
  id: string,
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    `/sisreg/mapeamento/profissionais/${id}/habilitacao`,
    { habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

export async function alternarProcedimento(
  id: string,
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    `/sisreg/mapeamento/procedimentos/${id}/habilitacao`,
    { habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

export async function alternarProfissionaisEmLote(
  ids: string[],
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    '/sisreg/mapeamento/profissionais/habilitacao-lote',
    { ids, habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

export async function sincronizarFhir(unidadeId?: string | null): Promise<SisregSincronizacaoFhir> {
  const { data } = await http.post<SisregSincronizacaoFhir>(
    '/sisreg/mapeamento/sincronizar-fhir',
    undefined,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

/** De-para do procedimento do SISREG (o `pa`) para o SIGTAP. Catálogo GLOBAL, sem unidade. */
export async function listarDeParaSigtap(somenteNaoConfirmados = false): Promise<ProcedimentoSigtapDePara[]> {
  const { data } = await http.get<ProcedimentoSigtapDePara[]>('/sisreg/mapeamento/procedimentos-sigtap', {
    params: { somenteNaoConfirmados },
  });
  return data;
}

export async function sugerirDeParaSigtap(): Promise<{ sugeridos: number }> {
  const { data } = await http.post<{ sugeridos: number }>('/sisreg/mapeamento/procedimentos-sigtap/sugerir');
  return data;
}

/**
 * Liga/desliga o aviso por WhatsApp deste procedimento NESTA unidade. O back aplica a todas as
 * linhas do mesmo procedimento na unidade (ele costuma aparecer sob vários profissionais) e
 * devolve quantas foram afetadas.
 */
export async function alternarEnvioConfirmacao(
  id: string,
  enviar: boolean,
  unidadeId?: string | null,
): Promise<{ afetados: number }> {
  const { data } = await http.put<{ afetados: number }>(
    `/sisreg/mapeamento/procedimentos/${id}/confirmacao`,
    { enviar },
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function confirmarDeParaSigtap(
  id: string,
  procedimentoSigtapId: string,
): Promise<ProcedimentoSigtapDePara> {
  const { data } = await http.put<ProcedimentoSigtapDePara>(
    `/sisreg/mapeamento/procedimentos-sigtap/${id}`,
    { procedimentoSigtapId },
  );
  return data;
}

export async function obterCredencial(unidadeId?: string | null): Promise<SisregCredencialUnidade> {
  const { data } = await http.get<SisregCredencialUnidade>(
    '/sisreg/mapeamento/credencial',
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function salvarCredencial(
  payload: SalvarCredencialPayload,
  unidadeId?: string | null,
): Promise<SisregAutenticacaoResultado> {
  const { data } = await http.put<SisregAutenticacaoResultado>(
    '/sisreg/mapeamento/credencial',
    payload,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function testarCredencial(unidadeId?: string | null): Promise<SisregAutenticacaoResultado> {
  const { data } = await http.post<SisregAutenticacaoResultado>(
    '/sisreg/mapeamento/credencial/testar',
    undefined,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function removerCredencial(unidadeId?: string | null): Promise<void> {
  await http.delete('/sisreg/mapeamento/credencial', cabecalhoUnidade(unidadeId));
}
