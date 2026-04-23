import { z } from 'zod';

export const unidadeSchema = z.object({
  nome: z.string().min(2, 'Nome obrigatório.').max(200, 'Nome excede 200 caracteres.'),
  endereco: z
    .string()
    .min(3, 'Endereço obrigatório.')
    .max(400, 'Endereço excede 400 caracteres.'),
  telefone: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  latitude: z.number({ invalid_type_error: 'Latitude inválida.' }).min(-90).max(90),
  longitude: z.number({ invalid_type_error: 'Longitude inválida.' }).min(-180).max(180),
});

export type UnidadeInput = z.infer<typeof unidadeSchema>;
