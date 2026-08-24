import { http } from '@/shared/api/httpClient';
import type {
  AnexoExameDto,
  AnexoUploadTokenDto,
  SalvarAnexoPayload,
} from '@/features/anamnese/types';

/**
 * Cria um token de upload para a ponte QR → PWA. O médico exibe o QR (= `url`)
 * na tela; o aparelho do cidadão lê e envia os documentos. Multi-uso dentro do TTL.
 */
export async function criarTokenAnexo(solicitacaoExameId: string): Promise<AnexoUploadTokenDto> {
  const { data } = await http.post<AnexoUploadTokenDto>(
    `/anamneses/${solicitacaoExameId}/anexos/tokens`,
  );
  return data;
}

/** Lista os documentos anexados (todos os status, não-excluídos) da solicitação. */
export async function listarAnexosExame(solicitacaoExameId: string): Promise<AnexoExameDto[]> {
  const { data } = await http.get<AnexoExameDto[]>(`/anamneses/${solicitacaoExameId}/anexos`);
  return data;
}

/** Confirma/atualiza um documento: Pendente → Salvo. */
export async function salvarAnexo(id: string, payload: SalvarAnexoPayload): Promise<AnexoExameDto> {
  const { data } = await http.post<AnexoExameDto>(`/anexos/${id}/salvar`, payload);
  return data;
}

/** Exclusão lógica de um documento anexado. */
export async function excluirAnexo(id: string): Promise<void> {
  await http.delete(`/anexos/${id}`);
}
