import { z } from 'zod';

function apenasDigitos(valor: string): string {
  return valor.replace(/\D/g, '');
}

function cpfValido(valor: string): boolean {
  const d = apenasDigitos(valor);
  if (d.length !== 11) return false;
  if (/^(\d)\1{10}$/.test(d)) return false;
  const calc = (ate: number): number => {
    let s = 0;
    for (let i = 0; i < ate; i += 1) s += Number(d[i]) * (ate + 1 - i);
    const r = (s * 10) % 11;
    return r === 10 ? 0 : r;
  };
  return calc(9) === Number(d[9]) && calc(10) === Number(d[10]);
}

const enderecoSchema = z
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

const baseAtualizacao = {
  conselho: z
    .string()
    .min(2, 'Informe o conselho.')
    .max(12)
    .transform((v) => v.trim().toUpperCase()),
  registro: z
    .string()
    .min(3, 'Número do registro obrigatório.')
    .max(15, 'Registro excede 15 caracteres.'),
  ufConselho: z
    .string()
    .length(2, 'UF do conselho deve ter 2 letras.')
    .transform((v) => v.toUpperCase()),
  especialidade: z
    .string()
    .max(120)
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v.trim() : undefined)),
  rqe: z
    .string()
    .max(20)
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v.trim() : undefined)),
  validadeRegistro: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  telefone: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  endereco: enderecoSchema,
  fotoBase64: z.string().nullable().optional(),
};

export const cadastrarMedicoSchema = z.object({
  ...baseAtualizacao,
  nomeCompleto: z.string().min(3).max(200),
  cpf: z.string().refine(cpfValido, 'CPF inválido.'),
  dataNascimento: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  email: z
    .string()
    .email('E-mail inválido.')
    .optional()
    .or(z.literal('').transform(() => undefined)),
});

export const atualizarMedicoSchema = z.object(baseAtualizacao);

export type CadastrarMedicoInput = z.infer<typeof cadastrarMedicoSchema>;
export type AtualizarMedicoInput = z.infer<typeof atualizarMedicoSchema>;
