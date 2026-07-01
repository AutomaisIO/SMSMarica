import { z } from 'zod';

export const enderecoSchema = z
  .object({
    cep: z.string(),
    logradouro: z.string(),
    numero: z.string().nullable(),
    complemento: z.string().nullable(),
    bairro: z.string(),
    cidade: z.string(),
    uf: z.string(),
    pontoReferencia: z.string().nullable(),
  })
  .nullable();

export const unidadeSchema = z.object({
  nome: z.string().min(2, 'Nome obrigatório.').max(200, 'Nome excede 200 caracteres.'),
  cnes: z
    .string()
    .optional()
    .transform((v) => (v ? v.replace(/\D/g, '') : ''))
    .refine((v) => v === '' || v.length === 7, 'CNES deve ter 7 dígitos.')
    .transform((v) => (v.length > 0 ? v : undefined)),
  endereco: enderecoSchema,
  telefone: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  latitude: z
    .number({ invalid_type_error: 'Latitude inválida.' })
    .min(-90)
    .max(90)
    .nullable()
    .optional(),
  longitude: z
    .number({ invalid_type_error: 'Longitude inválida.' })
    .min(-180)
    .max(180)
    .nullable()
    .optional(),
});

export type UnidadeInput = z.infer<typeof unidadeSchema>;
