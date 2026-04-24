import { z } from 'zod';
import {
  ESCOLARIDADES,
  ESTADOS_CIVIS,
  FATORES_RH,
  RACAS,
  SEXOS,
  TIPOS_SANGUINEOS,
} from '@/features/pacientes/types';

function apenasDigitos(valor: string): string {
  return valor.replace(/\D/g, '');
}

export function cpfValido(valor: string): boolean {
  const digitos = apenasDigitos(valor);
  if (digitos.length !== 11) return false;
  if (/^(\d)\1{10}$/.test(digitos)) return false;
  const calcDigito = (ate: number): number => {
    let soma = 0;
    for (let i = 0; i < ate; i += 1) soma += Number(digitos[i]) * (ate + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  return calcDigito(9) === Number(digitos[9]) && calcDigito(10) === Number(digitos[10]);
}

const enderecoSchema = z
  .object({
    cep: z.string().refine((v) => apenasDigitos(v).length === 8, 'CEP precisa ter 8 dígitos.'),
    logradouro: z.string().min(2, 'Logradouro obrigatório.').max(200),
    numero: z.string().max(20).optional().nullable(),
    complemento: z.string().max(120).optional().nullable(),
    bairro: z.string().min(2, 'Bairro obrigatório.').max(120),
    cidade: z.string().min(2, 'Cidade obrigatória.').max(120),
    uf: z.string().length(2, 'UF deve ter 2 letras.'),
    pontoReferencia: z.string().max(200).optional().nullable(),
  })
  .nullable();

const contatoEmergenciaSchema = z
  .object({
    nome: z.string().min(2, 'Nome obrigatório.').max(200),
    parentesco: z.string().max(60).optional().nullable(),
    telefone: z.string().min(8, 'Telefone obrigatório.').max(20),
  })
  .nullable();

export const pacientePassoIdentificacaoSchema = z.object({
  cpf: z.string().refine(cpfValido, 'CPF inválido.'),
  dataNascimento: z
    .string()
    .min(1, 'Data de nascimento obrigatória.')
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Data inválida.'),
});

export const pacienteFormSchema = z.object({
  nomeCompleto: z.string().min(3, 'Nome precisa ter ao menos 3 caracteres.').max(200),
  cpf: z.string().refine(cpfValido, 'CPF inválido.'),
  dataNascimento: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Data inválida.'),
  cns: z
    .string()
    .optional()
    .nullable()
    .transform((v) => (v && v.trim().length > 0 ? v : null))
    .refine((v) => v === null || apenasDigitos(v).length === 15, 'CNS precisa ter 15 dígitos.'),
  rg: z.string().max(20).optional().nullable(),
  sexo: z.enum(SEXOS),
  estadoCivil: z.enum(ESTADOS_CIVIS),
  racaCor: z.enum(RACAS),
  escolaridade: z.enum(ESCOLARIDADES),
  ocupacao: z.string().max(120).optional().nullable(),
  naturalidade: z.string().max(120).optional().nullable(),
  nacionalidade: z.string().max(60).optional().nullable(),
  nomeDaMae: z.string().max(200).optional().nullable(),
  nomeDoPai: z.string().max(200).optional().nullable(),
  responsavelLegal: z.string().max(200).optional().nullable(),
  endereco: enderecoSchema,
  telefonePrincipal: z.string().max(20).optional().nullable(),
  telefoneCelular: z.string().max(20).optional().nullable(),
  telefoneResidencial: z.string().max(20).optional().nullable(),
  email: z
    .string()
    .max(200)
    .optional()
    .nullable()
    .refine(
      (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
      'E-mail inválido.',
    ),
  contatoEmergencia: contatoEmergenciaSchema,
  alturaCm: z
    .number({ invalid_type_error: 'Altura inválida.' })
    .int()
    .min(30)
    .max(250)
    .optional()
    .nullable(),
  pesoKg: z
    .number({ invalid_type_error: 'Peso inválido.' })
    .min(1)
    .max(500)
    .optional()
    .nullable(),
  tipoSanguineo: z.enum(TIPOS_SANGUINEOS),
  fatorRh: z.enum(FATORES_RH),
  alergias: z.array(z.string().min(1)).default([]),
  medicamentosContinuos: z.array(z.string().min(1)).default([]),
  comorbidades: z.array(z.string().min(1)).default([]),
  deficiencias: z.array(z.string().min(1)).default([]),
  planoSaude: z.string().max(120).optional().nullable(),
  observacoes: z.string().max(2000).optional().nullable(),
});

export type PacienteFormInput = z.infer<typeof pacienteFormSchema>;
