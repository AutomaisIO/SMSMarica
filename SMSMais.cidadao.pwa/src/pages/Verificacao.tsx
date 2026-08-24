import { useState } from 'react';
import { useLocation, useNavigate, Navigate } from 'react-router-dom';
import { MessageCircle } from 'lucide-react';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { normalizarCelularBr } from '@/lib/telefone';
import { AuthShell } from '@/components/AuthShell';
import { PrimaryButton } from '@/components/ui';

/** dd/mm/aaaa enquanto digita. */
function mascararData(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

/** (DDD) 9XXXX-XXXX enquanto digita. */
function mascararTelefone(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 2) return d;
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

/** dd/mm/aaaa → aaaa-mm-dd; null se a data não existe no calendário. */
function paraIso(valor: string): string | null {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valor);
  if (!m) return null;
  const [, dia, mes, ano] = m;
  const d = new Date(Number(ano), Number(mes) - 1, Number(dia));
  if (
    d.getFullYear() !== Number(ano) ||
    d.getMonth() !== Number(mes) - 1 ||
    d.getDate() !== Number(dia) ||
    d > new Date() ||
    Number(ano) < 1900
  ) {
    return null;
  }
  return `${ano}-${mes}-${dia}`;
}

const CAMPO =
  'min-h-[52px] w-full rounded-2xl border border-areia bg-white px-4 text-base tabular-nums text-tinta shadow-carta transition placeholder:text-tinta-mute/40 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15';

/**
 * Passo 2 do login de quem não tem o WhatsApp verificado (ou perdeu o número). Prova de
 * identidade + o telefone que vai receber o código. Quem já tem cadastro informa o nº da
 * solicitação; quem ainda não tem cai na conferência do CPF na Receita (o backend decide).
 */
export function Verificacao() {
  const navigate = useNavigate();
  const location = useLocation();
  const estado = location.state as
    | { cpf?: string; situacao?: string; telefoneMascarado?: string }
    | null;
  const cpf = estado?.cpf;
  const cadastroNovo = estado?.situacao === 'cadastro';
  const telefoneDoCadastro = estado?.telefoneMascarado;

  const [nascimento, setNascimento] = useState('');
  const [solicitacao, setSolicitacao] = useState('');
  const [telefone, setTelefone] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  if (!cpf) return <Navigate to="/login" replace />;

  const iso = paraIso(nascimento);
  const telefoneOk = telefone.replace(/\D/g, '').length >= 10;
  const valido = !!iso && telefoneOk && (cadastroNovo || solicitacao.replace(/\D/g, '').length > 0);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    if (!iso) return;
    setErro(null);
    setEnviando(true);
    try {
      const { data } = await http.post<{ codigoTeste?: string; telefoneMascarado?: string }>(
        '/auth/paciente/solicitar-otp-verificacao',
        {
          cpf,
          dataNascimento: iso,
          codigoSolicitacao: cadastroNovo ? null : solicitacao.replace(/\D/g, ''),
          telefone: normalizarCelularBr(telefone),
        },
      );
      navigate('/login/codigo', {
        state: {
          cpf,
          codigoTeste: data?.codigoTeste,
          telefoneMascarado: data?.telefoneMascarado,
          semTroca: true,
        },
        replace: true,
      });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthShell
      titulo="Confirme seus dados"
      subtitulo={
        cadastroNovo
          ? 'Não encontramos seu CPF na Saúde de Maricá. Confirme seus dados para receber o código no WhatsApp.'
          : 'Seu WhatsApp ainda não foi confirmado. Informe os dados abaixo para receber o código com segurança.'
      }
    >
      <form onSubmit={enviar} className="space-y-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-tinta">Data de nascimento</span>
          <input
            inputMode="numeric"
            autoComplete="bday"
            autoFocus
            placeholder="dd/mm/aaaa"
            value={nascimento}
            onChange={(e) => setNascimento(mascararData(e.target.value))}
            className={CAMPO}
          />
        </label>

        {!cadastroNovo && (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-tinta">Nº da solicitação</span>
            <input
              inputMode="numeric"
              placeholder="Somente números"
              value={solicitacao}
              onChange={(e) => setSolicitacao(e.target.value.replace(/\D/g, '').slice(0, 20))}
              className={CAMPO}
            />
            <span className="mt-1 block text-xs text-tinta-mute">
              Está no papel do seu exame ou consulta marcada. Não tem em mãos? Procure a sua unidade.
            </span>
          </label>
        )}

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-tinta">Seu WhatsApp</span>
          <input
            inputMode="numeric"
            autoComplete="tel"
            placeholder="(21) 99999-0000"
            value={telefone}
            onChange={(e) => setTelefone(mascararTelefone(e.target.value))}
            className={CAMPO}
          />
          <span className="mt-1 block text-xs text-tinta-mute">
            {telefoneDoCadastro
              ? `O número do seu cadastro termina em ${telefoneDoCadastro.replace(/\D/g, '')}. Confirme-o ou informe o número atual.`
              : 'É neste número que você vai receber o código.'}
          </span>
        </label>

        {erro && <p className="text-center text-sm text-marica">{erro}</p>}

        <PrimaryButton type="submit" disabled={!valido} carregando={enviando}>
          <MessageCircle className="h-5 w-5" />
          Receber código
        </PrimaryButton>

        <button
          type="button"
          onClick={() => navigate('/login', { replace: true })}
          className="block w-full text-center text-sm font-medium text-tinta-mute underline"
        >
          Voltar
        </button>
      </form>
    </AuthShell>
  );
}
