import { useEffect, useState, type ComponentType } from 'react';
import { CheckCircle2, Loader2, Phone, UserCheck } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import type { SessaoRegulacao } from '@/shared/regulacao/ModalFollowUp';

type Campos = { residencial: string; whatsapp: string; contato: string };

/** Telefones lidos ao vivo do sistema de origem — só o que esta tela usa. */
type ContatosLidos = {
  residencial: string | null;
  whatsApp: string | null;
  contato: string | null;
  editavel: boolean;
  motivoNaoEditavel: string | null;
};

/** Recorte do `useQuery` de contatos que esta tela consome. */
type QueryContatos = {
  data?: ContatosLidos;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => unknown;
};

type Props = {
  /** Rótulo do sistema de origem (`"SER"`/`"SERNIT"`) — usado só em tooltips de suporte. */
  sistema: string;
  solicitacaoId: string;
  sessao: SessaoRegulacao;
  /** Reconhece o erro "sem sessão de operador" do sistema (código próprio de SER/SERNIT). */
  ehFaltaDeSessao: (erro: unknown) => boolean;
  /** Hook de leitura dos telefones (`useContatosSer`/`useContatosSernit`). Injetado pela feature. */
  useContatos: (id: string | undefined, habilitado: boolean) => QueryContatos;
  /** Hook de gravação (`useAlterarContatosSer`/`useAlterarContatosSernit`). */
  useAlterar: (id: string | undefined) => {
    mutateAsync: (corpo: Partial<Campos>) => Promise<ContatosLidos>;
    isPending: boolean;
  };
  /** Modal de autenticação do sistema (`ModalLoginSer`/`ModalLoginSernit`). */
  ModalLogin: ComponentType<{ aberto: boolean; aoFechar: () => void; aoAutenticar?: () => void }>;
};

/**
 * Alterar os telefones da solicitação no sistema de origem (SER/SERNIT) — <b>um único modal</b>
 * para os dois, espelhando {@link import('./ModalFollowUp').ModalFollowUp}.
 *
 * <p>Os valores vêm lidos AO VIVO da tela de edição do sistema, não do nosso espelho: editar em
 * cima de número velho sobrescreveria o que a regulação já tem. <b>Não altera o cadastro do
 * paciente no nosso hub</b> — telefone no FHIR tem regras próprias que não passam por aqui.</p>
 *
 * <p>A própria leitura abre a aba Editar e exige sessão de escrita; por isso a query só liga
 * depois de autenticado, e a recusa do backend vira pedido de senha, nunca erro vermelho.</p>
 */
export function ModalContato({
  sistema,
  solicitacaoId,
  sessao,
  ehFaltaDeSessao,
  useContatos,
  useAlterar,
  ModalLogin,
}: Props) {
  const [aberto, setAberto] = useState(false);
  const [campos, setCampos] = useState<Campos | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const { autenticado, operador, usuarioSistema, comSessao, tratouFaltaDeSessao, modal } = sessao;

  const contatos = useContatos(solicitacaoId, aberto && autenticado);
  const alterar = useAlterar(solicitacaoId);

  // A sessão pode cair ENTRE abrir o modal e a leitura. Tratar aqui, não no JSX: chamar
  // `tratouFaltaDeSessao` durante o render dispararia setState no meio da renderização.
  const faltaSessao = ehFaltaDeSessao(contatos.error);
  useEffect(() => {
    if (faltaSessao) {
      tratouFaltaDeSessao(contatos.error, () => {
        void contatos.refetch();
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [faltaSessao]);

  const atuais = contatos.data;
  const valores: Campos = campos ?? {
    residencial: atuais?.residencial ?? '',
    whatsapp: atuais?.whatsApp ?? '',
    contato: atuais?.contato ?? '',
  };

  function set(campo: keyof Campos, valor: string) {
    setCampos({ ...valores, [campo]: valor });
    setOk(null);
    setErro(null);
  }

  /** Só vai ao sistema o que o operador mudou — o resto fica como a regulação tem. */
  function mudancas(): Partial<Campos> {
    const so = (v: string) => v.replace(/\D/g, '');
    const pares: [keyof Campos, string | null | undefined][] = [
      ['residencial', atuais?.residencial],
      ['whatsapp', atuais?.whatsApp],
      ['contato', atuais?.contato],
    ];
    const corpo: Partial<Campos> = {};
    for (const [chave, antes] of pares) {
      if (so(valores[chave]) !== so(antes ?? '')) corpo[chave] = valores[chave].trim();
    }
    return corpo;
  }

  function fechar() {
    setAberto(false);
    setCampos(null);
    setErro(null);
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
      // O backend só devolve OK depois de REABRIR a tela do sistema e conferir número a número.
      setOk('Alterado e conferido na regulação.');
      setCampos({
        residencial: r.residencial ?? '',
        whatsapp: r.whatsApp ?? '',
        contato: r.contato ?? '',
      });
      setAberto(false);
    } catch (e) {
      if (tratouFaltaDeSessao(e, salvar)) return;
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <>
      {/* Pede a senha antes de qualquer requisição: a própria LEITURA abre a aba Editar do
          sistema e exige sessão de escrita. */}
      <Button
        variante="secundaria"
        tamanho="sm"
        onClick={() =>
          comSessao(() => {
            setOk(null);
            setErro(null);
            setAberto(true);
          })
        }
        title="Alterar os telefones no cadastro da regulação (não muda o do paciente no nosso sistema)"
      >
        <Phone className="mr-1 size-4" />
        Editar contato
      </Button>

      {/* Confirmação fica na última linha da barra (basis-full + order-last), sem se meter entre
          os botões — senão empurraria o botão vizinho para baixo. */}
      {ok && !aberto && (
        <p className="order-last flex basis-full items-center gap-1 text-xs text-emerald-700">
          <CheckCircle2 className="size-4" /> {ok}
        </p>
      )}

      <Modal
        aberto={aberto}
        // Enquanto grava, não deixa fechar por Esc/clique fora: a ação está a caminho do sistema.
        aoFechar={alterar.isPending ? () => {} : fechar}
        titulo="Editar contato"
        largura="md"
      >
        <div className="space-y-3">
          {autenticado && (
            <span
              className="flex items-center gap-1 text-[11px] text-emerald-700"
              title={`Login do ${sistema}: ${usuarioSistema ?? '—'}`}
            >
              <UserCheck className="size-3.5" />
              assinando como {operador}
            </span>
          )}

          {contatos.isLoading && (
            <p className="flex items-center gap-2 text-xs text-slate-500">
              <Loader2 className="size-4 animate-spin" /> lendo os telefones na regulação…
            </p>
          )}

          {contatos.isError && !faltaSessao && (
            <p className="rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
              {extrairMensagemDeErro(contatos.error)}
            </p>
          )}

          {atuais && !atuais.editavel && (
            // Situação terminal (Cancelada, Alta): o sistema mostra, mas não deixa editar. Melhor
            // dizer isso agora do que deixar digitar para recusar no fim.
            <p className="rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
              Esta solicitação não permite alterar contato.
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
                Altera o cadastro <strong>na regulação</strong>. Não muda o telefone do paciente no
                nosso sistema.
              </p>
            </>
          )}

          {erro && (
            <p className="rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
              {erro}
            </p>
          )}

          {atuais?.editavel && (
            <div className="mt-1 flex justify-end gap-2">
              <Button variante="ghost" tamanho="sm" onClick={fechar} disabled={alterar.isPending}>
                Cancelar
              </Button>
              <Button tamanho="sm" onClick={salvar} disabled={alterar.isPending}>
                {alterar.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
                Salvar
              </Button>
            </div>
          )}
        </div>
      </Modal>

      <ModalLogin {...modal} />
    </>
  );
}
