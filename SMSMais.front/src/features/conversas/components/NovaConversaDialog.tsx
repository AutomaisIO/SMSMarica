import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Loader2, Search, X } from 'lucide-react';
import { useBuscarContatos, useIniciarConversa, useTemplates } from '@/features/conversas/api/queries';
import { usePacientePorId } from '@/features/pacientes/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarNomeProprio, primeiroNomeProprio } from '@/shared/lib/nomes';
import { ModalNumeroNegado } from '@/features/conversas/components/ModalNumeroNegado';
import { ROTULO_ASSUNTO, type AssuntoConversa, type ContatoConversa } from '@/features/conversas/types';

type Props = {
  onFechar: () => void;
  onCriada: (id: string) => void;
  /**
   * Abre já com este paciente escolhido (atalho do WhatsApp ao lado do nome, em qualquer
   * tela). Nome e telefone vêm do cadastro — o operador só confere e dispara.
   */
  pacienteInicialId?: string;
  /**
   * Abre já com este telefone preenchido — usado ao REABRIR uma conversa cuja janela de 24h
   * expirou e que não tem cadastro vinculado (só o número). Ignorado quando há
   * <see cref="pacienteInicialId" />, que traz o telefone do cadastro.
   */
  telefoneInicial?: string;
  /** Nome do perfil do WhatsApp, para adiantar o tratamento ({{1}}) quando não há cadastro. */
  nomeContatoInicial?: string;
  /** Título do modal — "Reabrir conversa" quando vem do banner de janela expirada. */
  titulo?: string;
};

const ASSUNTOS: AssuntoConversa[] = ['Tfd', 'MarcacaoConsulta', 'Duvida', 'Atendente', 'Outro'];

function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}

function cpfFmt(cpf: string | null): string {
  const d = (cpf ?? '').replace(/\D/g, '');
  if (d.length !== 11) return cpf ?? '';
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

function telefoneFmt(fone: string | null): string {
  const d = (fone ?? '').replace(/\D/g, '').replace(/^55/, '');
  if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
  if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return fone ?? '';
}

/** Substitui {{n}} pelo valor digitado; onde ainda não há valor, mantém o marcador visível. */
function preencher(corpo: string, valores: string[]): string {
  return corpo.replace(/\{\{(\d+)\}\}/g, (marcador, n: string) => {
    const v = valores[Number(n) - 1];
    return v?.trim() ? v : marcador;
  });
}

/**
 * Abertura de conversa. A Meta não deixa falar com quem não tem janela de 24h aberta — o
 * primeiro contato só existe via template aprovado, e é ele que provoca a resposta que abre
 * a janela. Por isso o modelo é obrigatório aqui (a lista vem filtrada pelo backend).
 */
export function NovaConversaDialog({
  onFechar,
  onCriada,
  pacienteInicialId,
  telefoneInicial,
  nomeContatoInicial,
  titulo = 'Nova conversa',
}: Props) {
  const { data: templates } = useTemplates(true);
  const iniciar = useIniciarConversa();
  const pacienteInicial = usePacientePorId(pacienteInicialId ?? null);

  // Reabertura sem cadastro vinculado: já entra no modo telefone, com o número preenchido —
  // não há o que buscar, o operador só escolhe o modelo e dispara.
  const soTelefone = !pacienteInicialId && Boolean(telefoneInicial);
  const [modo, setModo] = useState<'paciente' | 'telefone'>(soTelefone ? 'telefone' : 'paciente');
  const [termo, setTermo] = useState('');
  const termoDebounced = useDebounce(termo, 300);
  const busca = useBuscarContatos(termoDebounced);
  const [contato, setContato] = useState<ContatoConversa | null>(null);
  const [avisoNegadoAberto, setAvisoNegadoAberto] = useState(false);

  const [telefone, setTelefone] = useState(soTelefone ? telefoneFmt(telefoneInicial!) : '');
  const [assunto, setAssunto] = useState<AssuntoConversa | ''>('');
  const [templateNome, setTemplateNome] = useState('');
  const [params, setParams] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  const selecionado = useMemo(
    () => templates?.find((t) => t.nome === templateNome) ?? null,
    [templates, templateNome],
  );

  // Só há um modelo de abertura hoje (validação cadastral) — pré-seleciona para o operador
  // não ter de escolher em lista de um item.
  // Nome para adiantar o tratamento ({{1}}): o do cadastro quando há contato; senão o do
  // perfil do WhatsApp (reabertura de conversa sem cadastro vinculado).
  const nomeSugerido = contato?.nome ?? nomeContatoInicial ?? null;

  useEffect(() => {
    if (templateNome || !templates?.length) return;
    const unico = templates[0];
    setTemplateNome(unico.nome);
    // O contato pode ter chegado antes do modelo (atalho do WhatsApp): mantém o {{1}}.
    setParams(Array.from({ length: unico.parametros }, (_, i) =>
      i === 0 && nomeSugerido ? primeiroNomeProprio(nomeSugerido) : ''));
  }, [templates, templateNome, nomeSugerido]);

  function aoTrocarTemplate(nome: string) {
    setTemplateNome(nome);
    const t = templates?.find((x) => x.nome === nome);
    setParams(Array.from({ length: t?.parametros ?? 0 }, () => ''));
  }

  function aoSelecionarContato(c: ContatoConversa) {
    setContato(c);
    setTelefone(c.telefone ?? '');
    setTermo('');
    // {{1}} é o tratamento ("Sr./Sra. Fulano") em todos os modelos de abertura — adianta o
    // primeiro nome e deixa o operador acrescentar o pronome.
    setParams((atual) =>
      atual[0]?.trim() ? atual : atual.map((v, i) => (i === 0 ? primeiroNomeProprio(c.nome) : v)));
  }

  // Atalho do WhatsApp ao lado do nome: o paciente já vem escolhido, com nome e telefone
  // do cadastro. Roda uma vez, quando o cadastro chega.
  const pacienteCarregado = pacienteInicial.data;
  useEffect(() => {
    if (!pacienteCarregado || contato) return;
    aoSelecionarContato({
      pacienteId: pacienteCarregado.id,
      nome: pacienteCarregado.nomeCompleto,
      // Numa REABERTURA, o número da conversa manda: o cadastro pode ter vários telefones e
      // reabrir no principal falaria com outro aparelho, não com quem está na thread.
      telefone: telefoneInicial ? telefoneFmt(telefoneInicial) : pacienteCarregado.telefonePrincipal ?? null,
      cpf: pacienteCarregado.cpf ?? null,
      dataNascimento: pacienteCarregado.dataNascimento ?? null,
      origem: 'Cadastro',
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pacienteCarregado]);

  const idioma = selecionado?.idioma ?? 'pt_BR';
  const faltaVariavel = params.some((p) => !p.trim());
  const podeEnviar = Boolean(telefone.trim() && templateNome && !faltaVariavel);

  async function aoEnviar(cienteDoNegado = false) {
    setErro(null);
    if (!podeEnviar) {
      setErro('Informe o telefone, o modelo e todas as variáveis.');
      return;
    }
    // ❗ número negado: só envia com ciência explícita do operador.
    if (contato?.contatoNegado && !cienteDoNegado) {
      setAvisoNegadoAberto(true);
      return;
    }
    try {
      const id = await iniciar.mutateAsync({
        telefone: telefone.trim(),
        pacienteId: contato?.pacienteId ?? null,
        nomeContato: contato ? formatarNomeProprio(contato.nome) : null,
        assunto: assunto || null,
        template: templateNome,
        idioma,
        parametros: params.map((p) => p.trim()),
      });
      onCriada(id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const buscando =
    termo.trim().length >= 3 && (termo.trim() !== termoDebounced.trim() || busca.isFetching);

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-lg flex-col rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">{titulo}</h2>
          <button type="button" onClick={onFechar} className="rounded p-1 text-gray-400 hover:bg-gray-100" aria-label="Fechar">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4 overflow-y-auto p-4">
          <div>
            {pacienteInicial.isLoading ? (
              <p className="flex items-center gap-2 text-sm text-gray-500">
                <Loader2 className="h-4 w-4 animate-spin" /> Carregando o paciente…
              </p>
            ) : null}

            {!contato && !pacienteInicial.isLoading ? (
              <>
            <div className="mb-2 flex gap-1 rounded-md bg-gray-100 p-1">
              {(['paciente', 'telefone'] as const).map((m) => (
                <button
                  key={m}
                  type="button"
                  onClick={() => setModo(m)}
                  className={`flex-1 rounded px-3 py-1.5 text-xs font-medium ${
                    modo === m ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
                  }`}
                >
                  {m === 'paciente' ? 'Buscar paciente' : 'Digitar telefone'}
                </button>
              ))}
            </div>

            {modo === 'paciente' ? (
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
                <input
                  value={termo}
                  onChange={(e) => setTermo(e.target.value)}
                  placeholder="Nº da solicitação, CPF, CNS ou parte do nome"
                  className="w-full rounded-md border border-gray-300 py-2 pl-9 pr-9 text-sm outline-none focus:border-primary-400"
                  autoFocus
                />
                {buscando ? (
                  <Loader2 className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 animate-spin text-gray-400" />
                ) : null}

                {termoDebounced.trim().length >= 3 && !buscando && (busca.data?.length ?? 0) === 0 ? (
                  <p className="mt-2 text-xs text-gray-500">Nenhum paciente encontrado.</p>
                ) : null}

                {(busca.data?.length ?? 0) > 0 ? (
                  <ul className="mt-2 max-h-56 overflow-auto rounded-md border border-gray-200 bg-white shadow">
                    {busca.data!.map((c) => (
                      <li key={c.pacienteId}>
                        <button
                          type="button"
                          onClick={() => aoSelecionarContato(c)}
                          className="w-full px-3 py-2 text-left text-sm hover:bg-gray-50"
                        >
                          <span className="flex min-w-0 items-center gap-1 font-medium text-gray-900">
                            <span className="truncate">{formatarNomeProprio(c.nome)}</span>
                            {c.contatoNegado ? (
                              <AlertTriangle
                                className="h-3.5 w-3.5 shrink-0 text-amber-500"
                                aria-label="Número negado: quem atende disse que não é o paciente"
                              />
                            ) : null}
                          </span>
                          <span className="block truncate text-xs text-gray-500">
                            {c.telefone ? telefoneFmt(c.telefone) : 'sem telefone cadastrado'}
                            {c.cpf ? ` · CPF ${cpfFmt(c.cpf)}` : ''} · {c.origem}
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </div>
            ) : null}
              </>
            ) : null}

            {contato ? (
              <div className="mt-2 flex items-center justify-between rounded-md border border-primary-200 bg-primary-50 px-3 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-gray-900">
                    {formatarNomeProprio(contato.nome)}
                  </p>
                  <p className="truncate text-xs text-gray-500">{contato.origem}</p>
                </div>
                <button
                  type="button"
                  onClick={() => { setContato(null); setTelefone(''); }}
                  className="ml-2 rounded p-1 text-gray-400 hover:bg-white"
                  aria-label="Remover paciente"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            ) : null}
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Telefone</label>
            <input
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              placeholder="(21) 99999-0000"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            />
            <p className="mt-1 text-xs text-gray-400">
              Aceita com ou sem DDD, com ou sem máscara. Sem DDD, assume 21.
            </p>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Modelo da mensagem</label>
            <select
              value={templateNome}
              onChange={(e) => aoTrocarTemplate(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            >
              <option value="">Selecione…</option>
              {templates?.map((t) => (
                <option key={`${t.nome}:${t.idioma}`} value={t.nome}>
                  {t.nome} ({t.idioma})
                </option>
              ))}
            </select>
            {!templates?.length ? (
              <p className="mt-1 text-xs text-amber-600">
                Nenhum modelo de abertura disponível — confira a conexão com o Automais.Zap em Integrações.
              </p>
            ) : null}
          </div>

          {params.map((p, i) => (
            <div key={i}>
              <label className="mb-1 block text-xs font-medium text-gray-600">{`Variável {{${i + 1}}}`}</label>
              <input
                value={p}
                onChange={(e) => setParams((arr) => arr.map((v, j) => (j === i ? e.target.value : v)))}
                placeholder={selecionado?.exemplos[i] ?? ''}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              />
            </div>
          ))}

          {selecionado?.corpo ? (
            <div>
              <p className="mb-1 text-xs font-medium text-gray-600">Prévia</p>
              <p className="whitespace-pre-wrap rounded-md bg-gray-50 p-3 text-xs text-gray-600">
                {preencher(selecionado.corpo, params)}
              </p>
            </div>
          ) : null}

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Assunto (opcional)</label>
            <select
              value={assunto}
              onChange={(e) => setAssunto(e.target.value as AssuntoConversa | '')}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            >
              <option value="">—</option>
              {ASSUNTOS.map((a) => (
                <option key={a} value={a}>{ROTULO_ASSUNTO[a]}</option>
              ))}
            </select>
          </div>

          {contato?.contatoNegado ? (
            <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                <b>Número negado:</b> quem atende este telefone já disse que <b>não é o paciente</b>. Confira
                a pendência em Pendências de Cadastro antes de enviar.
              </span>
            </div>
          ) : null}

          {erro && <p className="text-xs text-red-600">{erro}</p>}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-200 px-4 py-3">
          <button type="button" onClick={onFechar} className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100">
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => void aoEnviar()}
            disabled={iniciar.isPending || !podeEnviar}
            className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {iniciar.isPending ? 'Enviando…' : 'Iniciar conversa'}
          </button>
        </div>

        {avisoNegadoAberto ? (
          <ModalNumeroNegado
            telefone={telefone || contato?.telefone}
            onCancelar={() => setAvisoNegadoAberto(false)}
            onConfirmar={() => {
              setAvisoNegadoAberto(false);
              void aoEnviar(true);
            }}
          />
        ) : null}
      </div>
    </div>
  );
}
