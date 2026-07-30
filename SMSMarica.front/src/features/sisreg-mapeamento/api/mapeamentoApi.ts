import { http } from '@/shared/api/httpClient';
import type {
  SalvarCredencialPayload,
  SisregAutenticacaoResultado,
  SisregCredencialUnidade,
  SisregMapeamento,
  SisregMapeamentoAtualizacao,
  SisregSincronizacaoFhir,
} from '@/features/sisreg-mapeamento/types';

/**
 * Todos os endpoints trabalham no contexto da unidade selecionada (header X-Unidade-Id,
 * posto pelo httpClient). Sem UMA unidade escolhida a API recusa com 400.
 */

export async function obterMapeamento(): Promise<SisregMapeamento> {
  const { data } = await http.get<SisregMapeamento>('/sisreg/mapeamento');
  return data;
}

export async function atualizarMapeamento(): Promise<SisregMapeamentoAtualizacao> {
  const { data } = await http.post<SisregMapeamentoAtualizacao>('/sisreg/mapeamento/atualizar');
  return data;
}

export async function alternarProfissional(id: string, habilitado: boolean): Promise<void> {
  await http.put(`/sisreg/mapeamento/profissionais/${id}/habilitacao`, { habilitado });
}

export async function alternarProcedimento(id: string, habilitado: boolean): Promise<void> {
  await http.put(`/sisreg/mapeamento/procedimentos/${id}/habilitacao`, { habilitado });
}

export async function alternarProfissionaisEmLote(ids: string[], habilitado: boolean): Promise<void> {
  await http.put('/sisreg/mapeamento/profissionais/habilitacao-lote', { ids, habilitado });
}

export async function sincronizarFhir(): Promise<SisregSincronizacaoFhir> {
  const { data } = await http.post<SisregSincronizacaoFhir>('/sisreg/mapeamento/sincronizar-fhir');
  return data;
}

/**
 * As operações de credencial rodam sobre a unidade do header X-Unidade-Id. Quando um
 * `unidadeId` é informado, ele é enviado explicitamente na requisição (o interceptor não
 * o sobrescreve com a unidade ativa) — é o que permite configurar a senha de uma unidade
 * qualquer a partir da Configuração SISREG ou do detalhe da unidade, sem depender do
 * seletor de unidade ativa do topo.
 */
function cabecalhoUnidade(unidadeId?: string | null) {
  return unidadeId ? { headers: { 'X-Unidade-Id': unidadeId } } : undefined;
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
