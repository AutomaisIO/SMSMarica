import { z } from 'zod';

function apenasDigitos(valor: string): string {
  return valor.replace(/\D/g, '');
}

function cpfValido(valor: string): boolean {
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

const baseSchema = {
  nomeCompleto: z
    .string()
    .min(3, 'Nome precisa ter ao menos 3 caracteres.')
    .max(200, 'Nome excede 200 caracteres.'),
  cns: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined))
    .refine((v) => v === undefined || apenasDigitos(v).length === 15, 'CNS precisa ter 15 dígitos.'),
  latitude: z
    .number({ invalid_type_error: 'Latitude inválida.' })
    .min(-90)
    .max(90),
  longitude: z
    .number({ invalid_type_error: 'Longitude inválida.' })
    .min(-180)
    .max(180),
};

export const cadastrarPacienteSchema = z.object({
  ...baseSchema,
  cpf: z.string().refine(cpfValido, 'CPF inválido.'),
});

export const atualizarPacienteSchema = z.object(baseSchema);

export type CadastrarPacienteInput = z.infer<typeof cadastrarPacienteSchema>;
export type AtualizarPacienteInput = z.infer<typeof atualizarPacienteSchema>;
