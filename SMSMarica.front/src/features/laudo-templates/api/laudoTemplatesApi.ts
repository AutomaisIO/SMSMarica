import { http } from '@/shared/api/httpClient';
import type {
  LaudoTemplate,
  LaudoTemplateListItem,
  SalvarLaudoTemplatePayload,
} from '@/features/laudo-templates/types';

export async function listarTemplates(
  categoria?: string,
  incluirInativos = false,
): Promise<LaudoTemplateListItem[]> {
  const { data } = await http.get<LaudoTemplateListItem[]>('/laudo-templates', {
    params: {
      categoria: categoria || undefined,
      incluirInativos: incluirInativos ? 'true' : undefined,
    },
  });
  return data;
}

export async function obterTemplate(id: string): Promise<LaudoTemplate> {
  const { data } = await http.get<LaudoTemplate>(`/laudo-templates/${id}`);
  return data;
}

export async function cadastrarTemplate(payload: SalvarLaudoTemplatePayload): Promise<string> {
  const { data } = await http.post<string>('/laudo-templates', payload);
  return data;
}

export async function atualizarTemplate(
  id: string,
  payload: SalvarLaudoTemplatePayload,
): Promise<void> {
  await http.put(`/laudo-templates/${id}`, payload);
}

export async function desativarTemplate(id: string): Promise<void> {
  await http.delete(`/laudo-templates/${id}`);
}

export async function reativarTemplate(id: string): Promise<void> {
  await http.post(`/laudo-templates/${id}/reativar`);
}
