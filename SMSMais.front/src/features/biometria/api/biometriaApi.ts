import { http } from '@/shared/api/httpClient';
import type {
  BiometriaMotorista,
  FiltroVerificacoesFaciais,
  FotoBiometria,
  LiberacaoGerada,
  VerificacaoFacial,
} from '@/features/biometria/types';

// --- Ficha do motorista (permissão Motoristas) ---

export async function obterBiometriaMotorista(motoristaId: string): Promise<BiometriaMotorista> {
  const { data } = await http.get<BiometriaMotorista>(`/motoristas/${motoristaId}/biometria`);
  return data;
}

/** Foto de referência do rosto. `null` quando não há (404 é resposta, não falha). */
export async function obterFotoBiometria(motoristaId: string): Promise<string | null> {
  const resposta = await http.get<FotoBiometria>(`/motoristas/${motoristaId}/biometria/foto`, {
    validateStatus: (s) => s === 200 || s === 404,
  });
  return resposta.status === 404 ? null : resposta.data.fotoBase64;
}

/** Apaga o rosto cadastrado: o motorista refaz o primeiro acesso no tablet. */
export async function apagarBiometriaMotorista(motoristaId: string): Promise<void> {
  await http.delete(`/motoristas/${motoristaId}/biometria`);
}

// --- Veículo e conferências (permissão Rastreamento) ---

/** Gerar outro código para o mesmo veículo anula o anterior. */
export async function gerarLiberacaoMotorista(
  veiculoId: string,
  motoristaId: string,
): Promise<LiberacaoGerada> {
  const { data } = await http.post<LiberacaoGerada>(`/veiculos/${veiculoId}/liberacao-motorista`, {
    motoristaId,
  });
  return data;
}

/** Mais recentes primeiro, no máximo 200. Sem `dia`, os últimos 7 dias. */
export async function listarVerificacoesFaciais(
  filtro: FiltroVerificacoesFaciais,
): Promise<VerificacaoFacial[]> {
  const params: Record<string, string> = {};
  if (filtro.veiculoId) params.veiculoId = filtro.veiculoId;
  if (filtro.motoristaId) params.motoristaId = filtro.motoristaId;
  if (filtro.dia) params.dia = filtro.dia;
  const { data } = await http.get<VerificacaoFacial[]>('/rastreamento/verificacoes-faciais', {
    params: Object.keys(params).length ? params : undefined,
  });
  return data;
}

/** Selfie da conferência. `null` quando já foi apagada pela retenção de 90 dias (404). */
export async function obterSelfieVerificacao(id: string): Promise<string | null> {
  const resposta = await http.get<FotoBiometria>(`/rastreamento/verificacoes-faciais/${id}/selfie`, {
    validateStatus: (s) => s === 200 || s === 404,
  });
  return resposta.status === 404 ? null : resposta.data.fotoBase64;
}
