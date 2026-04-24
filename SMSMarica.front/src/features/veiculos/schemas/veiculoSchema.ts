import { z } from 'zod';
import {
  TIPOS_ASSENTO,
  TIPOS_VEICULO,
  type TipoAssento,
  type TipoVeiculo,
} from '@/features/veiculos/types';

const tiposVeiculo = Object.values(TIPOS_VEICULO) as string[];
const tiposAssento = Object.values(TIPOS_ASSENTO) as string[];

export const assentoInputSchema = z.object({
  numero: z.number().int().positive(),
  tipo: z
    .string()
    .refine((v): v is TipoAssento => tiposAssento.includes(v), {
      message: 'Tipo de assento inválido.',
    }),
  bloqueado: z.boolean().default(false),
});

export const fileiraInputSchema = z.object({
  ordem: z.number().int().positive().max(30),
  assentos: z
    .array(assentoInputSchema)
    .min(1, 'Fileira deve ter pelo menos um assento.')
    .max(10, 'Fileira não pode ter mais de 10 assentos.')
    .refine((arr) => new Set(arr.map((a) => a.numero)).size === arr.length, {
      message: 'Números de assento duplicados.',
    }),
});

export const cadastrarVeiculoSchema = z.object({
  placa: z
    .string()
    .min(1, 'Placa obrigatória.')
    .max(10, 'Placa excede 10 caracteres.')
    .transform((v) => v.trim().toUpperCase()),
  modelo: z.string().min(1, 'Modelo obrigatório.').max(100),
  fabricante: z.string().min(1, 'Fabricante obrigatório.').max(80),
  cor: z.string().min(1, 'Cor obrigatória.').max(40),
  tipo: z
    .string()
    .refine((v): v is TipoVeiculo => tiposVeiculo.includes(v), {
      message: 'Tipo de veículo inválido.',
    }),
  fileiras: z
    .array(fileiraInputSchema)
    .min(1, 'Veículo deve ter pelo menos uma fileira.')
    .max(30, 'Veículo não pode ter mais de 30 fileiras.')
    .refine((arr) => new Set(arr.map((f) => f.ordem)).size === arr.length, {
      message: 'Ordens de fileira duplicadas.',
    }),
});

export const atualizarVeiculoSchema = cadastrarVeiculoSchema.omit({ fileiras: true });

export type CadastrarVeiculoInput = z.infer<typeof cadastrarVeiculoSchema>;
export type AtualizarVeiculoInput = z.infer<typeof atualizarVeiculoSchema>;
