import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { AlertTriangle, CheckCircle2, FileText, Loader2, Send, UserPlus, XCircle } from 'lucide-react';

import { ModalLoginSer } from '@/features/ser/components/ModalLoginSer';
import { useSessaoSerObrigatoria } from '@/features/ser/lib/sessaoSer';
import { ModalLoginSernit } from '@/features/sernit/components/ModalLoginSernit';
import { useSessaoSernitObrigatoria } from '@/features/sernit/lib/sessaoSernit';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

import { resolverMedicoPendente, TIPOS_DOCUMENTO, type MedicoParecido } from '../api/medicosApi';
import { useEnviarAoSer, usePrepararEnvioSer } from '../api/solicitacoesQueries';
import type {
  AutorizoCadastroMedico,
  MedicoNovoNoSistema,
  PassoEnvioSer,
  PreparoEnvioSer,
  ResultadoEnvioSer,
  SistemaEnvioAutomatico,
} from '../api/solicitacoesApi';

type Etapa = 'preparando' | 'previa' | 'enviando' | 'enviado' | 'erro';

/**
 * "Enviar ao SER" / "Enviar ao SERNIT": a plataforma preenche a tela de criação do sistema inteira,
 * anexa os documentos e grava — como o envio ao SISCAN na anamnese. SER-RJ e SERNIT são a mesma
 * aplicação em instâncias diferentes; muda a sessão (cada um tem login próprio) e o nome.
 *
 * <p><b>Duas etapas, e a primeira não grava nada.</b> Ao abrir, o servidor percorre a tela do sistema
 * com a solicitação (recurso, paciente, médico, risco, unidade, CID, campos do recurso) e para
 * antes de anexar. A pessoa vê campo a campo o que vai, os anexos e os pedidos parecidos que o sistema
 * já tem para o paciente — só então envia.</p>
 *
 * <p><b>Médico pedido que não está na lista do sistema</b>: a prévia mostra os nomes parecidos ("É
 * este") e, se não for nenhum, o regulador AUTORIZA o cadastro — quem cadastra é o próprio envio, na
 * tela de nova solicitação do sistema (ícone "Adicionar médico"), antes de preencher o pedido.</p>
 *
 * <p><b>Assina quem envia</b>, com o usuário e a senha DELE no sistema (o modal de login aparece
 * sozinho quando falta). O desfecho — número, ou o erro com o texto do sistema — fica AQUI, no modal:
 * é ato irreversível no sistema do Estado, e toast no canto ninguém vê.</p>
 */
export function ModalEnviarAoSistema({
  sistema,
  ...props
}: PropsModal & { sistema: SistemaEnvioAutomatico }) {
  // Um componente por sistema: cada um chama o SEU hook de sessão (hook não é condicional).
  return sistema === 'Sernit' ? <EnvioSernit {...props} /> : <EnvioSer {...props} />;
}

type PropsModal = { solicitacaoId: string; aberto: boolean; aoFechar: () => void };

function EnvioSer(props: PropsModal) {
  const sessao = useSessaoSerObrigatoria();
  return <ModalEnvioAutomatico {...props} nome="SER" sessao={sessao} ModalLogin={ModalLoginSer} />;
}

function EnvioSernit(props: PropsModal) {
  const sessao = useSessaoSernitObrigatoria();
  return <ModalEnvioAutomatico {...props} nome="SERNIT" sessao={sessao} ModalLogin={ModalLoginSernit} />;
}

/** O que o modal usa da sessão — igual nos dois hooks (o usuário de cada sistema fica de fora). */
type Sessao = Pick<
  ReturnType<typeof useSessaoSerObrigatoria>,
  'operador' | 'comSessao' | 'tratouFaltaDeSessao' | 'modal'
>;

function ModalEnvioAutomatico({
  solicitacaoId,
  aberto,
  aoFechar,
  nome,
  sessao,
  ModalLogin,
}: PropsModal & {
  nome: 'SER' | 'SERNIT';
  sessao: Sessao;
  ModalLogin: (props: Sessao['modal']) => React.ReactNode;
}) {
  const preparar = usePrepararEnvioSer();
  const enviar = useEnviarAoSer();

  const [etapa, setEtapa] = useState<Etapa>('preparando');
  const [previa, setPrevia] = useState<PreparoEnvioSer | null>(null);
  const [resultado, setResultado] = useState<ResultadoEnvioSer | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroDepoisDeEnviar, setErroDepoisDeEnviar] = useState(false);
  const [conferiParecidos, setConferiParecidos] = useState(false);
  /** Desfecho do médico resolvido nesta abertura ("cadastrado e conferido") — fica à vista na prévia refeita. */
  const [avisoMedico, setAvisoMedico] = useState<string | null>(null);
  /** O "Autorizo cadastrar" do médico fora da lista — vai junto com o envio, que faz o cadastro. */
  const [autorizoMedico, setAutorizoMedico] = useState<AutorizoCadastroMedico | null>(null);
  const iniciou = useRef(false);

  async function fazerPrevia() {
    setEtapa('preparando');
    setErro(null);
    setErroDepoisDeEnviar(false);
    setConferiParecidos(false);
    setAutorizoMedico(null);
    try {
      setPrevia(await preparar.mutateAsync(solicitacaoId));
      setEtapa('previa');
    } catch (e) {
      if (sessao.tratouFaltaDeSessao(e, fazerPrevia)) return;
      setErro(extrairMensagemDeErro(e));
      setEtapa('erro');
    }
  }

  async function fazerEnvio() {
    setEtapa('enviando');
    setErro(null);
    try {
      setResultado(
        await enviar.mutateAsync({
          id: solicitacaoId,
          mesmoComParecido: conferiParecidos,
          medicoNovo: previa?.medicoNovo ? autorizoMedico : null,
        }),
      );
      setEtapa('enviado');
    } catch (e) {
      if (sessao.tratouFaltaDeSessao(e, fazerEnvio)) return;
      // Pedido parecido descoberto na hora do envio: nada foi enviado — volta para a prévia, que
      // mostra os pedidos e a caixinha de conferência.
      const parecido =
        e instanceof AxiosError && e.response?.status === 409
        && (e.response.data as { type?: string } | undefined)?.type === 'ser.pedido_parecido';
      setErroDepoisDeEnviar(!parecido);
      setErro(
        e instanceof AxiosError && !e.response
          ? 'A resposta do servidor não chegou. O envio pode ter terminado do lado de lá: feche, '
            + 'atualize a solicitação e veja o status antes de tentar de novo.'
          : extrairMensagemDeErro(e),
      );
      setEtapa('erro');
    }
  }

  useEffect(() => {
    if (!aberto) {
      iniciou.current = false;
      return;
    }
    if (iniciou.current) return;
    iniciou.current = true;
    setPrevia(null);
    setResultado(null);
    setAvisoMedico(null);
    sessao.comSessao(fazerPrevia);
    // Só na abertura: a prévia não pode ser refeita a cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aberto]);

  const ocupado = etapa === 'preparando' || etapa === 'enviando';
  const temParecidos = (previa?.possiveisDuplicados.length ?? 0) > 0;
  const cadastraMedico = !!previa?.medicoNovo && !!autorizoMedico;

  return (
    <>
      <Modal
        aberto={aberto && !sessao.modal.aberto}
        aoFechar={() => {
          // Fechar no meio do envio não o interrompe (o servidor vai até o fim); só esconderia o
          // desfecho. Por isso o X fica mudo enquanto envia.
          if (etapa !== 'enviando') aoFechar();
        }}
        titulo={`Enviar ao ${nome}`}
        largura="lg"
      >
        {etapa === 'preparando' && (
          <Aguarde
            titulo={`Preenchendo a tela do ${nome}…`}
            texto={`A plataforma está abrindo a tela de nova solicitação do ${nome} e preenchendo tudo. Nada é gravado nesta etapa.`}
          />
        )}

        {etapa === 'enviando' && (
          <Aguarde
            titulo={`Enviando ao ${nome}…`}
            texto={
              cadastraMedico
                ? `Cadastrando o médico pelo “Adicionar Médico” da tela de nova solicitação do ${nome} e conferindo na lista; depois, preenchendo, anexando os documentos, gravando e relendo o pedido. Leva cerca de um minuto — não feche esta janela.`
                : `Anexando os documentos, gravando e relendo o pedido no ${nome} para conferir. Leva cerca de um minuto — não feche esta janela.`
            }
          />
        )}

        {etapa === 'previa' && previa && (
          <div className="space-y-4">
            <p className="text-sm text-slate-600">
              A tela do {nome} foi preenchida assim — <b>nada foi gravado ainda</b>. Confira e envie.
              Vai assinado por <b title={`usuário do ${nome}: ${previa.operadorSer}`}>{sessao.operador ?? previa.operadorSer}</b>.
            </p>

            {avisoMedico && (
              <p className="flex items-center gap-2 rounded border border-emerald-300 bg-emerald-50 p-2 text-sm text-emerald-900">
                <CheckCircle2 className="size-4 shrink-0" /> {avisoMedico}
              </p>
            )}

            {previa.medicoNovo && (
              <BlocoMedicoNovo
                key={`${previa.medicoNovo.pendenteId}-${previa.medicoNovo.situacao}`}
                m={previa.medicoNovo}
                nome={nome}
                aoAutorizar={setAutorizoMedico}
                aoResolver={(mensagem) => {
                  setAvisoMedico(mensagem);
                  void fazerPrevia();
                }}
              />
            )}

            <ListaPassos passos={previa.passos} />

            <div>
              <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Anexos</h3>
              <ul className="space-y-1">
                {previa.anexos.map((a) => (
                  <li key={a.nome} className="flex items-center gap-2 text-sm text-slate-700">
                    <FileText className="size-4 text-slate-400" />
                    {a.nome}
                    <span className="text-xs text-slate-400">
                      {(a.tamanho / 1024 / 1024).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} MB
                      {a.arquivosJuntados > 1 && ` · ${a.arquivosJuntados} arquivos juntados num PDF (o ${nome} aceita no máximo 2)`}
                    </span>
                  </li>
                ))}
              </ul>
            </div>

            {temParecidos && (
              <div className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                <p className="flex items-center gap-2 font-medium">
                  <AlertTriangle className="size-4" /> O {nome} já tem pedido deste paciente para este recurso
                </p>
                <ul className="mt-1 list-inside list-disc">
                  {previa.possiveisDuplicados.map((d) => (
                    <li key={d.idSer}>
                      nº {d.idSer} — {d.situacao}
                      {d.dataSolicitacao && `, pedido em ${d.dataSolicitacao}`}
                    </li>
                  ))}
                </ul>
                <label className="mt-2 flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={conferiParecidos}
                    onChange={(e) => setConferiParecidos(e.target.checked)}
                  />
                  Conferi: é outro caso, pode enviar mesmo assim.
                </label>
              </div>
            )}

            <div className="flex justify-end gap-2">
              <Button variante="secundaria" onClick={aoFechar}>
                Cancelar
              </Button>
              <Button
                onClick={fazerEnvio}
                disabled={(temParecidos && !conferiParecidos) || (!!previa.medicoNovo && !autorizoMedico)}
                title={
                  previa.medicoNovo && !autorizoMedico
                    ? `Escolha um dos nomes parecidos ou autorize o cadastro do médico antes de enviar ao ${nome}`
                    : undefined
                }
              >
                <Send className="size-4" />
                {cadastraMedico ? `Cadastrar o médico e enviar ao ${nome}` : `Enviar ao ${nome}`}
              </Button>
            </div>
          </div>
        )}

        {etapa === 'enviado' && resultado && (
          <div className="space-y-4">
            <div className="rounded border border-emerald-300 bg-emerald-50 p-4 text-emerald-900">
              <p className="flex items-center gap-2 text-base font-semibold">
                <CheckCircle2 className="size-5" /> Enviado ao {nome} — nº {resultado.numeroExterno}
              </p>
              <p className="mt-1 text-sm">
                {resultado.conferido
                  ? `O pedido foi relido do ${nome} com este paciente e este recurso.`
                  : `O ${nome} devolveu o número, mas a releitura não achou o pedido na hora. Confira no ${nome}.`}
              </p>
              {resultado.mensagemDoSer && (
                <p className="mt-1 text-xs text-emerald-800">O {nome} disse: “{resultado.mensagemDoSer}”</p>
              )}
            </div>
            <ListaPassos passos={resultado.passos} />
            <div className="flex justify-end">
              <Button onClick={aoFechar}>Fechar</Button>
            </div>
          </div>
        )}

        {etapa === 'erro' && (
          <div className="space-y-4">
            <div className="rounded border border-red-300 bg-red-50 p-4 text-sm text-red-900">
              <p className="flex items-center gap-2 font-semibold">
                <XCircle className="size-5" />
                {erroDepoisDeEnviar ? `O envio ao ${nome} não terminou` : 'Não deu para preparar o envio'}
              </p>
              <p className="mt-1 whitespace-pre-line">{erro}</p>
            </div>
            <div className="flex justify-end gap-2">
              <Button variante="secundaria" onClick={aoFechar}>
                Fechar
              </Button>
              {!erroDepoisDeEnviar && (
                <Button onClick={() => sessao.comSessao(fazerPrevia)} disabled={ocupado}>
                  Tentar de novo
                </Button>
              )}
            </div>
          </div>
        )}
      </Modal>

      <ModalLogin {...sessao.modal} />
    </>
  );
}

/**
 * O médico que a unidade pediu e que a lista do sistema não tem (ADR-0065, complemento de
 * 08/10/2026). O regulador decide aqui mesmo, sem sair do envio:
 * <ul>
 *   <li><b>É este</b> — um dos nomes parecidos da lista de HOJE (o sistema abrevia muito); a
 *   solicitação passa a usar o cadastro de lá;</li>
 *   <li><b>Autorizo cadastrar</b> — o regulador confere os dados e AUTORIZA; o cadastro é feito pelo
 *   próprio envio, no "Adicionar Médico" da tela de nova solicitação do sistema, antes de preencher o
 *   pedido. O cadastro do Estado não tem editar nem apagar: por isso a autorização é expressa e a
 *   especialidade é escolhida da lista de lá.</li>
 * </ul>
 * Recusar continua no cartão do médico na solicitação (o motivo vai para a unidade).
 */
function BlocoMedicoNovo({
  m,
  nome,
  aoAutorizar,
  aoResolver,
}: {
  m: MedicoNovoNoSistema;
  nome: 'SER' | 'SERNIT';
  /** A autorização completa (ou `null` enquanto falta algo) — o envio leva junto. */
  aoAutorizar: (a: AutorizoCadastroMedico | null) => void;
  aoResolver: (mensagem: string) => void;
}) {
  const qc = useQueryClient();
  const [abrirCadastro, setAbrirCadastro] = useState(false);
  const [nomeMedico, setNomeMedico] = useState(m.nome);
  const [tipo, setTipo] = useState(m.numeroDocumento ? (m.tipoDocumento ?? '') : '');
  const [numero, setNumero] = useState(m.numeroDocumento ?? '');
  const [especialidade, setEspecialidade] = useState(m.especialidadeSugerida ?? '');
  const [autorizo, setAutorizo] = useState(false);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const incerto = m.situacao === 'CadastroIncerto';

  const completo =
    abrirCadastro && autorizo && !!especialidade && !!nomeMedico.trim() && !(numero.trim() && !tipo);
  useEffect(() => {
    aoAutorizar(
      completo
        ? {
            especialidade,
            nome: nomeMedico.trim() || null,
            tipoDocumento: numero.trim() ? tipo || null : null,
            numeroDocumento: numero.trim() || null,
          }
        : null,
    );
  }, [completo, especialidade, nomeMedico, tipo, numero, aoAutorizar]);

  async function usarEste(p: MedicoParecido) {
    setErro(null);
    setOcupado(true);
    try {
      await resolverMedicoPendente(m.pendenteId, 'JaExistia', p.nome, null);
      // Resolver troca o médico em todas as solicitações que o usavam: o detalhe por trás também muda.
      void qc.invalidateQueries({ queryKey: ['regulacao'] });
      aoResolver(`A solicitação passa a usar o cadastro “${p.nome}” do ${nome}.`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <section className="rounded border border-amber-300 bg-amber-50 p-3 text-sm">
      <h3 className="flex items-center gap-2 font-semibold text-amber-900">
        <UserPlus className="size-4" /> Médico não cadastrado no {nome}
      </h3>
      <p className="mt-1 text-slate-800">
        A unidade pediu <b>{m.nome}</b>
        {m.numeroDocumento && ` (${m.tipoDocumento} ${m.numeroDocumento})`}
        {m.especialidadePedida && ` — ${m.especialidadePedida}`}, que não está na lista de médicos do {nome}.
        Confira os nomes parecidos: se for um deles, use “É este”; se não for nenhum, autorize o cadastro e o
        envio cadastra o médico no {nome}.
      </p>

      {incerto && (
        <p className="mt-2 rounded border border-red-300 bg-red-50 p-2 text-red-900">
          Já houve uma tentativa de cadastrar este médico no {nome} e a plataforma não conseguiu confirmar.
          Confira no {nome}: se ele está lá, escolha abaixo (ou use “Já existia” no cartão do médico); se não
          está, use “Não entrou” no cartão do médico. A plataforma não tenta de novo sozinha — repetir pode
          duplicar o cadastro do Estado.
        </p>
      )}

      <div className="mt-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Nomes parecidos na lista do {nome} hoje
        </p>
        {m.parecidos.length === 0 ? (
          <p className="mt-1 text-slate-600">Nenhum nome parecido na lista do {nome}.</p>
        ) : (
          <ul className="mt-1 space-y-1">
            {m.parecidos.map((p) => (
              <li key={p.valor} className="flex items-center justify-between gap-2 rounded border border-slate-200 bg-white px-2 py-1">
                <span>
                  {p.nome} <span className="text-xs text-slate-500">· {p.motivo}</span>
                </span>
                <Button variante="secundaria" onClick={() => usarEste(p)} disabled={ocupado}>
                  É este
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {m.podeCadastrar && !abrirCadastro && (
        <div className="mt-3">
          <Button onClick={() => setAbrirCadastro(true)} disabled={ocupado}>
            <UserPlus className="size-4" /> Não é nenhum — autorizo cadastrar no {nome}
          </Button>
        </div>
      )}

      {m.podeCadastrar && abrirCadastro && (
        <div className="mt-3 space-y-2 rounded border border-slate-200 bg-white p-3">
          <p className="text-slate-700">
            Ao enviar, a plataforma abre a tela de nova solicitação do {nome} com o seu usuário, cadastra o
            médico pelo “Adicionar Médico” (ao lado de “Médico responsável”), confere se o nome entrou na lista
            e só então preenche e grava a solicitação. <b>O cadastro do Estado não tem editar nem apagar</b> —
            confira os dados.
          </p>
          <label className="block">
            <span className="text-xs text-slate-500">Nome (como vai ficar no {nome})</span>
            <Input value={nomeMedico} onChange={(e) => setNomeMedico(e.target.value.toUpperCase())} />
          </label>
          <div className="grid grid-cols-[8rem_1fr] gap-2">
            <label className="block">
              <span className="text-xs text-slate-500">Documento</span>
              <select
                value={tipo}
                onChange={(e) => setTipo(e.target.value)}
                className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              >
                <option value="">—</option>
                {TIPOS_DOCUMENTO.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="text-xs text-slate-500">Número</span>
              <Input value={numero} onChange={(e) => setNumero(e.target.value)} />
            </label>
          </div>
          <label className="block">
            <span className="text-xs text-slate-500">
              Especialidade (lista do {nome}){m.especialidadePedida && ` — a unidade escreveu “${m.especialidadePedida}”`}
            </span>
            <select
              value={especialidade}
              onChange={(e) => setEspecialidade(e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
            >
              <option value="">Escolha…</option>
              {m.especialidades.map((e) => (
                <option key={e} value={e}>
                  {e}
                </option>
              ))}
            </select>
          </label>
          <label className="flex items-start gap-2">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={autorizo}
              onChange={(e) => setAutorizo(e.target.checked)}
            />
            <span>
              Autorizo cadastrar este médico no {nome} no envio desta solicitação. Conferi os nomes parecidos
              acima: não é nenhum deles.
            </span>
          </label>
          <div className="flex justify-end">
            <Button
              variante="ghost"
              onClick={() => {
                setAbrirCadastro(false);
                setAutorizo(false);
              }}
            >
              Voltar
            </Button>
          </div>
        </div>
      )}

      {!m.podeCadastrar && !incerto && (
        <p className="mt-2 text-xs text-amber-900">
          A tela do {nome} não ofereceu o “Adicionar Médico” agora — feche e tente de novo mais tarde, ou escolha
          um dos nomes parecidos.
        </p>
      )}

      {erro && <p className="mt-2 whitespace-pre-line rounded border border-red-300 bg-red-50 p-2 text-red-900">{erro}</p>}
      <p className="mt-2 text-xs text-slate-500">Para recusar o médico, use o cartão do médico na solicitação.</p>
    </section>
  );
}

function Aguarde({ titulo, texto }: { titulo: string; texto: string }) {
  return (
    <div className="flex flex-col items-center gap-3 py-8 text-center">
      <Loader2 className="size-8 animate-spin text-primary-600" />
      <p className="text-sm font-medium text-slate-800">{titulo}</p>
      <p className="max-w-md text-sm text-slate-500">{texto}</p>
    </div>
  );
}

function ListaPassos({ passos }: { passos: PassoEnvioSer[] }) {
  return (
    <div className="overflow-hidden rounded border border-slate-200">
      <table className="w-full text-sm">
        <tbody>
          {passos.map((p, i) => (
            <tr key={`${p.campo}-${i}`} className="border-b border-slate-100 last:border-0 align-top">
              <td className="w-6 py-1.5 pl-2">
                {p.ok ? (
                  <CheckCircle2 className="size-4 text-emerald-600" />
                ) : (
                  <AlertTriangle className="size-4 text-amber-600" />
                )}
              </td>
              <td className="w-44 py-1.5 pr-2 font-medium text-slate-700">{p.campo}</td>
              <td className="py-1.5 pr-2 text-slate-700">
                <span className="line-clamp-3 whitespace-pre-line">{p.valor}</span>
                {p.observacao && <span className="block text-xs text-slate-500">{p.observacao}</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
