import { http } from '@/shared/api/httpClient';
import type {
  AlertaDestinatario,
  AlertaEnvio,
  AlertaOrigem,
  AlertaPainel,
  SalvarDestinatario,
} from '@/features/alertas-plataforma/types';

export async function obterPainelAlertas(): Promise<AlertaPainel> {
  const { data } = await http.get<AlertaPainel>('/alertas-plataforma');
  return data;
}

export async function listarEnviosAlerta(origem?: string): Promise<AlertaEnvio[]> {
  const { data } = await http.get<AlertaEnvio[]>('/alertas-plataforma/envios', {
    params: origem ? { origem, limite: 200 } : { limite: 200 },
  });
  return data;
}

export async function adicionarDestinatario(body: SalvarDestinatario): Promise<AlertaDestinatario> {
  const { data } = await http.post<AlertaDestinatario>('/alertas-plataforma/destinatarios', body);
  return data;
}

export async function atualizarDestinatario(id: string, body: SalvarDestinatario): Promise<AlertaDestinatario> {
  const { data } = await http.put<AlertaDestinatario>(`/alertas-plataforma/destinatarios/${id}`, body);
  return data;
}

export async function removerDestinatario(id: string): Promise<void> {
  await http.delete(`/alertas-plataforma/destinatarios/${id}`);
}

export async function silenciarOrigem(chave: string, silenciada: boolean): Promise<AlertaOrigem> {
  const { data } = await http.put<AlertaOrigem>(
    `/alertas-plataforma/origens/${encodeURIComponent(chave)}/silencio`,
    { silenciada },
  );
  return data;
}

export async function testarAlerta(): Promise<AlertaEnvio> {
  const { data } = await http.post<AlertaEnvio>('/alertas-plataforma/testar');
  return data;
}
