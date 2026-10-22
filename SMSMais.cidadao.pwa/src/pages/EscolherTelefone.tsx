import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { MessageCircle, Search } from 'lucide-react';
import { api, type EntradaCidadao, type OpcaoTelefoneEntrada } from '@/lib/api';
import { AVISO_ENTRADA_EXPIRADA, entradaExpirou, mensagemDaEntrada } from '@/lib/entrada';
import { cn } from '@/lib/cn';
import { AuthShell } from '@/components/AuthShell';
import { PrimaryButton } from '@/components/ui';

/** A escolha "Nenhum desses é meu" — fica no mesmo grupo, depois dos números. */
const NENHUM = 'nenhum';

const AVISO =
  'rounded-2xl border border-areia bg-white/70 px-4 py-3 text-center text-sm leading-relaxed text-tinta-mute';

/** Quem já tem WhatsApp confirmado vê só ele: trocar é no posto (ADR-0057 §5). */
function unicoVerificado(entrada: EntradaCidadao): OpcaoTelefoneEntrada | null {
  const { opcoes } = entrada;
  return opcoes.length === 1 && opcoes[0].verificado ? opcoes[0] : null;
}

function rotuloOrigem(opcao: OpcaoTelefoneEntrada): string {
  if (opcao.verificado) return 'Seu WhatsApp confirmado';
  return opcao.origem === 'esus' ? 'Encontrado no e-SUS' : 'Do seu cadastro';
}

/**
 * Passo 2 do login (ADR-0078): para onde vai o código. CPF e nascimento já conferiram; o servidor
 * mostra só o final dos números que conhece e a pessoa escolhe o dela — o código nunca vai para um
 * número digitado. "Nenhum desses é meu" procura no e-SUS quando o canal permite
 * (`podeBuscarEsus`); senão, ou se lá também não houver, o caminho é o posto.
 */
export function EscolherTelefone() {
  const location = useLocation();
  const estado = location.state as { cpf?: string; entrada?: EntradaCidadao } | null;
  if (!estado?.cpf || !estado.entrada) return <Navigate to="/login" replace />;
  // Uma entrada nova (outro desafio) recomeça a tela do zero, mesmo sem sair da rota.
  return <Escolha key={location.key} cpf={estado.cpf} entradaInicial={estado.entrada} />;
}

function Escolha({ cpf, entradaInicial }: { cpf: string; entradaInicial: EntradaCidadao }) {
  const navigate = useNavigate();
  const [entrada, setEntrada] = useState(entradaInicial);
  // O confirmado sozinho já vem marcado; numa lista, a pessoa escolhe — reconhecer o próprio
  // número entre os mascarados faz parte da prova.
  const [escolha, setEscolha] = useState<string | null>(() => unicoVerificado(entradaInicial)?.id ?? null);
  const [enviando, setEnviando] = useState(false);
  const [procurando, setProcurando] = useState(false);
  const [semOutros, setSemOutros] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const semNumeros = entrada.opcoes.length === 0;
  const unico = unicoVerificado(entrada);
  const nenhum = semNumeros || escolha === NENHUM;
  // Para quem só tem o confirmado, "nenhum desses" levaria ao mesmo posto do aviso abaixo.
  const mostrarNenhum = !semNumeros && (!unico || entrada.podeBuscarEsus);
  const ocupado = enviando || procurando;

  function voltar(aviso?: string) {
    navigate('/login', { replace: true, state: { cpf, aviso } });
  }

  function escolher(id: string) {
    setEscolha(id);
    setErro(null);
  }

  async function enviar() {
    const opcao = entrada.opcoes.find((o) => o.id === escolha);
    if (!opcao) return;
    setErro(null);
    setEnviando(true);
    try {
      const r = await api.enviarCodigo(entrada.desafioId, opcao.id);
      navigate('/login/codigo', {
        state: {
          cpf,
          codigoTeste: r.codigoTeste ?? undefined,
          telefoneMascarado: r.telefoneMascarado ?? opcao.final,
          // "Trocar é no posto" só se diz do WhatsApp já confirmado; o do cadastro acabou de ser
          // escolhido numa lista que já oferecia "Nenhum desses é meu".
          semTroca: !opcao.verificado,
        },
      });
    } catch (e) {
      if (entradaExpirou(e)) return voltar(AVISO_ENTRADA_EXPIRADA);
      setErro(mensagemDaEntrada(e));
    } finally {
      setEnviando(false);
    }
  }

  async function procurarNoEsus() {
    setErro(null);
    setProcurando(true);
    try {
      const nova = await api.procurarTelefoneNoEsus(entrada.desafioId);
      const conhecidos = new Set(entrada.opcoes.map((o) => o.id));
      const achouOutros = nova.opcoes.some((o) => !conhecidos.has(o.id));
      setEntrada(nova);
      // A lista mudou: a pessoa escolhe de novo. Sem nada novo, fica no "nenhum" e vê o posto.
      if (achouOutros) setEscolha(null);
      else setSemOutros(true);
    } catch (e) {
      if (entradaExpirou(e)) return voltar(AVISO_ENTRADA_EXPIRADA);
      setErro(mensagemDaEntrada(e));
    } finally {
      setProcurando(false);
    }
  }

  return (
    <AuthShell
      titulo="Para onde enviamos o código?"
      subtitulo={
        semNumeros
          ? 'Não encontramos um celular no seu cadastro.'
          : 'Escolha o seu WhatsApp. Mostramos só o final de cada número.'
      }
    >
      <div className="space-y-5">
        {!semNumeros && (
          <div role="radiogroup" aria-label="Seu WhatsApp" className="space-y-3">
            {entrada.opcoes.map((o) => (
              <OpcaoCartao
                key={o.id}
                marcada={escolha === o.id}
                desabilitada={ocupado}
                aoEscolher={() => escolher(o.id)}
              >
                <span className="block text-[15px] text-tinta">
                  WhatsApp final{' '}
                  <span className="font-display text-lg font-semibold tabular-nums tracking-[0.12em]">
                    {o.final}
                  </span>
                </span>
                <span
                  className={cn(
                    'mt-1.5 inline-block rounded-full px-3 py-1 text-xs font-semibold',
                    o.verificado ? 'bg-lagoa-claro text-lagoa-escuro' : 'bg-areia/60 text-tinta-mute',
                  )}
                >
                  {rotuloOrigem(o)}
                </span>
              </OpcaoCartao>
            ))}
            {mostrarNenhum && (
              <OpcaoCartao
                marcada={escolha === NENHUM}
                desabilitada={ocupado}
                aoEscolher={() => escolher(NENHUM)}
              >
                <span className="block text-[15px] font-medium text-tinta">Nenhum desses é meu</span>
              </OpcaoCartao>
            )}
          </div>
        )}

        {erro && <p className="text-center text-sm text-marica">{erro}</p>}

        {!nenhum ? (
          <PrimaryButton type="button" onClick={enviar} disabled={!escolha} carregando={enviando}>
            <MessageCircle className="h-5 w-5" />
            Enviar código
          </PrimaryButton>
        ) : entrada.podeBuscarEsus ? (
          <>
            <PrimaryButton type="button" onClick={procurarNoEsus} carregando={procurando}>
              <Search className="h-5 w-5" />
              Procurar outros números
            </PrimaryButton>
            {procurando && (
              <p aria-live="polite" className="text-center text-sm text-tinta-mute">
                Procurando outros números… isso pode levar alguns segundos.
              </p>
            )}
          </>
        ) : (
          <p className={AVISO}>
            {semOutros && 'Não encontramos outros números. '}
            Procure o <strong>posto de saúde</strong> onde você é atendido(a), com um documento com
            foto, para atualizar o seu WhatsApp.
          </p>
        )}

        {/* Quem já tem número confirmado NÃO troca de número por aqui: seria a porta para alguém
            que sabe CPF e nascimento apontar o contato para o próprio celular. */}
        {unico && !nenhum && (
          <p className={AVISO}>
            Não tem mais esse número? Para trocar, procure o <strong>posto de saúde</strong> onde
            você é atendido(a), com um documento com foto.
          </p>
        )}

        <button
          type="button"
          onClick={() => voltar()}
          className="block w-full text-center text-sm font-medium text-tinta-mute underline"
        >
          Voltar
        </button>
      </div>
    </AuthShell>
  );
}

/** Cartão de escolha única (radio), com o anel lagoa do foco quando marcado. */
function OpcaoCartao({
  marcada,
  desabilitada,
  aoEscolher,
  children,
}: {
  marcada: boolean;
  desabilitada: boolean;
  aoEscolher: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={marcada}
      disabled={desabilitada}
      onClick={aoEscolher}
      className={cn(
        'flex min-h-[64px] w-full items-center gap-3 rounded-2xl border bg-white px-4 py-3 text-left shadow-carta transition active:scale-[.99] disabled:opacity-60',
        marcada ? 'border-lagoa ring-4 ring-lagoa/15' : 'border-areia',
      )}
    >
      <span
        aria-hidden
        className={cn(
          'grid h-5 w-5 shrink-0 place-items-center rounded-full border-2 transition',
          marcada ? 'border-lagoa' : 'border-tinta-mute/40',
        )}
      >
        {marcada && <span className="h-2.5 w-2.5 rounded-full bg-lagoa" />}
      </span>
      <span className="min-w-0 flex-1">{children}</span>
    </button>
  );
}
