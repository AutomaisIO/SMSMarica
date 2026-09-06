import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';

import type { CampoPacienteRegulacao } from './tiposCampo';

type Props = {
  c: CampoPacienteRegulacao;
  valor: string;
  desabilitado?: boolean;
  onChange: (v: string) => void;
  /** Nosso número verificado por OTP, quando houver. Oferecido, nunca aplicado sozinho. */
  sugestao?: string | null;
};

/**
 * Campo do bloco de identificação do paciente, como o sistema de regulação o entrega.
 *
 * <p>Compartilhado por SER, SERNIT e pelo fluxo Externo (ADR-0052). O que ele guarda de regra é
 * o respeito ao `editavel`: a identidade vem travada de lá e nem chega ao Gravar, então deixar
 * digitar seria mentir para o operador.</p>
 */
export function CampoPaciente({ c, valor, desabilitado, onChange, sugestao }: Props) {
  const id = `pac-${c.campo.replace(/[^\w]/g, '_')}`;
  const rotulo = `${c.rotulo}${c.obrigatorio ? ' *' : ''}`;
  const travado = !c.editavel || desabilitado;

  if (c.tipo === 'select' && c.opcoes?.length) {
    return (
      <Campo label={rotulo} htmlFor={id} className="w-52">
        <Select id={id} value={valor} disabled={travado} onChange={(e) => onChange(e.target.value)}>
          <option value="">Selecione…</option>
          {c.opcoes.map((o) => (
            <option key={o.valor} value={o.valor}>
              {o.rotulo}
            </option>
          ))}
        </Select>
      </Campo>
    );
  }

  const mesmosDigitos = (a: string, b: string) => {
    const x = a.replace(/\D/g, '');
    const y = b.replace(/\D/g, '');
    // Tolera DDI: um é sufixo do outro (o verificado guarda "55…", o sistema guarda nacional).
    return x.length >= 8 && y.length >= 8 && (x.endsWith(y) || y.endsWith(x));
  };
  const oferecer = sugestao && !travado && !mesmosDigitos(sugestao, valor);

  return (
    <Campo label={rotulo} htmlFor={id} className="w-52">
      <Input id={id} value={valor} disabled={travado} onChange={(e) => onChange(e.target.value)} />
      {oferecer ? (
        <button
          type="button"
          onClick={() => onChange(sugestao!)}
          className="mt-1 text-left text-xs text-emerald-700 underline decoration-dotted hover:text-emerald-900"
          title="Número verificado por código no nosso cadastro — clique para usar"
        >
          verificado no nosso cadastro: {sugestao}
        </button>
      ) : null}
    </Campo>
  );
}
