import { http } from './httpClient';

/** Resposta de `GET /anexos/sessao/{token}` (token válido). */
export type SessaoValida = {
  valido: boolean;
  expiraEm: string; // ISO-8601
  paciente: { nome: string };
  solicitacao: { id: string; resumo?: string | null };
};

/** Resposta 201 de `POST /anexos/sessao/{token}`. */
export type DocumentoEnviado = {
  id: string;
  nome: string;
};

export type EnviarDocumento = {
  arquivo: Blob;
  nome: string;
  descricao: string;
  paginas?: number;
};

export const api = {
  /**
   * Valida o token do QR. 200 → sessão válida; 404/410 → inválido/expirado/revogado
   * (axios rejeita; o chamador trata como "abra pelo QR code").
   */
  validarSessao: (token: string) =>
    http.get<SessaoValida>(`/anexos/sessao/${encodeURIComponent(token)}`).then((r) => r.data),

  /**
   * Envia o PDF montado (multipart/form-data). NÃO consome o token (multi-uso no TTL).
   *
   * Obs. axios: o cliente padrão tem `Content-Type: application/json`; com FormData
   * isso faria o axios serializar para JSON. Forçamos `multipart/form-data` aqui —
   * o adapter do browser substitui pelo header com o `boundary` correto.
   */
  enviarDocumento: (token: string, { arquivo, nome, descricao, paginas }: EnviarDocumento) => {
    const form = new FormData();
    form.append('arquivo', arquivo, 'documento.pdf');
    form.append('nome', nome);
    form.append('descricao', descricao);
    if (typeof paginas === 'number') form.append('paginas', String(paginas));
    return http
      .post<DocumentoEnviado>(`/anexos/sessao/${encodeURIComponent(token)}`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
};
