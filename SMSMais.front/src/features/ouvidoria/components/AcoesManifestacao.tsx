import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { AjudaCampo } from '@/shared/ui/AjudaCampo';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';
import {
  useAnotar,
  useArquivar,
  useAtualizarTeorPseudonimizado,
  useCobrar,
  useComplementar,
  useConcluir,
  useDevolverArea,
  useEncaminhar,
  useEncaminharExterno,
  useEscalonar,
  useHabilitarDenuncia,
  useMarcadores,
  usePedirComplementacao,
  useProrrogar,
  useRegistrarRecurso,
  useResponderArea,
  useResponderCidadao,
  useTriar,
} from '@/features/ouvidoria/api/queries';
import { AnexosOuvidoriaInput } from '@/features/ouvidoria/components/AnexosOuvidoria';
import { SeletorAssunto, SeletorPontoResposta, SeletorUnidade } from '@/features/ouvidoria/components/Seletores';
import { Textarea } from '@/features/ouvidoria/components/Textarea';
import {
  ROTULO_ACAO,
  acoesPorStatus,
  ajudaConteudoMinimo,
  identificacoesPermitidas,
  situacoesFinaisPorTipo,
  type AcaoManifestacao,
} from '@/features/ouvidoria/lib/regras';
import {
  MOTIVOS_ARQUIVAMENTO,
  MOTIVOS_NAO_ATENDIMENTO,
  PRIORIDADES,
  ROTULO_MOTIVO_ARQUIVAMENTO,
  ROTULO_MOTIVO_NAO_ATENDIMENTO,
  ROTULO_PRIORIDADE,
  ROTULO_RESOLUTIVIDADE,
  ROTULO_SITUACAO_FINAL,
  ROTULO_TIPO,
  TIPOS,
} from '@/features/ouvidoria/lib/rotulos';
import type {
  AnexoRef,
  ManifestacaoDetalheDto,
  OuvidoriaMotivoArquivamento,
  OuvidoriaMotivoNaoAtendimento,
  OuvidoriaPrioridade,
  OuvidoriaResolutividade,
  OuvidoriaSituacaoFinal,
  OuvidoriaTipo,
} from '@/features/ouvidoria/types';

type Props = { manifestacao: ManifestacaoDetalheDto };

/** Ações que ganham destaque (botão primário); as demais ficam em outline. */
const PRIMARIAS: AcaoManifestacao[] = ['triar', 'encaminhar', 'responderArea', 'responderCidadao', 'complementar', 'concluir'];
const PERIGOSAS: AcaoManifestacao[] = ['arquivar'];

/** Barra de botões conforme status/perfil + modal com o formulário da ação escolhida. */
export function AcoesManifestacao({ manifestacao: m }: Props) {
  const podeEditar = usePermissao('Ouvidoria', 'Edicao');
  const podeArquivar = usePermissao('Ouvidoria', 'Exclusao');
  const podeGestao = usePermissao('OuvidoriaGestao', 'Edicao');
  const podeHabilitar = usePermissao('OuvidoriaSigilo', 'Inclusao');
  const podeEditarSigilo = usePermissao('OuvidoriaSigilo', 'Edicao');
  const podeResponderPonto = usePermissao('OuvidoriaPontoResposta', 'Edicao');
  const [acao, setAcao] = useState<AcaoManifestacao | null>(null);

  const acoes = acoesPorStatus(
    m.status,
    m.tipo,
    {
      podeEditar,
      podeArquivar,
      podeGestao,
      podeHabilitar,
      podeEditarSigilo,
      podeResponderPonto,
      identificacao: m.identificacao,
      complementacaoUsada: m.complementacaoUsada,
      prorrogadoEm: m.prorrogadoEm,
      habilitadaEm: m.habilitadaEm,
      recursoUsado: m.eventos.some((e) => e.tipo === 'Recurso'),
    },
    m.acoesPermitidas,
  );

  if (acoes.length === 0) {
    return <p className="text-sm text-slate-500">Nenhuma ação disponível para você neste status.</p>;
  }

  const fechar = () => setAcao(null);
  const concluidoOk = (msg: string) => {
    notificar(msg, 'sucesso');
    fechar();
  };

  return (
    <>
      <div className="flex flex-wrap gap-2" role="group" aria-label="Ações da manifestação">
        {acoes.map((a) => (
          <Button
            key={a}
            type="button"
            tamanho="sm"
            variante={PERIGOSAS.includes(a) ? 'danger' : PRIMARIAS.includes(a) ? 'primaria' : 'outline'}
            onClick={() => setAcao(a)}
          >
            {ROTULO_ACAO[a]}
          </Button>
        ))}
      </div>

      {acao === 'concluir' ? (
        <ConcluirDialog id={m.id} aoFechar={fechar} aoOk={() => concluidoOk('Manifestação concluída.')} />
      ) : acao ? (
        <Modal aberto aoFechar={fechar} titulo={ROTULO_ACAO[acao]} largura={acao === 'triar' || acao === 'responderCidadao' ? 'lg' : 'md'}>
          {acao === 'triar' && <FormTriar m={m} aoOk={() => concluidoOk('Triagem salva.')} />}
          {acao === 'encaminhar' && <FormEncaminhar m={m} aoOk={() => concluidoOk('Encaminhada à área.')} />}
          {acao === 'pedirComplementacao' && (
            <FormTexto
              id={m.id}
              usar={usePedirComplementacao}
              rotulo="O que falta o cidadão informar"
              dica="Vai ao cidadão. O prazo fica suspenso até a resposta (só uma vez por manifestação)."
              minimo={10}
              aoOk={() => concluidoOk('Pedido de complementação registrado.')}
            />
          )}
          {acao === 'complementar' && (
            <FormTextoAnexos
              id={m.id}
              usar={useComplementar}
              rotulo="Complementação recebida"
              dica="O que o cidadão trouxe (por telefone, presencial, e-mail…). O relógio do prazo volta a contar."
              aoOk={() => concluidoOk('Complementação registrada.')}
            />
          )}
          {acao === 'responderArea' && (
            <FormTextoAnexos
              id={m.id}
              usar={useResponderArea}
              rotulo="Resposta da área"
              dica="Interna: a ouvidoria valida antes de responder ao cidadão. Diga o que foi apurado e o que foi feito."
              aoOk={() => concluidoOk('Resposta da área registrada.')}
            />
          )}
          {acao === 'devolverArea' && (
            <FormTexto
              id={m.id}
              usar={useDevolverArea}
              rotulo="O que precisa ser reanalisado"
              dica="Interna. A área ganha novo prazo (metade do original, mínimo 2 dias)."
              minimo={10}
              aoOk={() => concluidoOk('Devolvida à área.')}
            />
          )}
          {acao === 'responderCidadao' && <FormResponderCidadao m={m} aoOk={() => concluidoOk('Resposta ao cidadão registrada.')} />}
          {acao === 'prorrogar' && (
            <FormTexto
              id={m.id}
              usar={useProrrogar}
              rotulo="Justificativa da prorrogação"
              dica="Vai ao cidadão. Só é possível prorrogar uma vez; mínimo de 20 caracteres."
              minimo={20}
              aoOk={() => concluidoOk('Prazo prorrogado.')}
            />
          )}
          {acao === 'cobrar' && <FormCobrar id={m.id} aoOk={() => concluidoOk('Cobrança registrada.')} />}
          {acao === 'escalonar' && (
            <FormTexto
              id={m.id}
              usar={useEscalonar}
              rotulo="Para quem e por quê"
              dica="Interna. Registra que a manifestação subiu de nível (gestão)."
              minimo={10}
              aoOk={() => concluidoOk('Escalonamento registrado.')}
            />
          )}
          {acao === 'recurso' && (
            <FormTexto
              id={m.id}
              usar={useRegistrarRecurso}
              rotulo="Razões do recurso do cidadão"
              dica="Vai ao acompanhamento. Só cabe um recurso por manifestação."
              minimo={10}
              aoOk={() => concluidoOk('Recurso registrado.')}
            />
          )}
          {acao === 'arquivar' && <FormArquivar m={m} aoOk={() => concluidoOk('Manifestação arquivada.')} />}
          {acao === 'encaminharExterno' && <FormEncaminharExterno id={m.id} aoOk={() => concluidoOk('Encaminhada a outro órgão.')} />}
          {acao === 'habilitar' && (
            <FormTexto
              id={m.id}
              usar={useHabilitarDenuncia}
              rotulo="Análise de admissibilidade"
              dica="Interna. Autoria, materialidade e competência: por que a denúncia segue para apuração."
              minimo={10}
              aoOk={() => concluidoOk('Denúncia habilitada.')}
            />
          )}
          {acao === 'editarTeorPseudonimizado' && (
            <FormTexto
              id={m.id}
              usar={useAtualizarTeorPseudonimizado}
              rotulo="Teor pseudonimizado"
              dica="É esta versão que a unidade apuratória recebe. Retire nomes, contatos e qualquer pista de quem denunciou."
              inicial={m.teorPseudonimizado ?? m.teor}
              linhas={10}
              minimo={10}
              aoOk={() => concluidoOk('Teor pseudonimizado atualizado.')}
            />
          )}
          {acao === 'anotar' && (
            <FormTexto
              id={m.id}
              usar={useAnotar}
              rotulo="Anotação interna"
              dica="Fica só na trilha interna; o cidadão não vê."
              minimo={1}
              aoOk={() => concluidoOk('Anotação registrada.')}
            />
          )}
        </Modal>
      ) : null}
    </>
  );
}

// ---- Peças comuns ----

function Rodape({ ocupado, rotulo = 'Confirmar', erro }: { ocupado: boolean; rotulo?: string; erro: string | null }) {
  return (
    <>
      {erro ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </p>
      ) : null}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="submit" disabled={ocupado}>
          {ocupado ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : null}
          {ocupado ? 'Salvando…' : rotulo}
        </Button>
      </div>
    </>
  );
}

type MutacaoTexto = { mutateAsync: (p: { texto: string }) => Promise<unknown>; isPending: boolean };

function FormTexto({
  id,
  usar,
  rotulo,
  dica,
  minimo,
  inicial = '',
  linhas = 5,
  aoOk,
}: {
  id: string;
  usar: (id: string) => MutacaoTexto;
  rotulo: string;
  dica?: string;
  minimo: number;
  inicial?: string;
  linhas?: number;
  aoOk: () => void;
}) {
  const mut = usar(id);
  const [texto, setTexto] = useState(inicial);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (texto.trim().length < minimo) {
      setErro(`Escreva pelo menos ${minimo} ${minimo === 1 ? 'caractere' : 'caracteres'}.`);
      return;
    }
    try {
      await mut.mutateAsync({ texto: texto.trim() });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <Campo label={rotulo} htmlFor="ouv-acao-texto" required dica={dica}>
        <Textarea id="ouv-acao-texto" rows={linhas} value={texto} onChange={(e) => setTexto(e.target.value)} autoFocus maxLength={10000} />
      </Campo>
      <Rodape ocupado={mut.isPending} erro={erro} />
    </form>
  );
}

type MutacaoTextoAnexos = { mutateAsync: (p: { texto: string; anexos: AnexoRef[] }) => Promise<unknown>; isPending: boolean };

function FormTextoAnexos({
  id,
  usar,
  rotulo,
  dica,
  aoOk,
}: {
  id: string;
  usar: (id: string) => MutacaoTextoAnexos;
  rotulo: string;
  dica?: string;
  aoOk: () => void;
}) {
  const mut = usar(id);
  const [texto, setTexto] = useState('');
  const [anexos, setAnexos] = useState<AnexoRef[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (texto.trim().length < 10) {
      setErro('Escreva pelo menos 10 caracteres.');
      return;
    }
    try {
      await mut.mutateAsync({ texto: texto.trim(), anexos });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <Campo label={rotulo} htmlFor="ouv-acao-texto" required dica={dica}>
        <Textarea id="ouv-acao-texto" rows={6} value={texto} onChange={(e) => setTexto(e.target.value)} autoFocus maxLength={10000} />
      </Campo>
      <Campo label="Anexos (opcional)" htmlFor="ouv-acao-anexos">
        <AnexosOuvidoriaInput id="ouv-acao-anexos" anexos={anexos} aoMudar={setAnexos} disabled={mut.isPending} />
      </Campo>
      <Rodape ocupado={mut.isPending} erro={erro} />
    </form>
  );
}

// ---- Triar ----

function FormTriar({ m, aoOk }: { m: ManifestacaoDetalheDto; aoOk: () => void }) {
  const triar = useTriar(m.id);
  const { data: marcadores = [] } = useMarcadores();
  const [tipo, setTipo] = useState<OuvidoriaTipo>(m.tipo);
  const [assuntoId, setAssuntoId] = useState<string | null>(m.assuntoId);
  const [subassuntoId, setSubassuntoId] = useState<string | null>(m.subassuntoId);
  const [prioridade, setPrioridade] = useState<OuvidoriaPrioridade>(m.prioridade);
  const [unidadeId, setUnidadeId] = useState(m.unidadeId ?? '');
  const [resumo, setResumo] = useState(m.resumo ?? '');
  const [marcadorIds, setMarcadorIds] = useState<string[]>(m.marcadorIds);
  const [erro, setErro] = useState<string | null>(null);

  // Reclassificar não pode violar identificação × tipo (ex.: sigilosa não vira solicitação).
  const tipoIncompativel = !identificacoesPermitidas(tipo).includes(m.identificacao);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (tipoIncompativel) {
      setErro(`${ROTULO_TIPO[tipo]} não admite manifestação ${m.identificacao === 'Anonima' ? 'anônima' : 'sigilosa'}.`);
      return;
    }
    try {
      await triar.mutateAsync({
        tipo: tipo !== m.tipo ? tipo : null,
        assuntoId,
        subassuntoId,
        prioridade,
        unidadeId: unidadeId || null,
        resumo: resumo.trim() || null,
        responsavelId: m.responsavelId,
        regulacaoSolicitacaoId: m.regulacaoSolicitacaoId,
        marcadorIds,
      });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <Campo label="Tipo" htmlFor="ouv-tri-tipo" erro={tipoIncompativel ? 'Incompatível com a identificação desta manifestação.' : undefined}>
          <Select id="ouv-tri-tipo" value={tipo} onChange={(e) => setTipo(e.target.value as OuvidoriaTipo)}>
            {TIPOS.map((t) => (
              <option key={t} value={t} disabled={!identificacoesPermitidas(t).includes(m.identificacao)}>
                {ROTULO_TIPO[t]}
              </option>
            ))}
          </Select>
        </Campo>
        <Campo label="Prioridade" htmlFor="ouv-tri-prioridade" dica="Urgente = 2 dias úteis para a área; Alta = 10; Normal = 20 (configurável).">
          <Select id="ouv-tri-prioridade" value={prioridade} onChange={(e) => setPrioridade(e.target.value as OuvidoriaPrioridade)}>
            {PRIORIDADES.map((p) => (
              <option key={p} value={p}>
                {ROTULO_PRIORIDADE[p]}
              </option>
            ))}
          </Select>
        </Campo>
      </div>
      <SeletorAssunto
        idBase="ouv-tri-assunto"
        assuntoId={assuntoId}
        subassuntoId={subassuntoId}
        aoMudar={(a, s) => {
          setAssuntoId(a);
          setSubassuntoId(s);
        }}
        incluirInativos
      />
      <Campo label="Unidade" htmlFor="ouv-tri-unidade">
        <SeletorUnidade id="ouv-tri-unidade" value={unidadeId} onChange={setUnidadeId} rotuloVazio="Não se aplica" />
      </Campo>
      <Campo label="Resumo" htmlFor="ouv-tri-resumo" dica="Uma linha para a fila (até 200 caracteres).">
        <Input id="ouv-tri-resumo" value={resumo} maxLength={200} onChange={(e) => setResumo(e.target.value)} />
      </Campo>
      {marcadores.length > 0 ? (
        <fieldset>
          <legend className="label">Marcadores</legend>
          <div className="flex flex-wrap gap-2">
            {marcadores
              .filter((mk) => mk.ativo || marcadorIds.includes(mk.id))
              .map((mk) => {
                const marcado = marcadorIds.includes(mk.id);
                return (
                  <label
                    key={mk.id}
                    className={`inline-flex cursor-pointer items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs ${
                      marcado ? 'border-red-300 bg-red-50 text-red-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="checkbox"
                      className="sr-only"
                      checked={marcado}
                      onChange={(e) => setMarcadorIds(e.target.checked ? [...marcadorIds, mk.id] : marcadorIds.filter((x) => x !== mk.id))}
                    />
                    {mk.nome}
                  </label>
                );
              })}
          </div>
        </fieldset>
      ) : null}
      <Rodape ocupado={triar.isPending} erro={erro} rotulo="Salvar triagem" />
    </form>
  );
}

// ---- Encaminhar ----

function FormEncaminhar({ m, aoOk }: { m: ManifestacaoDetalheDto; aoOk: () => void }) {
  const encaminhar = useEncaminhar(m.id);
  const denuncia = m.tipo === 'Denuncia';
  const [pontoRespostaId, setPontoRespostaId] = useState(m.pontoRespostaId ?? '');
  const [prazoDias, setPrazoDias] = useState('');
  const [texto, setTexto] = useState('');
  const [teorPseudonimizado, setTeorPseudonimizado] = useState(m.teorPseudonimizado ?? '');
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!pontoRespostaId) {
      setErro('Escolha o ponto de resposta.');
      return;
    }
    if (denuncia && !m.habilitadaEm) {
      setErro('Denúncia precisa ser habilitada (análise de admissibilidade) antes de ir à apuração.');
      return;
    }
    if (denuncia && teorPseudonimizado.trim().length < 10) {
      setErro('Denúncia exige o teor pseudonimizado — é ele que a unidade apuratória recebe.');
      return;
    }
    try {
      await encaminhar.mutateAsync({
        pontoRespostaId,
        prazoDias: prazoDias ? Number(prazoDias) : null,
        texto: texto.trim() || null,
        teorPseudonimizado: denuncia ? teorPseudonimizado.trim() : null,
      });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      {denuncia && !m.habilitadaEm ? (
        <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Esta denúncia ainda não foi habilitada. Quem tem o módulo Sigilo precisa habilitá-la antes do encaminhamento.
        </p>
      ) : null}
      <Campo
        label="Ponto de resposta"
        htmlFor="ouv-enc-ponto"
        required
        dica={denuncia ? 'Denúncia só vai para unidade apuratória.' : 'Unidade, área central ou apuração que vai responder.'}
      >
        <SeletorPontoResposta id="ouv-enc-ponto" value={pontoRespostaId} onChange={setPontoRespostaId} tipo={denuncia ? 'Apuracao' : undefined} />
      </Campo>
      <Campo label="Prazo para a área (dias)" htmlFor="ouv-enc-prazo" dica="Em branco: usa a prioridade e a configuração (ou o prazo do ponto).">
        <Input id="ouv-enc-prazo" type="number" min={1} max={90} value={prazoDias} onChange={(e) => setPrazoDias(e.target.value)} className="w-32" />
      </Campo>
      <Campo label="Orientação à área (opcional)" htmlFor="ouv-enc-texto" dica="Interna. O cidadão vê só 'Encaminhada à área responsável', sem nomes.">
        <Textarea id="ouv-enc-texto" rows={3} value={texto} onChange={(e) => setTexto(e.target.value)} maxLength={5000} />
      </Campo>
      {denuncia ? (
        <Campo
          label="Teor pseudonimizado"
          htmlFor="ouv-enc-teor"
          required
          dica="Versão SEM nomes, contatos ou pistas de quem denunciou. É o que a apuração recebe."
        >
          <Textarea id="ouv-enc-teor" rows={8} value={teorPseudonimizado} onChange={(e) => setTeorPseudonimizado(e.target.value)} maxLength={10000} />
        </Campo>
      ) : null}
      <Rodape ocupado={encaminhar.isPending} erro={erro} rotulo="Encaminhar" />
    </form>
  );
}

// ---- Responder ao cidadão ----

function FormResponderCidadao({ m, aoOk }: { m: ManifestacaoDetalheDto; aoOk: () => void }) {
  const responder = useResponderCidadao(m.id);
  const [texto, setTexto] = useState('');
  const [conclusiva, setConclusiva] = useState(true);
  const [resolutividade, setResolutividade] = useState<OuvidoriaResolutividade | ''>('');
  const [situacaoFinal, setSituacaoFinal] = useState<OuvidoriaSituacaoFinal | ''>('');
  const [motivo, setMotivo] = useState<OuvidoriaMotivoNaoAtendimento | ''>('');
  const [erro, setErro] = useState<string | null>(null);

  const situacoes = situacoesFinaisPorTipo(m.tipo);
  const anonima = m.identificacao === 'Anonima';
  const exigeMotivo = situacaoFinal === 'NaoAtendida';

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (texto.trim().length < (conclusiva ? 20 : 10)) {
      setErro(`A resposta precisa ter pelo menos ${conclusiva ? 20 : 10} caracteres.`);
      return;
    }
    if (conclusiva) {
      if (!resolutividade) return setErro('Informe a resolutividade.');
      if (!situacaoFinal) return setErro('Informe a situação final.');
      if (exigeMotivo && !motivo) return setErro('Informe o motivo do não atendimento.');
    }
    try {
      await responder.mutateAsync({
        texto: texto.trim(),
        conclusiva,
        resolutividade: conclusiva ? resolutividade || null : null,
        situacaoFinal: conclusiva ? situacaoFinal || null : null,
        motivoNaoAtendimento: conclusiva && exigeMotivo ? motivo || null : null,
      });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      {anonima ? (
        <p className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600">
          Manifestação anônima: a resposta conclusiva só fica registrada e já conclui — ninguém é notificado.
        </p>
      ) : null}

      <fieldset className="flex flex-wrap gap-4 text-sm">
        <legend className="label">Tipo de resposta</legend>
        <label className="inline-flex items-center gap-2">
          <input type="radio" name="ouv-conclusiva" checked={conclusiva} onChange={() => setConclusiva(true)} />
          Conclusiva (encerra a manifestação)
        </label>
        <label className="inline-flex items-center gap-2">
          <input type="radio" name="ouv-conclusiva" checked={!conclusiva} onChange={() => setConclusiva(false)} />
          Intermediária (só informa o andamento)
        </label>
      </fieldset>

      <Campo
        label="Resposta ao cidadão"
        htmlFor="ouv-resp-texto"
        required
        ajuda={
          conclusiva ? (
            <AjudaCampo titulo={`Conteúdo mínimo da resposta — ${ROTULO_TIPO[m.tipo]}`}>
              <p>{ajudaConteudoMinimo(m.tipo)}</p>
              <p className="text-xs text-gray-500">Portaria Normativa CGU nº 116/2021, art. 29. Linguagem simples, sem jargão.</p>
            </AjudaCampo>
          ) : undefined
        }
        dica={conclusiva ? ajudaConteudoMinimo(m.tipo) : 'Vai ao acompanhamento do cidadão; o status não muda.'}
      >
        <Textarea id="ouv-resp-texto" rows={7} value={texto} onChange={(e) => setTexto(e.target.value)} autoFocus maxLength={10000} />
      </Campo>

      {conclusiva ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Resolutividade" htmlFor="ouv-resp-resol" required>
            <Select id="ouv-resp-resol" value={resolutividade} onChange={(e) => setResolutividade(e.target.value as OuvidoriaResolutividade | '')}>
              <option value="">Selecione</option>
              {(['Resolvida', 'NaoResolvida'] as OuvidoriaResolutividade[]).map((r) => (
                <option key={r} value={r}>
                  {ROTULO_RESOLUTIVIDADE[r]}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo label="Situação final" htmlFor="ouv-resp-situacao" required>
            <Select
              id="ouv-resp-situacao"
              value={situacaoFinal}
              onChange={(e) => {
                setSituacaoFinal(e.target.value as OuvidoriaSituacaoFinal | '');
                setMotivo('');
              }}
            >
              <option value="">Selecione</option>
              {situacoes.map((s) => (
                <option key={s} value={s}>
                  {ROTULO_SITUACAO_FINAL[s]}
                </option>
              ))}
            </Select>
          </Campo>
          {exigeMotivo ? (
            <Campo label="Motivo do não atendimento" htmlFor="ouv-resp-motivo" required className="sm:col-span-2">
              <Select id="ouv-resp-motivo" value={motivo} onChange={(e) => setMotivo(e.target.value as OuvidoriaMotivoNaoAtendimento | '')}>
                <option value="">Selecione</option>
                {MOTIVOS_NAO_ATENDIMENTO.map((mo) => (
                  <option key={mo} value={mo}>
                    {ROTULO_MOTIVO_NAO_ATENDIMENTO[mo]}
                  </option>
                ))}
              </Select>
            </Campo>
          ) : null}
        </div>
      ) : null}

      <Rodape ocupado={responder.isPending} erro={erro} rotulo={conclusiva ? 'Enviar resposta conclusiva' : 'Enviar resposta intermediária'} />
    </form>
  );
}

// ---- Cobrar (texto opcional) ----

function FormCobrar({ id, aoOk }: { id: string; aoOk: () => void }) {
  const cobrar = useCobrar(id);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    try {
      await cobrar.mutateAsync(texto.trim() ? { texto: texto.trim() } : null);
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <Campo label="Mensagem à área (opcional)" htmlFor="ouv-cobrar-texto" dica="Interna. Fica registrado que a área foi cobrada; os membros do ponto veem na trilha.">
        <Textarea id="ouv-cobrar-texto" rows={4} value={texto} onChange={(e) => setTexto(e.target.value)} autoFocus maxLength={5000} />
      </Campo>
      <Rodape ocupado={cobrar.isPending} erro={erro} rotulo="Registrar cobrança" />
    </form>
  );
}

// ---- Arquivar ----

function FormArquivar({ m, aoOk }: { m: ManifestacaoDetalheDto; aoOk: () => void }) {
  const arquivar = useArquivar(m.id);
  const [motivo, setMotivo] = useState<OuvidoriaMotivoArquivamento | ''>('');
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const duplicidade = motivo === 'Duplicidade';

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!motivo) return setErro('Escolha o motivo do arquivamento.');
    if (duplicidade && !texto.trim()) return setErro('Em duplicidade, informe o protocolo da manifestação original.');
    try {
      await arquivar.mutateAsync({ motivo, texto: texto.trim() || null });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <p className="text-sm text-slate-600">
        Arquivar encerra a manifestação sem resposta de mérito. O cidadão vê &quot;arquivada&quot; e o motivo.
      </p>
      <Campo label="Motivo" htmlFor="ouv-arq-motivo" required>
        <Select id="ouv-arq-motivo" value={motivo} onChange={(e) => setMotivo(e.target.value as OuvidoriaMotivoArquivamento | '')} autoFocus>
          <option value="">Selecione</option>
          {MOTIVOS_ARQUIVAMENTO.map((mo) => (
            <option key={mo} value={mo}>
              {ROTULO_MOTIVO_ARQUIVAMENTO[mo]}
            </option>
          ))}
        </Select>
      </Campo>
      {m.possiveisDuplicatas.length > 0 && duplicidade ? (
        <p className="text-xs text-slate-500">Possíveis duplicatas detectadas: {m.possiveisDuplicatas.join(', ')}</p>
      ) : null}
      <Campo
        label={duplicidade ? 'Protocolo da manifestação original' : 'Observação (opcional)'}
        htmlFor="ouv-arq-texto"
        required={duplicidade}
      >
        {duplicidade ? (
          <Input id="ouv-arq-texto" value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="AAAA-NNNNNN" maxLength={200} />
        ) : (
          <Textarea id="ouv-arq-texto" rows={3} value={texto} onChange={(e) => setTexto(e.target.value)} maxLength={2000} />
        )}
      </Campo>
      <Rodape ocupado={arquivar.isPending} erro={erro} rotulo="Arquivar" />
    </form>
  );
}

// ---- Encaminhar a outro órgão ----

function FormEncaminharExterno({ id, aoOk }: { id: string; aoOk: () => void }) {
  const encaminhar = useEncaminharExterno(id);
  const [sistemaExterno, setSistemaExterno] = useState('');
  const [protocoloExterno, setProtocoloExterno] = useState('');
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!sistemaExterno.trim()) return setErro('Informe o órgão/sistema de destino.');
    if (texto.trim().length < 10) return setErro('Explique ao cidadão por que foi encaminhada (mínimo 10 caracteres).');
    try {
      await encaminhar.mutateAsync({ sistemaExterno: sistemaExterno.trim(), protocoloExterno: protocoloExterno.trim() || null, texto: texto.trim() });
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={enviar} className="space-y-4">
      <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
        Depois de encaminhada a outro órgão, a manifestação não pode mais ser prorrogada aqui.
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Campo label="Órgão / sistema de destino" htmlFor="ouv-ext-sistema" required>
          <Input id="ouv-ext-sistema" value={sistemaExterno} onChange={(e) => setSistemaExterno(e.target.value)} maxLength={40} autoFocus />
        </Campo>
        <Campo label="Protocolo lá (opcional)" htmlFor="ouv-ext-protocolo">
          <Input id="ouv-ext-protocolo" value={protocoloExterno} onChange={(e) => setProtocoloExterno(e.target.value)} maxLength={60} />
        </Campo>
      </div>
      <Campo label="Informação ao cidadão" htmlFor="ouv-ext-texto" required dica="Vai ao acompanhamento: para onde foi e como acompanhar lá.">
        <Textarea id="ouv-ext-texto" rows={4} value={texto} onChange={(e) => setTexto(e.target.value)} maxLength={5000} />
      </Campo>
      <Rodape ocupado={encaminhar.isPending} erro={erro} rotulo="Encaminhar" />
    </form>
  );
}

// ---- Concluir ----

function ConcluirDialog({ id, aoFechar, aoOk }: { id: string; aoFechar: () => void; aoOk: () => void }) {
  const concluir = useConcluir(id);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    setErro(null);
    try {
      await concluir.mutateAsync();
      aoOk();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
      notificar(extrairMensagemDeErro(err), 'erro');
    }
  }

  return (
    <ConfirmDialog
      aberto
      titulo="Concluir manifestação"
      mensagem={
        erro ??
        'A manifestação já foi respondida e não houve recurso. Concluir encerra o ciclo (o sistema faria isso sozinho após o prazo de recurso).'
      }
      rotuloConfirmar="Concluir"
      carregando={concluir.isPending}
      aoConfirmar={confirmar}
      aoCancelar={aoFechar}
    />
  );
}
