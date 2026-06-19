import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { MessageCircle } from 'lucide-react';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { AuthShell } from '@/components/AuthShell';
import { Field, PrimaryButton } from '@/components/ui';

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
        <Field
          label="CPF"
          inputMode="numeric"
          autoComplete="off"
          autoFocus
          placeholder="000.000.000-00"
          value={cpf}
          onChange={(e) => setCpf(mascararCpf(e.target.value))}
          className="text-lg tracking-wide"
        />
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" disabled={!valido} carregando={enviando}>
          <MessageCircle className="h-5 w-5" />
          Receber código
        </PrimaryButton>
        <p className="text-center text-xs text-tinta-mute">
          Sem cadastro na Saúde de Maricá? Procure a sua unidade.
        </p>
      </form>
    </AuthShell>
  );
}
