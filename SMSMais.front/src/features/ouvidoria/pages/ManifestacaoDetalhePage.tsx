import { useNavigate, useParams } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, Copy as CopyIcon, ExternalLink } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarCpf } from '@/shared/lib/cpf';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { useAssuntos, useManifestacao, useMarcadores } from '@/features/ouvidoria/api/queries';
import { AcoesManifestacao } from '@/features/ouvidoria/components/AcoesManifestacao';
import { AnexosOuvidoriaLista } from '@/features/ouvidoria/components/AnexosOuvidoria';
import {
  IdentificacaoBadge,
  PrioridadeManifestacaoBadge,
  StatusManifestacaoBadge,
  TipoManifestacaoBadge,
} from '@/features/ouvidoria/components/badges';
import { LinhaDoTempo } from '@/features/ouvidoria/components/LinhaDoTempo';
import { Bloco, Item, ManifestanteCard } from '@/features/ouvidoria/components/ManifestanteCard';
import { PrazoChip } from '@/features/ouvidoria/components/PrazoChip';
import {
  ROTULO_CANAL,
  ROTULO_MOTIVO_ARQUIVAMENTO,
  ROTULO_MOTIVO_NAO_ATENDIMENTO,
  ROTULO_ORIGEM,
  ROTULO_RESOLUTIVIDADE,
  ROTULO_SITUACAO_FINAL,
} from '@/features/ouvidoria/lib/rotulos';

type Props = {
  /** Aberta pela fila "Meu ponto": volta para lá e não mostra o card do manifestante (o DTO já vem sem). */
  modoPonto?: boolean;
};

export function ManifestacaoDetalhePage({ modoPonto = false }: Props) {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { data: m, isLoading, isError, error } = useManifestacao(id);
  const { data: assuntos = [] } = useAssuntos();
  const { data: marcadores = [] } = useMarcadores();

  const voltar = () => navigate(modoPonto ? '/app/ouvidoria/meu-ponto' : '/app/ouvidoria');

  if (isLoading) return <p className="p-6 text-sm text-slate-500">Carregando…</p>;
  if (isError || !m) {
    return (
      <div className="p-6 text-sm text-red-600" role="alert">
        {isError ? extrairMensagemDeErro(error) : 'Manifestação não encontrada.'}
      </div>
    );
  }

  const nomeAssunto = (aid: string | null) => assuntos.find((a) => a.id === aid)?.nome ?? null;
  const nomesMarcadores = m.marcadorIds.map((mid) => marcadores.find((mk) => mk.id === mid)?.nome ?? '?');
  const denuncia = m.tipo === 'Denuncia';
  const teorExibido = modoPonto && denuncia && m.teorPseudonimizado ? m.teorPseudonimizado : m.teor;

  return (
    <div className="space-y-5">
      <button type="button" onClick={voltar} className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" /> Voltar
      </button>

      {/* Cabeçalho */}
      <header className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-lg font-semibold text-slate-900">
                <CodigoCopiavel codigo={m.protocolo} dica="Copiar protocolo" />
              </span>
              <TipoManifestacaoBadge tipo={m.tipo} />
              <StatusManifestacaoBadge status={m.status} />
              <PrioridadeManifestacaoBadge prioridade={m.prioridade} />
              <IdentificacaoBadge identificacao={m.identificacao} />
            </div>
            <p className="mt-1 text-xs text-slate-500">
              Registrada em {formatarInstante(m.registradaEm)} · {ROTULO_CANAL[m.canal]} · {ROTULO_ORIGEM[m.origem]}
              {m.responsavelNome ? ` · Responsável: ${m.responsavelNome}` : ''}
            </p>
          </div>
          <div className="flex flex-col items-end gap-1">
            <PrazoChip prazo={m.prazoRespostaEm} status={m.status} rotulo="Cidadão" />
            {m.prazoAreaEm && (m.status === 'Encaminhada' || m.areaAtrasada) ? <PrazoChip prazo={m.prazoAreaEm} status={m.status} rotulo="Área" /> : null}
            {m.prorrogadoEm ? <span className="text-xs text-slate-500">Prorrogada em {formatarInstante(m.prorrogadoEm)}</span> : null}
          </div>
        </div>

        {m.possiveisDuplicatas.length > 0 ? (
          <div className="mt-3 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800" role="status">
            <CopyIcon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>
              Possível duplicidade: mesmo CPF, assunto e unidade nos últimos 90 dias em <strong>{m.possiveisDuplicatas.join(', ')}</strong>. Confira antes de
              encaminhar; arquivar por duplicidade é decisão sua.
            </span>
          </div>
        ) : null}

        {denuncia && !m.habilitadaEm && !modoPonto ? (
          <div className="mt-3 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="status">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>Denúncia ainda não habilitada: só vai à apuração depois da análise de admissibilidade (módulo Sigilo).</span>
          </div>
        ) : null}

        <div className="mt-4 border-t border-slate-100 pt-4">
          <AcoesManifestacao manifestacao={m} />
        </div>
      </header>

      <div className="grid gap-5 lg:grid-cols-3">
        {/* Coluna 1-2: relato, classificação, pessoas */}
        <div className="space-y-5 lg:col-span-2">
          <Bloco titulo={modoPonto && denuncia && m.teorPseudonimizado ? 'Relato (versão pseudonimizada)' : 'Relato'}>
            {m.resumo ? <p className="mb-2 text-sm font-medium text-slate-800">{m.resumo}</p> : null}
            <p className="whitespace-pre-wrap text-sm text-slate-800">{teorExibido}</p>
            {!modoPonto && denuncia ? (
              <details className="mt-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
                <summary className="cursor-pointer text-sm font-medium text-slate-700">Teor pseudonimizado (o que a apuração recebe)</summary>
                <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">
                  {m.teorPseudonimizado ?? <span className="text-slate-400">Ainda não preenchido — obrigatório para encaminhar.</span>}
                </p>
              </details>
            ) : null}
            {m.anexos.length > 0 ? (
              <div className="mt-3">
                <p className="mb-1 text-xs text-slate-500">Anexos</p>
                <AnexosOuvidoriaLista anexos={m.anexos} />
              </div>
            ) : null}
          </Bloco>

          {m.respostaConclusiva ? (
            <section className="rounded-xl border border-green-200 bg-green-50 p-4">
              <h3 className="text-sm font-semibold text-green-800">Resposta conclusiva ao cidadão</h3>
              <p className="mt-1 whitespace-pre-wrap text-sm text-green-900">{m.respostaConclusiva}</p>
              <dl className="mt-3 grid gap-x-4 gap-y-1 text-sm sm:grid-cols-3">
                <Item rotulo="Resolutividade" valor={m.resolutividade ? ROTULO_RESOLUTIVIDADE[m.resolutividade] : null} />
                <Item rotulo="Situação final" valor={m.situacaoFinal ? ROTULO_SITUACAO_FINAL[m.situacaoFinal] : null} />
                <Item rotulo="Respondida em" valor={formatarInstante(m.respondidaEm)} />
                {m.motivoNaoAtendimento ? <Item rotulo="Motivo do não atendimento" valor={ROTULO_MOTIVO_NAO_ATENDIMENTO[m.motivoNaoAtendimento]} /> : null}
              </dl>
            </section>
          ) : null}

          {m.motivoArquivamento ? (
            <section className="rounded-xl border border-gray-200 bg-gray-50 p-4">
              <h3 className="text-sm font-semibold text-gray-700">Arquivada</h3>
              <p className="mt-1 text-sm text-gray-700">{ROTULO_MOTIVO_ARQUIVAMENTO[m.motivoArquivamento]}</p>
            </section>
          ) : null}

          {!modoPonto ? <ManifestanteCard manifestacao={m} /> : null}

          {(m.referido || m.envolvidoDescricao) && (
            <div className="grid gap-5 sm:grid-cols-2">
              {m.referido ? (
                <Bloco titulo="Paciente referido">
                  <dl className="grid gap-y-1 text-sm">
                    <Item rotulo="Nome" valor={m.referido.nome} />
                    <Item rotulo="CPF" valor={m.referido.cpf ? formatarCpf(m.referido.cpf) : null} />
                    <Item rotulo="CNS" valor={m.referido.cns} />
                  </dl>
                  {m.referido.patientId ? (
                    <a
                      href={`/app/pacientes/${m.referido.patientId}`}
                      target="_blank"
                      rel="noreferrer"
                      className="mt-2 inline-flex items-center gap-1 text-xs text-red-700 hover:underline"
                    >
                      Abrir cadastro <ExternalLink className="h-3 w-3" aria-hidden="true" />
                    </a>
                  ) : null}
                </Bloco>
              ) : null}
              {m.envolvidoDescricao && !(modoPonto && denuncia) ? (
                <Bloco titulo="Agente/serviço envolvido">
                  <p className="text-sm text-slate-800">{m.envolvidoDescricao}</p>
                </Bloco>
              ) : null}
            </div>
          )}

          <Bloco titulo="Linha do tempo">
            <LinhaDoTempo eventos={m.eventos} />
          </Bloco>
        </div>

        {/* Coluna 3: classificação e contexto */}
        <div className="space-y-5">
          <Bloco titulo="Classificação">
            <dl className="grid gap-y-2 text-sm">
              <Item rotulo="Assunto" valor={nomeAssunto(m.assuntoId) ?? m.assuntoNome} />
              <Item rotulo="Subassunto" valor={nomeAssunto(m.subassuntoId)} />
              <Item rotulo="Unidade" valor={m.unidadeNome} />
              <Item rotulo="Ponto de resposta" valor={m.pontoRespostaNome} />
              <Item
                rotulo="Marcadores"
                valor={
                  nomesMarcadores.length ? (
                    <span className="flex flex-wrap gap-1">
                      {nomesMarcadores.map((n) => (
                        <span key={n} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                          {n}
                        </span>
                      ))}
                    </span>
                  ) : null
                }
              />
            </dl>
          </Bloco>

          <Bloco titulo="Fato e contexto">
            <dl className="grid gap-y-2 text-sm">
              <Item rotulo="Data do fato" valor={m.dataFato ? formatarWallClock(m.dataFato) : null} />
              <Item rotulo="Local do fato" valor={m.localFato} />
              <Item rotulo="Sistema externo" valor={m.sistemaExterno} />
              <Item rotulo="Protocolo externo" valor={m.protocoloExterno} />
              {m.regulacaoSolicitacaoId ? (
                <Item
                  rotulo="Pedido de regulação vinculado"
                  valor={
                    <a href={`/app/regulacao/solicitacoes/${m.regulacaoSolicitacaoId}`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-red-700 hover:underline">
                      Abrir pedido <ExternalLink className="h-3 w-3" aria-hidden="true" />
                    </a>
                  }
                />
              ) : null}
            </dl>
          </Bloco>

          <Bloco titulo="Relógios">
            <dl className="grid gap-y-2 text-sm">
              <Item rotulo="Prazo ao cidadão" valor={formatarWallClock(m.prazoRespostaEm)} />
              <Item rotulo="Prazo da área" valor={m.prazoAreaEm ? formatarWallClock(m.prazoAreaEm) : null} />
              <Item rotulo="Encaminhada em" valor={m.encaminhadaEm ? formatarInstante(m.encaminhadaEm) : null} />
              <Item rotulo="Respondida em" valor={m.respondidaEm ? formatarInstante(m.respondidaEm) : null} />
              <Item rotulo="Concluída em" valor={m.concluidaEm ? formatarInstante(m.concluidaEm) : null} />
              <Item rotulo="Complementação" valor={m.complementacaoUsada ? 'Já pedida (uma vez)' : 'Não pedida'} />
              {m.prorrogadoEm ? <Item rotulo="Justificativa da prorrogação" valor={m.prorrogacaoJustificativa} /> : null}
              {m.habilitadaEm ? <Item rotulo="Denúncia habilitada em" valor={formatarInstante(m.habilitadaEm)} /> : null}
              <Item rotulo="Última atividade" valor={formatarInstante(m.ultimaAtividadeEm)} />
            </dl>
          </Bloco>
        </div>
      </div>
    </div>
  );
}
