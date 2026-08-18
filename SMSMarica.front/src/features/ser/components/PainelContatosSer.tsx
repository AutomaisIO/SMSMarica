import { useState } from 'react';
import { AxiosError } from 'axios';
import { CheckCircle2, Loader2, Phone, UserCheck } from 'lucide-react';

import {
  useAlterarContatosSer,
  useContatosSer,
  useSessaoOperadorSer,
} from '@/features/ser/api/queries';
import { ModalLoginSer } from '@/features/ser/components/ModalLoginSer';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro, type ProblemaApi } from '@/shared/api/httpClient';

function temCodigo(erro: unknown, codigo: string): boolean {
  if (!(erro instanceof AxiosError)) return false;
  const dados = erro.response?.data as ProblemaApi | undefined;
  return Boolean(dados?.errors && codigo in dados.errors);
}

type Campos = { residencial: string; whatsapp: string; contato: string };

/**
 * Alterar os telefones da solicitação <b>no SER</b>.
 *
 * <p>Os valores vêm lidos AO VIVO da tela de edição do Estado, não do nosso espelho: o espelho só
 * se atualiza na varredura, e editar em cima de número velho sobrescreveria o que o SER já tem.</p>
 *
 * <p><b>Isto não altera o cadastro do paciente no nosso hub.</b> Telefone no FHIR tem regras
 * próprias (o verificado por OTP é a fonte única, e contato só acumula), e elas não passam por
 * esta tela.</p>
 */
export function PainelContatosSer({ solicitacaoId }: { solicitacaoId: string }) {
  const [aberto, setAberto] = useState(false);
  const [campos, setCampos] = useState<Campos | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [pedindoLogin, setPedindoLogin] = useState(false);

  const { data: sessao } = useSessaoOperadorSer();
  // Só bate no SER quando o operador abre o painel — a leitura custa duas requisições lá.
  const contatos = useContatosSer(solicitacaoId, aberto);
  const alterar = useAlterarContatosSer(solicitacaoId);

  const atuais = contatos.data;
  const valores: Campos = campos ?? {
    residencial: atuais?.residencial ?? '',
    whatsapp: atuais?.whatsApp ?? '',
    contato: atuais?.contato ?? '',
  };

  function set(campo: keyof Campos, valor: string) {
    setCampos({ ...valores, [campo]: valor });
    setOk(null);
  }

  /** Só vai ao SER o que o operador mudou — o resto fica exatamente como o Estado tem. */
  function mudancas() {
    const so = (v: string) => v.replace(/\D/g, '');
    const pares: [keyof Campos, string | null | undefined][] = [
      ['residencial', atuais?.residencial],
      ['whatsapp', atuais?.whatsApp],
      ['contato', atuais?.contato],
    ];
    const corpo: Record<string, string> = {};
    for (const [chave, antes] of pares) {
      if (so(valores[chave]) !== so(antes ?? '')) corpo[chave] = valores[chave].trim();
    }
    return corpo;
  }

  async function salvar() {
    setErro(null);
    setOk(null);

    const corpo = mudancas();
    if (Object.keys(corpo).length === 0) {
      setErro('Nenhum telefone foi alterado.');
      return;
    }

    try {
      const r = await alterar.mutateAsync(corpo);
      // O backend só devolve OK depois de REABRIR a tela do SER e conferir número a número.
      setOk('Alterado e conferido na tela do SER.');
      setCampos({
        residencial: r.residencial ?? '',
        whatsapp: r.whatsApp ?? '',
        contato: r.contato ?? '',
      });
    } catch (e) {
      if (temCodigo(e, 'ser.sessao_operador_ausente')) {
        setPedindoLogin(true);
        return;
      }
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="rounded border border-slate-200 bg-slate-50 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Phone className="size-4 text-red-700" />
        <h4 className="text-sm font-semibold text-slate-800">Dados de contato no SER</h4>

        {sessao?.autenticado ? (
          <span className="ml-auto flex items-center gap-1 text-[11px] text-emerald-700">
            <UserCheck className="size-3.5" />
            assinando como {sessao.usuarioSer}
          </span>
        ) : (
          <span className="ml-auto text-[11px] text-slate-500">exige seu login do SER</span>
        )}
      </div>

      {!aberto && (
        <div className="mt-2">
          <Button variante="secundaria" tamanho="sm" onClick={() => setAberto(true)}>
            Alterar dados de contato
          </Button>
        </div>
      )}

      {aberto && (
        <div className="mt-3 space-y-3">
          {contatos.isLoading && (
            <p className="flex items-center gap-2 text-xs text-slate-500">
              <Loader2 className="size-4 animate-spin" /> lendo os telefones no SER…
            </p>
          )}

          {contatos.isError && (
            <p className="rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
              {extrairMensagemDeErro(contatos.error)}
            </p>
          )}

          {atuais && !atuais.editavel && (
            // Situação terminal (Cancelada, Alta): o SER mostra, mas não deixa editar. Melhor
            // dizer isso agora do que deixar digitar para recusar no fim.
            <p className="rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
              Esta solicitação não permite alterar contato no SER.
              {atuais.motivoNaoEditavel ? ` ${atuais.motivoNaoEditavel}` : ''}
            </p>
          )}

          {atuais?.editavel && (
            <>
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="block space-y-1">
                  <span className="text-xs text-slate-600">WhatsApp</span>
                  <Input
                    value={valores.whatsapp}
                    onChange={(e) => set('whatsapp', e.target.value)}
                    inputMode="tel"
                  />
                </label>
                <label className="block space-y-1">
                  <span className="text-xs text-slate-600">Contato</span>
                  <Input
                    value={valores.contato}
                    onChange={(e) => set('contato', e.target.value)}
                    inputMode="tel"
                  />
                </label>
                <label className="block space-y-1">
                  <span className="text-xs text-slate-600">Residencial</span>
                  <Input
                    value={valores.residencial}
                    onChange={(e) => set('residencial', e.target.value)}
                    inputMode="tel"
                  />
                </label>
              </div>

              <p className="text-[11px] text-slate-500">
                Altera o cadastro <strong>no SER</strong>. Não muda o telefone do paciente no nosso
                sistema.
              </p>

              <div className="flex gap-2">
                <Button tamanho="sm" onClick={salvar} disabled={alterar.isPending}>
                  {alterar.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
                  Salvar no SER
                </Button>
                <Button
                  variante="ghost"
                  tamanho="sm"
                  onClick={() => {
                    setAberto(false);
                    setCampos(null);
                    setErro(null);
                  }}
                  disabled={alterar.isPending}
                >
                  Fechar
                </Button>
              </div>
            </>
          )}
        </div>
      )}

      {ok && (
        <p className="mt-2 flex items-center gap-1 text-xs text-emerald-700">
          <CheckCircle2 className="size-4" /> {ok}
        </p>
      )}
      {erro && (
        <p className="mt-2 rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
          {erro}
        </p>
      )}

      <ModalLoginSer
        aberto={pedindoLogin}
        aoFechar={() => setPedindoLogin(false)}
        aoAutenticar={() => {
          setPedindoLogin(false);
          void salvar();
        }}
      />
    </section>
  );
}
