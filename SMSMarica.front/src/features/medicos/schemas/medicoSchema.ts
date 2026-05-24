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
  crm: z
    .string()
    .min(3, 'CRM obrigatório.')
    .max(15, 'CRM excede 15 caracteres.'),
  ufCrm: z
    .string()
    .length(2, 'UF do CRM deve ter 2 letras.')
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
  validadeCrm: z
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
  email: z
    .string()
    .email('E-mail inválido.')
    .optional()
    .or(z.literal('').transform(() => undefined)),
});

export const atualizarMedicoSchema = z.object(baseAtualizacao);

export type CadastrarMedicoInput = z.infer<typeof cadastrarMedicoSchema>;
export type AtualizarMedicoInput = z.infer<typeof atualizarMedicoSchema>;
