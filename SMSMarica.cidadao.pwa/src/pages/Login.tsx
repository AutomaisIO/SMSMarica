import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { MessageCircle } from 'lucide-react';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { AuthShell } from '@/components/AuthShell';
import { PrimaryButton } from '@/components/ui';

function mascararCpf(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  return d
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d{1,2})$/, '$1-$2');
}

export function Login() {
  const navigate = useNavigate();
  const [cpf, setCpf] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const cpfLimpo = cpf.replace(/\D/g, '');
  const valido = cpfLimpo.length === 11;

  async function solicitar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      // Modo de teste: o backend devolve o código (codigoTeste) enquanto o WhatsApp não está ativo.
      const { data } = await http.post<{ codigoTeste?: string }>('/auth/paciente/solicitar-otp', {
        cpf: cpfLimpo,
      });
      navigate('/login/codigo', { state: { cpf: cpfLimpo, codigoTeste: data?.codigoTeste } });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthShell
      titulo="Entrar"
      subtitulo="Informe seu CPF. Enviaremos um código de acesso pelo WhatsApp do número cadastrado na Saúde."
    >
      <form onSubmit={solicitar} className="space-y-5">
        <div>
          <label
            htmlFor="cpf"
            className="mb-2 block text-center text-[11px] font-semibold uppercase tracking-[0.22em] text-tinta-mute"
          >
            Seu CPF
          </label>
          <input
            id="cpf"
            inputMode="numeric"
            autoComplete="off"
            autoFocus
            placeholder="000.000.000-00"
            value={cpf}
            onChange={(e) => setCpf(mascararCpf(e.target.value))}
            className="w-full rounded-2xl border border-areia bg-white py-4 text-center font-display text-[26px] font-semibold tabular-nums tracking-[0.06em] text-tinta shadow-carta transition placeholder:font-normal placeholder:text-tinta-mute/35 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15"
          />
        </div>
        {erro && <p className="text-center text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" disabled={!valido} carregando={enviando}>
          <MessageCircle className="h-5 w-5" />
          Receber código
        </PrimaryButton>
        <p className="mx-auto max-w-[17rem] text-center text-xs leading-relaxed text-tinta-mute">
          Sem cadastro na Saúde de Maricá? Procure a sua unidade.
        </p>
      </form>
    </AuthShell>
  );
}
