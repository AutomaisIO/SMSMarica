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
  cnh: z
    .string()
    .min(5, 'CNH obrigatória.')
    .max(20, 'CNH excede 20 caracteres.'),
  telefone: z
    .string()
    .optional()
    .transform((v) => (v && v.trim().length > 0 ? v : undefined)),
  endereco: enderecoSchema,
  fotoBase64: z.string().nullable().optional(),
};

export const cadastrarMotoristaSchema = z.object({
  ...baseAtualizacao,
  nomeCompleto: z.string().min(3).max(200),
  cpf: z.string().refine(cpfValido, 'CPF inválido.'),
});

export const atualizarMotoristaSchema = z.object(baseAtualizacao);

export type CadastrarMotoristaInput = z.infer<typeof cadastrarMotoristaSchema>;
export type AtualizarMotoristaInput = z.infer<typeof atualizarMotoristaSchema>;
