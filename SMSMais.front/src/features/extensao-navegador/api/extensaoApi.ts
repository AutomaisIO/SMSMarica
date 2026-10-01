import { http } from '@/shared/api/httpClient';
import type {
  AtivacaoPendente,
  Canal,
  ChaveGerada,
  ChavePublicacao,
  Dispositivo,
  Pacote,
  PublicarPacote,
  SituacaoDistribuicao,
} from '@/features/extensao-navegador/types';

// ------------------------------------------------------------- qualquer usuário logado

export async function obterSituacao(): Promise<SituacaoDistribuicao> {
  const { data } = await http.get<SituacaoDistribuicao>('/extensao/situacao');
  return data;
}

/**
 * Baixa o instalador. Cada download traz um código de ativação próprio (de uso único), então o
 * arquivo vem sempre da API — nunca de cache — e vale para um computador só.
 */
export async function baixarInstalador(): Promise<void> {
  const resp = await http.get<Blob>('/extensao/instalador', { responseType: 'blob' });
  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  let nome = 'SMSMais-Atualizador.exe';
  if (match?.[1]) {
    try {
      nome = decodeURIComponent(match[1]);
    } catch {
      nome = match[1];
    }
  }
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/octet-stream' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** Com `responseType: 'blob'` o ProblemDetails do erro chega como Blob — lê antes de mostrar. */
export async function mensagemErroDoDownload(erro: unknown): Promise<string> {
  const dados = (erro as { response?: { data?: unknown } })?.response?.data;
  if (dados instanceof Blob) {
    try {
      const problema = JSON.parse(await dados.text()) as { detail?: string; title?: string };
      return problema.detail ?? problema.title ?? 'Não foi possível baixar o instalador.';
    } catch {
      return 'Não foi possível baixar o instalador.';
    }
  }
  return 'Não foi possível baixar o instalador.';
}

export async function obterAtivacao(codigo: string): Promise<AtivacaoPendente> {
  const { data } = await http.get<AtivacaoPendente>(`/extensao/dispositivos/ativacoes/${encodeURIComponent(codigo)}`);
  return data;
}

export async function autorizarAtivacao(codigo: string): Promise<AtivacaoPendente> {
  const { data } = await http.post<AtivacaoPendente>(
    `/extensao/dispositivos/ativacoes/${encodeURIComponent(codigo)}/autorizar`,
  );
  return data;
}

// ---------------------------------------------------------------------- administração

export async function listarDispositivos(): Promise<Dispositivo[]> {
  const { data } = await http.get<Dispositivo[]>('/extensao/dispositivos');
  return data;
}

export async function definirCanal(id: string, canal: Canal): Promise<Dispositivo> {
  const { data } = await http.put<Dispositivo>(`/extensao/dispositivos/${id}/canal`, { canal });
  return data;
}

export async function revogarDispositivo(id: string): Promise<Dispositivo> {
  const { data } = await http.post<Dispositivo>(`/extensao/dispositivos/${id}/revogar`);
  return data;
}

export async function listarPacotes(): Promise<Pacote[]> {
  const { data } = await http.get<Pacote[]>('/extensao/pacotes');
  return data;
}

export async function publicarPacote(pedido: PublicarPacote): Promise<Pacote> {
  const form = new FormData();
  form.append('artefato', pedido.artefato);
  form.append('arquivo', pedido.arquivo);
  if (pedido.versao?.trim()) form.append('versao', pedido.versao.trim());
  if (pedido.notas?.trim()) form.append('notas', pedido.notas.trim());
  // Remove o Content-Type application/json padrão do client para o axios setar
  // multipart/form-data + boundary a partir do FormData (senão o backend responde 415).
  const { data } = await http.post<Pacote>('/extensao/pacotes', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

export async function promoverPacote(id: string): Promise<Pacote> {
  const { data } = await http.post<Pacote>(`/extensao/pacotes/${id}/promover`);
  return data;
}

export async function retirarPacote(id: string): Promise<Pacote> {
  const { data } = await http.post<Pacote>(`/extensao/pacotes/${id}/retirar`);
  return data;
}

export async function obterChavePublicacao(): Promise<ChavePublicacao> {
  const { data } = await http.get<ChavePublicacao>('/extensao/publicacao/chave');
  return data;
}

export async function gerarChavePublicacao(): Promise<ChaveGerada> {
  const { data } = await http.post<ChaveGerada>('/extensao/publicacao/chave');
  return data;
}

export async function revogarChavePublicacao(): Promise<ChavePublicacao> {
  const { data } = await http.delete<ChavePublicacao>('/extensao/publicacao/chave');
  return data;
}
