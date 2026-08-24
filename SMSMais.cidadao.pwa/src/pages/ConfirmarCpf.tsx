import { useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { PrimaryButton } from '@/components/ui';

function mascararCpf(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  return d
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d{1,2})$/, '$1-$2');
}

/**
 * Desafio de CPF dos links que carregam RESULTADO (exame, laudo). Sem ele, possuir o link do
 * WhatsApp era a credencial inteira — quem o recebesse por engano entrava no prontuário alheio.
 *
 * Deliberadamente NÃO usa o `AuthShell`: aquela moldura é a do login (hero vinho + logo + "App do
 * Cidadão"), e o paciente que clicou no link do exame não está tentando fazer login — repetir a
 * cara do login faria ele achar que errou o caminho. Aqui a cor é a `lagoa` (afordância clínica) e
 * a fala é curta, grande e sem tecniquês: o público é idoso e de baixa escolaridade.
 */
export function ConfirmarCpf({
  onConfirmar,
  enviando,
  erro,
  tentativasRestantes,
}: {
  onConfirmar: (cpf: string) => void;
  enviando: boolean;
  erro: string | null;
  tentativasRestantes: number | null;
}) {
  const [cpf, setCpf] = useState('');
  const cpfLimpo = cpf.replace(/\D/g, '');

  return (
    <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col bg-papel px-6 pb-8 pt-14 shadow-2xl">
      <div className="animate-rise flex flex-1 flex-col justify-center">
        <span className="mx-auto grid h-20 w-20 place-items-center rounded-full bg-lagoa-claro text-lagoa">
          <ShieldCheck className="h-10 w-10" />
        </span>

        <h1 className="mt-7 text-center font-display text-[30px] font-semibold leading-tight text-tinta">
          Digite seu CPF
        </h1>
        <p className="mx-auto mt-3 max-w-[19rem] text-center text-[17px] leading-relaxed text-tinta-mute">
          É para ter certeza de que só você vê o seu exame.
        </p>

        <form
          className="mt-8 space-y-5"
          onSubmit={(e) => {
            e.preventDefault();
            if (cpfLimpo.length === 11 && !enviando) onConfirmar(cpfLimpo);
          }}
        >
          <input
            id="cpf"
            aria-label="Seu CPF"
            inputMode="numeric"
            autoComplete="off"
            autoFocus
            placeholder="000.000.000-00"
            value={cpf}
            onChange={(e) => setCpf(mascararCpf(e.target.value))}
            className="w-full rounded-2xl border border-areia bg-white py-5 text-center font-display text-[28px] font-semibold tabular-nums tracking-[0.06em] text-tinta shadow-carta transition placeholder:font-normal placeholder:text-tinta-mute/35 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15"
          />

          {erro && (
            <p className="text-center text-[15px] font-medium leading-relaxed text-marica">
              {erro}
              {tentativasRestantes !== null && tentativasRestantes > 0 && (
                <>
                  <br />
                  <span className="font-normal text-tinta-mute">
                    {tentativasRestantes === 1
                      ? 'Resta 1 tentativa.'
                      : `Restam ${tentativasRestantes} tentativas.`}
                  </span>
                </>
              )}
            </p>
          )}

          <PrimaryButton type="submit" disabled={cpfLimpo.length !== 11} carregando={enviando}>
            Ver meu exame
          </PrimaryButton>
        </form>

        <p className="mx-auto mt-7 max-w-[18rem] text-center text-[13px] leading-relaxed text-tinta-mute">
          Não conseguiu? Procure a unidade de saúde onde fez o exame.
        </p>
      </div>
    </div>
  );
}
