import { z } from 'zod';

export const laudoTemplateSchema = z.object({
  nome: z.string().trim().min(1, 'Nome é obrigatório.').max(200),
  categoria: z.string().trim().min(1, 'Categoria é obrigatória.').max(80),
  descricao: z.string().trim().max(500).optional().nullable(),
  conteudoJson: z.string().min(1),
  conteudoHtml: z.string(),
});

export type LaudoTemplateFormValores = z.infer<typeof laudoTemplateSchema>;
